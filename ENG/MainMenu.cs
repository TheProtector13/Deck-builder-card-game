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
                "Controls:", //
                "Press 'ESC' to exit. Click on objects with the right mouse\n" +
                "button (for example cards, decks and piles) to inspect them. Use the\n" +
                "left mouse button to select options in windows and menus.\n", //
                "Hold down the left mouse button over cards to move them. Release\n" +
                "them over the highlighted orange areas for the following actions:\n" +
                "Buy a card from the shop, Play a card!", //
                "Both the player and the enemy have the following attributes:\n" +
                "Red circle - Attack this turn\nYellow circle - Money this turn\nBlue heart - Player/enemy health", //
                ResourceManager.Textures["MANUAL_IMG"][0], //
                "Gameplay:", //
                "1. At the start of the game, each player gets 10 cards in their deck. These cards\n" +
                "are not tied to any faction and each provides one unit of money.\n" +
                "2. The game is played in turns, with players taking turns one after another, then a new round begins.\n", //
                "3. At the start of each turn, the player draws 5 cards from their own deck, all of\n" +
                "which must be played during the turn. A turn can only end for a given\n" +
                "player once no cards remain in their hand.", //
                "4. While playing cards, or even afterwards, the player may buy cards from\n" +
                "the shop using the money earned from playing cards. Purchased cards go\n" +
                "into the player's \"scrap pile\", i.e. the discard pile. Money\n" +
                "earned during a turn is only valid until the end of that turn and does not carry over.", //
                "5. At the end of the turn, the attack points collected by the player are deducted from the enemy's health.\n" +
                "The played cards go into the discard pile.", //
                "6. The next turn begins by dealing cards from the player's deck;\n" +
                "if the deck is empty, the discard pile is shuffled back into the deck.\n" +
                "7. Turns repeat until either the enemy's or the player's health runs out!", //
                "Objective of the game:", //
                "Defeat the enemy by reducing their health to zero", //
                "Factions and their abilities:", //
                "\"The Eye\" - Reveals the enemy's card, or hides our own.\n" +
                "Steals a card from the enemy for this turn.", //
                ResourceManager.Textures["TheEyeIconWB"][0], //
                "\"Empire\" - Medium attack, forces the enemy to discard some cards\n" +
                "instead of playing them. Some of its cards can draw extra cards from the deck this turn.", //
                ResourceManager.Textures["EmpireIconWB"][0], //
                "\"Alliance\" - Provides money and health. Can remove cards from the shop,\n" +
                "preventing the enemy from building their deck.", //
                ResourceManager.Textures["AllianceIconWB"][0], //
                "\"Machines\" - Strong attack and attack bonuses. They have no other special abilities.", //
                ResourceManager.Textures["MachinesIconWB"][0], //
                "\"Curatorium\" - Provides money, weak attack. Can permanently remove cards\n" +
                "from players' decks, so more important cards can be played more often.", //
                ResourceManager.Textures["CollectorCultIconWB"][0],
                "Have Fun!"
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
                    Text = "Single Player",
                    Location = new(contentbackgroundRectangle.X, contentbackgroundRectangle.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["PlayButton"][0],ResourceManager.Textures["PlayButton"][2]], mouseInfo) {
                    Text = "Multiplayer (LAN)",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, contentbackgroundRectangle.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["ManualButton"][0],ResourceManager.Textures["ManualButton"][1]], mouseInfo) {
                    Text = "Manual",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["SettingsButton"][0],ResourceManager.Textures["SettingsButton"][1]], mouseInfo) {
                    Text = "Settings",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Exit",
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
                    Text = "Back",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + hieght_until_B),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                },
                new(ResourceManager.Textures["BUTTON"], mouseInfo) {
                    Text = GameSettings.MultiCastEnabled ? "MultiCast mode" : "BroadCast mode",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + hieght_until_B - (bSize.Y/2*3)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                },
                new(ResourceManager.Textures["BUTTON"], mouseInfo) {
                    Text = GameSettings.RandomAIEnabled ? "Random AI" : "Neural Net AI",
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
                    Text = "Music volume"
                },
                new(new(new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (hieght_until_B/2) - (bSize.Y/2)), bSize), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = "Sound effects volume"
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
                Text = "Card Cosmos"
            };
            int framexy = (int)MathF.Round(width * 0.0572916666f);
            Point mbSize = new((int)MathF.Round((col1xy - framexy) / ratio), col1xy - framexy);
            manualbuttons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Back",
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
                    Text = "Host",
                    Location = new(contentbackgroundRectangle.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["SettingsButton"][0],ResourceManager.Textures["SettingsButton"][1]], mouseInfo) {
                    Text = "Join",
                    Location = new(menubackgroundRectangle.X + width - col1xy - bSize.X, menubackgroundRectangle.Y + (height/2) - (bSize.Y/2)),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 2
                },
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Back",
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
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Username:" } ];
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
                    Text = "Back",
                    Location = new(menubackgroundRectangle.X + (width/2) - (bSize.X/2), menubackgroundRectangle.Y + height - col1xy - bSize.Y),
                    Size = bSize,
                    SetTextOffsetY = bSize.Y / 4,
                    SetTextOffsetW = bSize.Y / 4
                } ];
            hostbuttons[0].Click += MultiBackEventHandler;
            hostlabels = [
                new(new(mainmenubuttons[0].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Username:", Alignment = TextBox.TextAlignment.Right },
                new(new(mainmenubuttons[1].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = UDP_Broadcast_Helper.UserName, Alignment = TextBox.TextAlignment.Left },
                new(new(mainmenubuttons[2].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Password:", Alignment = TextBox.TextAlignment.Right },
                new(new(mainmenubuttons[3].Location, bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "UDP_Broadcast_Helper.Secret", Alignment = TextBox.TextAlignment.Left } ];
            clientButtons = [
                new([ResourceManager.Textures["BUTTON"][0],ResourceManager.Textures["BUTTON"][1]], mouseInfo) {
                    Text = "Back",
                    Location = new(menubackgroundRectangle.X + (width/2) - (mbSize.X/2), menubackgroundRectangle.Bottom - framexy - mbSize.Y),
                    Size = mbSize
                } ];
            clientButtons[0].Click += MultiBackEventHandler;
            clientlabels = [
                new(new(new(contentbackgroundRectangle.X + (((contentbackgroundRectangle.Width/2)-bSize.X)/2), contentbackgroundRectangle.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Username:", Alignment = TextBox.TextAlignment.Right },
                new(new(new(contentbackgroundRectangle.Right - (((contentbackgroundRectangle.Width / 2) - bSize.X) / 2) - bSize.X, contentbackgroundRectangle.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = UDP_Broadcast_Helper.UserName, Alignment = TextBox.TextAlignment.Left },
                new(new(new(contentbackgroundRectangle.X + (((contentbackgroundRectangle.Width/2)-bSize.X)/2), contentbackgroundRectangle.Y + bSize.Y), bSize),
                    ResourceManager.Fonts["FONT_DEF_B"]) { Text = "Enter the password:", Alignment = TextBox.TextAlignment.Right },
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
            settingsmenubuttons[2].Text = GameSettings.RandomAIEnabled ? "Random AI" : "Neural Net AI";
        }

        private void SetMultiCastEventHandler(object? sender, EventArgs e)
        {
            GameSettings.MultiCastEnabled = !GameSettings.MultiCastEnabled;
            settingsmenubuttons[1].Text = GameSettings.MultiCastEnabled ? "MultiCast mode" : "BroadCast mode";
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
            menuTitle.Text = "Manual";
        }
        private void SettingsEventHandler(object? sender, EventArgs e)
        {
            settingsmenuopen = true;
            menuTitle.Text = "Settings";
        }
        private void ExitEventHandler(object? sender, EventArgs e) => CurrentMenuState = MenuState.Exit;
        private void SettingsBackEventHandler(object? sender, EventArgs e)
        {
            settingsmenuopen = false;
            manualopen = false;
            menuTitle.Text = "Card Cosmos";
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
