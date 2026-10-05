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
                    D(Card.Fraction.None, "Money", string.Empty, "'Gold is no different from mud,\nif you don't put it to use.'", 0, 0, 1, 0, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)))
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
                D(Card.Fraction.None, "Diamond", "Destroyed after being played!", "'What we desire most,\nwe rarely receive.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));
            list.Add(new Card(rect,
                ResourceManager.Textures["Money"][2],
                null,
                D(Card.Fraction.None, "Pearl", "Destroyed after being played!", "'All that is useful is ugly.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));

            return list[RandomNumberGenerator.GetInt32(list.Count)];
        }

        public static Card[] GetMoneyCards()
        {
            var rect = new Rectangle(100, 100, 200, 400);
            var list = new List<Card>();

            list.Add(new Card(rect,
                ResourceManager.Textures["Money"][1],
                null,
                D(Card.Fraction.None, "Diamond", "Destroyed after being played!", "'What we desire most,\nwe rarely receive.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));
            list.Add(new Card(rect,
                ResourceManager.Textures["Money"][2],
                null,
                D(Card.Fraction.None, "Pearl", "Destroyed after being played!", "'All that is useful is ugly.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.SelfDestruct, 1, Card.Fraction.None)));

            return list.ToArray();
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
                    D(Card.Fraction.TheEye, "Drones", "Reveals a card in the enemy's hand", "'A new perspective on the world!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.ShowHand, 1, Card.Fraction.None)) { FGCentered = true })
            ;

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Media"][0],
                    null,
                    D(Card.Fraction.TheEye, "Media", string.Empty, "'People want to go\nwherever they are led.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.TheEye)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect,
                    bgtextures[Random.Shared.Next(0, bgtextures.Length)],
                    ResourceManager.Textures["Counter_inteligence"][0],
                    D(Card.Fraction.TheEye, "Counter-Intelligence", "Prevents your cards from being revealed", "'Silence is the loudest lock.'", 0, 0, 1, 3, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Intelligence"][0],
                    null,
                    D(Card.Fraction.TheEye, "Intelligence", "Reveals two cards from the enemy's deck", "'I'd rather know the consequences\nthan the cause.'", 1, 0, 1, 3, false, Vector3.Zero, Card.Effect.ShowDeck, 2, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    skytexture[Random.Shared.Next(0, skytexture.Length)],
                    ResourceManager.Textures["Lopakodo"][0],
                    D(Card.Fraction.TheEye, "Stealth Unit", string.Empty, "'Invisible, yet effective.'", 5, 0, 0, 3, true, new Vector3(-0.5f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.TheEye)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Corruption"][0],
                    null,
                    D(Card.Fraction.TheEye, "Corruption", "Steals a card from the enemy", "'...the enemy of democracy.'", 2, 1, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.TheEye)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Lawyer"][0],
                    null,
                    D(Card.Fraction.TheEye, "Lawyer", string.Empty, "'The power of law lies in legal uncertainty.'", 0, 3, 3, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.TheEye)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Sabotage"][0],
                    null,
                    D(Card.Fraction.TheEye, "Sabotage", "Prevents your cards from being revealed", "'The art of disrupting order.'", 7, 0, 0, 4, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    ResourceManager.Textures["Spy"][0],
                    null,
                    D(Card.Fraction.TheEye, "Spy", "Steals a card from the enemy", "'Only shadows know the truth.'", 0, 0, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect,
                    skytexture[Random.Shared.Next(0, skytexture.Length)],
                    ResourceManager.Textures["Satelite"][0],
                    D(Card.Fraction.TheEye, "Satellite", "Reveals the enemy's hand", "'..just a blazing comet.'", 0, 1, 0, 5, false, Vector3.Zero, Card.Effect.ShowHand, 5, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Puppet"][0],
                null,
                D(Card.Fraction.TheEye, "Puppet", "Steals a card from the enemy", "'Free will is a character trait.'", 4, 2, 2, 6, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["The_council"][0],
                null,
                D(Card.Fraction.TheEye, "The Council", "Reveals the enemy's deck", "'..from behind the scenes.'", 7, 3, 0, 8, false, Vector3.Zero, Card.Effect.ShowDeck, 10, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["MrNobody"][0],
                null,
                D(Card.Fraction.TheEye, "Mr. Nobody", "Steals a card from the enemy", "'Apparent innocence\nis the best disguise.'", 8, 0, 2, 8, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            // EMPIRE
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Militia"][0],
                    D(Card.Fraction.Empire, "Militia", string.Empty, "'The main goal is for every man\nto be armed.'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Scout"][0],
                    D(Card.Fraction.Empire, "Scout", "Draws a card from the deck", "'Time spent preparing\nis rarely wasted!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Infantry"][0],
                    D(Card.Fraction.Empire, "Infantry", string.Empty, "'The backbone of every army.'", 2, 0, 0, 2, true, new Vector3(0.5f, -0.5f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Mechanized"][0],
                    D(Card.Fraction.Empire, "Mechanized Infantry", string.Empty, "'Standing still under fire is foolish.'", 3, 0, 0, 3, true, new Vector3(-0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Heavy_infantry"][0],
                    D(Card.Fraction.Empire, "Heavy Infantry", string.Empty, "'..the mountains tremble.'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Specialist"][0],
                    D(Card.Fraction.Empire, "Specialist", string.Empty, "'The best of the best!'", 6, 0, 0, 4, true, new Vector3(0.5f, 0.3f, 0.2f), Card.Effect.AttackBonus, 2, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Anti_air"][0],
                    D(Card.Fraction.Empire, "Anti-Air", "The enemy discards a card", "'The best sky is a clear sky'", 4, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Mine"][0], null,
                    D(Card.Fraction.Empire, "Minefield", "The enemy discards a card", "'Every step can be triumph or tragedy.'", 6, 0, 0, 5, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][0],
                D(Card.Fraction.Empire, "Thermospheric Bombardment", "The enemy discards a card", "'..and the sky comes crashing down!'", 8, 0, 0, 6, true, new Vector3(-0.25f, 0f, 0f), Card.Effect.ScrapEnemyCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][0],
                    D(Card.Fraction.Empire, "Ferry", string.Empty, "'Simple, but unshakable.'", 6, 0, 1, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.Empire)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][1],
                    D(Card.Fraction.Empire, "Destroyer", "Draws a card from the deck", "'Don't give up the ship!'", 7, 0, 2, 7, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["General"][0], null,
                D(Card.Fraction.Empire, "General", string.Empty, "'The purpose of war is to achieve peace.'", 7, 2, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Emperor"][0], null,
                D(Card.Fraction.Empire, "The Ruler", "Draws two cards from the deck", "'If the heart is not royal,\nits bearer is never a king.'", 8, 0, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.None)));

            // ALLIANCE
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][0], null,
                    D(Card.Fraction.Alliance, "Medicine", string.Empty, "'Health is the greatest gift.'", 0, 2, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][1], null,
                    D(Card.Fraction.Alliance, "Medical Kit", string.Empty, "'The soul heals, the body follows.'", 0, 3, 0, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Medicine"][2], null,
                    D(Card.Fraction.Alliance, "Trauma Kit", string.Empty, "'Where there is life, there is hope!'", 0, 4, 0, 3, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Trader"][0],
                    D(Card.Fraction.Alliance, "Trader", string.Empty, "'Quality is the best business plan.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["Trading_post"][0],
                    D(Card.Fraction.Alliance, "Trading Post", "Removes a card from the shop", "'Fortune begins with a single penny.'", 0, 1, 2, 3, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Sanctions"][0], null,
                    D(Card.Fraction.Alliance, "Sanctions", "Removes a card from the shop", "'The road to hell can be so fast!'", 3, 1, 0, 4, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Embassy"][0], null,
                    D(Card.Fraction.Alliance, "Embassy", string.Empty, "'The homeland is more than this!'", 0, 5, 2, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Scientists"][0], null,
                    D(Card.Fraction.Alliance, "Scientists", string.Empty, "'Copying one author is plagiarism,\ncopying many is research.'", 0, 3, 2, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Citadella"][0], null,
                    D(Card.Fraction.Alliance, "Citadel", "Removes two cards from the shop", "'A kaleidoscope of cultural diversity.'", 0, 2, 3, 5, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.Alliance)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Utopia"][0], null,
                    D(Card.Fraction.Alliance, "Utopia", string.Empty, "'Utopia is the horizon. You\ncan see it, yet it's so far away.'", 0, 5, 3, 6, false, Vector3.Zero, Card.Effect.HealthBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["HeadScientist"][0], null,
                D(Card.Fraction.Alliance, "Head Scientist", string.Empty, "'My mind is a bad neighborhood\nI don't like to go into alone.'", 2, 5, 0, 7, false, Vector3.Zero, Card.Effect.MoneyBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Ambassador"][0], null,
                D(Card.Fraction.Alliance, "Ambassador", "Removes all cards\nfrom the shop", "'Honesty breeds questions.'", 0, 2, 2, 8, false, Vector3.Zero, Card.Effect.ScrapFromShop, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Minister"][0], null,
                D(Card.Fraction.Alliance, "Prime Minister", "Draws a card from the deck", "'The spark of truth leaps from debate.'", 3, 5, 5, 8, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Alliance)));

            // MACHINES
            for (int i = 0; i < 3; i++) {
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Combat_drones"][0],
                    D(Card.Fraction.Machines, "Combat Drone", string.Empty, "'Strength in numbers...'", 2, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][0],
                    D(Card.Fraction.Machines, "FR-2.1.7", string.Empty, "'The vanguard has arrived...'", 3, 0, 0, 2, true, new Vector3(0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][1],
                    D(Card.Fraction.Machines, "SP-0.2.3", string.Empty, "'..closing in on the target!'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][2],
                    D(Card.Fraction.Machines, "HX-1.0.1", string.Empty, "'Bullets don't affect them!!'", 5, 0, 0, 4, true, new Vector3(0.6f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));
            }

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Booting"][0], null,
                    D(Card.Fraction.Machines, "Booting..", string.Empty, "Deploy reserves?.. Y/N", 4, 3, 0, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Unstoppable"][0],
                    D(Card.Fraction.Machines, "Unstoppable", string.Empty, "'I will complete the mission..'", 6, 0, 0, 4, false, Vector3.Zero, Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["RobotDrone"][0],
                    D(Card.Fraction.Machines, "Robot Drone", string.Empty, "'Target locked..'", 7, 0, 0, 5, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][1],
                D(Card.Fraction.Machines, "Orbital Bombardment", "The enemy discards a card", "'At first you think it's lightning..'", 9, 0, 0, 6, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Machines)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][2],
                    D(Card.Fraction.Machines, "Troop Transport", string.Empty, "'Prepare for deployment..'", 5, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.Machines)) { FGCentered = true });

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][3],
                    D(Card.Fraction.Machines, "Cruiser", string.Empty, "'Circling above the target area..'", 6, 0, 0, 7, true, new Vector3(0f, 0.5f, 0.5f), Card.Effect.AttackBonus, 6, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["Factory"][0], null,
                D(Card.Fraction.Machines, "The Factory", "Draws two cards from the deck", "'Another batch..'", 5, 5, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_fleet"][0], null,
                D(Card.Fraction.Machines, "The Fleet", string.Empty, "'No man can stand in its way.'", 9, 0, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 7, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_inteligence"][0], null,
                D(Card.Fraction.Machines, "The Intelligence", string.Empty, "'Sometimes even a miracle can't help.'", 9, 5, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 9, Card.Fraction.Machines)));

            // COLLECTORCULT
            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Black_market"][0],
                    D(Card.Fraction.CollectorCult, "Black Market", string.Empty, "'Profitable, but dangerous!'", 0, 0, 2, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Collectors"][0],
                    D(Card.Fraction.CollectorCult, "Collectors", "Removes one of your cards from your deck", "'One man's junk is another's treasure!'", 1, 0, 1, 2, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 3; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Relic"][0], null,
                    D(Card.Fraction.CollectorCult, "Relic", string.Empty, "'There is nothing new, except\nwhat we've forgotten.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Sacred_scripture"][0], null,
                    D(Card.Fraction.CollectorCult, "Sacred Scriptures", string.Empty, "'Let no falsehood slip into the book,\nand let no truth be left out of it!'", 0, 1, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Inaugurated"][0], null,
                    D(Card.Fraction.CollectorCult, "Ordained", string.Empty, "'The river of progress is fed by a thousand springs.'", 3, 0, 1, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Pilgrim"][0], null,
                    D(Card.Fraction.CollectorCult, "Pilgrim", "Removes one of your cards from your deck", "'I go where my soul finds rest.'", 5, 0, 1, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Heretic"][0], null,
                    D(Card.Fraction.CollectorCult, "Heretic", "Removes one of your cards from your deck", "'Honesty breeds questions.'", 3, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["The_archive"][0], null,
                    D(Card.Fraction.CollectorCult, "The Archive", string.Empty, "'The library is the storehouse of intellectual nourishment!'", 0, 0, 3, 5, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["The_archivist"][0], null,
                    D(Card.Fraction.CollectorCult, "The Archivist", "Removes two cards from the shop", "'Great times give birth to great men.'", 5, 0, 1, 6, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.CollectorCult)));

            for (int i = 0; i < 2; i++)
                list.Add(new Card(rect, ResourceManager.Textures["Transcendence"][0], null,
                    D(Card.Fraction.CollectorCult, "Transcendence", string.Empty, "'Perfection is not a goal,\nbut a fundamental standard.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Cyborg"][0], null,
                D(Card.Fraction.CollectorCult, "Cyborg", string.Empty, "'Life changes with time,\nand we change with it.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 4, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Blessed_mars"][0], null,
                D(Card.Fraction.CollectorCult, "Blessed Mars", string.Empty, "'Mars aeternum! Mars forever!'", 0, 3, 5, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_builder"][0], null,
                D(Card.Fraction.CollectorCult, "The Creator", "Removes one of your cards from your deck", "'It is inherently impossible for anything to be impossible.'", 7, 0, 2, 8, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            return list;
        }

        public static Card[] GenSingleDeck(BackGroundType terrainType)
        {
            TerrainType = terrainType;
            var rect = new Rectangle(100, 100, 200, 400);
            var list = new List<Card>(130);

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
                D(Card.Fraction.TheEye, "Drones", "Reveals a card in the enemy's hand", "'A new perspective on the world!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.ShowHand, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Media"][0],
                null,
                D(Card.Fraction.TheEye, "Media", string.Empty, "'People want to go\nwherever they are led.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                bgtextures[Random.Shared.Next(0, bgtextures.Length)],
                ResourceManager.Textures["Counter_inteligence"][0],
                D(Card.Fraction.TheEye, "Counter-Intelligence", "Prevents your cards from being revealed", "'Silence is the loudest lock.'", 0, 0, 1, 3, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Intelligence"][0],
                null,
                D(Card.Fraction.TheEye, "Intelligence", "Reveals two cards from the enemy's deck", "'I'd rather know the consequences\nthan the cause.'", 1, 0, 1, 3, false, Vector3.Zero, Card.Effect.ShowDeck, 2, Card.Fraction.None)));

            list.Add(new Card(rect,
                skytexture[Random.Shared.Next(0, skytexture.Length)],
                ResourceManager.Textures["Lopakodo"][0],
                D(Card.Fraction.TheEye, "Stealth Unit", string.Empty, "'Invisible, yet effective.'", 5, 0, 0, 3, true, new Vector3(-0.5f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.TheEye)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Corruption"][0],
                null,
                D(Card.Fraction.TheEye, "Corruption", "Steals a card from the enemy", "'...the enemy of democracy.'", 2, 1, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Lawyer"][0],
                null,
                D(Card.Fraction.TheEye, "Lawyer", string.Empty, "'The power of law lies in legal uncertainty.'", 0, 3, 3, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.TheEye)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Sabotage"][0],
                null,
                D(Card.Fraction.TheEye, "Sabotage", "Prevents your cards from being revealed", "'The art of disrupting order.'", 7, 0, 0, 4, false, Vector3.Zero, Card.Effect.AntiShow, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["Spy"][0],
                null,
                D(Card.Fraction.TheEye, "Spy", "Steals a card from the enemy", "'Only shadows know the truth.'", 0, 0, 0, 4, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                skytexture[Random.Shared.Next(0, skytexture.Length)],
                ResourceManager.Textures["Satelite"][0],
                D(Card.Fraction.TheEye, "Satellite", "Reveals the enemy's hand", "'..just a blazing comet.'", 0, 1, 0, 5, false, Vector3.Zero, Card.Effect.ShowHand, 5, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect,
                ResourceManager.Textures["Puppet"][0],
                null,
                D(Card.Fraction.TheEye, "Puppet", "Steals a card from the enemy", "'Free will is a character trait.'", 4, 2, 2, 6, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["The_council"][0],
                null,
                D(Card.Fraction.TheEye, "The Council", "Reveals the enemy's deck", "'..from behind the scenes.'", 7, 3, 0, 8, false, Vector3.Zero, Card.Effect.ShowDeck, 10, Card.Fraction.None)));

            list.Add(new Card(rect,
                ResourceManager.Textures["MrNobody"][0],
                null,
                D(Card.Fraction.TheEye, "Mr. Nobody", "Steals a card from the enemy", "'Apparent innocence\nis the best disguise.'", 8, 0, 2, 8, false, Vector3.Zero, Card.Effect.StealCard, 1, Card.Fraction.None)));

            // EMPIRE
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Militia"][0],
                D(Card.Fraction.Empire, "Militia", string.Empty, "'The main goal is for every man\nto be armed.'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Scout"][0],
                D(Card.Fraction.Empire, "Scout", "Draws a card from the deck", "'Time spent preparing\nis rarely wasted!'", 1, 0, 0, 1, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Infantry"][0],
                D(Card.Fraction.Empire, "Infantry", string.Empty, "'The backbone of every army.'", 2, 0, 0, 2, true, new Vector3(0.5f, -0.5f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Mechanized"][0],
                D(Card.Fraction.Empire, "Mechanized Infantry", string.Empty, "'Standing still under fire is foolish.'", 3, 0, 0, 3, true, new Vector3(-0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Heavy_infantry"][0],
                D(Card.Fraction.Empire, "Heavy Infantry", string.Empty, "'..the mountains tremble.'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Specialist"][0],
                D(Card.Fraction.Empire, "Specialist", string.Empty, "'The best of the best!'", 6, 0, 0, 4, true, new Vector3(0.5f, 0.3f, 0.2f), Card.Effect.AttackBonus, 2, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Anti_air"][0],
                D(Card.Fraction.Empire, "Anti-Air", "The enemy discards a card", "'The best sky is a clear sky'", 4, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Mine"][0], null,
                D(Card.Fraction.Empire, "Minefield", "The enemy discards a card", "'Every step can be triumph or tragedy.'", 6, 0, 0, 5, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Empire)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][0],
                D(Card.Fraction.Empire, "Thermospheric Bombardment", "The enemy discards a card", "'..and the sky comes crashing down!'", 8, 0, 0, 6, true, new Vector3(-0.25f, 0f, 0f), Card.Effect.ScrapEnemyCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][0],
                D(Card.Fraction.Empire, "Ferry", string.Empty, "'Simple, but unshakable.'", 6, 0, 1, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 2, Card.Fraction.Empire)) { FGCentered = true });

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][1],
                D(Card.Fraction.Empire, "Destroyer", "Draws a card from the deck", "'Don't give up the ship!'", 7, 0, 2, 7, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.None)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["General"][0], null,
                D(Card.Fraction.Empire, "General", string.Empty, "'The purpose of war is to achieve peace.'", 7, 2, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Empire)));

            list.Add(new Card(rect, ResourceManager.Textures["Emperor"][0], null,
                D(Card.Fraction.Empire, "The Ruler", "Draws two cards from the deck", "'If the heart is not royal,\nits bearer is never a king.'", 8, 0, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.None)));

            // ALLIANCE
            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][0], null,
                D(Card.Fraction.Alliance, "Medicine", string.Empty, "'Health is the greatest gift.'", 0, 2, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][1], null,
                D(Card.Fraction.Alliance, "Medical Kit", string.Empty, "'The soul heals, the body follows.'", 0, 3, 0, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Medicine"][2], null,
                D(Card.Fraction.Alliance, "Trauma Kit", string.Empty, "'Where there is life, there is hope!'", 0, 4, 0, 3, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Trader"][0],
                D(Card.Fraction.Alliance, "Trader", string.Empty, "'Quality is the best business plan.'", 0, 0, 3, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.Alliance)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["Trading_post"][0],
                D(Card.Fraction.Alliance, "Trading Post", "Removes a card from the shop", "'Fortune begins with a single penny.'", 0, 1, 2, 3, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Sanctions"][0], null,
                D(Card.Fraction.Alliance, "Sanctions", "Removes a card from the shop", "'The road to hell can be so fast!'", 3, 1, 0, 4, false, Vector3.Zero, Card.Effect.ScrapFromShop, 1, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["Embassy"][0], null,
                D(Card.Fraction.Alliance, "Embassy", string.Empty, "'The homeland is more than this!'", 0, 5, 2, 4, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Scientists"][0], null,
                D(Card.Fraction.Alliance, "Scientists", string.Empty, "'Copying one author is plagiarism,\ncopying many is research.'", 0, 3, 2, 3, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Citadella"][0], null,
                D(Card.Fraction.Alliance, "Citadel", "Removes two cards from the shop", "'A kaleidoscope of cultural diversity.'", 0, 2, 3, 5, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Utopia"][0], null,
                D(Card.Fraction.Alliance, "Utopia", string.Empty, "'Utopia is the horizon. You\ncan see it, yet it's so far away.'", 0, 5, 3, 6, false, Vector3.Zero, Card.Effect.HealthBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["HeadScientist"][0], null,
                D(Card.Fraction.Alliance, "Head Scientist", string.Empty, "'My mind is a bad neighborhood\nI don't like to go into alone.'", 2, 5, 0, 7, false, Vector3.Zero, Card.Effect.MoneyBonus, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Ambassador"][0], null,
                D(Card.Fraction.Alliance, "Ambassador", "Removes all cards\nfrom the shop", "'Honesty breeds questions.'", 0, 2, 2, 8, false, Vector3.Zero, Card.Effect.ScrapFromShop, 5, Card.Fraction.Alliance)));

            list.Add(new Card(rect, ResourceManager.Textures["Minister"][0], null,
                D(Card.Fraction.Alliance, "Prime Minister", "Draws a card from the deck", "'The spark of truth leaps from debate.'", 3, 5, 5, 8, false, Vector3.Zero, Card.Effect.DrawCard, 1, Card.Fraction.Alliance)));

            // MACHINES
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Combat_drones"][0],
                D(Card.Fraction.Machines, "Combat Drone", string.Empty, "'Strength in numbers...'", 2, 0, 0, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][0],
                D(Card.Fraction.Machines, "FR-2.1.7", string.Empty, "'The vanguard has arrived...'", 3, 0, 0, 2, true, new Vector3(0.3f, 0f, 0f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][1],
                D(Card.Fraction.Machines, "SP-0.2.3", string.Empty, "'..closing in on the target!'", 4, 0, 0, 3, true, new Vector3(0.5f, 0f, 0.25f), Card.Effect.AttackBonus, 1, Card.Fraction.Machines)));
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Robot"][2],
                D(Card.Fraction.Machines, "HX-1.0.1", string.Empty, "'Bullets don't affect them!!'", 5, 0, 0, 4, true, new Vector3(0.6f, 0f, 0f), Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["Booting"][0], null,
                D(Card.Fraction.Machines, "Booting..", string.Empty, "Deploy reserves?.. Y/N", 4, 3, 0, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Unstoppable"][0],
                D(Card.Fraction.Machines, "Unstoppable", string.Empty, "'I will complete the mission..'", 6, 0, 0, 4, false, Vector3.Zero, Card.Effect.AttackBonus, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["RobotDrone"][0],
                D(Card.Fraction.Machines, "Robot Drone", string.Empty, "'Target locked..'", 7, 0, 0, 5, false, Vector3.Zero, Card.Effect.AttackBonus, 3, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Orbital"][1],
                D(Card.Fraction.Machines, "Orbital Bombardment", "The enemy discards a card", "'At first you think it's lightning..'", 9, 0, 0, 6, false, Vector3.Zero, Card.Effect.ScrapEnemyCard, 1, Card.Fraction.Machines)));

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][2],
                D(Card.Fraction.Machines, "Troop Transport", string.Empty, "'Prepare for deployment..'", 5, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, skytexture[Random.Shared.Next(0, skytexture.Length)], ResourceManager.Textures["SP"][3],
                D(Card.Fraction.Machines, "Cruiser", string.Empty, "'Circling above the target area..'", 6, 0, 0, 7, true, new Vector3(0f, 0.5f, 0.5f), Card.Effect.AttackBonus, 6, Card.Fraction.Machines)) { FGCentered = true });

            list.Add(new Card(rect, ResourceManager.Textures["Factory"][0], null,
                D(Card.Fraction.Machines, "The Factory", "Draws two cards from the deck", "'Another batch..'", 5, 5, 0, 8, false, Vector3.Zero, Card.Effect.DrawCard, 2, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_fleet"][0], null,
                D(Card.Fraction.Machines, "The Fleet", string.Empty, "'No man can stand in its way.'", 9, 0, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 7, Card.Fraction.Machines)));

            list.Add(new Card(rect, ResourceManager.Textures["The_inteligence"][0], null,
                D(Card.Fraction.Machines, "The Intelligence", string.Empty, "'Sometimes even a miracle can't help.'", 9, 5, 0, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 9, Card.Fraction.Machines)));

            // COLLECTORCULT
            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Black_market"][0],
                D(Card.Fraction.CollectorCult, "Black Market", string.Empty, "'Profitable, but dangerous!'", 0, 0, 2, 1, false, Vector3.Zero, Card.Effect.None, 0, Card.Fraction.None)));

            list.Add(new Card(rect, bgtextures[Random.Shared.Next(0, bgtextures.Length)], ResourceManager.Textures["Collectors"][0],
                D(Card.Fraction.CollectorCult, "Collectors", "Removes one of your cards from your deck", "'One man's junk is another's treasure!'", 1, 0, 1, 2, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Relic"][0], null,
                D(Card.Fraction.CollectorCult, "Relic", string.Empty, "'There is nothing new, except\nwhat we've forgotten.'", 0, 0, 2, 2, false, Vector3.Zero, Card.Effect.MoneyBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Sacred_scripture"][0], null,
                D(Card.Fraction.CollectorCult, "Sacred Scriptures", string.Empty, "'Let no falsehood slip into the book,\nand let no truth be left out of it!'", 0, 1, 2, 2, false, Vector3.Zero, Card.Effect.HealthBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Inaugurated"][0], null,
                D(Card.Fraction.CollectorCult, "Ordained", string.Empty, "'The river of progress is fed by a thousand springs.'", 3, 0, 1, 3, false, Vector3.Zero, Card.Effect.AttackBonus, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Pilgrim"][0], null,
                D(Card.Fraction.CollectorCult, "Pilgrim", "Removes one of your cards from your deck", "'I go where my soul finds rest.'", 5, 0, 1, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Heretic"][0], null,
                D(Card.Fraction.CollectorCult, "Heretic", "Removes one of your cards from your deck", "'Honesty breeds questions.'", 3, 0, 0, 4, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

            list.Add(new Card(rect, ResourceManager.Textures["The_archive"][0], null,
                D(Card.Fraction.CollectorCult, "The Archive", string.Empty, "'The library is the storehouse of intellectual nourishment!'", 0, 0, 3, 5, false, Vector3.Zero, Card.Effect.MoneyBonus, 2, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_archivist"][0], null,
                D(Card.Fraction.CollectorCult, "The Archivist", "Removes two cards from the shop", "'Great times give birth to great men.'", 5, 0, 1, 6, false, Vector3.Zero, Card.Effect.ScrapFromShop, 2, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Transcendence"][0], null,
                D(Card.Fraction.CollectorCult, "Transcendence", string.Empty, "'Perfection is not a goal,\nbut a fundamental standard.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.HealthBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Cyborg"][0], null,
                D(Card.Fraction.CollectorCult, "Cyborg", string.Empty, "'Life changes with time,\nand we change with it.'", 6, 0, 0, 7, false, Vector3.Zero, Card.Effect.AttackBonus, 4, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["Blessed_mars"][0], null,
                D(Card.Fraction.CollectorCult, "Blessed Mars", string.Empty, "'Mars aeternum! Mars forever!'", 0, 3, 5, 8, false, Vector3.Zero, Card.Effect.AttackBonus, 8, Card.Fraction.CollectorCult)));

            list.Add(new Card(rect, ResourceManager.Textures["The_builder"][0], null,
                D(Card.Fraction.CollectorCult, "The Creator", "Removes one of your cards from your deck", "'It is inherently impossible for anything to be impossible.'", 7, 0, 2, 8, false, Vector3.Zero, Card.Effect.ScrapOwnCard, 1, Card.Fraction.None)));

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
