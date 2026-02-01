using System;
using Microsoft.Xna.Framework.Media;

#nullable enable
namespace CardGame {
    internal static class MusicPlayer {
        private readonly static Song[] MainMenuSong = [ResourceManager.Songs["MAIN"]];
        private static Song[] CurrentAlbum = MainMenuSong;
        private readonly static Song[] ForestPlanet = [
            ResourceManager.Songs["1honor-and-sword"],
            ResourceManager.Songs["1Motivator"],
            ResourceManager.Songs["1Oboro-Mysterious_Lights"]
        ];
        private readonly static Song[] DesertPlanet = [
            ResourceManager.Songs["3 glass-wind"],
            ResourceManager.Songs["3Adding_the_Sun"],
            ResourceManager.Songs["3beauty-of-nature"]
        ];
        private readonly static Song[] IcePlanet = [
            ResourceManager.Songs["2Floating_Cities"],
            ResourceManager.Songs["2GoOn"],
            ResourceManager.Songs["2Rynos_Theme"],
            ResourceManager.Songs["2wind-peter-bamitale"]
        ];

        static MusicPlayer()
        {
            MediaPlayer.IsRepeating = false;
            MediaPlayer.Volume = GameSettings.MusicVolume;
            MediaPlayer.MediaStateChanged += MediaPlayer_MediaStateChanged;
            GameSettings.OnMusicVolumeChanged += SetVolume;
        }

        private static void SetVolume(object? sender, EventArgs e) => MediaPlayer.Volume = GameSettings.MusicVolume;

        private static void MediaPlayer_MediaStateChanged(object? sender, EventArgs e)
        {
            if (MediaPlayer.State == MediaState.Stopped) {
                MediaPlayer.Play(CurrentAlbum[Random.Shared.Next(0, CurrentAlbum.Length)]);
            }
        }

        public static void Init() => MediaPlayer.Play(CurrentAlbum[Random.Shared.Next(0, CurrentAlbum.Length)]);

        public static void Mute() => MediaPlayer.Volume = 0f;

        public static void Unmute() => MediaPlayer.Volume = GameSettings.MusicVolume;

        public static void SetAlbum(BackGround.BackGroundType? type = null)
        {
            CurrentAlbum = type switch {
                BackGround.BackGroundType.Forest => ForestPlanet,
                BackGround.BackGroundType.Desert => DesertPlanet,
                BackGround.BackGroundType.Ice => IcePlanet,
                _ => MainMenuSong,
            };
            MediaPlayer.Stop();
        }

    }
}
