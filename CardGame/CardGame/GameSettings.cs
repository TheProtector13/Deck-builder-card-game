using System;

#nullable enable
namespace CardGame {
    internal static class GameSettings {
        private static float sFXVolume = 0.5f;
        private static float musicVolume = 0.5f;
        public static event EventHandler? OnSFXVolumeChanged;
        public static event EventHandler? OnMusicVolumeChanged;

        static GameSettings()
        {
            float? SFX = DatabaseConnector.GetSetting("SFX");
            float? Music = DatabaseConnector.GetSetting("MUSIC");
            sFXVolume = SFX ?? sFXVolume;
            musicVolume = Music ?? musicVolume;
        }

        public static float MusicVolume
        {
            get => musicVolume;
            set {
                musicVolume = value;
                DatabaseConnector.SetSetting("MUSIC", musicVolume);
                OnMusicVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        public static float SFXVolume
        {
            get => sFXVolume;
            set {
                sFXVolume = value;
                DatabaseConnector.SetSetting("SFX", sFXVolume);
                OnSFXVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
