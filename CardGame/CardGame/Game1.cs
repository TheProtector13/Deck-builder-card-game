using System.Runtime;
using System.Threading.Tasks;
using CardGame.TCP;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using static CardGame.TCP.MessagePackHelper;

#nullable enable
namespace CardGame {
    public class Game1 : Game {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BackGround? bg;
        private IDrawable? fg;
        private MainMenu menu;
        private SplashScreen? splashScreen;
        private Texture2D[] CursorTexture;
        private readonly MouseInfo mouseInfo;
        private Task ResourceManagerLoading;
        private readonly TaskCompletionSource<bool> ResourceManagerLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Game1()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _graphics = new GraphicsDeviceManager(this) {
                PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height,
                PreferMultiSampling = true,
                HardwareModeSwitch = false,
                IsFullScreen = true
            };
            _graphics.PreparingDeviceSettings += (s, e) => {
                e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
            };
            _graphics.ApplyChanges();
            this.IsFixedTimeStep = true;
            this.TargetElapsedTime = System.TimeSpan.FromSeconds(1d / 60d);
            Content.RootDirectory = "Content";
            IsMouseVisible = false;
            mouseInfo = new MouseInfo(Mouse.GetState());
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            ResourceManager.TexturePath = "TEXTURES";
            ResourceManager.FontPath = "FONTS";
            ResourceManager.SoundPath = "SFX";
            ResourceManager.SongPath = "MUSIC";
            ResourceManager.TargetLoadTime = 8;
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            splashScreen = new SplashScreen(Content);
            ResourceManagerLoading = Task.Run(async () => {
                MLController.Init();
                DatabaseConnector.Init();
                ResourceManager.Init(Content, externallyCalledTextureLoading: true);
                await ResourceManagerLoaded.Task;
                MusicPlayer.Init();
                CursorTexture = ResourceManager.Textures["Cursor"];
            });
            //ResourceManager.Init(Content);
            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            //    if (splashScreen is null && (fg is null || fg.WINNER != ForeGround.GameWinner.InProgress))
            //        Exit();
            DisplayInfo.IsFocused = this.IsActive;
            mouseInfo.Update(Mouse.GetState());
            if (splashScreen is not null && !splashScreen.Finished) {
                splashScreen.Percentage = ResourceManager.GetLoadProgress();
                splashScreen.Update(gameTime);
                ResourceManager.LoadNextTextureBatch(Content);
                if (ResourceManager.IsLoaded)
                    ResourceManagerLoaded.TrySetResult(true);
                if (ResourceManagerLoading.IsCompleted)
                    splashScreen.AddNewCards = false;
            }
            else if (splashScreen is not null) {
                splashScreen.Dispose();
                splashScreen = null;
                menu = new();
            }
            else {
                if (menu.CurrentMenuState == MainMenu.MenuState.None)
                    menu.Update(gameTime);
                else {
                    switch (menu.CurrentMenuState) {
                        case MainMenu.MenuState.SinglePlayer:
                            if (fg is null || bg is null) {
                                bg = new BackGround();
                                fg = new ForeGround(bg) { RandomAI = GameSettings.RandomAIEnabled };
                                MusicPlayer.SetAlbum(bg.Type);
                            }
                            bg.Update(gameTime);
                            fg.Update(gameTime);
                            if (((ForeGround)fg).WINNER != ForeGround.GameWinner.InProgress) {
                                menu.ResetMenuState();
                                fg = null;
                                bg = null;
                                MusicPlayer.Unmute();
                                MusicPlayer.SetAlbum();
                            }
                            break;
                        case MainMenu.MenuState.MultiPlayer:
                            if (fg is null || bg is null) {
                                TcpTlsPeer peer = UDP_Broadcast_Helper.Connection!.Result;
                                peer.StartReceiving();
                                UDP_Broadcast_Helper.StopAsync().Wait();
                                if (peer.IsHost) {
                                    bg = new BackGround();
                                    ActionType type = bg.Type switch {
                                        BackGround.BackGroundType.Forest => ActionType.ForestPlanet,
                                        BackGround.BackGroundType.Ice => ActionType.IcePlanet,
                                        BackGround.BackGroundType.Desert => ActionType.DesertPlanet,
                                        _ => ActionType.ForestPlanet,
                                    };
                                    peer.SendAsync(new ActionPayload(type, null, CryptographyHelper.NowMs()));
                                }
                                else {
                                    Task.Delay(500).Wait();
                                    BackGround.BackGroundType type;
                                    if (peer.TryDequeueOldest() is ReceivedPacket packet && packet.Payload is ActionPayload payload) {
                                        type = payload.Action switch {
                                            ActionType.ForestPlanet => BackGround.BackGroundType.Forest,
                                            ActionType.IcePlanet => BackGround.BackGroundType.Ice,
                                            ActionType.DesertPlanet => BackGround.BackGroundType.Desert,
                                            _ => BackGround.BackGroundType.Forest,
                                        };
                                    }
                                    else {
                                        menu.ResetMenuState();
                                        peer.Dispose();
                                        MusicPlayer.Unmute();
                                        MusicPlayer.SetAlbum();
                                        break;
                                    }
                                    bg = new BackGround(type);
                                }
                                fg = new ForeGround_Multi(bg, peer);
                                MusicPlayer.SetAlbum(bg.Type);
                            }
                            bg.Update(gameTime);
                            fg.Update(gameTime);
                            if (((ForeGround_Multi)fg).WINNER != ForeGround_Multi.GameWinner.InProgress) {
                                menu.ResetMenuState();
                                ((ForeGround_Multi)fg).Dispose();
                                fg = null;
                                bg = null;
                                MusicPlayer.Unmute();
                                MusicPlayer.SetAlbum();
                            }
                            break;
                        case MainMenu.MenuState.Exit:
                            Exit();
                            break;
                        default:
                            break;
                    }
                }
            }

            // TODO: Add your update logic here

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                SamplerState.LinearClamp, DepthStencilState.None, new() { CullMode = CullMode.None, MultiSampleAntiAlias = true });
            if (splashScreen is not null) {
                splashScreen.Draw(gameTime, _spriteBatch);
            }
            else {
                if (menu.CurrentMenuState == MainMenu.MenuState.None)
                    menu.Draw(gameTime, _spriteBatch);
                else {
                    bg?.Draw(gameTime, _spriteBatch);
                    fg?.Draw(gameTime, _spriteBatch);
                }
            }
            //Cursor
            if (CursorTexture is not null)
                if (mouseInfo.Current.RightButton == ButtonState.Pressed)
                    _spriteBatch.Draw(CursorTexture[2], mouseInfo.GetMousePosition(true), Color.White);
                else if (mouseInfo.Current.LeftButton == ButtonState.Pressed)
                    _spriteBatch.Draw(CursorTexture[1], mouseInfo.GetMousePosition(true), Color.White);
                else if (mouseInfo.Current.MiddleButton == ButtonState.Pressed || mouseInfo.WheelDelta != 0)
                    _spriteBatch.Draw(CursorTexture[3], mouseInfo.GetMousePosition(true), Color.White);
                else
                    _spriteBatch.Draw(CursorTexture[0], mouseInfo.GetMousePosition(true), Color.White);
            //
            _spriteBatch.End();

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            DatabaseConnector.CloseConnection();
            ResourceManager.Dispose();
            UDP_Broadcast_Helper.StopAsync().Wait(5000);
            UDP_Broadcast_Helper.Dispose();
            base.OnExiting(sender, args);
        }
    }
}
