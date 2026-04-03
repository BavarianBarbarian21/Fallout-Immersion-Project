using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FIP_RobCo
{
    public class Projectile_FatManPolluting : Projectile_DoomsdayRocket
    {
        public float pollutionCells = 0.5f; // percentage of explosion area to pollute (0.0 - 1.0)

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 center = Position;

            base.Impact(hitThing, blockedByShield);

            if (map == null || !ModsConfig.BiotechActive) return;

            // Randomly pollute cells in explosion area
            var cells = GenRadial.RadialCellsAround(center, def.projectile.explosionRadius, true).ToList();
            int targetCount = (int)(cells.Count * pollutionCells);
            int applied = 0;
            foreach (IntVec3 c in cells.InRandomOrder())
            {
                if (!c.InBounds(map)) continue;
                if (map.pollutionGrid.IsPolluted(c)) continue;

                map.pollutionGrid.SetPolluted(c, true);
                applied++;
                if (applied >= targetCount) break;
            }
        }
    }
}
