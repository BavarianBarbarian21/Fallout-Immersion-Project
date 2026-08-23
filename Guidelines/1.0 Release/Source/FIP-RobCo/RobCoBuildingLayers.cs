using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.RobCo;

/// <summary>
/// Preserves the vanilla mech gestator draw stack and adds the RobCo light
/// layers that are supplied as separate textures. The lights are deliberately
/// real-time-only and only appear while the gestator has power.
/// </summary>
public class Building_RobCoMechGestator : Building_MechGestator
{
    private Graphic glowGraphic;
    private Graphic softGlowGraphic;

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);

        if (!PoweredOn)
        {
            return;
        }

        string glowPath = null;
        string softGlowPath = null;

        if (def.defName == "MechGestator")
        {
            glowPath = "FIP-RobCo/Buildings/MechGestator/RobCo_MechGestatorGlassGlow";
        }
        else if (def.defName == "LargeMechGestator" || def.defName == "RobCo_RestorationGantry")
        {
            glowPath = "FIP-RobCo/Buildings/LargeMechGestator/RobCo_LargeMechGestatorGlassGlow";
            softGlowPath = "FIP-RobCo/Buildings/LargeMechGestator/RobCo_LargeMechGestatorGlassGlowSoft";
        }

        if (softGlowPath != null)
        {
            softGlowGraphic ??= MakeGlowGraphic(softGlowPath);
            DrawGlowLayer(softGlowGraphic, drawLoc, 1f);
        }

        if (glowPath != null)
        {
            glowGraphic ??= MakeGlowGraphic(glowPath);
            DrawGlowLayer(glowGraphic, drawLoc, 2f);
        }
    }

    private Graphic MakeGlowGraphic(string texturePath)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            texturePath,
            ShaderDatabase.MoteGlow,
            def.graphicData.drawSize,
            Color.white);
    }

    private void DrawGlowLayer(Graphic graphic, Vector3 drawLoc, float subLayerOffset)
    {
        // Building_MechGestator places the glass at BuildingBelowTop and the
        // solid cap at BuildingOnTop. Keep both glow passes just above the
        // glass but below the cap, using RimWorld's sub-layer spacing so they
        // remain stable at every rotation and cannot z-fight.
        drawLoc.y = Altitudes.AltitudeFor(AltitudeLayer.BuildingBelowTop)
            + Altitudes.AltInc * subLayerOffset;
        graphic.Draw(drawLoc, Rotation, this, 0f);
    }
}

/// <summary>
/// Draws the RobCo scanner foreground after the contained pawn. RimWorld's
/// scanner class draws its occupant after the building but has no foreground
/// pass of its own; without this pass the pawn incorrectly appears on top of
/// the scanner armature.
/// </summary>
public class Building_RobCoSubcoreScanner : Building_SubcoreScanner
{
    private Graphic foregroundGraphic;

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        base.DynamicDrawPhaseAt(phase, drawLoc, flip);

        if (phase != DrawPhase.Draw || Occupant == null)
        {
            return;
        }

        string foregroundPath = null;
        if (def.defName == "SubcoreSoftscanner")
        {
            foregroundPath = "FIP-RobCo/Buildings/SubcoreSoftscanner/RobCo_SubcoreSoftscannerTop";
        }
        else if (def.defName == "SubcoreRipscanner")
        {
            foregroundPath = "FIP-RobCo/Buildings/SubcoreRipscanner/RobCo_SubcoreRipscannerTop";
        }

        if (foregroundPath == null)
        {
            return;
        }

        foregroundGraphic ??= GraphicDatabase.Get<Graphic_Multi>(
            foregroundPath,
            ShaderDatabase.Cutout,
            def.graphicData.drawSize,
            Color.white);

        // This is the same two-altitude-increment foreground spacing used by
        // vanilla Building_GrowthVat for its lid above a contained pawn.
        foregroundGraphic.Draw(drawLoc + Altitudes.AltIncVect * 2f, Rotation, this, 0f);
    }
}
