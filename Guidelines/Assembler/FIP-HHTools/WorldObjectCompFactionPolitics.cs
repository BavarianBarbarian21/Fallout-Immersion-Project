using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public class WorldObjectCompProperties_FactionPolitics : WorldObjectCompProperties
{
    public WorldObjectCompProperties_FactionPolitics()
    {
        compClass = typeof(WorldObjectCompFactionPolitics);
    }
}

public class WorldObjectCompFactionPolitics : WorldObjectComp
{
    public override IEnumerable<Gizmo> GetGizmos()
    {
        Command_Action command = CreatePoliticsCommand();
        if (command != null)
        {
            yield return command;
        }
    }

    public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
    {
        if (caravan == null)
        {
            yield break;
        }

        Command_Action command = CreatePoliticsCommand();
        if (command != null)
        {
            yield return command;
        }
    }

    private Command_Action CreatePoliticsCommand()
    {
        Faction faction = parent?.Faction;
        if (faction == null || parent is not Settlement settlement)
        {
            return null;
        }

        HHToolsFactionPoliticsExtension extension = faction.GetPoliticsExtension();
        if (extension == null || (extension.system != HHToolsFactionPoliticalSystem.Civilized && extension.system != HHToolsFactionPoliticalSystem.Authoritarian))
        {
            return null;
        }

        Texture2D icon = null;
        if (!faction.def.factionIconPath.NullOrEmpty())
        {
            icon = ContentFinder<Texture2D>.Get(faction.def.factionIconPath, false);
        }

        return new Command_Action
        {
            defaultLabel = "Open politics",
            defaultDesc = "View the internal power structure of this faction.",
            icon = icon,
            action = () => Find.WindowStack.Add(new Window_HHToolsFactionPolitics(settlement))
        };
    }
}