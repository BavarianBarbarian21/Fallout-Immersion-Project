using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace VBE
{
    public class BackgroundImageDef : Def
    {
        public const string VideoPath = "Videos/";

        private static readonly HashSet<string> AcceptableExtensionsVideo = new()
        {
            ".webm"
        };

        public bool animated;
        private Texture2D icon;
        public string iconPath;
        private bool initialized;
        public bool isCore;
        public bool isTheme;
        public bool isUser;
        public bool isVanilla;
        public string path;
        public int previewFrame;
        public string resolvedPath;
        private Texture2D texture;
        private string video;

        public BackgroundImageDef()
        {
        }

        public BackgroundImageDef(Texture2D tex, Texture2D icn = null)
        {
            texture = tex;
            icon = icn;
        }

        public BackgroundImageDef(string videoPath, Texture2D icn = null)
        {
            video = videoPath;
            icon = icn;
        }

        public bool NeedsInit => animated && !initialized;

        public string Video => video ??= FindPath();

        public Texture2D Texture => texture ??= ContentFinder<Texture2D>.Get(path ?? "", !animated);
        public Texture2D Icon => iconPath.NullOrEmpty() ? BaseContent.ClearTex : icon ??= ContentFinder<Texture2D>.Get(iconPath, iconPath is null);

        public static bool IsAcceptableVideoExtension(string extension) => AcceptableExtensionsVideo.Contains(extension);

        public void InitializeAnimated(Texture2D tex)
        {
            texture = tex;
            initialized = true;
        }


        private string FindPath()
        {
            foreach (var content in LoadedModManager.RunningModsListForReading)
            foreach (var (text, file) in ModContentPack.GetAllFilesForMod(content, VideoPath, IsAcceptableVideoExtension))
            {
                var foundPath = text;
                foundPath = foundPath.Replace('\\', '/');
                if (foundPath.StartsWith(VideoPath)) foundPath = foundPath.Substring(VideoPath.Length);
                if (foundPath.EndsWith(Path.GetExtension(foundPath))) foundPath = foundPath.Substring(0, foundPath.Length - Path.GetExtension(foundPath).Length);
                if (foundPath == path) return file.FullName;
            }

            Log.Error($"Could not load video at {path} in any active mod or in base resources.");
            return "";
        }
    }
}