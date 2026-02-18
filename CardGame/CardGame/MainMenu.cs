using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardGame.TCP;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using static CardGame.TCP.MessagePackHelper;

#nullable enable
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
        private readonly Button[] multiselectorbuttons;
        private readonly TextBox[] multiselectorlabels;
        private readonly TextInput[] multiselectorInputs;
        private readonly Button[] hostbuttons;
        private readonly TextBox[] hostlabels;
        private readonly Button[] clientButtons;
        private readonly TextBox[] clientlabels;
        private readonly TextInput[] clientInputs;
        private readonly ListView clientList;
        private Tuple<DiscoveryPayload, byte[], byte[]>[] currentdisclist = [];
        private readonly MouseInfo mouseInfo;
        private bool settingsmenuopen = false;
        private bool manualopen = false;
        private bool multiselectoropen = false;
        private bool hostopen = false;
        private bool clientopen = false;
        private Task<bool>? joinTask = null;
        private readonly Button[] manualbuttons;
        private readonly Slider manualslider;
        private readonly object[] manualcontent = [
                "Irányítás:", //
                "Kilépéshez nyomj 'ESC'-et. Kattints az objektumokra a jobb egérgombbal\n" +
                "(például kártyákra, paklikra és halmokra) azok megtekintéséhez. A bal\n" +
                "egérgombot használd az ablakokban és menükben található opciók kiválasztásához.\n", //
                "Tartsd lenyomva a bal egérgombot a kártyák felett a mozgatáshoz. Engedd el a\n" +
                "kijelölt narancssárga területek felett a következő műveletekhez: Kártya vásárlása\n" +
                "a boltból, Kártya kijátszása!", //
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
            int hieght_until_B = height - col1xy - bSize.Y;
            settingsmenubuttons = [
                new(ResourceManager.Textures["BUTTON"], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + hieght_until_B),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                },
                new(ResourceManager.Textures["BUTTON"], mouseInfo) {
                    Text = GameSettings.MultiCastEnabled ? "MultiCast mód" : "BroadCast mód",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + hieght_until_B - (bSize.Y/2*3)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                },
                new(ResourceManager.Textures["BUTTON"], mouseInfo) {
                    Text = GameSettings.RandomAIEnabled ? "Random AI" : "Neurálhálós AI",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + hieght_until_B - (bSize.Y/2*3)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            settingsmenubuttons[0].Click += SettingsBackEventHandler;
            settingsmenubuttons[1].Click += SetMultiCastEventHandler;
            settingsmenubuttons[2].Click += SetRandomAIEventHandler;
            settingsmenuLabels = [
                new(new(new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y), bSize), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = "Zene hangereje"
                },
                new(new(new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (hieght_until_B/2) - (bSize.Y/2)), bSize), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = "Hanghatások hangereje"
                } ];
            settingsmenuSliders = [
                new(mouseInfo) {Size = new(bSize.X, 32), Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, contentbackgroundRectangle.Y + ((bSize.Y - 32)/2)), Value = GameSettings.MusicVolume},
                new(mouseInfo) {Size = new(bSize.X, 32), Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (hieght_until_B/2) - (bSize.Y/2) + ((bSize.Y - 32)/2)), Value = GameSettings.SFXVolume}
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
            //
            multiselectorbuttons = [
                new([ResourceManager.Textures["SettingsButton"][0],ResourceManager.Textures["SettingsButton"][1]], mouseInfo) {
                    Text = "Létrehozás",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["SettingsButton"][0],ResourceManager.Textures["SettingsButton"][1]], mouseInfo) {
                    Text = "Csatlakozás",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + height - col1xy - bSize.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            multiselectorbuttons[0].Click += HostModeEventHandler;
            multiselectorbuttons[1].Click += ClientModeEventHandler;
            multiselectorbuttons[2].Click += MultiBackEventHandler;
            multiselectorlabels = [
                new(new(mainmenubuttons[0].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Felhasználónév:" } ];
            string uname = DatabaseConnector.GetUsername();
            multiselectorInputs = [
                new(new(mainmenubuttons[1].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"], mouseInfo, 16) {
                    Text = uname != string.Empty ? uname : UDP_Broadcast_Helper.UserName,
                    BGColor = Color.Silver}
                ];
            multiselectorInputs[0].OnChange += UserNameChangeEventHandler;
            hostbuttons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + height - col1xy - bSize.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            hostbuttons[0].Click += MultiBackEventHandler;
            hostlabels = [
                new(new(mainmenubuttons[0].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Felhasználónév:", Alignment = TextBox.TextAlignment.Right },
                new(new(mainmenubuttons[1].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = UDP_Broadcast_Helper.UserName, Alignment = TextBox.TextAlignment.Left },
                new(new(mainmenubuttons[2].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Jelszó:", Alignment = TextBox.TextAlignment.Right },
                new(new(mainmenubuttons[3].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "UDP_Broadcast_Helper.Secret", Alignment = TextBox.TextAlignment.Left } ];
            clientButtons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Vissza",
                    Location = new(menubackgroundRectangle.X + (width/2) - (mbSize.X/2), menubackgroundRectangle.Bottom - framexy - mbSize.Y),
                    Size = mbSize
                } ];
            clientButtons[0].Click += MultiBackEventHandler;
            clientlabels = [
                new(new(new(contentbackgroundRectangle.X + (((contentbackgroundRectangle.Width/2)-bSize.X)/2), contentbackgroundRectangle.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Felhasználónév:", Alignment = TextBox.TextAlignment.Right },
                new(new(new(contentbackgroundRectangle.Right - (((contentbackgroundRectangle.Width / 2) - bSize.X) / 2) - bSize.X, contentbackgroundRectangle.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = UDP_Broadcast_Helper.UserName, Alignment = TextBox.TextAlignment.Left },
                new(new(new(contentbackgroundRectangle.X + (((contentbackgroundRectangle.Width/2)-bSize.X)/2), contentbackgroundRectangle.Y + bSize.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Add meg a jelszót:", Alignment = TextBox.TextAlignment.Right },
                ];
            clientInputs = [
                new(new(new(clientlabels[1].Rect.X, contentbackgroundRectangle.Y + bSize.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"], mouseInfo, 8) {Text = "*", BGColor = Color.Silver} ];
            clientList = new(new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y + (bSize.Y * 2), contentbackgroundRectangle.Width, contentbackgroundRectangle.Height - (bSize.Y * 2)),
                mouseInfo);
        }

        private void SetRandomAIEventHandler(object? sender, EventArgs e)
        {
            GameSettings.RandomAIEnabled = !GameSettings.RandomAIEnabled;
            settingsmenubuttons[2].Text = GameSettings.RandomAIEnabled ? "Random AI" : "Neurálhálós AI";
        }

        private void SetMultiCastEventHandler(object? sender, EventArgs e)
        {
            GameSettings.MultiCastEnabled = !GameSettings.MultiCastEnabled;
            settingsmenubuttons[1].Text = GameSettings.MultiCastEnabled ? "MultiCast mód" : "BroadCast mód";
        }

        private void UserNameChangeEventHandler(object? sender, EventArgs e)
        {
            if (multiselectorInputs[0].Text == string.Empty) return;
            DatabaseConnector.SetUsername(multiselectorInputs[0].Text);
        }

        private void ClientModeEventHandler(object? sender, EventArgs e)
        {
            if (multiselectorInputs[0].Text == string.Empty) return;
            clientopen = true;
            multiselectoropen = false;
            UDP_Broadcast_Helper.StartClient(multiselectorInputs[0].Text);
            clientlabels[1].Text = UDP_Broadcast_Helper.UserName;
        }

        private void HostModeEventHandler(object? sender, EventArgs e)
        {
            if (multiselectorInputs[0].Text == string.Empty) return;
            hostopen = true;
            multiselectoropen = false;
            UDP_Broadcast_Helper.StartHosting(multiselectorInputs[0].Text);
            hostlabels[1].Text = UDP_Broadcast_Helper.UserName;
            hostlabels[3].Text = UDP_Broadcast_Helper.Secret;
        }

        private void MultiBackEventHandler(object? sender, EventArgs e)
        {
            multiselectoropen = false;
            hostopen = false;
            clientopen = false;
            UDP_Broadcast_Helper.StopAsync().Wait();
        }
        private void SinglePlayerEventHandler(object? sender, EventArgs e) => CurrentMenuState = MenuState.SinglePlayer;
        private void MultiPlayerEventHandler(object? sender, EventArgs e) => multiselectoropen = true;
        private void ManualEventHandler(object? sender, EventArgs e)
        {
            manualopen = true;
            menuTitle.Text = "Útmutató";
        }
        private void SettingsEventHandler(object? sender, EventArgs e)
        {
            settingsmenuopen = true;
            menuTitle.Text = "Beállítások";
        }
        private void ExitEventHandler(object? sender, EventArgs e) => CurrentMenuState = MenuState.Exit;
        private void SettingsBackEventHandler(object? sender, EventArgs e)
        {
            settingsmenuopen = false;
            manualopen = false;
            menuTitle.Text = "A Kozmosz Lapjai";
        }

        private void MusicVolumeChangedEventHandler(object? sender, EventArgs e)
        {
            Slider slider = (Slider)sender!;
            GameSettings.MusicVolume = slider.Value;
        }

        private void SFXVolumeChangedEventHandler(object? sender, EventArgs e)
        {
            Slider slider = (Slider)sender!;
            GameSettings.SFXVolume = slider.Value;
        }

        public void ResetMenuState()
        {
            CurrentMenuState = MenuState.None;
            clientopen = false;
            hostopen = false;
            multiselectoropen = false;
            settingsmenuopen = false;
            manualopen = false;
            joinTask = null;
        }

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
                manualslider.Value += mouseInfo.WheelDelta * -0.02f;
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
            else if (multiselectoropen) {
                foreach (var button in multiselectorbuttons) {
                    button.Update(gameTime);
                }
                foreach (var input in multiselectorInputs) {
                    input.Update(gameTime);
                }
                foreach (var label in multiselectorlabels) {
                    label.Update(gameTime);
                }
            }
            else if (hostopen) {
                foreach (var button in hostbuttons) {
                    button.Update(gameTime);
                }
                foreach (var label in hostlabels) {
                    label.Update(gameTime);
                }
                if (UDP_Broadcast_Helper.Connection is not null && UDP_Broadcast_Helper.Connection.IsCompleted) {
                    if (UDP_Broadcast_Helper.Connection.Result.IsConnected) {
                        // !!! set to multiplayer mode (in game1 stop UDPHELPER)
                        CurrentMenuState = MenuState.MultiPlayer;
                    }
                }
            }
            else if (clientopen) {
                foreach (var button in clientButtons) { button.Update(gameTime); }
                foreach (var input in clientInputs) { input.Update(gameTime); }
                foreach (var label in clientlabels) { label.Update(gameTime); }
                if (clientList.Selected is not null) {
                    if (joinTask is not null) {
                        if (joinTask.IsCompleted) {
                            if (joinTask.Result) {
                                if (UDP_Broadcast_Helper.Connection is not null && UDP_Broadcast_Helper.Connection.IsCompleted) {
                                    if (UDP_Broadcast_Helper.Connection.Result.IsConnected) {
                                        // !!! set to multiplayer mode (in game1 stop UDPHELPER)
                                        CurrentMenuState = MenuState.MultiPlayer;
                                    }
                                    else {
                                        joinTask = null;
                                        clientList.ResetSelected();
                                    }
                                }
                            }
                            else {
                                joinTask = null;
                                clientList.ResetSelected();
                            }
                        }
                    }
                    else {
                        if (clientInputs[0].Text != string.Empty) {
                            joinTask = UDP_Broadcast_Helper.SendJoinAsync((Tuple<DiscoveryPayload, byte[], byte[]>)clientList.Selected.Item2, clientInputs[0].Text);
                        }
                        else {
                            clientList.ResetSelected();
                        }
                    }
                }
                else {
                    if (!UDP_Broadcast_Helper.GetDiscovered().SequenceEqual(currentdisclist)) {
                        List<Tuple<string, object>> replacelist = [];
                        currentdisclist = UDP_Broadcast_Helper.GetDiscovered();
                        foreach (var element in currentdisclist) {
                            string str = $"{element.Item1.Username} || {element.Item1.IP.ToString()}";
                            replacelist.Add(new(str, element));
                        }
                        clientList.ReplaceOptions(replacelist.ToArray());
                    }
                }
                clientList.Update(gameTime);
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
            else if (multiselectoropen) {
                foreach (var button in multiselectorbuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
                foreach (var input in multiselectorInputs) {
                    input.Draw(gameTime, spriteBatch);
                }
                foreach (var label in multiselectorlabels) {
                    label.Draw(gameTime, spriteBatch);
                }
            }
            else if (hostopen) {
                foreach (var button in hostbuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
                foreach (var label in hostlabels) {
                    label.Draw(gameTime, spriteBatch);
                }
            }
            else if (clientopen) {
                foreach (var button in clientButtons) {
                    button.Draw(gameTime, spriteBatch);
                }
                foreach (var input in clientInputs) {
                    input.Draw(gameTime, spriteBatch);
                }
                foreach (var label in clientlabels) {
                    label.Draw(gameTime, spriteBatch);
                }
                clientList.Draw(gameTime, spriteBatch);
            }
            else {
                foreach (var button in mainmenubuttons) {
                    button.Draw(gameTime, spriteBatch);
                }
            }
        }

    }
}
