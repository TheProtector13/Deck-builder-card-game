using System;

#nullable enable
namespace CardGame {
    internal static class GameSettings {
        private static float sFXVolume = 0.5f;
        private static float musicVolume = 0.5f;
        public static event EventHandler? OnSFXVolumeChanged;
        public static event EventHandler? OnMusicVolumeChanged;

        public static float MusicVolume
        {
            get => musicVolume;
            set {
                musicVolume = value;
                OnMusicVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        public static float SFXVolume
        {
            get => sFXVolume;
            set {
                sFXVolume = value;
                OnSFXVolumeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
