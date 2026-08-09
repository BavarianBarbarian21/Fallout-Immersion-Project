using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Anthrosonae
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }
    [StaticConstructorOnStartup]
    [HotSwappable]
    public class Window_ColorPicker : Window
    {
        private Color colorOne;

        private Color oldColorOne;

        private Color colorTwo;

        private Color oldColorTwo;

        private string hexColorOne, hexColorTwo;
        private float luminosityOne, luminosityTwo;
        private string luminosityBuf1, luminosityBuf2;

        private bool colorTwoChosen;

        private FurGene furGene;

        private bool hsvColorWheelDragging;

        private string[] textfieldBuffersOne = new string[6];
        private string[] textfieldBuffersTwo = new string[6];
        private Color textfieldColorBufferOne, textfieldColorBufferTwo;

        private string previousFocusedControlName;

        public static Widgets.ColorComponents visibleColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;

        public static Widgets.ColorComponents editableColorTextfields = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat;
        public override Vector2 InitialSize => new Vector2(800f, 410f);
        private List<FurColorDef> furColors;
        private Rot4 pawnRot = Rot4.South;
        public Window_ColorPicker(FurGene furGene)
        {
            this.doCloseX = true;
            colorOne = furGene.colorOne;
            oldColorOne = colorOne;
            colorTwo = furGene.colorTwo ?? Color.white;
            oldColorTwo = colorTwo;
            hexColorOne = "#" + ColorUtility.ToHtmlStringRGB(colorOne);
            hexColorTwo = "#" + ColorUtility.ToHtmlStringRGB(colorTwo);
            luminosityOne = Utils.CalculateBrightnessLevel(colorOne);
            luminosityTwo = Utils.CalculateBrightnessLevel(colorTwo);
            this.furGene = furGene;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnAccept = false;
            furColors = new List<FurColorDef>();
            var extension = furGene.def.GetModExtension<FurColors>();
            foreach (var furColorDef in extension.allowedFurColors.OrderBy(x => x.displayOrder))
            {
                furColors.Add(furColorDef);
            }
        }

        private static void HeaderRow(ref RectDivider layout)
        {
            using (new TextBlock(GameFont.Medium))
            {
                TaggedString taggedString = "ColorPicker.ChangeFurColors".Translate().CapitalizeFirst();
                RectDivider rectDivider = layout.NewRow(Text.CalcHeight(taggedString, layout.Rect.width));
                GUI.SetNextControlName(Dialog_GlowerColorPicker.focusableControlNames[0]);
                Widgets.Label(rectDivider, taggedString);
            }
        }

        private void BottomButtons(ref RectDivider layout)
        {
            RectDivider rectDivider = layout.NewRow(Dialog_GlowerColorPicker.ButSize.y, VerticalJustification.Bottom);
            if (Widgets.ButtonText(rectDivider.NewCol(Dialog_GlowerColorPicker.ButSize.x), "Cancel".Translate()))
            {
                Close();
            }
            if (Widgets.ButtonText(rectDivider.NewCol(Dialog_GlowerColorPicker.ButSize.x, HorizontalJustification.Right), "Accept".Translate()))
            {
                if (colorOne != oldColorOne)
                {
                    furGene.colorOne = Utils.SetBrightness(colorOne, (int)luminosityOne);
                }
                if (colorTwo != oldColorTwo)
                {
                    furGene.colorTwo = Utils.SetBrightness(colorTwo, (int)luminosityTwo);
                }
                Close();
            }
        }

        public override void Close(bool doCloseSound = true)
        {
            base.Close(doCloseSound);
            furGene.ApplyColors();
        }

        private void ColorFields(ref RectDivider layout, ref Color color, string hexValue, ref float lumValue,
            ref string lumBuf, ref string[] textfieldBuffers, ref Color textfieldColorBuffer, out Vector2 size)
        {
            RectAggregator aggregator = new RectAggregator(new Rect(layout.Rect.position, new Vector2(125f, 0f)), 195906069);
            bool num = Widgets.ColorTextfields(ref aggregator, ref color, ref textfieldBuffers, ref textfieldColorBuffer, previousFocusedControlName, "colorTextfields", editableColorTextfields, visibleColorTextfields);
            size = aggregator.Rect.size;
            if (num)
            {
                Color.RGBToHSV(color, out var H, out var S, out var _);
                color = Color.HSVToRGB(H, S, 1f);
                hexValue = ResetHexValues(color);
            }
            var lumRectLabel = new Rect(layout.Rect.x, aggregator.Rect.yMax + 4, 50, 32);
            using (new TextBlock(TextAnchor.MiddleLeft))
                Widgets.Label(lumRectLabel, "ColorPicker.Lum".Translate());
            var lumRect = new Rect(lumRectLabel.xMax, lumRectLabel.y, 125 - 50, 32);
            var oldLum = lumValue;
            try
            {
                Widgets.TextFieldNumeric(lumRect, ref lumValue, ref lumBuf, min: 1f, max: 99f);
            }
            catch (Exception ex)
            {
                lumValue = 1f;
                lumBuf = lumValue.ToString();
            }
            if (lumBuf.NullOrEmpty() is false && oldLum != lumValue)
            {
                hexValue = ResetHexValues(color);
            }
            if (Event.current.type == EventType.Layout)
            {
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
            }
            var hexRectLabel = new Rect(lumRectLabel.x, lumRectLabel.yMax + 4, 50, 32);
            using (new TextBlock(TextAnchor.MiddleLeft))
                Widgets.Label(hexRectLabel, "ColorPicker.HexCode".Translate());
            var hexRect = new Rect(hexRectLabel.xMax, hexRectLabel.y, 125 - 50, 32);
            var oldValue = hexValue;
            hexValue = Widgets.TextField(hexRect, hexValue).Trim();
            if (Utils.TryGetColorFromHex(hexValue, out var tempColor))
            {
                color = tempColor;
                if (hexValue != oldValue)
                {
                    ResetColorValues(color);
                }
            }
            if (Event.current.type == EventType.Layout)
            {
                previousFocusedControlName = GUI.GetNameOfFocusedControl();
            }
        }

        private static void ColorReadback(Rect rect, Color color, Color oldColor)
        {
            rect.SplitVertically((rect.width - 26f) / 2f, out var left, out var right);
            RectDivider rectDivider = new RectDivider(left, 195906069);
            TaggedString label = "CurrentColor".Translate().CapitalizeFirst();
            TaggedString label2 = "OldColor".Translate().CapitalizeFirst();
            float width = Mathf.Max(100f, label.GetWidthCached(), label2.GetWidthCached());
            RectDivider rectDivider2 = rectDivider.NewRow(Text.LineHeight);
            Widgets.Label(rectDivider2.NewCol(width), label);
            Widgets.DrawBoxSolid(rectDivider2, color);
            RectDivider rectDivider3 = rectDivider.NewRow(Text.LineHeight);
            Widgets.Label(rectDivider3.NewCol(width), label2);
            Widgets.DrawBoxSolid(rectDivider3, oldColor);
            RectDivider rectDivider4 = new RectDivider(right, 195906069);
            rectDivider4.NewCol(26f);
        }

        private static void TabControl()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
            {
                bool num = !Event.current.shift;
                Event.current.Use();
                string text = GUI.GetNameOfFocusedControl();
                if (text.NullOrEmpty())
                {
                    text = Dialog_GlowerColorPicker.focusableControlNames[0];
                }
                int num2 = Dialog_GlowerColorPicker.focusableControlNames.IndexOf(text);
                if (num2 < 0)
                {
                    num2 = Dialog_GlowerColorPicker.focusableControlNames.Count;
                }
                num2 = ((!num) ? (num2 - 1) : (num2 + 1));
                if (num2 >= Dialog_GlowerColorPicker.focusableControlNames.Count)
                {
                    num2 = 0;
                }
                else if (num2 < 0)
                {
                    num2 = Dialog_GlowerColorPicker.focusableControlNames.Count - 1;
                }
                GUI.FocusControl(Dialog_GlowerColorPicker.focusableControlNames[num2]);
            }
        }
        private static readonly Vector3 PortraitOffset = new Vector3(0f, 0f, 0.15f);
        private static readonly Texture2D RotateButton = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");

        public override void DoWindowContents(Rect inRect)
        {
            using (TextBlock.Default())
            {
                var portrait = new Rect(inRect.x, inRect.y, 190, 240);
                Widgets.DrawMenuSection(portrait);
                var oldColors = (furGene.colorOne, furGene.colorTwo);
                furGene.colorOne = Utils.SetBrightness(colorOne, (int)luminosityOne);
                furGene.colorTwo = Utils.SetBrightness(colorTwo, (int)luminosityTwo);
                furGene.ApplyColors();
                RenderTexture image = PortraitsCache.Get(furGene.pawn, new Vector2(200, 200), pawnRot,
                    new Vector3(0, 0, 0.1f), healthStateOverride: PawnHealthState.Mobile, cameraZoom: 1.1f, renderClothes: false, renderHeadgear: false);
                furGene.colorOne = oldColors.colorOne;
                furGene.colorTwo = oldColors.colorTwo;
                furGene.ApplyColors();
                GUI.DrawTexture(portrait, image, ScaleMode.ScaleAndCrop);
                var buttonRotate = new Rect(portrait.xMax - 24, portrait.y, 24, 24);
                if (Widgets.ButtonImage(buttonRotate, RotateButton))
                {
                    pawnRot = pawnRot.Rotated(RotationDirection.Clockwise);
                }
                var layoutRect = new Rect(inRect.x + 200, inRect.y, inRect.width - 200, 240);
                RectDivider layout = new RectDivider(layoutRect, 195906069);
                HeaderRow(ref layout);
                layout.NewRow(0f);
                var color = colorTwoChosen is false ? colorOne : colorTwo;
                var oldColor = color;
                ColorPalette(ref layout, ref color, out var paletteHeight);
                if (oldColor != color)
                {
                    ResetColorValues(color);
                }
                Vector2 size;
                if (colorTwoChosen is false)
                {
                    ColorFields(ref layout, ref color, hexColorOne, ref luminosityOne, ref luminosityBuf1, 
                        ref textfieldBuffersOne, ref textfieldColorBufferOne, out size);
                }
                else
                {
                    ColorFields(ref layout, ref color, hexColorTwo, ref luminosityTwo, ref luminosityBuf2, 
                        ref textfieldBuffersTwo, ref textfieldColorBufferTwo, out size);
                }

                float height = Mathf.Max(paletteHeight, 128f, size.y);
                RectDivider rectDivider = layout.NewRow(height);
                rectDivider.NewCol(size.x);
                rectDivider.NewCol(250f, HorizontalJustification.Right);
                oldColor = color;
                Widgets.HSVColorWheel(rectDivider.Rect.ContractedBy((rectDivider.Rect.width - 128f) / 2f, 
                    (rectDivider.Rect.height - 128f) / 2f), ref color, ref hsvColorWheelDragging, 1f);
                if (oldColor != color)
                {
                    ResetColorValues(color);
                }
                layout = new RectDivider(new Rect(inRect.x, portrait.yMax + 24 + 15, inRect.width,
                    inRect.height - portrait.height - (24 + 15)), 65436135);
                BottomButtons(ref layout);
                layout.NewRow(0f, VerticalJustification.Bottom);
                if (colorTwoChosen is false)
                {
                    ColorReadback(layout, Utils.SetBrightness(colorOne, (int)luminosityOne), oldColorOne);
                }
                else
                {
                    ColorReadback(layout, Utils.SetBrightness(colorTwo, (int)luminosityTwo), oldColorTwo);
                }

                if (colorTwoChosen is false)
                {
                    colorOne = color;
                }
                else
                {
                    colorTwo = color;
                }

                var buttonsRect = new Rect(inRect.x, portrait.yMax + 10, 117, 24);
                Widgets.Label(buttonsRect, "ColorPicker.ColorChannel".Translate());
                buttonsRect = new Rect(buttonsRect.xMax, buttonsRect.y, 50, buttonsRect.width);
                var colorChannel = colorTwoChosen is false ? "ColorPicker.ColorA".Translate() : "ColorPicker.ColorB".Translate();
                Widgets.Label(buttonsRect, colorChannel);
                if (Widgets.RadioButton(new Vector2(buttonsRect.xMax, buttonsRect.y), colorTwoChosen == false))
                {
                    colorTwoChosen = false;
                }
                if (Widgets.RadioButton(new Vector2(buttonsRect.xMax + 40, buttonsRect.y), colorTwoChosen == true))
                {
                    colorTwoChosen = true;
                }

                TabControl();
                if (Event.current.type == EventType.Layout)
                {
                    previousFocusedControlName = GUI.GetNameOfFocusedControl();
                }
            }
        }

        private void ResetColorValues(Color color)
        {
            if (colorTwoChosen is false)
            {
                luminosityOne = Utils.CalculateBrightnessLevel(color);
                luminosityBuf1 = luminosityOne.ToString();
                hexColorOne = "#" + ColorUtility.ToHtmlStringRGB(Utils.SetBrightness(color, (int)luminosityOne));
            }
            else
            {
                luminosityTwo = Utils.CalculateBrightnessLevel(color);
                luminosityBuf2 = luminosityTwo.ToString();
                hexColorTwo = "#" + ColorUtility.ToHtmlStringRGB(Utils.SetBrightness(color, (int)luminosityTwo));
            }
        }

        private string ResetHexValues(Color color)
        {
            if (colorTwoChosen is false)
            {
                hexColorOne = "#" + ColorUtility.ToHtmlStringRGB(Utils.SetBrightness(color, (int)luminosityOne));
                return hexColorOne;
            }
            else
            {
                hexColorTwo = "#" + ColorUtility.ToHtmlStringRGB(Utils.SetBrightness(color, (int)luminosityTwo));
                return hexColorOne;
            }
        }

        private void ColorPalette(ref RectDivider layout, ref Color color, out float paletteHeight)
        {
            using (new TextBlock(TextAnchor.MiddleLeft))
            {
                RectDivider rectDivider = layout;
                RectDivider rectDivider2 = rectDivider.NewCol(250f, HorizontalJustification.Right);
                var colors = (colorTwoChosen is false ? furColors.Where(x => x.blacklistPrimary is false).Select(x => x.primaryColor).Distinct().ToList() :
                    furColors.Where(x => x.blacklistSecondary is false && x.secondaryColor.HasValue).Select(x => x.secondaryColor.Value).Distinct()).ToList();
                colors.SortByColor(x => x);
                Widgets.ColorSelector(rectDivider2, ref color, colors.ToList(), out paletteHeight, 
                    extraOnGUI: ColorSelecterExtraOnGUI);
            }
        }

        private void ColorSelecterExtraOnGUI(Color color, Rect boxRect)
        {
            var firstDef = (colorTwoChosen is false ? furColors.FirstOrDefault(x => x.blacklistPrimary is false && x.primaryColor == color) :
                    furColors.FirstOrDefault(x => x.blacklistSecondary is false && x.secondaryColor.HasValue && x.secondaryColor.Value == color));
            if (firstDef != null)
            {
                TooltipHandler.TipRegion(boxRect, firstDef.LabelCap);
            }
        }
    }
}
