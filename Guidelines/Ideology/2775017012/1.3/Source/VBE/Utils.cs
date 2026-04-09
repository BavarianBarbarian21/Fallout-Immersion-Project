using UnityEngine;
using UnityEngine.Video;
using Verse;

namespace VBE
{
    public static class Utils
    {
        public static VideoPlayer MakeVideoPlayer()
        {
            VideoPlayer videoPlayer;
            if (GenScene.InEntryScene)
                videoPlayer = Current.Root_Entry.gameObject.AddComponent<VideoPlayer>();
            else if (GenScene.InPlayScene)
                videoPlayer = Current.Root_Play.gameObject.AddComponent<VideoPlayer>();
            else return null;
            videoPlayer.enabled = true;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.targetCameraAlpha = 1f;
            videoPlayer.playOnAwake = true;
            videoPlayer.errorReceived += delegate(VideoPlayer source, string message) { Log.Error($"[VBE] Error in video player {source}: " + message + " "); };
            return videoPlayer;
        }

        public static void DrawBG(Texture texture)
        {
            GUI.DrawTexture(GetBGRect(texture), texture, ScaleMode.ScaleToFit);
        }

        public static Rect GetBGRect(Texture texture)
        {
            var vector = new Vector2(texture.width, texture.height);
            Rect rect;
            var screenWidth = UI.screenWidth;
            var screenHeight = UI.screenHeight;
            if (SettingsManager.Loading && SettingsManager.DoLoadingBackground && VBEMod.Settings.betterLoadingOverride && ModCompat.BetterLoading)
            {
                screenWidth = Screen.width;
                screenHeight = Screen.height;
            }

            if (screenWidth > screenHeight * (vector.x / vector.y))
            {
                float width = screenWidth;
                var num2 = screenWidth * (vector.y / vector.x);
                rect = new Rect(0f, (screenHeight - num2) * 0.5f, width, num2);
            }
            else
            {
                float height = screenHeight;
                var num = screenHeight * (vector.x / vector.y);
                rect = new Rect((UI.screenWidth - num) * 0.5f, 0f, num, height);
            }

            return rect;
        }

        public static int Wrap(int value, int min, int max)
        {
            if (min == max) return min;
            if (value < min) value = max - (min - value);
            if (value > max) value = min + (value - max);
            return value;
        }

        [DebugAction("General", "[VBE] Log Controller State", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
        public static void LogState()
        {
            Log.Message($"[VBE] Current Controller State:\n{BackgroundController.StateString()}");
        }
    }
}