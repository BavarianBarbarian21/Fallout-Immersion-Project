using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Anthrosonae
{
    [HotSwappable]
    public class FurGene : Gene
    {
        public FurColorDef furColor;
        public Color colorOne;
        public Color? colorTwo;

        public override void PostAdd()
        {
            base.PostAdd();
            SetFurColor();
        }
        
        private void SetFurColor()
        {
            var extension = def.GetModExtension<FurColors>();
            if (extension.allowedFurColors.TryRandomElementByWeight(x => x.selectionWeight, out var result))
            {
                SetFurColor(result);
            }
        }
        
        private void SetFurColor(FurColorDef colorDef)
        {
            furColor = colorDef;
            colorOne = furColor.primaryColor;
            colorTwo = furColor.secondaryColor;
            ApplyColors();
        }
        
        public Graphic GetGraphicOverriden(Graphic original, PawnRenderNode source)
        {
            if (furColor is null)
            {
                SetFurColor();
            }
            var color1 = GetColorOne(source);
            var color2 = GetColorTwo(source);
            if (colorTwo != null && color1.IndistinguishableFrom(color2) is false)
            {
                return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path,
                ShaderTypeDefOf.CutoutComplex.Shader, Vector2.one, color1, color2);
            }
            if (color1.IndistinguishableFrom(color2))
            {
                var shader = source.ShaderFor(pawn);
                if (shader == ShaderTypeDefOf.CutoutComplex.Shader)
                {
                    shader = ShaderTypeDefOf.Cutout.Shader;
                }
                return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path, shader, Vector2.one, color1, color2);
            }
            else
            {
                return (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(original.path, source.ShaderFor(pawn), Vector2.one, color1);
            }
        }

        public Shader GetShaderOverriden(Shader original, PawnRenderNode source)
        {
            if (furColor is null)
            {
                SetFurColor();
            }
            var color1 = GetColorOne(source);
            var color2 = GetColorTwo(source);
            if (colorTwo != null)
            {
                if (color1.IndistinguishableFrom(color2))
                {
                    if (original == ShaderTypeDefOf.CutoutComplex.Shader)
                    {
                        return ShaderTypeDefOf.Cutout.Shader;
                    }
                }
                else
                {
                    return ShaderTypeDefOf.CutoutComplex.Shader;
                }

            }
            if (color1.IndistinguishableFrom(color2))
            {
                if (original == ShaderTypeDefOf.CutoutComplex.Shader)
                {
                    original = ShaderTypeDefOf.Cutout.Shader;
                }
            }
            return original;
        }

        public Color GetColorOne(PawnRenderNode source)
        {
            if (ModsConfig.AnomalyActive)
            {
                if (pawn.IsShambler)
                {
                    return MutantUtility.GetShamblerColor(colorOne);
                }
                if (pawn.IsMutant && pawn.mutant.Def.useCorpseGraphics && pawn.mutant.rotStage == RotStage.Rotting)
                {
                    return PawnRenderUtility.GetRottenColor(colorOne);
                }
                if (pawn.IsMutant && pawn.mutant.def.skinColorOverride.HasValue)
                {
                    return PostProcess(source, pawn.mutant.def.skinColorOverride.Value);
                }
            }
            return PostProcess(source, colorOne);
        }

        public Color GetColorTwo(PawnRenderNode source)
        {
            if (ModsConfig.AnomalyActive && colorTwo.HasValue)
            {
                if (pawn.IsShambler)
                {
                    return MutantUtility.GetShamblerColor(colorTwo.Value);
                }
                if (pawn.IsMutant && pawn.mutant.Def.useCorpseGraphics && pawn.mutant.rotStage == RotStage.Rotting)
                {
                    return PawnRenderUtility.GetRottenColor(colorTwo.Value);
                }
            }

            if (pawn.IsMutant && pawn.mutant.def.skinColorOverride.HasValue)
            {
                return PostProcess(source, pawn.mutant.def.skinColorOverride.Value);
            }
            if (colorTwo is null)
            {
                return Color.white;
            }
            var color = colorTwo.Value;
            color = PostProcess(source, color);
            return color;
        }

        private Color PostProcess(PawnRenderNode source, Color color)
        {
            color *= source.props.colorRGBPostFactor;
            if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting)
            {
                color = PawnRenderUtility.GetRottenColor(color);
            }
            return color;
        }

        public void ApplyColors()
        {
            if (pawn != null)
            {
                pawn.story.skinColorOverride = colorOne;
                pawn.drawer.renderer.SetAllGraphicsDirty();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Change fur",
                    action = () =>
                    {
                        Find.WindowStack.Add(new Window_ColorPicker(this));
                    }
                };
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref furColor, "furColor");
            Scribe_Values.Look(ref colorOne, "colorOne");
            Scribe_Values.Look(ref colorTwo, "colorTwo");
        }
    }
}
