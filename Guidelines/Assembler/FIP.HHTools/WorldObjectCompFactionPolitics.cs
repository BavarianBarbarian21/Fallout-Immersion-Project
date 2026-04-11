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
    public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
    {
        Faction faction = parent?.Faction;
        if (caravan == null || faction == null)
        {
            yield break;
        }

        HHToolsFactionPoliticsExtension extension = faction.GetPoliticsExtension();
        if (extension == null || (extension.system != HHToolsFactionPoliticalSystem.Civilized && extension.system != HHToolsFactionPoliticalSystem.Authoritarian))
        {
            yield break;
        }

        Texture2D icon = null;
        if (!faction.def.factionIconPath.NullOrEmpty())
        {
            icon = ContentFinder<Texture2D>.Get(faction.def.factionIconPath, false);
        }

        yield return new Command_Action
        {
            defaultLabel = "Open politics",
            defaultDesc = "View the internal power structure of this faction.",
            icon = icon,
            action = () => Find.WindowStack.Add(new Window_HHToolsFactionPolitics((Settlement)parent))
        };
    }
}