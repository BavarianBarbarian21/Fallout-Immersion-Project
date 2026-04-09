using System;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using UnityEngine.Video;
using Verse;
using Object = UnityEngine.Object;

namespace VBE;

public static class BackgroundController
{
    private static BackgroundImage current;
    public static BackgroundImage Default;
    private static float transitionTime = float.MaxValue;
    private static float transitionPct = -1f;
    private static BackgroundImage transitionTo;
    private static bool initialized;
    public static bool PauseTransition;
    private static VideoPlayer videoPlayer;
    private static VideoPlayer videoPlayerTransition;

    private static int index;

    public static BackgroundImage Current
    {
        get => current ?? Default;
        set
        {
            // Log.Message($"[VBE] Changing background from {current?.ToString() ?? "nothing"} to {value?.ToString() ?? "nothing"}");
            if (value is null) return;
            if (value.animated)
            {
                if (!VBEMod.AllowAnimated)
                {
                    Log.Error("[VBE] Tried to load animated background while disallowed");
                    return;
                }

                if (value.Video.NullOrEmpty())
                {
                    Log.Error("[VBE] Tried to switch to invalid Background");
                    return;
                }
            }

            if (current is not null && current.animated && !value.animated)
            {
                if (VBEMod.AllowAnimated)
                {
                    videoPlayer.Stop();
                    videoPlayer.enabled = false;
                }
                else
                {
                    Object.Destroy(videoPlayer);
                    videoPlayer = null;
                }
            }

            var idx = SettingsManager.Index(value);
            if (idx >= 0) index = idx;


            current = value;
            if (current.animated)
                try
                {
                    videoPlayer ??= Utils.MakeVideoPlayer();
                    if (videoPlayer.url == current.Video && videoPlayer.isPrepared)
                        videoPlayer.Play();
                    else
                    {
                        videoPlayer.enabled = true;
                        videoPlayer.sendFrameReadyEvents = false;
                        videoPlayer.frame = 0;
                        videoPlayer.url = Current.Video;
                        videoPlayer.time = 0f;
                        videoPlayer.Prepare();
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"[VBE] Error while initializing video player for {current} with path {current.Video}: {e}");
                }
            else
                BackgroundMain.overrideBGImage = current.Texture;
        }
    }

    private static UI_BackgroundMain BackgroundMain => (UI_BackgroundMain)UIMenuBackgroundManager.background;

    public static string StateString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"  Current: {current} (def: {current.Def})");
        builder.AppendLine($"  Default: {Default} (def: {Default.Def})");
        builder.AppendLine($"  videoPlayer: {videoPlayer}");
        builder.AppendLine($"  overrideBGImage: {BackgroundMain.overrideBGImage}");
        builder.AppendLine("  Transition Data:");
        builder.AppendLine($"    transitionTo: {transitionTo} (def: {transitionTo.Def})");
        builder.AppendLine($"    transitionPct: {transitionPct}");
        builder.AppendLine($"    transitionTime: {transitionTime}");
        builder.AppendLine($"    videoPlayerTransition: {videoPlayerTransition}");
        return builder.ToString();
    }

    public static void Notify_SceneChanged()
    {
        if (!initialized) return;
        if (videoPlayer is not null)
        {
            Object.Destroy(videoPlayer);
            videoPlayer = null;
        }

        CancelTransition();

        if (Current.animated)
            Current = SettingsManager.Allowed(current) ? current : GetNext();
        else Current = GetNext();
    }

    private static void CancelTransition()
    {
        transitionPct = -1f;
        if (transitionTo is not null && SettingsManager.Allowed(transitionTo)) Current = transitionTo;
        transitionTo = null;
        transitionTime = float.MaxValue;
        if (videoPlayerTransition is not null)
        {
            Object.Destroy(videoPlayerTransition);
            videoPlayerTransition = null;
        }
    }

    public static void DoOverlay()
    {
        if (!SettingsManager.Initialized) SettingsManager.Initialize();
        if (!initialized) return;
        if (Current is null) return;
        if (Current.animated && videoPlayer is not null)
            if (videoPlayer.isPlaying) Utils.DrawBG(videoPlayer.texture);
            else if (videoPlayer.isPrepared) videoPlayer.Play();

        if (!VBEMod.Settings.cycle) return;
        if (Utils.expansionImageFades(BackgroundMain) is { } expansionImageFades && ModLister.AllExpansions.Any(key => expansionImageFades[key] > 0f)) return;
        DrawTransition();
    }

    private static void DrawTransition()
    {
        if (Time.time >= transitionTime)
        {
            transitionTo = GetNext();
            transitionTime = float.MaxValue;
            if (transitionTo.animated)
            {
                videoPlayerTransition ??= Utils.MakeVideoPlayer();
                videoPlayerTransition.frame = 0;
                videoPlayerTransition.url = transitionTo.Video;
                videoPlayerTransition.prepareCompleted += source =>
                {
                    transitionPct = 0f;
                    source.Play();
                };
                videoPlayerTransition.Prepare();
            }
            else transitionPct = 0f;
        }

        if (transitionPct >= 0f && transitionTo is not null)
        {
            transitionPct += Time.deltaTime;
            if (transitionTo != Current && (!transitionTo.animated || videoPlayerTransition is { isPrepared: true }))
            {
                GUI.color = new Color(1f, 1f, 1f, transitionPct);
                Utils.DrawBG(transitionTo.animated ? videoPlayerTransition.texture : transitionTo.Texture);
                GUI.color = Color.white;
            }

            if (transitionPct >= 1f && !PauseTransition)
            {
                if (transitionTo.animated)
                {
                    Object.Destroy(videoPlayer);
                    videoPlayer = videoPlayerTransition;
                    videoPlayerTransition = null;
                    Current = transitionTo;
                }
                else Current = transitionTo;

                transitionPct = -1f;
                transitionTo = null;
                transitionTime = Time.time + VBEMod.Settings.cycleTime.RandomInRange;
            }
        }
    }

    public static void Notify_SettingsChanged(VBESettings settings)
    {
        if (!initialized) return;
        if (settings.cycle)
        {
            if (SettingsManager.Allowed(Current))
            {
                CancelTransition();
                transitionTime = Time.time + settings.cycleTime.RandomInRange;
            }
            else
            {
                transitionTo = GetNext();
                transitionTime = float.MaxValue;
                transitionPct = 0f;
            }
        }
        else
            CancelTransition();

        if (!SettingsManager.Allowed(Current)) Current = GetNext();
    }

    private static BackgroundImage GetNext()
    {
        if (SettingsManager.UseSingle)
        {
            var image = SettingsManager.Single;
            return SettingsManager.Allowed(image) ? image : Default;
        }

        if (VBEMod.Settings.randomize)
            return SettingsManager.Images.TryRandomElement(out var random) ? random : Default;
        var list = SettingsManager.Images.ToList();
        return list.Count == 0 ? Default : list[Utils.Wrap(index + 1, 0, list.Count - 1)];
    }

    public static void Clear()
    {
        transitionPct = -1f;
        transitionTo = null;
        transitionTime = float.MaxValue;
        if (videoPlayerTransition is not null)
        {
            Object.Destroy(videoPlayerTransition);
            videoPlayerTransition = null;
        }

        if (videoPlayer is not null)
        {
            Object.Destroy(videoPlayer);
            videoPlayer = null;
        }

        Current = Default;
    }

    public static void Initialize()
    {
        var settings = VBEMod.Settings;
        if (!SettingsManager.Loading) settings.CheckInit();
        Current = GetNext();

        if (settings.cycle) transitionTime = Time.time + settings.cycleTime.RandomInRange;
        initialized = true;
    }

    public static void DrawBackground()
    {
        if (!SettingsManager.Initialized) SettingsManager.Initialize();
        if (!initialized) return;
        if (Current is null) return;
        Utils.DrawBG(Current.animated ? videoPlayer.texture : Current.Texture);
        if (!VBEMod.Settings.cycle) return;
        DrawTransition();
    }
}

public class BackgroundImage
{
    public bool animated;
    public string defName;
    public Texture2D Texture;
    public string Video;

    public BackgroundImageDef Def => DefDatabase<BackgroundImageDef>.GetNamedSilentFail(defName);
    public override string ToString() => (defName.NullOrEmpty() ? "" : defName + " ") + (animated ? Video : Texture?.name ?? "null texture");

    public static implicit operator BackgroundImage(BackgroundImageDef def) => def == null
        ? null
        : new BackgroundImage
        {
            defName = def.defName,
            animated = def.animated,
            Texture = def.animated ? null : def.Texture,
            Video = def.animated ? def.Video : null
        };
}