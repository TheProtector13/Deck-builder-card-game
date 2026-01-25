using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CardGame {
    internal class MainMenu : IDrawable {
        private readonly Texture2D backgroundTexture;
        private readonly Rectangle backgroundRectangle;
        private readonly Texture2D menubackground;
        private readonly Rectangle menubackgroundRectangle, contentbackgroundRectangle;
        private readonly Button[] mainmenubuttons;
        private readonly Button[] settingsmenubuttons;
        private readonly TextBox menuTitle;
        private readonly TextBox[] settingsmenuLabels;
        private readonly Slider[] settingsmenuSliders;
        private readonly MouseInfo mouseInfo;
        private bool settingsmenuopen = false;
        private bool manualopen = false;
        private readonly Button[] manualbuttons;
        private readonly Slider manualslider;
        private readonly object[] manualcontent = [
                "Irányítás:", //
                "Kilépéshez nyomj 'ESC'-et. Kattints az objektumokra a jobb egérgombbal\n" +
                "(például kártyákra, paklikra és halmokra) azok megtekintéséhez. A bal\n" +
                "egérgombot használd az ablakokban és menükben található opciók kiválasztásához.\n", //
                "Tartsd lenyomva a bal egérgombot a kártyák felett a mozgatáshoz. Engedd el a\n" +
                "kijelölt narancssárga területek felett a következő műveletekhez: Kártya vásárlása\n" +
                "a boltban, Kártya kijátszása!", //
                "A játékos és az ellenfél a következő tulajdonságokkal rendelkezik:\n" +
                "Piros kör - Támadás ebben a körben\nSárga kör - Pénz ebben a körben\nKék szív - Játékos/ellenfél életereje", //
                ResourceManager.Textures["MANUAL_IMG"][0], //
                "Játékmenet:", //
                "1. A játék kezdetén minden játékos 10-10 lapot kap a pakliába. Ezek a lapok nem\n" +
                "frakciókötöttek. Egy darab pénz egységet biztosítanak a játékosnak.\n" +
                "2. A játék körökből áll, ahol a játékosok egymás után kerülnek sorra, majd új kör kezdődik.\n", //
                "3. Minden kör kezdetén a játékos 5 lapot húz a saját paklijából, amiből a kör során\n" +
                "mindet ki is kell játszania. Egy kör egy adott játékos számára csak akkor érhet véget,\n" +
                "ha kezében nem marad kártya.", //
                "4. A lapok kijátszása során, vagy akár azt követően, a játékos jogosult kártyákat\n" +
                "vásárolni a boltból a lapok kijátszása során szerzett pénzösszegből. A megvásárolt\n" +
                "lapok az adott játékos \"scrap pila\"-jába, azaz az eldobott halomba kerülnek. A\n" +
                "kártyák kijátszása által szerzett pénzösszeg csak az adott kör végéig érvényes, nem marad meg.", //
                "5. A kör végén a játékos által begyűjtött támadási pontok levondónak az ellenfél életerejéből.\n" +
                "A kijátszott lapok az eldobott halomba kerülnek.", //
                "6. A következő kör megkezdődik a lapok kiosztásával az adott játékos paklijából,\n" +
                "ha a pakliban nincs több lap, akkor az eldobott halomból keverjük be ismét a paklit.\n" +
                "7. A körök addig ismétlődnek, amíg az ellenfél vagy a játékos életereje el nem fogy!", //
                "A játék célja:", //
                "Az ellenfél legyőzése az életerejének nullára csökkentésével", //
                "Frakciók és képességeik:", //
                "\"A mindent látó szem\" - Felfedi az ellenfél kártyáját, vagy elrejti a sajátunkat.\n" +
                "Ellop egy kártyát az ellenféltől erre a körre.", //
                ResourceManager.Textures["TheEyeIconWB"][0], //
                "\"Birodalom\" - Közepes támadás, kényszeríti az ellenfelet, hogy dobjon el néhány kártyát\n" +
                "a kijátszás helyett. Bizonyos lapjai képesek felhúzni még több lapot a pakliból az adott körre.", //
                ResourceManager.Textures["EmpireIconWB"][0], //
                "\"Szövetség\" - Pénzt és életerőt biztosít. Eltávolíthat kártyákat a boltból, így\n" +
                "megakadályozva az ellenfelet a pakliépítésben.", //
                ResourceManager.Textures["AllianceIconWB"][0], //
                "\"Gépek\" - Erős támadás és támadásbónuszok. Nincs egyéb különleges képességük.", //
                ResourceManager.Textures["MachinesIconWB"][0], //
                "\"Kuratórium\" - Pénzt biztosít, gyenge támadás. Véglegesen eltávolíthat kártyákat a\n" +
                "játékosok paklijából, így a fontosabb kártyák gyakrabban kijátszhatóak.", //
                ResourceManager.Textures["CollectorCultIconWB"][0],
                "Jó szórakozást kívánunk a játékhoz!"
            ];
        private readonly object[] drawablemanualcontent;
        private readonly Rectangle[] manualcontentlocs;
        private readonly int contentheight;

        public MenuState CurrentMenuState { get; private set; } = MenuState.None;

        public enum MenuState {
            SinglePlayer,
            MultiPlayer,
            Exit,
            None
        }

        public MainMenu()
        {
            backgroundTexture = ResourceManager.Textures["Space"][1];
            backgroundRectangle = DisplayInfo.FillRect(backgroundTexture.Bounds);
            menubackground = ResourceManager.Textures["SelectWindow"][0];
            int height = DisplayInfo.GetPXfromHeight(0.8);
            int width = height / 3 * 4;
            menubackgroundRectangle = DisplayInfo.CenterRect(new(0, 0, width, height), DisplayInfo.ScreenRect);
            mouseInfo = new MouseInfo(Mouse.GetState());
            float ratio = 128f / 384f;
            int bwidth = (int)MathF.Round(width * ratio);
            Point bSize = new(bwidth, (int)MathF.Round(bwidth * ratio));
            int col1xy = (int)MathF.Round(width * 0.114583333f);
            contentbackgroundRectangle = new(menubackgroundRectangle.X + col1xy, menubackgroundRectangle.Y + col1xy, menubackgroundRectangle.Width - (2 * col1xy), menubackgroundRectangle.Height - (2 * col1xy));
            mainmenubuttons = [
                new([ResourceManager.Textures["PlayButton"][0],ResourceManager.Textures["PlayButton"][1]], mouseInfo) {
                    Text = "Egyjátékos",
                    Location = new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["PlayButton"][0],ResourceManager.Textures["PlayButton"][2]], mouseInfo) {
                    Text = "Többjátékos (LAN)",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, contentbackgroundRectangle.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["ManualButton"][0],ResourceManager.Textures["ManualButton"][1]], mouseInfo) {
                    Text = "Útmutató",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["SettingsButton"][0],ResourceManager.Textures["SettingsButton"][1]], mouseInfo) {
                    Text = "Beállítások",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Kilépés",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + height - col1xy - bSize.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            mainmenubuttons[0].Click += SinglePlayerEventHandler;
            mainmenubuttons[1].Click += MultiPlayerEventHandler;
            mainmenubuttons[2].Click += ManualEventHandler;
            mainmenubuttons[3].Click += SettingsEventHandler;
            mainmenubuttons[4].Click += ExitEventHandler;
            settingsmenubuttons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + height - col1xy - bSize.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            settingsmenubuttons[0].Click += SettingsBackEventHandler;
            settingsmenuLabels = [
                new(new(new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y), bSize), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = "Zene hangereje"
                },
                new(new(new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)), bSize), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = "Hanghatások hangereje"
                } ];
            settingsmenuSliders = [
                new(mouseInfo) {Size = new(bSize.X, 32), Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, contentbackgroundRectangle.Y + ((bSize.Y - 32)/2)), Value = GameSettings.MusicVolume},
                new(mouseInfo) {Size = new(bSize.X, 32), Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2) + ((bSize.Y - 32)/2)), Value = GameSettings.SFXVolume}
                ];
            settingsmenuSliders[0].OnChange += MusicVolumeChangedEventHandler;
            settingsmenuSliders[1].OnChange += SFXVolumeChangedEventHandler;
            int boxheight = DisplayInfo.GetPXfromHeight(0.05);
            menuTitle = new TextBox(new(menubackgroundRectangle.X, menubackgroundRectangle.Top - boxheight, menubackgroundRectangle.Width, boxheight), ResourceManager.Fonts["FONT_DEF_B"]) {
                BGColor = Color.GhostWhite,
                Text = "A Kozmosz Lapjai"
            };
            int framexy = (int)MathF.Round(width * 0.0572916666f);
            Point mbSize = new((int)MathF.Round((col1xy - framexy) / ratio), col1xy - framexy);
            manualbuttons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (mbSize.X/2), menubackgroundRectangle.Bottom - framexy - mbSize.Y),
                    Size = mbSize
                } ];
            manualbuttons[0].Click += SettingsBackEventHandler;
            manualslider = new(mouseInfo) {
                Size = new(menubackgroundRectangle.Height, 32),
                Location = new(menubackgroundRectangle.Right, menubackgroundRectangle.Y),
                IsVertical = true,
                Value = 0.0f
            };
            List<object> darwablelist = [];
            List<Rectangle> locs = [];
            contentheight = (menubackgroundRectangle.Height - (2 * col1xy)) / 6;
            int offsetY = 0;
            foreach (var item in manualcontent) {
                if (item is string str) {
                    TextBox tb = new(new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y + (offsetY * contentheight), menubackgroundRectangle.Width - (2 * col1xy), contentheight),
                        ResourceManager.Fonts["FONT_DEF_B"]) {
                        Text = str,
                        Alignment = TextBox.TextAlignment.Left
                    };
                    darwablelist.Add(tb);
                    locs.Add(tb.Rect);
                }
                else if (item is Texture2D tex) {
                    Rectangle recttofit = new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y + (offsetY * contentheight), menubackgroundRectangle.Width - (2 * col1xy), contentheight);
                    Rectangle rect = DisplayInfo.FitRectBottom(tex.Bounds, recttofit);
                    darwablelist.Add(rect);
                    locs.Add(rect);
                }
                offsetY++;
            }
            drawablemanualcontent = darwablelist.ToArray();
            manualcontentlocs = locs.ToArray();
        }

        private void SinglePlayerEventHandler(object sender, EventArgs e) => CurrentMenuState = MenuState.SinglePlayer;
        private void MultiPlayerEventHandler(object sender, EventArgs e) => CurrentMenuState = MenuState.MultiPlayer;
        private void ManualEventHandler(object sender, EventArgs e)
        {
            manualopen = true;
            menuTitle.Text = "Útmutató";
        }
        private void SettingsEventHandler(object sender, EventArgs e)
        {
            settingsmenuopen = true;
            menuTitle.Text = "Beállítások";
        }
        private void ExitEventHandler(object sender, EventArgs e) => CurrentMenuState = MenuState.Exit;
        private void SettingsBackEventHandler(object sender, EventArgs e)
        {
            settingsmenuopen = false;
            manualopen = false;
            menuTitle.Text = "A Kozmosz Lapjai";
        }

        private void MusicVolumeChangedEventHandler(object sender, EventArgs e)
        {
            Slider slider = (Slider)sender;
            GameSettings.MusicVolume = slider.Value;
        }

        private void SFXVolumeChangedEventHandler(object sender, EventArgs e)
        {
            Slider slider = (Slider)sender;
            GameSettings.SFXVolume = slider.Value;
        }

        public void ResetMenuState() => CurrentMenuState = MenuState.None;

        public void Update(GameTime gameTime)
        {
            mouseInfo.Update(Mouse.GetState());
            menuTitle.Update(gameTime);
            if (settingsmenuopen) {
                foreach (var button in settingsmenubuttons) {
                    button.Update(gameTime);
                }
                foreach (var slider in settingsmenuSliders) {
                    slider.Update(gameTime);
                }
                foreach (var label in settingsmenuLabels) {
                    label.Update(gameTime);
                }
            }
            else if (manualopen) {
                manualslider.Update(gameTime);
                foreach (var button in manualbuttons) {
                    button.Update(gameTime);
                }
                float scrollvalue = manualslider.Value;
                for (int i = 0; i < drawablemanualcontent.Length; i++) {
                    if (drawablemanualcontent[i] is TextBox tb) {
                        Rectangle rect = manualcontentlocs[i];
                        rect.Y -= (int)MathF.Round(contentheight * (drawablemanualcontent.Length - 1) * scrollvalue);
                        tb.Rect = rect;
                        tb.Update(gameTime);
                    }
                    else if (drawablemanualcontent[i] is Rectangle rect) {
                        Rectangle _rect = manualcontentlocs[i];
                        _rect.Y -= (int)MathF.Round(contentheight * (drawablemanualcontent.Length - 1) * scrollvalue);
                        if (_rect != rect) {
                            drawablemanualcontent[i] = _rect;
                        }
                    }
                }
            }
            else {
                foreach (var button in mainmenubuttons) {
                    button.Update(gameTime);
                }
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(backgroundTexture, DisplayInfo.ScreenRect, backgroundRectangle, Color.White);
            spriteBatch.Draw(menubackground, menubackgroundRectangle, Color.White);
            menuTitle.Draw(gameTime, spriteBatch);
            if (settingsmenuopen) {
                foreach (var label in settingsmenuLabels) {
                    label.Draw(gameTime, spriteBatch);
                }
                foreach (var slider in settingsmenuSliders) {
                    slider.Draw(gameTime, spriteBatch);
                }
                foreach (var button in settingsmenubuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
            }
            else if (manualopen) {
                for (int i = 0; i < drawablemanualcontent.Length; i++) {
                    if (drawablemanualcontent[i] is TextBox tb && contentbackgroundRectangle.Contains(tb.Rect)) {
                        tb.Draw(gameTime, spriteBatch);
                    }
                    else if (drawablemanualcontent[i] is Rectangle rect && contentbackgroundRectangle.Contains(rect)) {
                        spriteBatch.Draw((Texture2D)manualcontent[i], rect, Color.White);
                    }
                }
                manualslider.Draw(gameTime, spriteBatch);
                foreach (var button in manualbuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
            }
            else {
                foreach (var button in mainmenubuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
            }
        }

    }
}
