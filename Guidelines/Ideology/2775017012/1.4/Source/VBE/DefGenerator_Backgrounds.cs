using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace VBE
{
    public static class DefGenerator_Backgrounds
    {
        public static IEnumerable<BackgroundImageDef> BackgroundDefsFromExpansions()
        {
            foreach (var def in ModLister.AllExpansions)
            {
                var bgDef = new BackgroundImageDef
                {
                    label = def.label,
                    description = def.description,
                    defName = def.defName,
                    path = def.backgroundPath,
                    iconPath = def.iconPath,
                    isCore = def.isCore,
                    isVanilla = true
                };
                if (def.isCore) BackgroundController.Default = bgDef;

                yield return bgDef;
            }
        }

        public static IEnumerable<BackgroundImageDef> BackgroundDefsFromFolder(DirectoryInfo customFolder)
        {
            foreach (var file in customFolder.EnumerateFiles())
                if (ModContentLoader<Texture2D>.IsAcceptableExtension(file.Extension))
                {
                    var texture2D = new Texture2D(2, 2, TextureFormat.Alpha8, true);
                    texture2D.LoadImage(File.ReadAllBytes(file.FullName));
                    if (Prefs.TextureCompression) texture2D.Compress(true);
                    texture2D.name = Path.GetFileNameWithoutExtension(file.Name);
                    texture2D.filterMode = FilterMode.Trilinear;
                    texture2D.anisoLevel = 2;
                    texture2D.Apply(true, true);
                    var bgDef = new BackgroundImageDef(texture2D) {resolvedPath = file.FullName};
                    PopulateDefFromFile(bgDef, file);
                    yield return bgDef;
                }
                else if (BackgroundImageDef.IsAcceptableVideoExtension(file.Extension))
                {
                    var bgDef = new BackgroundImageDef(file.FullName);
                    PopulateDefFromFile(bgDef, file);
                    yield return bgDef;
                }
        }

        private static void PopulateDefFromFile(BackgroundImageDef def, FileSystemInfo file)
        {
            def.label = Path.GetFileNameWithoutExtension(file.Name).Replace('_', ' ').Replace('-', ' ');
            def.description = "User provided background found in:\n" + VBEMod.CustomBackgroundFolderPath;
            def.defName = "CustomByUser_" + Path.GetFileNameWithoutExtension(file.Name).Replace(" ", "").Replace("-", "").Replace("_", "");
            def.isUser = true;
        }
    }
}