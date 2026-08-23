using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.RobCo;

public class GenStep_RobCoVaultThreats : GenStep
{
    public override int SeedPart => 18042026;

    public override void Generate(Map map, GenStepParams parms)
    {
        BuildVaultFacility(map);

        if (map.Parent is RobCoVaultSite site)
        {
            site.EnsureMapInitialized(map);
        }
    }

    private static void BuildVaultFacility(Map map)
    {
        IntVec3 center = map.Center;
        CellRect facility = new(center.x - 18, center.z - 13, 37, 27);
        TerrainDef concrete = DefDatabase<TerrainDef>.GetNamed("Concrete");

        IntVec3 southEntrance = new(center.x, 0, facility.minZ);
        IntVec3 northEntrance = new(center.x, 0, facility.maxZ);
        BuildRoomShell(
            facility,
            map,
            concrete,
            new HashSet<IntVec3> { southEntrance, northEntrance },
            prepareCells: true);

        // The recovery target is generated close to map.Center immediately
        // after this layout. Keep that area walkable, but enclose it in a
        // separately accessible inner vault.
        CellRect innerVault = new(center.x - 6, center.z - 5, 13, 11);
        IntVec3 vaultDoor = new(center.x, 0, innerVault.minZ);
        BuildRoomShell(
            innerVault,
            map,
            concrete,
            new HashSet<IntVec3> { vaultDoor },
            prepareCells: false);

        BuildWallLine(
            facility.minX + 1,
            innerVault.minX - 1,
            center.z,
            new IntVec3(innerVault.minX - 3, 0, center.z),
            map);
        BuildWallLine(
            innerVault.maxX + 1,
            facility.maxX - 1,
            center.z,
            new IntVec3(innerVault.maxX + 3, 0, center.z),
            map);

        MapGenerator.UsedRects.Add(facility.ExpandedBy(4));
    }

    private static void BuildRoomShell(
        CellRect rect,
        Map map,
        TerrainDef floor,
        HashSet<IntVec3> doorCells,
        bool prepareCells)
    {
        foreach (IntVec3 cell in rect)
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            if (prepareCells)
            {
                ClearCellForStructure(cell, map);
                map.terrainGrid.SetTerrain(cell, floor);
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            }

            bool edge = cell.x == rect.minX
                || cell.x == rect.maxX
                || cell.z == rect.minZ
                || cell.z == rect.maxZ;
            if (edge)
            {
                SpawnSteelBarrier(cell, map, doorCells.Contains(cell));
            }
        }
    }

    private static void BuildWallLine(
        int minX,
        int maxX,
        int z,
        IntVec3 doorCell,
        Map map)
    {
        for (int x = minX; x <= maxX; x++)
        {
            IntVec3 cell = new(x, 0, z);
            if (cell.InBounds(map))
            {
                SpawnSteelBarrier(cell, map, cell == doorCell);
            }
        }
    }

    private static void SpawnSteelBarrier(IntVec3 cell, Map map, bool door)
    {
        ThingDef buildingDef = door ? ThingDefOf.Door : ThingDefOf.Wall;
        Thing building = ThingMaker.MakeThing(buildingDef, ThingDefOf.Steel);
        GenSpawn.Spawn(building, cell, map, WipeMode.Vanish);
    }

    private static void ClearCellForStructure(IntVec3 cell, Map map)
    {
        foreach (Thing thing in cell.GetThingList(map).ToList())
        {
            if (thing.def.category is ThingCategory.Building
                or ThingCategory.Plant
                or ThingCategory.Item)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
