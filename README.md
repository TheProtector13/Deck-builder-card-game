# Pakliépítő kártyajáték (HUN)
Egy pakliépítő kártyajáték, amely a MonoGame motorra épül.

<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 195943" src="https://github.com/user-attachments/assets/d6d5ab2a-b4af-4172-9787-a1e4ee544a60" />

## Irányítás:
Kilépéshez nyomj **ESC**-et.
Kattints az objektumokra a **jobb egérgombbal** (például kártyákra, paklikra és halmokra) azok megtekintéséhez.  
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200435" src="https://github.com/user-attachments/assets/a2f17bc8-5094-43ff-b430-d274dd97bc56" />
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200525" src="https://github.com/user-attachments/assets/154f6c7f-b84d-4622-ab25-10515cbec43c" />
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200626" src="https://github.com/user-attachments/assets/42ad46eb-c70a-476d-8686-b50491f94b5a" />

A **bal egérgombot** használd az ablakokban és menükben található opciók kiválasztásához.  
**Tartsd lenyomva** a **bal egérgombot** a kártyák felett a mozgatáshoz. Engedd el a kijelölt narancssárga területek felett a következő műveletekhez:  

- Kártya vásárlása a boltban  
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 201501" src="https://github.com/user-attachments/assets/0908e7b4-d1f8-4778-a3b7-cb11d5ced9e3" />

- Kártya kijátszása  
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 201514" src="https://github.com/user-attachments/assets/e6a3b2a5-4699-4a24-9092-5a6a3e1f2563" />

A játékos és az ellenfél a következő tulajdonságokkal rendelkezik:  
- Piros kör - Támadás ebben a körben  
- Sárga kör - Pénz ebben a körben
- Kék szív - Játékos/ellenfél életereje  
<img width="448" height="202" alt="Képernyőkép 2025-12-15 201514" src="https://github.com/user-attachments/assets/7f80cd22-8a99-41c9-bbe5-a3dd8afdd4d9" />

## Játékmenet:
1. A játék kezdetén minden játékos 10-10 lapot kap a pakliába. Ezek a lapok nem frakciókötöttek. Egy darab pénz egységet biztosítanak a játékosnak.
2. A játék körökből áll, ahol a játékosok egymás után kerülnek sorra, majd új kör kezdődik.
3. Minden kör kezdetén a játékosok 5 lapot húznak a pakliukból, amiből a kör során mindet ki is kell játszaniuk. Egy kör egy adott játékos számára csak akkor érhet véget, ha kezében nem marad kártya.
4. A lapok kijátszása során, vagy akár azt követően, a játékos jogosult kártyákat vásárolni a boltból a lapok kijátszása során szerzett pénzösszegből. A megvásárolt lapok az adott játékos "scrap pila"-jába, azaz az *eldobott halomba* kerülnek. A kártyák kijátszása által szerzett pénzösszeg csak az adott kör végéig érvényes, nem marad meg.
5. A kör végén a játékos által begyűjtött támadási pontok levondónak az ellenfél életerejéből. A kijátszott lapok az *eldobott halomba* kerülnek.
6. A következő kör megkezdődik a lapok kiosztásával az adott játékos pakliából, ha a pakliban nincs több lap, akkor az *eldobott halomból* keverjük be ismét a paklit.
7. A körök addig ismétlődnek, amíg az ellenfél vagy a játékos életereje el nem fogy!

<img width="1920" height="1080" alt="Képernyőkép 2025-12-16 133338" src="https://github.com/user-attachments/assets/bf2685ee-ad9d-4b83-b88c-6fe1074ab274" />

## A játék célja:
- Az ellenfél legyőzése az életerejének nullára csökkentésével

## Ehhez szükséges lépések:
- Építsd a paklidat kártyák vásárlásával a boltból 
- Használj pakliépítési stratégiát, amely ellensúlyozza az ellenfelét

A játékpakliba található kártyák öt frakcióhoz tartoznak. Ezek a frakciók általában a következő stratégiákat kínálják:  

- **A mindent látó szem** - Felfedi az ellenfél kártyáját, vagy elrejti a sajátunkat. Ellop egy kártyát az ellenféltől erre a körre.  
<img width="128" height="128" alt="TheEyeIconWB" src="https://github.com/user-attachments/assets/421f05a4-3ba7-446f-88cc-68f7eb2143d2" />

- **Birodalom** - Közepes támadás, kényszeríti az ellenfelet, hogy dobjon el néhány kártyát a kijátszás helyett. Bizonyos lapjai képesek felhúzni még több lapot a pakliból az adott körre.
<img width="128" height="128" alt="EmpireIconWB" src="https://github.com/user-attachments/assets/d69368fe-a42a-43b0-9d84-a86b1d289002" />

- **Szövetség** - Pénzt és életerőt biztosít. Eltávolíthat kártyákat a boltból, így megakadályozva az ellenfelet a pakliépítésben.  
<img width="128" height="128" alt="AllianceIconWB" src="https://github.com/user-attachments/assets/6ea452f5-38f9-439e-908d-b9367d5de7a0" />

- **Gépek** - Erős támadás és támadásbónuszok.  
<img width="128" height="128" alt="MachinesIconWB" src="https://github.com/user-attachments/assets/8eb063bb-0127-4da1-bcba-a4078e72eef6" />

- **Kuratórium** - Pénzt biztosít, gyenge támadás. Tartósan eltávolíthat kártyákat a játékosok pakliából, így a fontosabb kártyák gyakrabban kijátszhatóak.
<img width="128" height="128" alt="CollectorCultIconWB" src="https://github.com/user-attachments/assets/7ee2a051-ea76-4f3a-bc85-12a55bdd8a44" />

---
# Deck builder card game (ENG)
A deck builder card game based on monogame engine.

<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 195943" src="https://github.com/user-attachments/assets/d6d5ab2a-b4af-4172-9787-a1e4ee544a60" />

## Controls:
Press **ESC** key to leave and close the game.
Click on objects with the **right mouse button** (for example cards, decks and piles) to inspect them. 
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200435" src="https://github.com/user-attachments/assets/a2f17bc8-5094-43ff-b430-d274dd97bc56" />
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200525" src="https://github.com/user-attachments/assets/154f6c7f-b84d-4622-ab25-10515cbec43c" />
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 200626" src="https://github.com/user-attachments/assets/42ad46eb-c70a-476d-8686-b50491f94b5a" />

Use the **left mouse button** to select options from the windows and menus provided for this purpose.
**Hold** down the **left mouse button** above cards to move them. Release them above the designated orange areas to perform the next actions:
- Buy card from the shop
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 201501" src="https://github.com/user-attachments/assets/0908e7b4-d1f8-4778-a3b7-cb11d5ced9e3" />

- Play a card
<img width="1920" height="1080" alt="Képernyőkép 2025-12-15 201514" src="https://github.com/user-attachments/assets/e6a3b2a5-4699-4a24-9092-5a6a3e1f2563" />

Both the player and the enemy has the next attributes:
- Red circle - Attack in this turn
- Yellow circle - Money in this turn
- Blue hearth - The health of the player/enemy
<img width="448" height="202" alt="Képernyőkép 2025-12-15 201514" src="https://github.com/user-attachments/assets/7f80cd22-8a99-41c9-bbe5-a3dd8afdd4d9" />

## Gameplay:
1. At the start of the game, each player receives 10 cards in their deck. These cards are not tied to any faction and each provides one unit of money.
2. The game is played in turns, with players taking turns one after another, after which a new round begins.
3. At the beginning of each turn, players draw 5 cards from their deck. All drawn cards must be played during that turn. A turn for a player can only end when they have no cards left in their hand.
4. While playing cards, or after playing them, the player may purchase cards from the shop using the money gained from played cards. Purchased cards are placed into the player’s *scrap pile* (discard pile). Money gained during a turn is only valid until the end of that turn and does not carry over.
5. At the end of the turn, the attack points collected by the player are deducted from the opponent’s health. All played cards are placed into the *discard pile*.
6. The next turn begins by drawing cards from the player’s deck. If the deck is empty, the *discard pile* is shuffled back into the deck.
7. Turns repeat until either the player’s or the enemy’s health is reduced to zero.

<img width="1920" height="1080" alt="Screenshot 2025-12-16 133338" src="https://github.com/user-attachments/assets/bf2685ee-ad9d-4b83-b88c-6fe1074ab274" />

## Objective of the game:
- Defeat the enemy by reducing their health to ZERO

## To archive this:
- Build your deck by buying cards from the shop
- Use a deck-building strategy that counters the enemies

The cards in the deck belong to five factions. These factions usually provide the following strategies:
- **The eye** - Show enemy card, or hide ours. Steal a card this turn from the enemy.
<img width="128" height="128" alt="TheEyeIconWB" src="https://github.com/user-attachments/assets/421f05a4-3ba7-446f-88cc-68f7eb2143d2" />

- **Empire** - Medium attacks, force the enemy to scrap some of their cards insted of playing them this turn. Can be used to draw more cards from deck this turn insted of five.
<img width="128" height="128" alt="EmpireIconWB" src="https://github.com/user-attachments/assets/d69368fe-a42a-43b0-9d84-a86b1d289002" />

- **Alliance** - Provide money and health. Can remove cards from the shop, this way preventing the enemy from building their deck.
<img width="128" height="128" alt="AllianceIconWB" src="https://github.com/user-attachments/assets/6ea452f5-38f9-439e-908d-b9367d5de7a0" />

- **Machines** - Strong attack and attack bonuses.
<img width="128" height="128" alt="MachinesIconWB" src="https://github.com/user-attachments/assets/8eb063bb-0127-4da1-bcba-a4078e72eef6" />

- **Advisory board** - Provide some money, weak attack. Can permanently remove cards from your own deck, this way you can play your important cards more often.
<img width="128" height="128" alt="CollectorCultIconWB" src="https://github.com/user-attachments/assets/7ee2a051-ea76-4f3a-bc85-12a55bdd8a44" />
