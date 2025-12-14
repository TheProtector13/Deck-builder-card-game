using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static CardGame.BackGround;

namespace CardGame {
    internal static class DeckGenerator {
        public static BackGroundType TerrainType { get; private set; } = 0;

        private static CardDetails D(Card.Fraction frac, string name, string desc, string quote, int atk, int hp, int money, int price,
                                 bool terrain, Vector3 terrainAmount, Card.Effect effect, int effectAmount, Card.Fraction req) =>
                new(frac, name, desc, quote, atk, hp, money, price, terrain, terrainAmount, effect, effectAmount, req);

        public static List<Card> GenStartDeck()
        {
            var list = new List<Card>();
            var rect = new Rectangle(100, 100, 200, 400);

            for (int i = 0; i < 10; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Money"][0],
                    null,
                    D(Card.Fraction.None, "Pénz", string.Empty, "'Az arany sem különbözik a sártól,\nha nem élsz véle.'", 0, 0, 1, 0, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)))
            ;
            return list;
        }

        public static Card GetMoneyCard()
        {
            var rect = new Rectangle(100, 100, 200, 400);
            var list = new List<Card>();

            list.Add(new Card(rect,
                ResourceManager.Textures["Money"][1],
                null,
                D(Card.Fraction.None, "Gyémánt", "Kijátszás után elpusztul!", "'Ha nagyon kívánunk valamit,\nritkán kapjuk meg.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));
            list.Add(new Card(rect,
                ResourceManager.Textures["Money"][2],
                null,
                D(Card.Fraction.None, "Igazgyöngy", "Kijátszás után elpusztul!", "'Mindaz, ami hasznos, az csúf.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));

            return list[RandomNumberGenerator.GetInt32(list.Count)];
        }

        public static List<Card> GenDeck(BackGroundType terrainType)
        {
            TerrainType = terrainType;
            var rect = new Rectangle(100, 100, 200, 400);
            var list = new List<Card>();

            Texture2D[] skytexture = ResourceManager.Textures["Sky"];
            Texture2D[] bgtextures;
            if (terrainType == BackGroundType.Forest) {
                List<Texture2D> bgt = [];
                bgt.AddRange(ResourceManager.Textures["Forest"]);
                bgt.AddRange(ResourceManager.Textures["Plains"]);
                bgtextures = bgt.ToArray();
            }
            else if (terrainType == BackGroundType.Desert)
                bgtextures = ResourceManager.Textures["Desert"];
            else
                bgtextures = ResourceManager.Textures["Snow"];

            // THEEYE
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect,
                    skytexture[Random.Shared.Next(0, skytexture.Length)],
                    ResourceManager.Textures["Drone"][0],
                    D(Card.Fraction.TheEye, "Drónok", "Felfed egy kártyát az ellenfél kezében", "'Új szemszög a világhoz!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.ShowHand, 1, Card.Fraction.None)) { FGCentered = true })
            ;

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Media"][0],
                    null,
                    D(Card.Fraction.TheEye, "Média", string.Empty, "'Az emberek oda akarnak menni,\nahová vezetni akarják őket'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.TheEye)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect,
                    bgtextures[Random.Shared.Next(0, bgtextures.Length)],
                    ResourceManager.Textures["Counter_inteligence"][0],
                    D(Card.Fraction.TheEye, "Ellenhírszerzés", "Megakadályozza a lapok felfedését", "'A csend a leghangosabb retesz.'", 0, 0, 1, 3, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Intelligence"][0],
                    null,
                    D(Card.Fraction.TheEye, "Hírszerzés", "Felfed két lapot az ellenfél pakliából", "'Jobb szeretem ismerni a következményeket,\nmint az okot.'", 1, 0, 1, 3, false, Vector3.Zero, Card.Effect.ShowDeck, 2, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    skytexture[Random.Shared.Next(0, skytexture.Length)],
                    ResourceManager.Textures["Lopakodo"][0],
                    D(Card.Fraction.TheEye, "Lopakodó", string.Empty, "'Láthatatlan, de hatékony.'", 5, 0, 0, 3, true, new Vector3(-0.5f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.TheEye)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Corruption"][0],
                    null,
                    D(Card.Fraction.TheEye, "Korrupció", "Ellopja az ellenfél egy kártyáját", "'... a demokrácia ellensége.'", 2, 1, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.TheEye)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Lawyer"][0],
                    null,
                    D(Card.Fraction.TheEye, "Ügyvéd", string.Empty, "'A jog ereje a jogi bizonytalanságban rejlik.'", 0, 3, 3, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.TheEye)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Sabotage"][0],
                    null,
                    D(Card.Fraction.TheEye, "Szabotázs", "Megakadályozza a lapok felfedését", "'A rend megzavarásának művészete.'", 7, 0, 0, 4, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Spy"][0],
                    null,
                    D(Card.Fraction.TheEye, "Kém", "Ellopja az ellenfél egy kártyáját", "'A valóságot csak az árnyékok ismerik.'", 0, 0, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    skytexture[Random.Shared.Next(0, skytexture.Length)],
                    ResourceManager.Textures["Satelite"][0],
                    D(Card.Fraction.TheEye, "Műhold", "Felfedi az ellenfél kezét", "'..csupán egy lángoló üstökös.'", 0, 1, 0, 5, false, Vector3.Zero, Card.Effect.ShowHand, 5, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Puppet"][0],
                null,
                D(Card.Fraction.TheEye, "Báb", "Ellopja az ellenfél egy kártyáját", "'A szabad akarat jellemvonás.'", 4, 2, 2, 6, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["The_council"][0],
                null,
                D(Card.Fraction.TheEye, "A tanács", "Felfedi az ellenfél pakliát", "'..a színfalak mögül.'", 7, 3, 0, 8, false, Vector3.Zero, Card.Effect.ShowDeck, 10, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["MrNobody"][0],
                null,
                D(Card.Fraction.TheEye, "Mr. Senki", "Ellopja az ellenfél egy kártyáját", "'A látszólagos ártatlanság\na legjobb álcázás.'", 8, 0, 2, 8, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            // EMPIRE
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Militia"][0],
                    D(Card.Fraction.Empire, "Milícia", string.Empty, "'A fő cél az, hogy minden ember\nfel legyen fegyverezve.'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Scout"][0],
                    D(Card.Fraction.Empire, "Felderítő", "Egy krátyát húz a pakliból", "'A felkészülésre fordított idő\nritkán vész kárba!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Infantry"][0],
                    D(Card.Fraction.Empire, "Gyalogság", string.Empty, "'Minden sereg alappillére.'", 2, 0, 0, 2, true, new Vector3(0.5f, -0.5f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Mechanized"][0],
                    D(Card.Fraction.Empire, "Gépesített gyalogság", string.Empty, "'Tűz alatt megállni ostobaság.'", 3, 0, 0, 3, true, new Vector3(-0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Heavy_infantry"][0],
                    D(Card.Fraction.Empire, "Nehéz gyalogság", string.Empty, "'..a hegyek remegnek.'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Specialist"][0],
                    D(Card.Fraction.Empire, "Specialista", string.Empty, "'A legjobbak legjobbjai!'", 6, 0, 0, 4, true, new Vector3(0.5f, 0.3f, 0.2f), Card.Effect.AttackBonus, 2, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Anti_air"][0],
                    D(Card.Fraction.Empire, "Légelhárító", "Az ellenfél eldob egy kártyát", "'A legjobb égbolt, a tiszta égbolt'", 4, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Mine"][0], null,
                    D(Card.Fraction.Empire, "Aknamező", "Az ellenfél eldob egy kártyát", "'Minden lépés lehet diadal vagy tragédia.'", 6, 0, 0, 5, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][0],
                D(Card.Fraction.Empire, "Termoszférikus bombázás", "Az ellenfél eldob egy kártyát", "'..és leszakad az ég!'", 8, 0, 0, 6, true, new Vector3(-0.25f, 0f, 0f), Card.Effect.ScrapEnemyCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][0],
                    D(Card.Fraction.Empire, "Komp", string.Empty, "'Egyszerű, de rendíthetetlen.'", 6, 0, 1, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.Empire)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][1],
                    D(Card.Fraction.Empire, "Romboló", "Egy krátyát húz a pakliból", "'Ne add fel a hajót!'", 7, 0, 2, 7, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["General"][0], null,
                D(Card.Fraction.Empire, "Tábornok", string.Empty, "'A háború célja a béke elérése.'", 7, 2, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Emperor"][0], null,
                D(Card.Fraction.Empire, "Az uralkodó", "Két krátyát húz a pakliból", "'Ha a szív nem királyi,\nannak birtokosa sohasem király.'", 8, 0, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.None)));

            // ALLIANCE
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][0], null,
                    D(Card.Fraction.Alliance, "Gyógyszerek", string.Empty, "'Az egészség a legnagyobb ajándék.'", 0, 2, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][1], null,
                    D(Card.Fraction.Alliance, "Orvosi csomag", string.Empty, "'Lélek gyógyul, test követi.'", 0, 3, 0, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][2], null,
                    D(Card.Fraction.Alliance, "Traumakészlet", string.Empty, "'Ahol élet, ott remény!'", 0, 4, 0, 3, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Trader"][0],
                    D(Card.Fraction.Alliance, "Kereskedő", string.Empty, "'A minőség a legjobb üzleti terv.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["Trading_post"][0],
                    D(Card.Fraction.Alliance, "Kereskedelmi állomás", "Eltávolít egy lapot a boltból", "'A vagyon egy pennyvel kezdődik.'", 0, 1, 2, 3, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Sanctions"][0], null,
                    D(Card.Fraction.Alliance, "Szankciók", "Eltávolít egy lapot a boltból", "'A pokolba vezető út olyan gyors tud lenni!'", 3, 1, 0, 4, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Embassy"][0], null,
                    D(Card.Fraction.Alliance, "Követség", string.Empty, "'Több a haza ennél!'", 0, 5, 2, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Scientists"][0], null,
                    D(Card.Fraction.Alliance, "Tudósok", string.Empty, "'Egyetlen szerzőt másolni plágium,\nsok szerzőt másolni kutatás.'", 0, 3, 2, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Citadella"][0], null,
                    D(Card.Fraction.Alliance, "Fellegvár", "Eltávolít két lapot a boltból", "'A kulturális sokszínűség kaleidoszkópja.'", 0, 2, 3, 5, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Utopia"][0], null,
                    D(Card.Fraction.Alliance, "Utópia", string.Empty, "'Az utópia a horizont. Már\nlátod, s mégis oly távoli.'", 0, 5, 3, 6, false, Vector3.Zero, Card.Effect.HealthBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["HeadScientist"][0], null,
                D(Card.Fraction.Alliance, "Vezető tudós", string.Empty, "'Az elmém rossz környék, ahová\nnem szeretek egyedül menni.'", 2, 5, 0, 7, false, Vector3.Zero, Card.Effect.MoneyBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Ambassador"][0], null,
                D(Card.Fraction.Alliance, "Nagykövet", "Eltávolítja az összes\nlapot a boltból", "'Az őszinteség kérdéseket szül.'", 0, 2, 2, 8, false, Vector3.Zero, Card.Effect.ScrapFromShop, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Minister"][0], null,
                D(Card.Fraction.Alliance, "Miniszterelnök", "Egy krátyát húz a pakliból", "'Vitából pattan elő az igazság szikrája.'", 3, 5, 5, 8, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Alliance)));

            // MACHINES
            for (int i = 0; i < 3; i++) {
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Combat_drones"][0],
                    D(Card.Fraction.Machines, "Harci drón", string.Empty, "'Sok lúd ...'", 2, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][0],
                    D(Card.Fraction.Machines, "FR-2.1.7", string.Empty, "'Megérkezett az előőrs ...'", 3, 0, 0, 2, true, new Vector3(0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][1],
                    D(Card.Fraction.Machines, "SP-0.2.3", string.Empty, "'..célba tévedünk!'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][2],
                    D(Card.Fraction.Machines, "HX-1.0.1", string.Empty, "'Nem fog rajtuk a golyó!!'", 5, 0, 0, 4, true, new Vector3(0.6f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));
            }

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Booting"][0], null,
                    D(Card.Fraction.Machines, "Bootolás..", string.Empty, "Tartalékok üzembehelyezése?.. Y/N", 4, 3, 0, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Unstoppable"][0],
                    D(Card.Fraction.Machines, "Megállíthatatlan", string.Empty, "'A küldetést befejezem ..'", 6, 0, 0, 4, false, Vector3.Zero, Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["RobotDrone"][0],
                    D(Card.Fraction.Machines, "Robotgép", string.Empty, "'Célpont bemérve..'", 7, 0, 0, 5, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][1],
                D(Card.Fraction.Machines, "Orbitális bombázás", "Az ellenfél eldob egy kártyát", "'Először villámlásnak hiszed..'", 9, 0, 0, 6, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][2],
                    D(Card.Fraction.Machines, "Csapatszállító", string.Empty, "'Bevetésre felkészülni..'", 5, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.Machines)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][3],
                    D(Card.Fraction.Machines, "Cirkáló", string.Empty, "'Körözés a célterület felett..'", 6, 0, 0, 7, true, new Vector3(0f, 0.5f, 0.5f), Card.Effect.AttackBonus, 6, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["Factory"][0], null,
                D(Card.Fraction.Machines, "A Gyár", "Két krátyát húz a pakliból", "'Egy újabb széria..'", 5, 5, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_fleet"][0], null,
                D(Card.Fraction.Machines, "A flotta", string.Empty, "'Nincs ember, aki útját állhatná.'", 9, 0, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 7, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_inteligence"][0], null,
                D(Card.Fraction.Machines, "Az inteligencia", string.Empty, "'Van, hogy még a csoda sem segíthet.'", 9, 5, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 9, Card.Fraction.Machines)));

            // COLLECTORCULT
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Black_market"][0],
                    D(Card.Fraction.CollectorCult, "Fekete piac", string.Empty, "'Jövedelmező, de veszélyes!'", 0, 0, 2, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Collectors"][0],
                    D(Card.Fraction.CollectorCult, "Gyűjtögetők", "Eltávolítja egy kártyád a pakliból", "'Egyeseknek lom, másnak kincs!'", 1, 0, 1, 2, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Relic"][0], null,
                    D(Card.Fraction.CollectorCult, "Relikvia", string.Empty, "'Nincs semmi új, kivéve,\namit elfelejtettünk.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Sacred_scripture"][0], null,
                    D(Card.Fraction.CollectorCult, "Szent iratok", string.Empty, "'A könyvbe hamisság ne csússzék,\nés az igazság ki ne maradjon belőle!'", 0, 1, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Inaugurated"][0], null,
                    D(Card.Fraction.CollectorCult, "Felavatott", string.Empty, "'A haladás folyóját ezer forrás táplálja.'", 3, 0, 1, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Pilgrim"][0], null,
                    D(Card.Fraction.CollectorCult, "Zarándok", "Eltávolítja egy kártyád a pakliból", "'Oda megyek, hol lelkem megpihen.'", 5, 0, 1, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Heretic"][0], null,
                    D(Card.Fraction.CollectorCult, "Eretnek", "Eltávolítja egy kártyád a pakliból", "'Az őszinteség kérdéseket szül.'", 3, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["The_archive"][0], null,
                    D(Card.Fraction.CollectorCult, "Az archívum", string.Empty, "'A könyvtár a szellemi táplálék tárháza!'", 0, 0, 3, 5, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["The_archivist"][0], null,
                    D(Card.Fraction.CollectorCult, "Az irattáros", "Eltávolít két lapot a boltból", "'Nagy idők szülik a nagy embereket.'", 5, 0, 1, 6, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Transcendence"][0], null,
                    D(Card.Fraction.CollectorCult, "Transzcendencia", string.Empty, "'A tökéletesség nem cél,\nhanem alapvető norma.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Cyborg"][0], null,
                D(Card.Fraction.CollectorCult, "Cyborg", string.Empty, "'Idővel változik az élet,\nés vele változunk mi is.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 4, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Blessed_mars"][0], null,
                D(Card.Fraction.CollectorCult, "Áldott Mars", string.Empty, "'Mars aeternum! Mars mindörökké!'", 0, 3, 5, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_builder"][0], null,
                D(Card.Fraction.CollectorCult, "Az alkotó", "Eltávolítja egy kártyád a pakliból", "'Eleve lehetetlen, hogy bármi lehetetlen.'", 7, 0, 2, 8, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            return list;
        }

        public static Card[] GenSingleDeck(BackGroundType terrainType)
        {
            TerrainType = terrainType;
            var rect = new Rectangle(100, 100, 200, 400);
            var list = new List<Card>();

            Texture2D[] skytexture = ResourceManager.Textures["Sky"];
            Texture2D[] bgtextures;
            if (terrainType == BackGroundType.Forest) {
                List<Texture2D> bgt = [];
                bgt.AddRange(ResourceManager.Textures["Forest"]);
                bgt.AddRange(ResourceManager.Textures["Plains"]);
                bgtextures = bgt.ToArray();
            }
            else if (terrainType == BackGroundType.Desert)
                bgtextures = ResourceManager.Textures["Desert"];
            else
                bgtextures = ResourceManager.Textures["Snow"];

            // THEEYE
            list.Add(new Card(rect,
                skytexture[Random.Shared.Next(0, skytexture.Length)],
                ResourceManager.Textures["Drone"][0],
                D(Card.Fraction.TheEye, "Drónok", "Felfed egy kártyát az ellenfél kezében", "'Új szemszög a világhoz!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.ShowHand, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Media"][0],
                null,
                D(Card.Fraction.TheEye, "Média", string.Empty, "'Az emberek oda akarnak menni,\nahová vezetni akarják őket'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                bgtextures[Random.Shared.Next(0, bgtextures.Length)],
                ResourceManager.Textures["Counter_inteligence"][0],
                D(Card.Fraction.TheEye, "Ellenhírszerzés", "Megakadályozza a lapok felfedését", "'A csend a leghangosabb retesz.'", 0, 0, 1, 3, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Intelligence"][0],
                null,
                D(Card.Fraction.TheEye, "Hírszerzés", "Felfed két lapot az ellenfél pakliából", "'Jobb szeretem ismerni a következményeket,\nmint az okot.'", 1, 0, 1, 3, false, Vector3.Zero, Card.Effect.ShowDeck, 2, Card.Fraction.None)));

            list.Add(new Card(rect,
                skytexture[Random.Shared.Next(0, skytexture.Length)],
                ResourceManager.Textures["Lopakodo"][0],
                D(Card.Fraction.TheEye, "Lopakodó", string.Empty, "'Láthatatlan, de hatékony.'", 5, 0, 0, 3, true, new Vector3(-0.5f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.TheEye)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Corruption"][0],
                null,
                D(Card.Fraction.TheEye, "Korrupció", "Ellopja az ellenfél egy kártyáját", "'... a demokrácia ellensége.'", 2, 1, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Lawyer"][0],
                null,
                D(Card.Fraction.TheEye, "Ügyvéd", string.Empty, "'A jog ereje a jogi bizonytalanságban rejlik.'", 0, 3, 3, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Sabotage"][0],
                null,
                D(Card.Fraction.TheEye, "Szabotázs", "Megakadályozza a lapok felfedését", "'A rend megzavarásának művészete.'", 7, 0, 0, 4, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Spy"][0],
                null,
                D(Card.Fraction.TheEye, "Kém", "Ellopja az ellenfél egy kártyáját", "'A valóságot csak az árnyékok ismerik.'", 0, 0, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                skytexture[Random.Shared.Next(0, skytexture.Length)],
                ResourceManager.Textures["Satelite"][0],
                D(Card.Fraction.TheEye, "Műhold", "Felfedi az ellenfél kezét", "'..csupán egy lángoló üstökös.'", 0, 1, 0, 5, false, Vector3.Zero, Card.Effect.ShowHand, 5, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Puppet"][0],
                null,
                D(Card.Fraction.TheEye, "Báb", "Ellopja az ellenfél egy kártyáját", "'A szabad akarat jellemvonás.'", 4, 2, 2, 6, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["The_council"][0],
                null,
                D(Card.Fraction.TheEye, "A tanács", "Felfedi az ellenfél pakliát", "'..a színfalak mögül.'", 7, 3, 0, 8, false, Vector3.Zero, Card.Effect.ShowDeck, 10, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["MrNobody"][0],
                null,
                D(Card.Fraction.TheEye, "Mr. Senki", "Ellopja az ellenfél egy kártyáját", "'A látszólagos ártatlanság\na legjobb álcázás.'", 8, 0, 2, 8, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            // EMPIRE
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Militia"][0],
                D(Card.Fraction.Empire, "Milícia", string.Empty, "'A fő cél az, hogy minden ember\nfel legyen fegyverezve.'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Scout"][0],
                D(Card.Fraction.Empire, "Felderítő", "Egy krátyát húz a pakliból", "'A felkészülésre fordított idő\nritkán vész kárba!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Infantry"][0],
                D(Card.Fraction.Empire, "Gyalogság", string.Empty, "'Minden sereg alappillére.'", 2, 0, 0, 2, true, new Vector3(0.5f, -0.5f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Mechanized"][0],
                D(Card.Fraction.Empire, "Gépesített gyalogság", string.Empty, "'Tűz alatt megállni ostobaság.'", 3, 0, 0, 3, true, new Vector3(-0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Heavy_infantry"][0],
                D(Card.Fraction.Empire, "Nehéz gyalogság", string.Empty, "'..a hegyek remegnek.'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Specialist"][0],
                D(Card.Fraction.Empire, "Specialista", string.Empty, "'A legjobbak legjobbjai!'", 6, 0, 0, 4, true, new Vector3(0.5f, 0.3f, 0.2f), Card.Effect.AttackBonus, 2, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Anti_air"][0],
                D(Card.Fraction.Empire, "Légelhárító", "Az ellenfél eldob egy kártyát", "'A legjobb égbolt, a tiszta égbolt'", 4, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Mine"][0], null,
                D(Card.Fraction.Empire, "Aknamező", "Az ellenfél eldob egy kártyát", "'Minden lépés lehet diadal vagy tragédia.'", 6, 0, 0, 5, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][0],
                D(Card.Fraction.Empire, "Termoszférikus bombázás", "Az ellenfél eldob egy kártyát", "'..és leszakad az ég!'", 8, 0, 0, 6, true, new Vector3(-0.25f, 0f, 0f), Card.Effect.ScrapEnemyCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][0],
                D(Card.Fraction.Empire, "Komp", string.Empty, "'Egyszerű, de rendíthetetlen.'", 6, 0, 1, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.Empire)) { FGCentered = true });

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][1],
                D(Card.Fraction.Empire, "Romboló", "Egy krátyát húz a pakliból", "'Ne add fel a hajót!'", 7, 0, 2, 7, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["General"][0], null,
                D(Card.Fraction.Empire, "Tábornok", string.Empty, "'A háború célja a béke elérése.'", 7, 2, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Emperor"][0], null,
                D(Card.Fraction.Empire, "Az uralkodó", "Két krátyát húz a pakliból", "'Ha a szív nem királyi,\nannak birtokosa sohasem király.'", 8, 0, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.None)));

            // ALLIANCE
            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][0], null,
                D(Card.Fraction.Alliance, "Gyógyszerek", string.Empty, "'Az egészség a legnagyobb ajándék.'", 0, 2, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][1], null,
                D(Card.Fraction.Alliance, "Orvosi csomag", string.Empty, "'Lélek gyógyul, test követi.'", 0, 3, 0, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][2], null,
                D(Card.Fraction.Alliance, "Traumakészlet", string.Empty, "'Ahol élet, ott remény!'", 0, 4, 0, 3, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Trader"][0],
                D(Card.Fraction.Alliance, "Kereskedő", string.Empty, "'A minőség a legjobb üzleti terv.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.Alliance)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["Trading_post"][0],
                D(Card.Fraction.Alliance, "Kereskedelmi állomás", "Eltávolít egy lapot a boltból", "'A vagyon egy pennyvel kezdődik.'", 0, 1, 2, 3, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Sanctions"][0], null,
                D(Card.Fraction.Alliance, "Szankciók", "Eltávolít egy lapot a boltból", "'A pokolba vezető út olyan gyors tud lenni!'", 3, 1, 0, 4, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["Embassy"][0], null,
                D(Card.Fraction.Alliance, "Követség", string.Empty, "'Több a haza ennél!'", 0, 5, 2, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Scientists"][0], null,
                D(Card.Fraction.Alliance, "Tudósok", string.Empty, "'Egyetlen szerzőt másolni plágium,\nsok szerzőt másolni kutatás.'", 0, 3, 2, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Citadella"][0], null,
                D(Card.Fraction.Alliance, "Fellegvár", "Eltávolít két lapot a boltból", "'A kulturális sokszínűség kaleidoszkópja.'", 0, 2, 3, 5, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Utopia"][0], null,
                D(Card.Fraction.Alliance, "Utópia", string.Empty, "'Az utópia a horizont. Már\nlátod, s mégis oly távoli.'", 0, 5, 3, 6, false, Vector3.Zero, Card.Effect.HealthBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["HeadScientist"][0], null,
                D(Card.Fraction.Alliance, "Vezető tudós", string.Empty, "'Az elmém rossz környék, ahová\nnem szeretek egyedül menni.'", 2, 5, 0, 7, false, Vector3.Zero, Card.Effect.MoneyBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Ambassador"][0], null,
                D(Card.Fraction.Alliance, "Nagykövet", "Eltávolítja az összes\nlapot a boltból", "'Az őszinteség kérdéseket szül.'", 0, 2, 2, 8, false, Vector3.Zero, Card.Effect.ScrapFromShop, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Minister"][0], null,
                D(Card.Fraction.Alliance, "Miniszterelnök", "Egy krátyát húz a pakliból", "'Vitából pattan elő az igazság szikrája.'", 3, 5, 5, 8, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Alliance)));

            // MACHINES
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Combat_drones"][0],
                D(Card.Fraction.Machines, "Harci drón", string.Empty, "'Sok lúd ...'", 2, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][0],
                D(Card.Fraction.Machines, "FR-2.1.7", string.Empty, "'Megérkezett az előőrs ...'", 3, 0, 0, 2, true, new Vector3(0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][1],
                D(Card.Fraction.Machines, "SP-0.2.3", string.Empty, "'..célba tévedünk!'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][2],
                D(Card.Fraction.Machines, "HX-1.0.1", string.Empty, "'Nem fog rajtuk a golyó!!'", 5, 0, 0, 4, true, new Vector3(0.6f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["Booting"][0], null,
                D(Card.Fraction.Machines, "Bootolás..", string.Empty, "Tartalékok üzembehelyezése?.. Y/N", 4, 3, 0, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Unstoppable"][0],
                D(Card.Fraction.Machines, "Megállíthatatlan", string.Empty, "'A küldetést befejezem ..'", 6, 0, 0, 4, false, Vector3.Zero, Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["RobotDrone"][0],
                D(Card.Fraction.Machines, "Robotgép", string.Empty, "'Célpont bemérve..'", 7, 0, 0, 5, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][1],
                D(Card.Fraction.Machines, "Orbitális bombázás", "Az ellenfél eldob egy kártyát", "'Először villámlásnak hiszed..'", 9, 0, 0, 6, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Machines)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][2],
                D(Card.Fraction.Machines, "Csapatszállító", string.Empty, "'Bevetésre felkészülni..'", 5, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][3],
                D(Card.Fraction.Machines, "Cirkáló", string.Empty, "'Körözés a célterület felett..'", 6, 0, 0, 7, true, new Vector3(0f, 0.5f, 0.5f), Card.Effect.AttackBonus, 6, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["Factory"][0], null,
                D(Card.Fraction.Machines, "A Gyár", "Két krátyát húz a pakliból", "'Egy újabb széria..'", 5, 5, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_fleet"][0], null,
                D(Card.Fraction.Machines, "A flotta", string.Empty, "'Nincs ember, aki útját állhatná.'", 9, 0, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 7, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_inteligence"][0], null,
                D(Card.Fraction.Machines, "Az inteligencia", string.Empty, "'Van, hogy még a csoda sem segíthet.'", 9, 5, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 9, Card.Fraction.Machines)));

            // COLLECTORCULT
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Black_market"][0],
                D(Card.Fraction.CollectorCult, "Fekete piac", string.Empty, "'Jövedelmező, de veszélyes!'", 0, 0, 2, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Collectors"][0],
                D(Card.Fraction.CollectorCult, "Gyűjtögetők", "Eltávolítja egy kártyád a pakliból", "'Egyeseknek lom, másnak kincs!'", 1, 0, 1, 2, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Relic"][0], null,
                D(Card.Fraction.CollectorCult, "Relikvia", string.Empty, "'Nincs semmi új, kivéve,\namit elfelejtettünk.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Sacred_scripture"][0], null,
                D(Card.Fraction.CollectorCult, "Szent iratok", string.Empty, "'A könyvbe hamisság ne csússzék,\nés az igazság ki ne maradjon belőle!'", 0, 1, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Inaugurated"][0], null,
                D(Card.Fraction.CollectorCult, "Felavatott", string.Empty, "'A haladás folyóját ezer forrás táplálja.'", 3, 0, 1, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Pilgrim"][0], null,
                D(Card.Fraction.CollectorCult, "Zarándok", "Eltávolítja egy kártyád a pakliból", "'Oda megyek, hol lelkem megpihen.'", 5, 0, 1, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Heretic"][0], null,
                D(Card.Fraction.CollectorCult, "Eretnek", "Eltávolítja egy kártyád a pakliból", "'Az őszinteség kérdéseket szül.'", 3, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["The_archive"][0], null,
                D(Card.Fraction.CollectorCult, "Az archívum", string.Empty, "'A könyvtár a szellemi táplálék tárháza!'", 0, 0, 3, 5, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_archivist"][0], null,
                D(Card.Fraction.CollectorCult, "Az irattáros", "Eltávolít két lapot a boltból", "'Nagy idők szülik a nagy embereket.'", 5, 0, 1, 6, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Transcendence"][0], null,
                D(Card.Fraction.CollectorCult, "Transzcendencia", string.Empty, "'A tökéletesség nem cél,\nhanem alapvető norma.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Cyborg"][0], null,
                D(Card.Fraction.CollectorCult, "Cyborg", string.Empty, "'Idővel változik az élet,\nés vele változunk mi is.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 4, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Blessed_mars"][0], null,
                D(Card.Fraction.CollectorCult, "Áldott Mars", string.Empty, "'Mars aeternum! Mars mindörökké!'", 0, 3, 5, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_builder"][0], null,
                D(Card.Fraction.CollectorCult, "Az alkotó", "Eltávolítja egy kártyád a pakliból", "'Eleve lehetetlen, hogy bármi lehetetlen.'", 7, 0, 2, 8, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            return list.ToArray();
        }

        public static void ShuffleDeck(List<Card> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--) {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (deck[j], deck[i]) = (deck[i], deck[j]);
            }
        }

        public static Card GetCard(List<Card> deck) => deck[RandomNumberGenerator.GetInt32(0, deck.Count)];
    }
}
