using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace VBE
{
    public class VBEMod : Mod
    {
        public static Harmony Harm;
        public static VBESettings Settings;
        public static VBEMod Instance;

        public static string CustomBackgroundFolderPath;
        private bool allowAnimatedOnLoad;

        private Queue<BackgroundImageDef> animatedQueue;

        private bool initing;
        private float lastHeight;
        private Vector2 scrollPos;

        public VBEMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Harm = new Harmony("vanillaexpanded.backgrounds");
            Settings = GetSettings<VBESettings>();
            allowAnimatedOnLoad = Settings.allowAnimated;
            HarmonyPatches.DoPatches(Harm);
            ModCompat.ApplyCompat(Harm);
        }

        public static IEnumerable<BackgroundImageDef> AllDefsInOrder => from def in DefDatabase<BackgroundImageDef>.AllDefs
            where AllowAnimated || !def.animated
            orderby def.isCore descending, def.isVanilla descending, def.isUser descending, def.isTheme, def.modContentPack?.Name, def.label
            select def;

        public static bool AllowAnimated => Settings.allowAnimated && (GenScene.InEntryScene || GenScene.InPlayScene);

        public void Initialize()
        {
            CustomBackgroundFolderPath = Path.Combine(GenFilePaths.ConfigFolderPath, "VanillaBackgroundsExpanded", "Backgrounds");
            var customFolder = new DirectoryInfo(CustomBackgroundFolderPath);
            if (!customFolder.Exists) customFolder.Create();

            foreach (var def in DefGenerator_Backgrounds.BackgroundDefsFromExpansions().Concat(DefGenerator_Backgrounds.BackgroundDefsFromFolder(customFolder)))
                DefGenerator.AddImpliedDef(def);

            if (ModLister.HasActiveModWithName("RimThemes")) ModCompat.LoadRimThemesImages();

            Settings.CheckInit();

            InitializeAnimated();

            SettingsManager.Loading = false;
            SettingsManager.Clear();
            if (!SettingsManager.DoLoadingBackground) BackgroundController.Initialize();
        }

        public void InitializeAnimated()
        {
            if (!AllowAnimated) return;
            if (initing)
            {
                foreach (var def in DefDatabase<BackgroundImageDef>.AllDefs.Except(animatedQueue).Where(def => def.NeedsInit))
                    animatedQueue.Enqueue(def);
                return;
            }

            animatedQueue = new Queue<BackgroundImageDef>();
            foreach (var def in DefDatabase<BackgroundImageDef>.AllDefs.Where(def => def.NeedsInit)) animatedQueue.Enqueue(def);

            initing = true;
            InitNext();
        }

        private void InitNext()
        {
            if (!initing) return;
            var videoPlayer = Utils.MakeVideoPlayer();
            if (animatedQueue is null || animatedQueue.Count == 0)
            {
                initing = false;
                if (videoPlayer.enabled) videoPlayer.enabled = false;
                return;
            }

            var def = animatedQueue.Dequeue();
            videoPlayer.url = def.Video;
            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.frame = def.previewFrame;
            videoPlayer.frameReady += (source, idx) =>
            {
                AsyncGPUReadback.Request(source.texture, source.texture.mipmapCount - 1, req =>
                {
                    if (req.hasError) Log.Error($"[VBE] Failed to fetch image for {def.label}");
                    else
                        try
                        {
                            if (!req.done) req.WaitForCompletion();
                            var tex = new Texture2D(req.width, req.height, TextureFormat.RGBA32, false);
                            var data = req.GetData<uint>();
                            tex.LoadRawTextureData(data);
                            tex.Apply();
                            def.InitializeAnimated(tex);
                            Object.Destroy(videoPlayer);
                            InitNext();
                        }
                        catch (Exception e)
                        {
                            Log.Error($"[VBE] Exception while loading image for {def.label}: {e}");
                        }
                });
            };
            videoPlayer.prepareCompleted += source =>
            {
                videoPlayer.frame = def.previewFrame;
                source.Pause();
            };
            videoPlayer.Prepare();
        }

        public override string SettingsCategory() => "VBE".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            BackgroundController.PauseTransition = true;
            var leftRect = inRect.LeftPartPixels(200f);
            Widgets.DrawLineVertical(leftRect.xMax + 15f, leftRect.yMin, leftRect.height);
            inRect.xMin += 210f;
            Widgets.DrawMenuSection(inRect);
            inRect = inRect.ContractedBy(7f);
            var listing = new Listing_Standard();
            listing.Begin(leftRect);
            if (listing.ButtonText("VBE.OpenBackgrounds".Translate())) Application.OpenURL(CustomBackgroundFolderPath);
            listing.GapLine();
            listing.CheckboxLabeled("VBE.Randomize".Translate(), ref Settings.randomize, "VBE.Randomize.Desc".Translate());
            listing.CheckboxLabeled("VBE.Cycle".Translate(), ref Settings.cycle, "VBE.Cycle.Desc".Translate());
            if (Settings.cycle)
            {
                listing.Label("VBE.CycleTime".Translate());
                Widgets.FloatRange(listing.GetRect(28f), (int) listing.CurHeight, ref Settings.cycleTime, 0.1f, 60f);
            }

            listing.GapLine();

            listing.CheckboxLabeled("VBE.AllowAnimated".Translate(), ref Settings.allowAnimated, "VBE.AllowAnimated.Desc".Translate());
            if (AllowAnimated && !allowAnimatedOnLoad)
            {
                InitializeAnimated();
                allowAnimatedOnLoad = true;
            }

            listing.CheckboxLabeled("VBE.OnLoad".Translate(), ref Settings.loadingBackground, "VBE.OnLoad.Desc".Translate());
            listing.CheckboxLabeled("VBE.AnimatedLoad".Translate(), ref Settings.animatedOnLoad, "VBE.AnimatedLoad.Desc".Translate());

            if (ModCompat.RimThemes) listing.CheckboxLabeled("VBE.RimThemes".Translate(), ref Settings.rimThemesOverride, "VBE.RimThemes.Desc".Translate());
            // if (ModCompat.BetterLoading)
            // {
            //     listing.CheckboxLabeled("VBE.BetterLoading".Translate(), ref Settings.betterLoadingOverride, "VBE.BetterLoading.Desc".Translate());
            //     listing.CheckboxLabeled("VBE.BetterLoading.Move".Translate(), ref Settings.betterLoadingAlternative, "VBE.BetterLoading.Move.Desc".Translate());
            // }

            listing.GapLine();
            if (Settings.cycle || Settings.randomize)
            {
                listing.Label("VBE.ToggleBackgrounds".Translate());
                var rect = listing.GetRect(30f);
                if (Widgets.ButtonText(rect.LeftHalf(), "VBE.AllowAll".Translate()))
                    foreach (var def in AllDefsInOrder)
                        Settings.enabled[def.defName] = true;

                if (Widgets.ButtonText(rect.RightHalf(), "VBE.DisallowAll".Translate()))
                {
                    foreach (var def in AllDefsInOrder) Settings.enabled[def.defName] = false;

                    Settings.enabled[Settings.current.NullOrEmpty() ? BackgroundController.Default.defName : Settings.current] = true;
                }
            }
            else
                listing.Label("VBE.SelectBackground".Translate());

            listing.End();

            var width = (inRect.width - 37f) / 3f;
            var height = 0f;
            var viewRect = new Rect(0, 0, inRect.width - 17f, lastHeight);
            lastHeight = 5f;
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            var curPos = new Vector2(5f, 5f);
            foreach (var def in AllDefsInOrder)
            {
                var enabled = Settings.enabled[def.defName];
                if (curPos.x + width > viewRect.xMax)
                {
                    curPos.x = 5f;
                    curPos.y += height + 5f;
                    lastHeight += height + 5f;
                    height = 0f;
                }


                float myHeight;
                Rect rect;
                if (def.Texture is null)
                {
                    Log.Warning($"Null texture for {def.label}");
                    myHeight = 24f;
                    rect = new Rect(curPos, new Vector2(width, myHeight));
                }
                else
                {
                    myHeight = (float) def.Texture.height / def.Texture.width * width;
                    height = Mathf.Max(height, myHeight);
                    rect = new Rect(curPos, new Vector2(width, myHeight));
                    GUI.DrawTexture(rect, def.Texture, ScaleMode.ScaleToFit);
                }

                Widgets.DrawHighlightIfMouseover(rect);
                TooltipHandler.TipRegion(rect, () => $"{def.LabelCap}\n\n{def.description}", def.shortHash);
                if (Settings.cycle || Settings.randomize)
                {
                    if (Widgets.ButtonInvisible(rect)) enabled = !enabled;
                    Widgets.Checkbox(new Vector2(rect.xMax - 24f, rect.yMax - 24f), ref enabled);
                }
                else
                {
                    if (Widgets.ButtonInvisible(rect))
                    {
                        Settings.current = def.defName;
                        enabled = true;
                    }

                    if (Settings.current == def.defName) Widgets.DrawBox(rect, 3, Texture2D.whiteTexture);
                }

                curPos.x += width + 5f;
                Settings.enabled[def.defName] = enabled;
            }

            lastHeight += height;
            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (Settings.randomize || Settings.cycle) Settings.current = null;
            BackgroundController.PauseTransition = false;
            BackgroundController.Notify_SettingsChanged(Settings);
            if (Settings.loadingBackground) SettingsManager.SaveForLoad(Settings);
            else File.Delete(SettingsManager.SettingsFilePath);
        }
    }
}