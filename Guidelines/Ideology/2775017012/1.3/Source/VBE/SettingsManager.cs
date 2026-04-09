using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace VBE
{
    public static class SettingsManager
    {
        public static string SettingsFilePath = Path.Combine(GenFilePaths.ConfigFolderPath, "VanillaBackgroundsExpanded_LoadingSettings.txt");
        public static bool Loading = true;
        private static readonly List<BackgroundImage> images = new();
        public static bool DoLoadingBackground;
        private static BackgroundImage singleImage;
        public static bool Initialized;

        public static IEnumerable<BackgroundImage> Images => Loading ? images : VBEMod.Settings.Enabled.Select(def => (BackgroundImage) def);
        public static bool UseSingle => Loading ? singleImage is not null : !VBEMod.Settings.current.NullOrEmpty();
        public static BackgroundImage Single => Loading ? singleImage : DefDatabase<BackgroundImageDef>.GetNamedSilentFail(VBEMod.Settings.current);
        public static bool Allowed(BackgroundImage image) => image != null && (Loading || VBEMod.Settings.Allowed(image.Def));

        public static int Index(BackgroundImage image) => Loading
            ? images.IndexOf(image)
            : VBEMod.Settings.Enabled.ToList().IndexOf(image.Def);

        public static void Clear()
        {
            foreach (var image in singleImage is null ? images : images.Append(singleImage))
                if (!image.animated)
                    Object.Destroy(image.Texture);

            images.Clear();
            singleImage = null;
            BackgroundController.Initialize();
        }

        public static void Initialize()
        {
            if (!File.Exists(SettingsFilePath)) return;
            DoLoadingBackground = true;
            ReadLoadSettings();
            Initialized = true;
            if (DoLoadingBackground && images.Count > 0) BackgroundController.Initialize();
        }

        private static void ReadLoadSettings()
        {
            var lines = File.ReadAllLines(SettingsFilePath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].NullOrEmpty()) continue;
                var image = LoadImage(lines[i]);
                if (image is null or ({Texture: null} and {Video: null or ""})) continue;
                images.Add(image);
            }

            if (images.Count == 1) singleImage = images[0];
        }

        private static BackgroundImage LoadImage(string path)
        {
            var image = new BackgroundImage {defName = null};
            if (!File.Exists(path))
            {
                Log.Warning($"[VBE] Cannot find image at {path}. This is most likely due to mod list changes, mod updates, or changes in the custom folder.");
                return null;
            }

            if (BackgroundImageDef.IsAcceptableVideoExtension(Path.GetExtension(path)))
            {
                image.animated = true;
                image.Video = path;
            }
            else if (ModContentLoader<Texture2D>.IsAcceptableExtension(Path.GetExtension(path)))
                try
                {
                    image.Texture = new Texture2D(2, 2, TextureFormat.Alpha8, true);
                    image.Texture.LoadImage(File.ReadAllBytes(path));
                    if (Prefs.TextureCompression) image.Texture.Compress(true);
                    image.Texture.name = Path.GetFileNameWithoutExtension(path);
                    image.Texture.filterMode = FilterMode.Trilinear;
                    image.Texture.anisoLevel = 2;
                    image.Texture.Apply(true, true);
                }
                catch (Exception e)
                {
                    Log.Error($"[VBE] Error loading image from {path}: {e}");
                }

            return image;
        }

        public static void SaveForLoad(VBESettings settings)
        {
            string[] lines;
            if (settings.current.NullOrEmpty())
                lines = settings.Enabled.Where(def => !def.animated || settings.animatedOnLoad).Select(def => def.ResolvedPath()).ToArray();
            else if (DefDatabase<BackgroundImageDef>.GetNamedSilentFail(settings.current) is { } def && (!def.animated || settings.animatedOnLoad))
                lines = new[] {def.ResolvedPath()};
            else lines = new string[0];

            File.WriteAllLines(SettingsFilePath, lines);
        }

        public static string ResolvedPath(this BackgroundImageDef def)
        {
            if (def.animated) return def.Video;
            if (!def.resolvedPath.NullOrEmpty()) return def.resolvedPath;
            foreach (var content in LoadedModManager.RunningModsListForReading)
            {
                var contentPath = GenFilePaths.ContentPath<Texture2D>();
                foreach (var (text, file) in ModContentPack.GetAllFilesForMod(content, contentPath, ModContentLoader<Texture2D>.IsAcceptableExtension))
                {
                    var foundPath = text;
                    foundPath = foundPath.Replace('\\', '/');
                    if (foundPath.StartsWith(contentPath)) foundPath = foundPath.Substring(contentPath.Length);
                    if (foundPath.EndsWith(Path.GetExtension(foundPath)))
                        foundPath = foundPath.Substring(0, foundPath.Length - Path.GetExtension(foundPath).Length);
                    if (foundPath == def.path) return def.resolvedPath = file.FullName;
                }
            }

            Log.Warning("[VBE] Returning empty string from ResolvedPath()." +
                        $" This is because Vanilla Backgrounds Expanded cannot find the full path to the background image {def.LabelCap}." +
                        " The most common reason for this is that it is part of a theme." +
                        $" All this means is that {def.LabelCap} will not appear while loading the game.");
            return "";
        }
    }
}