import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models

FRACTION_DIM = 5
EFFECT_DIM = 12
CARD_DIM = 22
INPUT_DIM = FRACTION_DIM + 2 * CARD_DIM  # 49
OUTPUT_DIM = 2

# -----------------------------------------------------------------------------
# Discarding AI v2
#
# Shared context:
#   5 own fraction distribution / synergy context
#
# Per card (22 features):
#   price_ratio     1
#   fraction        5 one-hot
#   effects         12 one-hot
#   free_effect     1
#   attack_norm     1
#   health_norm     1
#   money_norm      1
#
# Pairwise input:
#   5 + 22 + 22 = 49
#
# Output:
#   [keep_card_A, keep_card_B]
# -----------------------------------------------------------------------------

FRACTIONS = ["Alliance", "CollectorCult", "Empire", "Machines", "TheEye"] 
EFFECTS = [
    "ScrapEnemyCard", "ScrapFromShop", "AntiShow", "StealCard", "DrawCard",
    "ScrapOwnCard", "AttackBonus", "HealthBonus", "MoneyBonus",
    "ShowHand", "ShowDeck", "SelfDestruct",
]

CARD_DB = [
    # --- THE EYE ---
    dict(name="Drones", fraction="TheEye", attack=1, health=0, money=0, price=1, effect="ShowHand", effect_amount=1, effect_requirement="None"),
    dict(name="Media", fraction="TheEye", attack=0, health=0, money=2, price=2, effect="HealthBonus", effect_amount=2, effect_requirement="TheEye"),
    dict(name="Counter-Intelligence", fraction="TheEye", attack=0, health=0, money=1, price=3, effect="AntiShow", effect_amount=1, effect_requirement="None"),
    dict(name="Intelligence", fraction="TheEye", attack=1, health=0, money=1, price=3, effect="ShowDeck", effect_amount=2, effect_requirement="None"),
    dict(name="Stealth Unit", fraction="TheEye", attack=5, health=0, money=0, price=3, effect="AttackBonus", effect_amount=2, effect_requirement="TheEye"),
    dict(name="Corruption", fraction="TheEye", attack=2, health=1, money=0, price=4, effect="StealCard", effect_amount=1, effect_requirement="TheEye"),
    dict(name="Lawyer", fraction="TheEye", attack=0, health=3, money=3, price=4, effect="MoneyBonus", effect_amount=2, effect_requirement="TheEye"),
    dict(name="Sabotage", fraction="TheEye", attack=7, health=0, money=0, price=4, effect="AntiShow", effect_amount=1, effect_requirement="None"),
    dict(name="Spy", fraction="TheEye", attack=0, health=0, money=0, price=4, effect="StealCard", effect_amount=1, effect_requirement="None"),
    dict(name="Satellite", fraction="TheEye", attack=0, health=1, money=0, price=5, effect="ShowHand", effect_amount=5, effect_requirement="None"),
    dict(name="Puppet", fraction="TheEye", attack=4, health=2, money=2, price=6, effect="StealCard", effect_amount=1, effect_requirement="None"),
    dict(name="The Council", fraction="TheEye", attack=7, health=3, money=0, price=8, effect="ShowDeck", effect_amount=10, effect_requirement="None"),
    dict(name="Mr. Nobody", fraction="TheEye", attack=8, health=0, money=2, price=8, effect="StealCard", effect_amount=1, effect_requirement="None"),
    # --- EMPIRE ---
    dict(name="Militia", fraction="Empire", attack=1, health=0, money=0, price=1, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="Scout", fraction="Empire", attack=1, health=0, money=0, price=1, effect="DrawCard", effect_amount=1, effect_requirement="Empire"),
    dict(name="Infantry", fraction="Empire", attack=2, health=0, money=0, price=2, effect="AttackBonus", effect_amount=1, effect_requirement="Empire"),
    dict(name="Mechanized Infantry", fraction="Empire", attack=3, health=0, money=0, price=3, effect="AttackBonus", effect_amount=1, effect_requirement="Empire"),
    dict(name="Heavy Infantry", fraction="Empire", attack=4, health=0, money=0, price=3, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="Specialist", fraction="Empire", attack=6, health=0, money=0, price=4, effect="AttackBonus", effect_amount=2, effect_requirement="Empire"),
    dict(name="Anti-Air", fraction="Empire", attack=4, health=0, money=0, price=4, effect="ScrapEnemyCard", effect_amount=1, effect_requirement="Empire"),
    dict(name="Minefield", fraction="Empire", attack=6, health=0, money=0, price=5, effect="ScrapEnemyCard", effect_amount=1, effect_requirement="Empire"),
    dict(name="Thermospheric Bombardment", fraction="Empire", attack=8, health=0, money=0, price=6, effect="ScrapEnemyCard", effect_amount=1, effect_requirement="None"),
    dict(name="Ferry", fraction="Empire", attack=6, health=0, money=1, price=7, effect="HealthBonus", effect_amount=2, effect_requirement="Empire"),
    dict(name="Destroyer", fraction="Empire", attack=7, health=0, money=2, price=7, effect="DrawCard", effect_amount=1, effect_requirement="None"),
    dict(name="General", fraction="Empire", attack=7, health=2, money=0, price=8, effect="AttackBonus", effect_amount=3, effect_requirement="Empire"),
    dict(name="The Ruler", fraction="Empire", attack=8, health=0, money=0, price=8, effect="DrawCard", effect_amount=2, effect_requirement="None"),
    # --- ALLIANCE ---
    dict(name="Medicine", fraction="Alliance", attack=0, health=2, money=0, price=1, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="Medical Kit", fraction="Alliance", attack=0, health=3, money=0, price=2, effect="HealthBonus", effect_amount=1, effect_requirement="Alliance"),
    dict(name="Trauma Kit", fraction="Alliance", attack=0, health=4, money=0, price=3, effect="HealthBonus", effect_amount=1, effect_requirement="Alliance"),
    dict(name="Trader", fraction="Alliance", attack=0, health=0, money=3, price=3, effect="MoneyBonus", effect_amount=2, effect_requirement="Alliance"),
    dict(name="Trading Post", fraction="Alliance", attack=0, health=1, money=2, price=3, effect="ScrapFromShop", effect_amount=1, effect_requirement="Alliance"),
    dict(name="Sanctions", fraction="Alliance", attack=3, health=1, money=0, price=4, effect="ScrapFromShop", effect_amount=1, effect_requirement="None"),
    dict(name="Embassy", fraction="Alliance", attack=0, health=5, money=2, price=4, effect="MoneyBonus", effect_amount=1, effect_requirement="Alliance"),
    dict(name="Scientists", fraction="Alliance", attack=0, health=3, money=2, price=3, effect="MoneyBonus", effect_amount=1, effect_requirement="Alliance"),
    dict(name="Citadel", fraction="Alliance", attack=0, health=2, money=3, price=5, effect="ScrapFromShop", effect_amount=2, effect_requirement="Alliance"),
    dict(name="Utopia", fraction="Alliance", attack=0, health=5, money=3, price=6, effect="HealthBonus", effect_amount=5, effect_requirement="Alliance"),
    dict(name="Head Scientist", fraction="Alliance", attack=2, health=5, money=0, price=7, effect="MoneyBonus", effect_amount=5, effect_requirement="Alliance"),
    dict(name="Ambassador", fraction="Alliance", attack=0, health=2, money=2, price=8, effect="ScrapFromShop", effect_amount=5, effect_requirement="Alliance"),
    dict(name="Prime Minister", fraction="Alliance", attack=3, health=5, money=5, price=8, effect="DrawCard", effect_amount=1, effect_requirement="Alliance"),
    # --- MACHINES ---
    dict(name="Combat Drone", fraction="Machines", attack=2, health=0, money=0, price=1, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="FR-2.1.7", fraction="Machines", attack=3, health=0, money=0, price=2, effect="AttackBonus", effect_amount=1, effect_requirement="Machines"),
    dict(name="SP-0.2.3", fraction="Machines", attack=4, health=0, money=0, price=3, effect="AttackBonus", effect_amount=1, effect_requirement="Machines"),
    dict(name="HX-1.0.1", fraction="Machines", attack=5, health=0, money=0, price=4, effect="AttackBonus", effect_amount=2, effect_requirement="Machines"),
    dict(name="Booting..", fraction="Machines", attack=4, health=3, money=0, price=3, effect="AttackBonus", effect_amount=3, effect_requirement="Machines"),
    dict(name="Unstoppable", fraction="Machines", attack=6, health=0, money=0, price=4, effect="AttackBonus", effect_amount=2, effect_requirement="Machines"),
    dict(name="Robot Drone", fraction="Machines", attack=7, health=0, money=0, price=5, effect="AttackBonus", effect_amount=3, effect_requirement="Machines"),
    dict(name="Orbital Bombardment", fraction="Machines", attack=9, health=0, money=0, price=6, effect="ScrapEnemyCard", effect_amount=1, effect_requirement="Machines"),
    dict(name="Troop Transport", fraction="Machines", attack=5, health=0, money=0, price=7, effect="AttackBonus", effect_amount=8, effect_requirement="Machines"),
    dict(name="Cruiser", fraction="Machines", attack=6, health=0, money=0, price=7, effect="AttackBonus", effect_amount=6, effect_requirement="Machines"),
    dict(name="The Factory", fraction="Machines", attack=5, health=5, money=0, price=8, effect="DrawCard", effect_amount=2, effect_requirement="Machines"),
    dict(name="The Fleet", fraction="Machines", attack=9, health=0, money=0, price=8, effect="AttackBonus", effect_amount=7, effect_requirement="Machines"),
    dict(name="The Intelligence", fraction="Machines", attack=9, health=5, money=0, price=8, effect="AttackBonus", effect_amount=9, effect_requirement="Machines"),
    # --- COLLECTORCULT ---
    dict(name="Black Market", fraction="CollectorCult", attack=0, health=0, money=2, price=1, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="Collectors", fraction="CollectorCult", attack=1, health=0, money=1, price=2, effect="ScrapOwnCard", effect_amount=1, effect_requirement="CollectorCult"),
    dict(name="Relic", fraction="CollectorCult", attack=0, health=0, money=2, price=2, effect="MoneyBonus", effect_amount=1, effect_requirement="CollectorCult"),
    dict(name="Sacred Scriptures", fraction="CollectorCult", attack=0, health=1, money=2, price=2, effect="HealthBonus", effect_amount=1, effect_requirement="CollectorCult"),
    dict(name="Ordained", fraction="CollectorCult", attack=3, health=0, money=1, price=3, effect="AttackBonus", effect_amount=1, effect_requirement="CollectorCult"),
    dict(name="Pilgrim", fraction="CollectorCult", attack=5, health=0, money=1, price=4, effect="ScrapOwnCard", effect_amount=1, effect_requirement="CollectorCult"),
    dict(name="Heretic", fraction="CollectorCult", attack=3, health=0, money=0, price=4, effect="ScrapOwnCard", effect_amount=1, effect_requirement="None"),
    dict(name="The Archive", fraction="CollectorCult", attack=0, health=0, money=3, price=5, effect="MoneyBonus", effect_amount=2, effect_requirement="CollectorCult"),
    dict(name="The Archivist", fraction="CollectorCult", attack=5, health=0, money=1, price=6, effect="ScrapFromShop", effect_amount=2, effect_requirement="CollectorCult"),
    dict(name="Transcendence", fraction="CollectorCult", attack=6, health=0, money=0, price=7, effect="HealthBonus", effect_amount=8, effect_requirement="CollectorCult"),
    dict(name="Cyborg", fraction="CollectorCult", attack=6, health=0, money=0, price=7, effect="AttackBonus", effect_amount=4, effect_requirement="CollectorCult"),
    dict(name="Blessed Mars", fraction="CollectorCult", attack=0, health=3, money=5, price=8, effect="AttackBonus", effect_amount=8, effect_requirement="CollectorCult"),
    dict(name="The Creator", fraction="CollectorCult", attack=7, health=0, money=2, price=8, effect="ScrapOwnCard", effect_amount=1, effect_requirement="None"),
    # --- PÉNZLAPOK (frakciómentes) ---
    dict(name="Money", fraction="None", attack=0, health=0, money=1, price=0, effect="None", effect_amount=0, effect_requirement="None"),
    dict(name="Diamond", fraction="None", attack=0, health=0, money=2, price=2, effect="SelfDestruct", effect_amount=1, effect_requirement="None"),
    dict(name="Pearl", fraction="None", attack=0, health=0, money=3, price=3, effect="SelfDestruct", effect_amount=1, effect_requirement="None"),
]

# Normalizáló konstansok: a "true" (bázis + azonos típusú bónusz)
def _true_stat(card, stat, bonus_effect):
    base = card[stat]
    bonus = card["effect_amount"] if card["effect"] == bonus_effect else 0
    return base + bonus

ATTACK_NORM_MAX = max(_true_stat(c, "attack", "AttackBonus") for c in CARD_DB) or 1
HEALTH_NORM_MAX = max(_true_stat(c, "health", "HealthBonus") for c in CARD_DB) or 1
MONEY_NORM_MAX = max(_true_stat(c, "money", "MoneyBonus") for c in CARD_DB) or 1
PRICE_MAX = max(c["price"] for c in CARD_DB) or 1

# minél "balrébb" van egy effekt az enum sorrendben, annál fontosabb
EFFECT_PRIORITY = {name: (len(EFFECTS) - 1 - i) for i, name in enumerate(EFFECTS)}  # 11..0
EFFECT_PRIORITY["None"] = -1


def card_to_features(card):
    """22 dimenziós jellemzővektor egyetlen kártyához (lásd fájl fejléce)."""
    price_ratio = card["price"] / PRICE_MAX

    fraction_onehot = np.zeros(FRACTION_DIM, dtype=np.float32)
    if card["fraction"] in FRACTIONS:
        fraction_onehot[FRACTIONS.index(card["fraction"])] = 1.0

    effect_onehot = np.zeros(EFFECT_DIM, dtype=np.float32)
    if card["effect"] in EFFECTS:
        effect_onehot[EFFECTS.index(card["effect"])] = 1.0

    free_effect = 1.0 if (card["effect"] != "None" and card["effect_requirement"] == "None") else 0.0

    attack_norm = _true_stat(card, "attack", "AttackBonus") / ATTACK_NORM_MAX
    health_norm = _true_stat(card, "health", "HealthBonus") / HEALTH_NORM_MAX
    money_norm = _true_stat(card, "money", "MoneyBonus") / MONEY_NORM_MAX

    return np.concatenate([
        [price_ratio],
        fraction_onehot,
        effect_onehot,
        [free_effect],
        [attack_norm, health_norm, money_norm],
    ]).astype(np.float32)


DEFAULT_WEIGHTS = dict(
    atk=1.0,
    hp=1.0,
    money=1.5,
    effect=0.6,     # az EFFECT_PRIORITY (0..11) skálázása
    synergy=3.0,    # frakció-illeszkedés a jelenlegi eloszláshoz
    free=2.5,       # feltétel nélküli (EffectRequirement == None) effekt bónusza
    price=1.0,      # ár mint minőség-jelzés
)


def expert_score(card, context, weights=DEFAULT_WEIGHTS):
    """Egyetlen kártya 'igazi' értéke egy adott szinergia-kontextusban."""
    true_atk = _true_stat(card, "attack", "AttackBonus")
    true_hp = _true_stat(card, "health", "HealthBonus")
    true_money = _true_stat(card, "money", "MoneyBonus")

    base_value = weights["atk"] * true_atk + weights["hp"] * true_hp + weights["money"] * true_money
    effect_value = weights["effect"] * EFFECT_PRIORITY.get(card["effect"], -1)

    synergy = 0.0
    if card["fraction"] in FRACTIONS:
        synergy = weights["synergy"] * context[FRACTIONS.index(card["fraction"])]

    free_bonus = weights["free"] if (card["effect"] != "None" and card["effect_requirement"] == "None") else 0.0
    price_signal = weights["price"] * (card["price"] / PRICE_MAX)

    return base_value + effect_value + synergy + free_bonus + price_signal


def sample_context(rng):
    """Véletlen, de valósághű saját frakció-eloszlás (szinergia-kontextus).

    Dirichlet-mintavétel: az alpha<1 koncentráltabb (1-2 frakcióra épülő)
    paklikat, az alpha>=1 kiegyenlítettebb eloszlásokat ad.
    """
    alpha = rng.uniform(0.3, 2.0, size=FRACTION_DIM)
    dist = rng.dirichlet(alpha).astype(np.float32)
    if rng.random() < 0.1:
        # kezdő/frakciómentes állapot szimulálása
        dist = np.zeros(FRACTION_DIM, dtype=np.float32)
    return dist


def jitter_weights(weights, rng, spread=0.25):
    """Súlyok véletlenszerűrsítse (Zaj)"""
    return {k: max(0.0, v * (1.0 + rng.uniform(-spread, spread))) for k, v in weights.items()}


def generate_dataset(n_samples, seed=None, weights=None, jitter=True):
    """Legenerálja a (X, y) párost a valódi kártyaadatbázisból mintavételezve."""
    rng = np.random.default_rng(seed)
    weights = weights or DEFAULT_WEIGHTS

    X = np.zeros((n_samples, INPUT_DIM), dtype=np.float32)
    y = np.zeros((n_samples, OUTPUT_DIM), dtype=np.float32)

    for i in range(n_samples):
        ctx = sample_context(rng)
        idx_a, idx_b = rng.integers(0, len(CARD_DB), size=2)  # 2 egyforma lap is előfordulhat
        card_a, card_b = CARD_DB[idx_a], CARD_DB[idx_b]
        w = jitter_weights(weights, rng) if jitter else weights
        score_a = expert_score(card_a, ctx, w)
        score_b = expert_score(card_b, ctx, w)
        x, yy = make_pairwise_example(ctx, card_to_features(card_a), card_to_features(card_b), score_a, score_b)
        X[i] = x
        y[i] = yy

    return X, y

def build_model():
    inputs = layers.Input(shape=(INPUT_DIM,), dtype=tf.float32)
    x = layers.Dense(INPUT_DIM*3, activation="silu")(inputs)
    x = layers.LayerNormalization()(x)
    x = layers.Dense(INPUT_DIM*3, activation="silu")(x)
    x = layers.Dropout(0.10)(x)
    x = layers.Dense(INPUT_DIM, activation="silu")(x)
    outputs = layers.Dense(2, activation="softmax")(x)
    return models.Model(inputs, outputs)


def pairwise_symmetrize(x, y):
    """Return the original examples plus swapped A/B examples."""
    x = np.asarray(x, dtype=np.float32)
    y = np.asarray(y, dtype=np.float32)
    if x.ndim != 2 or x.shape[1] != INPUT_DIM:
        raise ValueError(f"Expected X shape (N,{INPUT_DIM}), got {x.shape}")
    if y.shape != (len(x), OUTPUT_DIM):
        raise ValueError(f"Expected y shape (N,2), got {y.shape}")

    ctx = x[:, :FRACTION_DIM]
    a = x[:, FRACTION_DIM:FRACTION_DIM + CARD_DIM]
    b = x[:, FRACTION_DIM + CARD_DIM:]

    swapped_x = np.concatenate([ctx, b, a], axis=1)
    swapped_y = y[:, ::-1]

    return (
        np.concatenate([x, swapped_x], axis=0),
        np.concatenate([y, swapped_y], axis=0),
    )


def pairwise_target(score_a, score_b, temperature=0.35):
    """Convert two scalar expert scores into a soft pairwise target."""
    values = np.asarray([score_a, score_b], dtype=np.float32)
    logits = values / max(float(temperature), 1e-6)
    logits -= np.max(logits)
    probs = np.exp(logits)
    probs /= np.sum(probs)
    return probs.astype(np.float32)


def make_pairwise_example(
    synergy_distribution,
    card_a_features,
    card_b_features,
    score_a,
    score_b,
):
    """Build one 49-feature pairwise example from two enriched card states."""
    context = np.asarray(synergy_distribution, dtype=np.float32)
    a = np.asarray(card_a_features, dtype=np.float32)
    b = np.asarray(card_b_features, dtype=np.float32)

    if context.shape != (FRACTION_DIM,):
        raise ValueError(f"synergy_distribution must have shape (5,), got {context.shape}")
    if a.shape != (CARD_DIM,) or b.shape != (CARD_DIM,):
        raise ValueError(f"Each card must have shape (22,), got {a.shape} / {b.shape}")

    x = np.concatenate([context, a, b]).astype(np.float32)
    y = pairwise_target(score_a, score_b)
    return x, y


def prepare_pairwise_dataset(x, y, symmetrize=True):
    x = np.asarray(x, dtype=np.float32)
    y = np.asarray(y, dtype=np.float32)
    if symmetrize:
        return pairwise_symmetrize(x, y)
    return x, y


def train_pairwise(x_train, y_train, epochs=150, batch_size=2048):
    x_train, y_train = prepare_pairwise_dataset(x_train, y_train, symmetrize=True)

    model = build_model()
    model.compile(
        optimizer=tf.keras.optimizers.AdamW(
            learning_rate=2e-4,
            weight_decay=1e-5,
        ),
        loss="categorical_crossentropy",
        metrics=["accuracy"],
    )

    model.fit(
        x_train,
        y_train,
        batch_size=batch_size,
        epochs=epochs,
        validation_split=0.1,
        shuffle=True,
        verbose=1,
    )
    model.export("F:\\MC\\TENSORS")
    return model


if __name__ == "__main__":
    print(f"Pairwise discarding input dimension: {INPUT_DIM}")
    print(f"Kártyaadatbázis mérete: {len(CARD_DB)} egyedi lap")
    print(f"Normalizáló konstansok -> attack_max={ATTACK_NORM_MAX}, health_max={HEALTH_NORM_MAX}, "
          f"money_max={MONEY_NORM_MAX}, price_max={PRICE_MAX}")

    N_SAMPLES = 2_000_000  # ~2M pár

    X_train, y_train = generate_dataset(N_SAMPLES, seed=42, jitter=True)
    print(f"Legenerálva: X={X_train.shape}, y={y_train.shape}")

    ctx_empty = np.zeros(FRACTION_DIM, dtype=np.float32)
    strong = next(c for c in CARD_DB if c["name"] == "The Intelligence")
    weak = next(c for c in CARD_DB if c["name"] == "Money")
    s_strong = expert_score(strong, ctx_empty)
    s_weak = expert_score(weak, ctx_empty)
    assert s_strong > s_weak, "Szenity check failed: 'The Intelligence' gyengébbnek pontozódott, mint a 'Money'!"
    print(f"Szenity check OK: The Intelligence score={s_strong:.2f} > Money score={s_weak:.2f}")

    model = train_pairwise(X_train, y_train, epochs=150, batch_size=2048)
    loss, acc = model.evaluate(X_train, y_train)
    print("Test loss:", loss, "Test acc:", acc)
