using System.Runtime;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable enable
namespace CardGame {
    public class Game1 : Game {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        //TEST
        private BackGround? bg;
        private ForeGround? fg;
        private MainMenu menu;
        private SplashScreen? splashScreen;
        private Task ResourceManagerLoading;

        public Game1()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.PreferMultiSampling = true;
            _graphics.HardwareModeSwitch = false;
            _graphics.IsFullScreen = true;
            this.IsFixedTimeStep = true;
            this.TargetElapsedTime = System.TimeSpan.FromSeconds(1d / 60d);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            ResourceManager.TexturePath = "TEXTURES";
            ResourceManager.FontPath = "FONTS";
            ResourceManager.SoundPath = "SFX";
            ResourceManager.SongPath = "MUSIC";
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            ResourceManagerLoading = Task.Run(() => {
                ResourceManager.Init(Content);
                MLController.Init();
                DatabaseConnector.Init();
                MusicPlayer.Init();
            });
            // ******* //
            // TESTING //            
            splashScreen = new SplashScreen(Content);
            // TODO: use this.Content to load your game content here
        }

        protected override void Update(GameTime gameTime)
        {
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            //    if (splashScreen is null && (fg is null || fg.WINNER != ForeGround.GameWinner.InProgress))
            //        Exit();

            if (splashScreen is not null && !splashScreen.Finished) {
                splashScreen.Percentage = ResourceManager.GetLoadProgress();
                splashScreen.Update(gameTime);
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
                                fg = new ForeGround(bg);
                                MusicPlayer.SetAlbum(bg.Type);
                            }
                            bg.Update(gameTime);
                            fg.Update(gameTime);
                            if (fg.WINNER != ForeGround.GameWinner.InProgress) {
                                menu.ResetMenuState();
                                fg = null;
                                bg = null;
                                MusicPlayer.SetAlbum();
                            }
                            break;
                        case MainMenu.MenuState.MultiPlayer:
                            menu.ResetMenuState();
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

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
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
            _spriteBatch.End();

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            DatabaseConnector.CloseConnection();
            ResourceManager.Dispose();
            base.OnExiting(sender, args);
        }
    }
}
