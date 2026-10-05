"""
Strategy AI treningadat - teljes bolt-szimulacioval es kombinalt frakciokkal
=============================================================================

Mindket oldalnak van sajat boltja (6 slot, a 0. mindig egy onmegsemmisulo
penzlapot ad, ahogy a ForeGround.cs-ben), es minden effekt (StealCard,
ScrapEnemyCard, ScrapOwnCard, ScrapFromShop, AntiShow, DrawCard,
AttackBonus/HealthBonus/MoneyBonus) fel van dolgozva -- ugyanazzal a
GreedyBuyAlgorithm heurisztikaval donti el mindket fel a vasarlast es a
scrap-valasztast, amit a valodi jatek "Heuristic" (PlayerDecisionMaking == 2)
modja hasznal.

A TÖBB FRAKCIÓS ("kombinált") ADATOK GENERÁLÁSA MEG VAN FORDÍTVA a korábbi
verzióhoz képest: nem az ELLENFÉL boltja kap 2-3-4 frakciót (feltételezett,
egyenletes súllyal), hanem a TESZTELT oldal kap több frakciót a saját
boltjába, és a GreedyBuyAlgorithm hagyja, hogy a paklija természetesen,
magától alakuljon ki vásárlás közben. A meccs(ek) végén LEMÉRJÜK, ténylegesen
milyen arányban áll össze a paklija (ez adja az X-et -- nem egy kitalált
1/k súly!), majd ezt a ténylegesen kialakult paklit teszteljük az 5 tiszta
egy-frakciós ellenféllel szemben -- ez adja az Y-t (győzelmi arány
ellenfelenként). Ebből a néhány valódi, szimulált "horgony-pontból" egy
egyszerű additív interpolációval (lásd `interpolate_from_base`) olcsón
becsülhetők további, nem közvetlenül lemért kombinációk is -- ezt a becslést
mindig a ténylegesen lemért értékkel vetjük össze, nem helyettesítjük vele.

FONTOS ADAPTÁCIÓK -- olvasd el, mielőtt futtatod!
--------------------------------------------------------------
1) KÜLÖN BOLTJA VAN MINDKÉT OLDALNAK (nem egy közös, megosztott
   GameDeck-ből húznak, ahogy az igazi játékban), hogy a két oldal
   fraciópool-ját tisztán, egymástól függetlenül lehessen szabályozni.
2) ScrapOwnCard: a scrap-ből eldobott lap VISSZAKERÜL a saját
   `shop_pool`-ba (tehát később újra megjelenhet a saját boltban) --
   ez felel meg annak, hogy "csak úgy van értelme".
3) ScrapFromShop: KÉT célpontja van, ahogy a valódi kártya szándéka is
   akadályozás. (a) Az ELLENFÉL boltjából (a 0. pénz-slot kivételével)
   kiválasztja a számukra legértékesebbnek tűnő lapot (GreedyBuyAlgorithm
   az ELLENFÉL frakció-eloszlásával pontozva) és eldobja -- ez az eredeti
   "vedd el előle a legjobbat" akadályozás. (b) A SAJÁT boltjából eldobja
   a jelenleg meg NEM vehető (túl drága) lapok közül a leggyengébbet.
   Mindkét eldobott lap a SAJÁT tulajdonosa `shop_pool`-jába kerül vissza
   (nem egy közösbe), így mindkét fél saját magának "gyűjti vissza" a
   kidobott lapjait.
4) StealCard: véletlenszerű lapot lop (DeckGenerator.GetCard == random
   élesben is), az AntiShow-blokkolást megtartottam.
5) Nincs terep-módosító (EffectsTerrainAmount) -- ez szándékos, nem
   hiányosság: alap támadásértékkel számolunk.
6) A GreedyBuyAlgorithm/GetWeightTable/effekt-index sorrend PONTOSAN
   a ForeGround.cs/Card.cs enum-sorrendjét követi (lásd EFFECT_ORDER),
   mert a pontszámformula `(13 - effekt_index)` ettől függ.
7) A tiszta 1-frakciós (k=1) sorban az önmagával szembeni mezőt 0-nak
   hagytam (mint az eredeti y_def-ben) -- "ne kontrázz önmagaddal".
"""

import os
import random
import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models
from itertools import combinations

# -----------------------------------------------------------------------
# 1) Alapadatok -- Card.cs enum-sorrendekkel megegyezően!
# -----------------------------------------------------------------------

FRACTIONS = ["Alliance", "CollectorCult", "Empire", "Machines", "TheEye"]  # index 0-4
NONE_FRACTION = 5  # Card.Fraction.None

EFFECT_ORDER = [
    "scrap_enemy", "scrap_shop", "anti_show", "steal", "draw",
    "scrap_own", "attack_bonus", "health_bonus", "money_bonus",
    "show_hand", "show_deck", "self_destruct", "none",
]
EFFECT_IDX = {e: i for i, e in enumerate(EFFECT_ORDER)}

INIT_HEALTH = 90
HAND_SIZE = 5
MAX_TURNS = 2000
SHOP_SLOTS = 6
START_MONEY_COUNT = 10

# (attack, health, money, price, effect, effect_amount, count)
FRACTION_CARDS = {
    "TheEye": [
        (1, 0, 0, 1, "show_hand", 1, 3),      # Drónok
        (0, 0, 2, 2, "health_bonus", 2, 3),   # Média
        (0, 0, 1, 3, "anti_show", 1, 3),      # Ellenhírszerzés
        (1, 0, 1, 3, "show_deck", 2, 2),      # Hírszerzés
        (5, 0, 0, 3, "attack_bonus", 2, 2),   # Lopakodó
        (2, 1, 0, 4, "steal", 1, 2),          # Korrupció
        (0, 3, 3, 4, "money_bonus", 2, 2),    # Ügyvéd
        (7, 0, 0, 4, "anti_show", 1, 2),      # Szabotázs
        (0, 0, 0, 4, "steal", 1, 2),          # Kém
        (0, 1, 0, 5, "show_hand", 5, 2),      # Műhold
        (4, 2, 2, 6, "steal", 1, 1),          # Báb
        (7, 3, 0, 8, "show_deck", 10, 1),     # A tanács
        (8, 0, 2, 8, "steal", 1, 1),          # Mr. Senki
    ],
    "Empire": [
        (1, 0, 0, 1, "none", 0, 3),           # Milícia
        (1, 0, 0, 1, "draw", 1, 3),           # Felderítő
        (2, 0, 0, 2, "attack_bonus", 1, 3),   # Gyalogság
        (3, 0, 0, 3, "attack_bonus", 1, 2),   # Gépesített gyalogság
        (4, 0, 0, 3, "none", 0, 2),           # Nehéz gyalogság
        (6, 0, 0, 4, "attack_bonus", 2, 2),   # Specialista
        (4, 0, 0, 4, "scrap_enemy", 1, 2),    # Légelhárító
        (6, 0, 0, 5, "scrap_enemy", 1, 2),    # Aknamező
        (8, 0, 0, 6, "scrap_enemy", 1, 1),    # Termoszférikus bombázás
        (6, 0, 1, 7, "health_bonus", 2, 2),   # Komp
        (7, 0, 2, 7, "draw", 1, 2),           # Romboló
        (7, 2, 0, 8, "attack_bonus", 3, 1),   # Tábornok
        (8, 0, 0, 8, "draw", 2, 1),           # Az uralkodó
    ],
    "Alliance": [
        (0, 2, 0, 1, "none", 0, 3),           # Gyógyszerek
        (0, 3, 0, 2, "health_bonus", 1, 3),   # Orvosi csomag
        (0, 4, 0, 3, "health_bonus", 1, 3),   # Traumakészlet
        (0, 0, 3, 3, "money_bonus", 2, 2),    # Kereskedő
        (0, 1, 2, 3, "scrap_shop", 1, 2),     # Kereskedelmi állomás
        (3, 1, 0, 4, "scrap_shop", 1, 2),     # Szankciók
        (0, 5, 2, 4, "money_bonus", 1, 2),    # Követség
        (0, 3, 2, 3, "money_bonus", 1, 2),    # Tudósok
        (0, 2, 3, 5, "scrap_shop", 2, 2),     # Fellegvár
        (0, 5, 3, 6, "health_bonus", 5, 2),   # Utópia
        (2, 5, 0, 7, "money_bonus", 5, 1),    # Vezető tudós
        (0, 2, 2, 8, "scrap_shop", 5, 1),     # Nagykövet
        (3, 5, 5, 8, "draw", 1, 1),           # Miniszterelnök
    ],
    "Machines": [
        (2, 0, 0, 1, "none", 0, 3),           # Harci drón
        (3, 0, 0, 2, "attack_bonus", 1, 3),   # FR-2.1.7
        (4, 0, 0, 3, "attack_bonus", 1, 3),   # SP-0.2.3
        (5, 0, 0, 4, "attack_bonus", 2, 3),   # HX-1.0.1
        (4, 3, 0, 3, "attack_bonus", 3, 2),   # Bootolás..
        (6, 0, 0, 4, "attack_bonus", 2, 2),   # Megállíthatatlan
        (7, 0, 0, 5, "attack_bonus", 3, 2),   # Robotgép
        (9, 0, 0, 6, "scrap_enemy", 1, 1),    # Orbitális bombázás
        (5, 0, 0, 7, "attack_bonus", 8, 2),   # Csapatszállító
        (6, 0, 0, 7, "attack_bonus", 6, 2),   # Cirkáló
        (5, 5, 0, 8, "draw", 2, 1),           # A Gyár
        (9, 0, 0, 8, "attack_bonus", 7, 1),   # A flotta
        (9, 5, 0, 8, "attack_bonus", 9, 1),   # Az intelligencia
    ],
    "CollectorCult": [
        (0, 0, 2, 1, "none", 0, 3),           # Fekete piac
        (1, 0, 1, 2, "scrap_own", 1, 3),      # Gyűjtögetők
        (0, 0, 2, 2, "money_bonus", 1, 3),    # Relikvia
        (0, 1, 2, 2, "health_bonus", 1, 2),   # Szent iratok
        (3, 0, 1, 3, "attack_bonus", 1, 2),   # Felavatott
        (5, 0, 1, 4, "scrap_own", 1, 2),      # Zarándok
        (3, 0, 0, 4, "scrap_own", 1, 2),      # Eretnek
        (0, 0, 3, 5, "money_bonus", 2, 2),    # Az archívum
        (5, 0, 1, 6, "scrap_shop", 2, 2),     # Az irattáros
        (6, 0, 0, 7, "health_bonus", 8, 2),   # Transzcendencia
        (6, 0, 0, 7, "attack_bonus", 4, 1),   # Cyborg
        (0, 3, 5, 8, "attack_bonus", 8, 1),   # Áldott Mars
        (7, 0, 2, 8, "scrap_own", 1, 1),      # Az alkotó
    ],
}

MONEY_CARD = dict(fraction=NONE_FRACTION, attack=0, health=0, money=1, price=0, effect="none", amount=0)
SHOP0_CARDS = [
    dict(fraction=NONE_FRACTION, attack=0, health=0, money=2, price=2, effect="self_destruct", amount=1),  # Gyémánt
    dict(fraction=NONE_FRACTION, attack=0, health=0, money=3, price=3, effect="self_destruct", amount=1),  # Igazgyöngy
]


def make_card(fraction_idx, atk, hp, money, price, effect, amount):
    return dict(fraction=fraction_idx, attack=atk, health=hp, money=money,
                price=price, effect=effect, amount=amount)


def build_shop_pool(fraction_names):
    """Egy oldal boltjának húzópakli-ja: a megadott frakció(k) OSSZES lapja,
    a DeckGenerator.GenDeck()-ben szereplő valódi darabszámmal."""
    pool = []
    for fname in fraction_names:
        fidx = FRACTIONS.index(fname)
        for atk, hp, money, price, effect, amount, count in FRACTION_CARDS[fname]:
            pool.extend([make_card(fidx, atk, hp, money, price, effect, amount)] * count)
    return pool


# -----------------------------------------------------------------------
# 2) GreedyBuyAlgorithm -- 1:1 port a ForeGround.cs-ből
# -----------------------------------------------------------------------

def get_weight_table(my_hp, enemy_hp):
    aggro_threshold = INIT_HEALTH * 0.2
    danger_threshold = INIT_HEALTH * 0.3
    if enemy_hp <= aggro_threshold or my_hp <= danger_threshold:
        return (2.5, 0.5, 0.3, 0.5)
    return (1.0, 1.0, 1.0, 1.5)


def greedy_buy(my_hp, enemy_hp, available_cards, frac_dist, inverse=False):
    """available_cards: dict-listája; frac_dist: 6 elemű lista."""
    w = get_weight_table(my_hp, enemy_hp)
    best_card, best_score = None, float("-inf")
    for card in available_cards:
        atk_bonus = card["amount"] if card["effect"] == "attack_bonus" else 0
        hp_bonus = card["amount"] if card["effect"] == "health_bonus" else 0
        money_bonus = card["amount"] if card["effect"] == "money_bonus" else 0
        score = (card["attack"] + atk_bonus) * w[0]
        score += (card["health"] + hp_bonus) * w[1]
        score += (card["money"] + money_bonus) * w[2]
        score += frac_dist[card["fraction"]] * (13 - EFFECT_IDX[card["effect"]]) * w[3]
        if inverse:
            score = -score
        if score > best_score:
            best_score, best_card = score, card
    return best_card


def fraction_distribution(hand, deck, scrap):
    counts = [0] * 6
    total = 0
    for card in hand + deck + scrap:
        counts[card["fraction"]] += 1
        total += 1
    if total == 0:
        return [0.0] * 6
    return [c / total for c in counts]


# -----------------------------------------------------------------------
# 3) Egy oldal (jatekos vagy ellenfel) allapota + boltja
# -----------------------------------------------------------------------

class Side:
    def __init__(self, shop_fraction_names, rng):
        self.rng = rng
        self.shop_pool = build_shop_pool(shop_fraction_names)
        self.rng.shuffle(self.shop_pool)
        self.deck = [dict(MONEY_CARD) for _ in range(START_MONEY_COUNT)]
        self.rng.shuffle(self.deck)
        self.scrap = []
        self.hand = []
        self.hp = INIT_HEALTH
        self.money = 0
        self.pending_scrap = 0  # ScrapEnemyCard miatt köre elején eldobandó lapok
        self.shop = [None] * SHOP_SLOTS
        self.refill_shop()
        self.draw(HAND_SIZE)

    def refill_shop(self):
        if self.shop[0] is None:
            self.shop[0] = dict(self.rng.choice(SHOP0_CARDS))
        for i in range(1, SHOP_SLOTS):
            if self.shop[i] is None:
                if self.shop_pool:
                    idx = self.rng.randrange(len(self.shop_pool))
                    self.shop[i] = self.shop_pool.pop(idx)
                else:
                    self.shop[i] = dict(self.rng.choice(SHOP0_CARDS))

    def draw(self, n):
        for _ in range(n):
            if not self.deck:
                self.deck.extend(self.scrap)
                self.scrap.clear()
                self.rng.shuffle(self.deck)
            if not self.deck:
                break
            self.hand.append(self.deck.pop())

    def frac_dist(self):
        return fraction_distribution(self.hand, self.deck, self.scrap)

    def frac_ratio5(self):
        """Csak az 5 valodi frakcio aranya (a Pénz/Gyémánt/Igazgyöngy None-
        lapok nélkül, azokra renormalizálva) -- ez adja a MÉRT X-et."""
        counts = np.zeros(5, dtype=np.float32)
        for card in self.hand + self.deck + self.scrap:
            if card["fraction"] != NONE_FRACTION:
                counts[card["fraction"]] += 1
        s = counts.sum()
        return counts / s if s > 0 else np.zeros(5, dtype=np.float32)


# -----------------------------------------------------------------------
# 4) Egy teljes meccs szimulálása -- mindket oldal Heuristic (GreedyBuy)
#    donteshozatallal, sajat, kulon bolttal
# -----------------------------------------------------------------------

def simulate_match(tested_fractions, opponent_fractions, rng, max_turns=MAX_TURNS):
    sides = [Side(tested_fractions, rng), Side(opponent_fractions, rng)]
    active = 0

    for _turn in range(max_turns):
        me, other = sides[active], sides[1 - active]

        # 1) elozo korben ram kuldott ScrapEnemyCard feloldasa (Heuristic mod: veletlen)
        if me.pending_scrap > 0:
            n = min(me.pending_scrap, len(me.hand))
            for _ in range(n):
                idx = rng.randrange(len(me.hand))
                me.scrap.append(me.hand.pop(idx))
            me.pending_scrap = 0

        # 2) kezben levo lapok kijatszasa
        attack_gain = 0
        my_dist = me.frac_dist()
        for card in me.hand:
            attack_gain += card["attack"]
            me.money += card["money"]
            me.hp += card["health"]
            eff, amt = card["effect"], card["amount"]
            if eff == "attack_bonus":
                attack_gain += amt
            elif eff == "health_bonus":
                me.hp += amt
            elif eff == "draw":
                me.draw(amt)
            elif eff == "scrap_enemy":
                other.pending_scrap += amt
            elif eff == "steal":
                if not any(c["effect"] == "anti_show" for c in other.hand):
                    for _ in range(amt):
                        if not other.hand:
                            break
                        idx = rng.randrange(len(other.hand))
                        me.scrap.append(other.hand.pop(idx))
            elif eff == "scrap_own":
                if me.scrap and (len(me.hand) + len(me.deck) + len(me.scrap) - amt > 6):
                    n = 0
                    while n < amt and me.scrap:
                        worst = greedy_buy(me.hp, other.hp, me.scrap, my_dist, inverse=True)
                        me.scrap.remove(worst)
                        me.shop_pool.append(worst)  # visszakerul a SAJAT bolt-poolba
                        n += 1
            elif eff == "scrap_shop":
                # Ket celpontja van: (1) az ELLENFEL boltjabol kivalasztja a
                # szamukra legertekesebb (nem 0.) slotot es eldobja -- ez az
                # eredeti "akadalyozas" szandeka; (2) a SAJAT boltjabol
                # kidobja egy olyan lapot, amit epp NEM tud megvenni (a
                # legrosszabbat ezek kozul). Mindket eldobott lap a SAJAT
                # tulajdonosa bolt-poolaba kerul vissza (nem kozos keszletbe).
                other_dist = other.frac_dist()
                for _ in range(amt):
                    other_present = [c for c in other.shop[1:] if c is not None]
                    if not other_present:
                        break
                    target = greedy_buy(other.hp, me.hp, other_present, other_dist, inverse=False)
                    idx = other.shop.index(target)
                    other.shop[idx] = None
                    other.shop_pool.append(target)
                other.refill_shop()
                for _ in range(amt):
                    unaffordable = [c for c in me.shop if c is not None and c["price"] > me.money]
                    if not unaffordable:
                        break
                    worst_mine = greedy_buy(me.hp, other.hp, unaffordable, my_dist, inverse=True)
                    idx = me.shop.index(worst_mine)
                    me.shop[idx] = None
                    me.shop_pool.append(worst_mine)
                me.refill_shop()
            # anti_show / show_hand / show_deck / none: NO-OP (a valodi kodban is azok)

        # kijatszott lapok scrap-be, kiveve az onmegsemmisulo lapokat
        for card in me.hand:
            if card["effect"] != "self_destruct":
                me.scrap.append(card)
        me.hand.clear()

        # 3) vasarlas a sajat boltbol, amig futja a penzbol
        my_dist = me.frac_dist()
        while True:
            affordable = [c for c in me.shop if c is not None and c["price"] <= me.money]
            if not affordable:
                break
            to_buy = greedy_buy(me.hp, other.hp, affordable, my_dist, inverse=False)
            idx = me.shop.index(to_buy)
            me.shop[idx] = None
            me.scrap.append(to_buy)   # BuyFromShop: a megvett lap egyenesen a scrap-be kerul
            me.money -= to_buy["price"]
            me.refill_shop()

        # 4) kor vege: penz nullazasa, tamadas feloldasa, kezfeltoltes
        me.money = 0
        other.hp -= attack_gain
        me.draw(HAND_SIZE)

        if other.hp <= 0 or me.hp <= 0:
            break
        active = 1 - active

    ratio0 = sides[0].frac_ratio5()
    ratio1 = sides[1].frac_ratio5()

    if sides[0].hp <= 0 and sides[1].hp <= 0:
        return 0.5, ratio0, ratio1
    if sides[1].hp <= 0:
        return 0.0, ratio0, ratio1   # sides[0] nyer
    if sides[0].hp <= 0:
        return 1.0, ratio0, ratio1  # sides[1] nyer
    outcome = 0.0 if sides[0].hp >= sides[1].hp else 1.0  # idotullepes -> nagyobb HP nyer
    return outcome, ratio0, ratio1


def win_rate(my_fractions, enemy_fractions, n_matches, seed=None):
    rng = random.Random(seed)
    enemy_wins = 0.0
    for _ in range(n_matches):
        outcome, _r0, _r1 = simulate_match(my_fractions, enemy_fractions, rng)
        enemy_wins += outcome
    return 1.0 - (enemy_wins / n_matches)


def measure_combo(tested_fractions, n_matches, seed=None):
    """A kombinalt bolt az ELSOKENT lepo (sides[0]) oldalon van. A
    GreedyBuyAlgorithm hagyja termeszetesen kialakulni a paklijat vasarlas
    kozben -- ezt lemerjuk (x = a tenylegesen kialakult frakcio-arany), es
    kozben az 5 tiszta egy-frakcios ellenfellel szemben teszteljuk
    (gyozelmi arany ellenfelenkent -> y_raw, MEG NEM normalizalt)."""
    rng = random.Random(seed)
    y_raw = np.zeros(5, dtype=np.float32)
    ratio_sum = np.zeros(5, dtype=np.float32)
    ratio_n = 0
    for opp_idx, opp_name in enumerate(FRACTIONS):
        wins = 0.0
        for _ in range(n_matches):
            outcome, ratio0, _ratio1 = simulate_match(tested_fractions, [opp_name], rng)
            wins += (1.0 - outcome)
            ratio_sum += ratio0
            ratio_n += 1
        y_raw[opp_idx] = wins / n_matches
    x = ratio_sum / ratio_n if ratio_n > 0 else np.zeros(5, dtype=np.float32)
    return x, y_raw


def measure_combo_reversed(tested_fractions, n_matches, seed=None):
    """Ugyanaz, mint measure_combo, csak a kombinalt bolt a MASODIKKENT
    lepo (sides[1]) oldalon van -- igy a korrendbol (ki lep elobb) adodo
    esetleges elonyt/hatranyt is lemerjuk mindket iranybol, nem csak
    egyszer."""
    rng = random.Random(seed)
    y_raw = np.zeros(5, dtype=np.float32)
    ratio_sum = np.zeros(5, dtype=np.float32)
    ratio_n = 0
    for opp_idx, opp_name in enumerate(FRACTIONS):
        wins = 0.0
        for _ in range(n_matches):
            outcome, _ratio0, ratio1 = simulate_match([opp_name], tested_fractions, rng)
            wins += outcome  # outcome==1 -> a kombinalt (masodik) oldal nyer
            ratio_sum += ratio1
            ratio_n += 1
        y_raw[opp_idx] = wins / n_matches
    x = ratio_sum / ratio_n if ratio_n > 0 else np.zeros(5, dtype=np.float32)
    return x, y_raw


def interpolate_from_base(base_raw, ratio):
    """Olcso becsles egy mert X (frakcio-arany) alapjan, a k=1 alap (nyers,
    NEM normalizalt) matrix additiv kombinaciojaval: ha a paklim ratio[j]
    aranyban all frakcio j-bol, a becsult gyozelmi arany az i. ellenfel
    ellen kb. sum_j ratio[j] * base_raw[i][j]. Ez csak egy gyors
    ellenorzo/becslo eszkoz -- MINDIG vesd ossze a tenyleges meressel!"""
    base_raw = np.asarray(base_raw, dtype=np.float32)
    return base_raw @ ratio


# -----------------------------------------------------------------------
# 5) Tanitohalmaz epitese.
#    k=1: alap eset, tiszta ellenfel-frakciok -- ez adja a "nyers" alap-
#         matrixot is (base_raw), amit a k>=2 becslesnel hasznalunk.
#    k=2..4: a kombinalt bolt oldalanak TENYLEGESEN kialakult frakcio-
#         aranyat merjuk (X), MINDKET iranybol (a kombinalt oldal egyszer
#         elsokent, egyszer masodikkent lep -- lasd measure_combo /
#         measure_combo_reversed), es minden iranyt 10 fuggetlen kotegre
#         bontva (nem egyetlen atlagolt sorra), hogy tobb, valodi szorast
#         hordozo tanitopont keletkezzen ugyanabbol a szimulacios
#         koltsegvetesbol.
# -----------------------------------------------------------------------

def build_training_data(n_matches_base=1000, n_per_batch=100, n_batches=10, seed=42,
                         cache_path="strategy_training_data.npz"):
    if cache_path and os.path.exists(cache_path):
        print(f"Mert adatok betoltve gyorsitotarbol: {cache_path}")
        data = np.load(cache_path)
        return data["X"], data["Y"]

    n_idx = list(range(5))
    X_rows, Y_rows = [], []

    # --- k=1: tiszta alap-matrix, es a nyers (nem normalizalt) valtozata is ---
    base_raw = np.zeros((5, 5), dtype=np.float32)  # base_raw[ellenfel][en]
    for i in n_idx:  # i = ellenfel frakcioja
        candidate_idx = [j for j in n_idx if j != i]
        row_raw = np.zeros(5, dtype=np.float32)
        for j in candidate_idx:
            row_raw[j] = win_rate([FRACTIONS[j]], [FRACTIONS[i]], n_matches_base,
                                   seed=seed + i * 10 + j)
        base_raw[i] = row_raw
        s = row_raw.sum()
        if s > 0:
            y = row_raw / s
        else:
            y = np.zeros(5, dtype=np.float32)
            for j in candidate_idx:
                y[j] = 1.0 / len(candidate_idx)
        x = np.zeros(5, dtype=np.float32)
        x[i] = 1.0
        X_rows.append(x)
        Y_rows.append(y)
        print(f"[k=1] ellenfel={FRACTIONS[i]:>13} -> y={y}")

    # --- k=2..4: kombinalt bolt, MINDKET iranybol, iranyonkent n_batches kotegben ---
    directions = [("A (kombinalt lep elsokent)", measure_combo),
                  ("B (kombinalt lep masodikkent)", measure_combo_reversed)]

    for k in range(2, 5):
        for combo_idx in combinations(n_idx, k):
            combo_names = [FRACTIONS[i] for i in combo_idx]
            combo_x_all, combo_y_all, combo_pred_all = [], [], []

            for dir_tag, measure_fn in directions:
                for batch in range(n_batches):
                    batch_seed = (seed + k * 100000 + sum(combo_idx) * 1000
                                  + (0 if dir_tag[0] == "A" else 50000) + batch)
                    x_measured, y_raw = measure_fn(combo_names, n_per_batch, seed=batch_seed)

                    s = y_raw.sum()
                    y = y_raw / s if s > 0 else np.full(5, 0.2, dtype=np.float32)

                    y_pred_raw = interpolate_from_base(base_raw, x_measured)
                    ps = y_pred_raw.sum()
                    y_pred = y_pred_raw / ps if ps > 0 else np.full(5, 0.2, dtype=np.float32)

                    X_rows.append(x_measured)
                    Y_rows.append(y)
                    combo_x_all.append(x_measured)
                    combo_y_all.append(y)
                    combo_pred_all.append(y_pred)

            x_mean = np.mean(combo_x_all, axis=0)
            x_std = np.std(combo_x_all, axis=0)
            y_mean = np.mean(combo_y_all, axis=0)
            diff_mean = np.abs(np.array(combo_y_all) - np.array(combo_pred_all)).mean()
            print(f"[k={k}] bolt={combo_names}: {len(combo_x_all)} mérés "
                  f"(2 irany x {n_batches} koteg) kesz")
            print(f"           X atlag={x_mean}  X szoras={x_std}")
            print(f"           y atlag={y_mean}  |  additiv-becsult elteres (atlag): {diff_mean:.3f}")

    X = np.array(X_rows, dtype=np.float32)
    Y = np.array(Y_rows, dtype=np.float32)
    if cache_path:
        np.savez(cache_path, X=X, Y=Y, base_raw=base_raw)
        print(f"\nOsszesen {X.shape[0]} tanito sor elmentve ide: {cache_path}")
        print("Ha csak a halot modositod, ujra ezt a fajlt fogja betolteni "
              "(nem szimulal ujra), amig a fajl letezik es nem torlod.")
    return X, Y


# -----------------------------------------------------------------------
# 6) Hálótanítás -- most már közvetlenül a mért (X, Y) párokon, NEM a
#    régi weight_sets() lineáris interpolációján keresztül.
# -----------------------------------------------------------------------

if __name__ == "__main__":
    print("Tanitoadat generalasa: k=1 tiszta ellenfel-frakciok (1000 meccs/"
          "ellenfel), k=2..4 kombinalt bolt MINDKET iranybol, iranyonkent "
          "10x100 meccses fuggetlen kotegben...")
    X_train, y_train = build_training_data(n_matches_base=1000, n_per_batch=100,
                                            n_batches=10, seed=42)

    print("X_train shape:", X_train.shape)
    print("y_train shape:", y_train.shape)
    print("pelda sor:")
    print(X_train[0], "=>", y_train[0])

    leaky_model = models.Sequential([
        layers.Input(shape=(5,)),
        layers.Dense(5, activation="softmax"),
    ])

    leaky_model.compile(
        optimizer="adam",
        loss="categorical_crossentropy",
        metrics=["accuracy"],
    )

    history2 = leaky_model.fit(
        X_train, y_train, batch_size=32, epochs=500, validation_split=0.1, shuffle=True
    )

    # mentés
    leaky_model.export("D:\\MC\\TENSORS")
