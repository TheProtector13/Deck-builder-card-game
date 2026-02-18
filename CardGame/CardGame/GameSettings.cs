using System;

#nullable enable
namespace CardGame {
    internal static class GameSettings {
        private static float sFXVolume = 0.5f;
        private static float musicVolume = 0.5f;
        private static bool randomAIEnabled = false;
        private static bool multiCastEnabled = true;
        public static event EventHandler? OnSFXVolumeChanged;
        public static event EventHandler? OnMusicVolumeChanged;
        public static event EventHandler? OnRandomAIEnabledChanged;
        public static event EventHandler? OnMultiCastEnabledChanged;

        static GameSettings()
        {
            float? SFX = DatabaseConnector.GetSetting("SFX");
            float? Music = DatabaseConnector.GetSetting("MUSIC");
            bool? RAI = DatabaseConnector.GetSetting("RandomAI", true);
            bool? MCE = DatabaseConnector.GetSetting("MultiCast", true);
            sFXVolume = SFX ?? sFXVolume;
            musicVolume = Music ?? musicVolume;
            randomAIEnabled = RAI ?? randomAIEnabled;
            multiCastEnabled = MCE ?? multiCastEnabled;
        }

        public static float MusicVolume
        {
            get => musicVolume;
            set {
                musicVolume = value;
                DatabaseConnector.SetSetting("MUSIC", value);
                OnMusicVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static float SFXVolume
        {
            get => sFXVolume;
            set {
                sFXVolume = value;
                DatabaseConnector.SetSetting("SFX", value);
                OnSFXVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool RandomAIEnabled
        {
            get => randomAIEnabled;
            set {
                randomAIEnabled = value;
                DatabaseConnector.SetSetting("RandomAI", value);
                OnRandomAIEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool MultiCastEnabled
        {
            get => multiCastEnabled;
            set {
                multiCastEnabled = value;
                DatabaseConnector.SetSetting("MultiCast", value);
                OnMultiCastEnabledChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
