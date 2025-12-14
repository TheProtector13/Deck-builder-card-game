using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace CardGame {
    internal static class ResourceManager {
        /// <summary>
        /// Specifies the maximum number of atlases that can be used per fontsystem.
        /// </summary>
        public static byte MaxAtlasCount = 3;
        /// <summary>
        /// Gets or sets the file path to the texture resources. Relative to the Content root directory.
        /// </summary>
        public static string TexturePath { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the file path of the songs. Relative to the Content root directory.
        /// </summary>
        public static string SongPath { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the file path to the sound resources. Relative to the Content root directory.
        /// </summary>
        public static string SoundPath { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the file path to the font resources. Relative to the Content root directory.
        /// </summary>
        public static string FontPath { get; set; } = string.Empty;

        private static HashSet<FontSystem> toReset = [];

        /// <summary>
        /// Gets the collection of textures grouped by their associated keys. The key is derived from the texture file name without its extension and any trailing digits or numbering.
        /// Returns a Texture2D array. If a texture is part of a sequence (e.g., texture1, texture2, texture3), all related textures are grouped into an array under the same key.
        /// </summary>
        public static ImmutableDictionary<string, Texture2D[]> Textures { get; private set; } = ImmutableDictionary<string, Texture2D[]>.Empty;
        private static readonly List<Texture2D> SingleColorTextures = [];
        /// <summary>
        /// Gets the collection of sound effects available in the application, indexed by unique string keys. The key is derived from the sound effect file name without its extension.
        /// </summary>
        public static ImmutableDictionary<string, SoundEffect> SoundEffects { get; private set; } = ImmutableDictionary<string, SoundEffect>.Empty;
        /// <summary>
        /// Gets the collection of songs, indexed by unique string keys. The key is derived from the song file name without its extension.
        /// </summary>
        public static ImmutableDictionary<string, Song> Songs { get; private set; } = ImmutableDictionary<string, Song>.Empty;
        /// <summary>
        /// Gets the collection of fonts available for use, indexed by unique string keys. The key is derived from the font file name without its extension.
        /// </summary>
        public static ImmutableDictionary<string, FontSystem> Fonts { get; private set; } = ImmutableDictionary<string, FontSystem>.Empty;


        /// <summary>
        /// Initializes the content manager and loads textures, songs, sound effects, and fonts from the specified
        /// paths.
        /// </summary>
        /// <remarks>This method initializes the asset dictionaries for textures, songs, sound effects,
        /// and fonts as immutable collections. It scans the specified directories for assets, ensuring that asset names
        /// are unique. If a path is not provided for a specific asset type, the previously set path will be
        /// used.</remarks>
        /// <param name="Content">The content manager used to load assets.</param>
        /// <param name="texturepath">The relative path to the directory containing texture assets. If <paramref name="texturepath"/> is null, the
        /// previously set texture path will be used. Throws an exception if no texture path has been set.</param>
        /// <param name="songpath">The relative path to the directory containing song assets. If null, the previously set song path will be
        /// used. If 'string.Empty' or its originally was 'string.Empty' and now it is null, then songs wont be loaded.</param>
        /// <param name="soundpath">The relative path to the directory containing sound effect assets. If null, the previously set sound path
        /// will be used. If 'string.Empty' or its originally was 'string.Empty' and now it is null, then sound effects wont be loaded.</param>
        /// <param name="fontpath">The relative path to the directory containing font assets. If null, the previously set font path will be
        /// used. If 'string.Empty' or its originally was 'string.Empty' and now it is null, then fonts wont be loaded.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="texturepath"/> is null and no texture path has been set previously.</exception>
        /// <exception cref="Exception">Thrown if duplicate keys are detected for textures, songs, sound effects, or fonts. Asset names must be
        /// unique.</exception>
        public static void Init(ContentManager Content, string texturepath = null, string songpath = null, string soundpath = null, string fontpath = null)
        {
            if (texturepath is null && TexturePath == string.Empty) {
                throw new ArgumentNullException(nameof(texturepath), "Texture path cannot be null if it has not been set before.");
            }
            FontSystemDefaults.TextureWidth = 2048;
            FontSystemDefaults.TextureHeight = 2048;
            FontSystemDefaults.FontResolutionFactor = 2.0f;
            FontSystemDefaults.KernelWidth = 2;
            FontSystemDefaults.KernelHeight = 2;
            FontSystemDefaults.DisableAntialiasing = true;
            FontSystemDefaults.PremultiplyAlpha = true;
            FontSystemDefaults.UseKernings = true;
            TexturePath = texturepath ?? TexturePath;
            SongPath = songpath ?? SongPath;
            SoundPath = soundpath ?? SoundPath;
            FontPath = fontpath ?? FontPath;
            Textures = ImmutableDictionary<string, Texture2D[]>.Empty;
            SoundEffects = ImmutableDictionary<string, SoundEffect>.Empty;
            Songs = ImmutableDictionary<string, Song>.Empty;
            Fonts = ImmutableDictionary<string, FontSystem>.Empty;
            Dictionary<string, Texture2D[]> textures = [];
            Dictionary<string, SoundEffect> soundeffects = [];
            Dictionary<string, Song> songs = [];
            Dictionary<string, FontSystem> fonts = [];
            string currentpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Content.RootDirectory);
            // Load Textures
            string currentpath_T = Path.Combine(currentpath, TexturePath);
            string[] files = Directory.GetFiles(currentpath_T, "*.xnb", SearchOption.AllDirectories);
            List<string> piplinepaths = [];
            foreach (string file in files) {
                string relativePath = Path.GetRelativePath(currentpath, file);
                string pipelinepath = Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '/');
                piplinepaths.Add(pipelinepath);
            }
            Dictionary<string, List<Tuple<string, int>>> texture_sequences = [];
            for (int i = piplinepaths.Count - 1; i > -1; i--) {
                if (Char.IsDigit(piplinepaths[i].Last())) {
                    int numlength = 1;
                    for (int j = piplinepaths[i].Length - 2; j > -1; j--) {
                        if (Char.IsDigit(piplinepaths[i][j])) {
                            numlength++;
                        }
                        else {
                            break;
                        }
                    }
                    string key = piplinepaths[i].Substring(0, piplinepaths[i].Length - numlength);
                    int num = int.Parse(piplinepaths[i].Substring(piplinepaths[i].Length - numlength));
                    if (texture_sequences.ContainsKey(key)) {
                        texture_sequences[key].Add(new(piplinepaths[i], num));
                    }
                    else {
                        texture_sequences.Add(key, [new(piplinepaths[i], num)]);
                    }
                    piplinepaths.RemoveAt(i);
                }
            }
            foreach (string key in texture_sequences.Keys) {
                List<Tuple<string, int>> sequence = texture_sequences[key];
                sequence.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                List<Texture2D> textureseq = [];
                foreach (Tuple<string, int> item in sequence) {
                    textureseq.Add(Content.Load<Texture2D>(item.Item1));
                }
                string newkey = key.Split("/").Last();
                if (!textures.ContainsKey(newkey)) {
                    textures.Add(newkey, textureseq.ToArray());
                }
                else {
                    throw new Exception($"Duplicate texture key: {newkey} ! Texture names MUST be unique!");
                }
            }
            foreach (string path in piplinepaths) {
                string key = path.Split("/").Last();
                if (!textures.ContainsKey(key)) {
                    textures.Add(key, [Content.Load<Texture2D>(path)]);
                }
                else {
                    throw new Exception($"Duplicate texture key: {key} ! Texture names MUST be unique!");
                }
            }
            Textures = textures.ToImmutableDictionary();
            // Load Songs
            if (SongPath != string.Empty) {
                string currentpath_S = Path.Combine(currentpath, SongPath);
                files = Directory.GetFiles(currentpath_S, "*.xnb", SearchOption.AllDirectories);
                piplinepaths.Clear();
                foreach (string file in files) {
                    string relativePath = Path.GetRelativePath(currentpath, file);
                    string pipelinepath = Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '/');
                    piplinepaths.Add(pipelinepath);
                }
                foreach (string path in piplinepaths) {
                    string key = path.Split("/").Last();
                    if (!songs.ContainsKey(key)) {
                        songs.Add(key, Content.Load<Song>(path));
                    }
                    else {
                        throw new Exception($"Duplicate song key: {key} ! Song names MUST be unique!");
                    }
                }
                Songs = songs.ToImmutableDictionary();
            }
            // Load SoundEffects
            if (SoundPath != string.Empty) {
                string currentpath_E = Path.Combine(currentpath, SoundPath);
                files = Directory.GetFiles(currentpath_E, "*.xnb", SearchOption.AllDirectories);
                piplinepaths.Clear();
                foreach (string file in files) {
                    string relativePath = Path.GetRelativePath(currentpath, file);
                    string pipelinepath = Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '/');
                    piplinepaths.Add(pipelinepath);
                }
                foreach (string path in piplinepaths) {
                    string key = path.Split("/").Last();
                    if (!soundeffects.ContainsKey(key)) {
                        soundeffects.Add(key, Content.Load<SoundEffect>(path));
                    }
                    else {
                        throw new Exception($"Duplicate sound effect key: {key} ! Sound effect names MUST be unique!");
                    }
                }
                SoundEffects = soundeffects.ToImmutableDictionary();
            }
            // Load Fonts
            if (FontPath != string.Empty) {
                string currentpath_F = Path.Combine(currentpath, FontPath);
                files = Directory.GetFiles(currentpath_F, "*.*", SearchOption.AllDirectories);
                piplinepaths.Clear();
                List<string> relativepaths = [];
                foreach (string file in files) {
                    string relativePath = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                    string pipelinepath = Path.ChangeExtension(relativePath, null).Split("/").Last();
                    relativepaths.Add(relativePath);
                    piplinepaths.Add(pipelinepath);
                }
                for (int i = 0; i < piplinepaths.Count; i++) {
                    if (!fonts.ContainsKey(piplinepaths[i])) {
                        FontSystem fontSystem = new();
                        fontSystem.CurrentAtlasFull += ClearFonts;
                        fontSystem.AddFont(File.ReadAllBytes(relativepaths[i]));
                        fonts.Add(piplinepaths[i], fontSystem);
                    }
                    else {
                        throw new Exception($"Duplicate font key: {piplinepaths[i]} ! Font names MUST be unique!");
                    }
                }
                Fonts = fonts.ToImmutableDictionary();
            }
        }

        private static void ClearFonts(object sender, EventArgs e)
        {
            FontSystem fontSystem = (FontSystem)sender;
            if (fontSystem.Atlases.Count >= MaxAtlasCount) {
                toReset.Add(fontSystem);
            }
        }

        public static void ResetFonts()
        {
            foreach (FontSystem fontSystem in toReset) {
                fontSystem.Reset();
            }
            toReset.Clear();
        }

        public static Texture2D GetColor(Color color, SpriteBatch spriteBatch)
        {
            foreach (Texture2D tex in SingleColorTextures) {
                Color[] data = new Color[1];
                tex.GetData(data);
                if (data[0] == color) {
                    return tex;
                }
            }
            Texture2D newTex = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
            newTex.SetData([color]);
            SingleColorTextures.Add(newTex);
            return newTex;
        }

    }
}
