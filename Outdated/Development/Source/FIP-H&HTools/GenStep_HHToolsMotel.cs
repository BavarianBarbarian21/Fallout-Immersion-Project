using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.HHTools;

public class GenStep_HHToolsMotel : GenStep
{
    public override int SeedPart => 91724431;

    public override void Generate(Map map, GenStepParams parms)
    {
        BuildRoadAndParking(map);
        BuildGuestWing(map);
        BuildServiceWing(map);
        BuildExteriorDetails(map);
        MapGenerator.PlayerStartSpot = new IntVec3(map.Size.x / 2, 0, 8);

        if (map.Parent is HHToolsMotelMissionSite missionSite)
        {
            missionSite.InitializeMissionMap(map);
        }
    }

    private static void BuildRoadAndParking(Map map)
    {
        TerrainDef paved = DefDatabase<TerrainDef>.GetNamed("PavedTile");
        TerrainDef concrete = DefDatabase<TerrainDef>.GetNamed("Concrete");

        CellRect road = new(4, 16, map.Size.x - 8, 12);
        PaintTerrain(road, map, paved);

        CellRect entrance = new(57, 28, 12, 13);
        PaintTerrain(entrance, map, paved);

        CellRect parking = new(48, 40, 52, 28);
        PaintTerrain(parking, map, concrete);
        MapGenerator.UsedRects.Add(road);
        MapGenerator.UsedRects.Add(parking);
    }

    private static void BuildGuestWing(Map map)
    {
        TerrainDef floor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
        for (int index = 0; index < 6; index += 1)
        {
            CellRect room = new(34 + index * 10, 71, 10, 12);
            bool sealedRoom = index == 5;
            IntVec3 door = new(room.minX + 5, 0, room.minZ);
            BuildRoom(room, map, floor, sealedRoom ? (IntVec3?)null : door);

            TrySpawnAncient("AncientBed", new IntVec3(room.minX + 3, 0, room.minZ + 5), map);
            TrySpawnAncient("AncientLamp", new IntVec3(room.minX + 7, 0, room.minZ + 8), map);
            if (index is 1 or 4)
            {
                TrySpawnAncient(
                    "AncientAirConditioner",
                    new IntVec3(room.maxX - 1, 0, room.maxZ - 1),
                    map);
            }
        }

        CellRect walkway = new(32, 67, 64, 4);
        PaintTerrain(walkway, map, DefDatabase<TerrainDef>.GetNamed("Concrete"));

        SpawnLoot("MedicineIndustrial", 3, new IntVec3(87, 0, 78), map);
        SpawnLoot("ComponentIndustrial", 5, new IntVec3(90, 0, 75), map);
        SpawnLoot("MealSurvivalPack", 4, new IntVec3(84, 0, 80), map);
        TrySpawnBuilding("TrapSpike", new IntVec3(86, 0, 74), map);
        TrySpawnBuilding("TrapSpike", new IntVec3(91, 0, 79), map);
    }

    private static void BuildServiceWing(Map map)
    {
        TerrainDef floor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");

        CellRect office = new(34, 58, 14, 13);
        BuildRoom(office, map, floor, new IntVec3(41, 0, office.minZ));
        TrySpawnAncient("AncientATM", new IntVec3(37, 0, 65), map);
        TrySpawnAncient("AncientLamp", new IntVec3(44, 0, 66), map);
        TrySpawnAncient("AncientCrate", new IntVec3(40, 0, 61), map);

        CellRect managerRoom = new(34, 47, 14, 11);
        BuildRoom(managerRoom, map, floor, doorCell: null);
        TrySpawnAncient("AncientCrate", new IntVec3(41, 0, 52), map);
        SpawnLoot("Silver", 90, new IntVec3(38, 0, 54), map);
        SpawnLoot("Beer", 8, new IntVec3(44, 0, 50), map);

        CellRect laundry = new(34, 36, 14, 11);
        BuildRoom(laundry, map, floor, new IntVec3(47, 0, 41));
        TrySpawnAncient("AncientWashingMachine", new IntVec3(38, 0, 41), map);
        TrySpawnAncient("AncientRefrigerator", new IntVec3(44, 0, 42), map);

        MapGenerator.UsedRects.Add(new CellRect(32, 34, 68, 51));
    }

    private static void BuildExteriorDetails(Map map)
    {
        TrySpawnAncient("AncientVendingMachine", new IntVec3(50, 0, 65), map);
        TrySpawnAncient("AncientPostbox", new IntVec3(57, 0, 35), map);
        TrySpawnAncient("AncientRustedCar", new IntVec3(59, 0, 49), map, Rot4.East);
        TrySpawnAncient("AncientRustedCar", new IntVec3(76, 0, 57), map, Rot4.West);
        TrySpawnAncient("AncientRustedCar", new IntVec3(90, 0, 45), map, Rot4.East);
        TrySpawnAncient("AncientRustedTruck", new IntVec3(24, 0, 20), map, Rot4.East);
        TrySpawnAncient("AncientRustedCar", new IntVec3(102, 0, 21), map, Rot4.West);
    }

    private static void BuildRoom(
        CellRect rect,
        Map map,
        TerrainDef floor,
        IntVec3? doorCell)
    {
        ThingDef wallDef = ThingDefOf.Wall;
        ThingDef doorDef = ThingDefOf.Door;
        ThingDef wallStuff = GenStuff.DefaultStuffFor(wallDef);
        ThingDef doorStuff = GenStuff.DefaultStuffFor(doorDef);

        foreach (IntVec3 cell in rect)
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            ClearCellForStructure(cell, map);
            map.terrainGrid.SetTerrain(cell, floor);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            if (cell.x != rect.minX
                && cell.x != rect.maxX
                && cell.z != rect.minZ
                && cell.z != rect.maxZ)
            {
                continue;
            }

            Thing building = doorCell.HasValue && cell == doorCell.Value
                ? ThingMaker.MakeThing(doorDef, doorStuff)
                : ThingMaker.MakeThing(wallDef, wallStuff);
            GenSpawn.Spawn(building, cell, map, WipeMode.Vanish);
        }
    }

    private static void PaintTerrain(CellRect rect, Map map, TerrainDef terrain)
    {
        foreach (IntVec3 cell in rect)
        {
            if (cell.InBounds(map))
            {
                ClearCellForStructure(cell, map);
                map.terrainGrid.SetTerrain(cell, terrain);
            }
        }
    }

    private static void TrySpawnAncient(
        string defName,
        IntVec3 cell,
        Map map,
        Rot4? rotation = null)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null || !cell.InBounds(map))
        {
            return;
        }

        Rot4 rot = rotation ?? Rot4.North;
        if (!GenSpawn.CanSpawnAt(def, cell, map, rot))
        {
            return;
        }

        Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        GenSpawn.Spawn(thing, cell, map, rot, WipeMode.Vanish);
    }

    private static void TrySpawnBuilding(string defName, IntVec3 cell, Map map)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null || !GenSpawn.CanSpawnAt(def, cell, map))
        {
            return;
        }

        Thing thing = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
    }

    private static void SpawnLoot(
        string defName,
        int stackCount,
        IntVec3 cell,
        Map map)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if (def == null || !cell.InBounds(map))
        {
            return;
        }

        Thing thing = ThingMaker.MakeThing(def);
        thing.stackCount = stackCount;
        GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
    }

    private static void ClearCellForStructure(IntVec3 cell, Map map)
    {
        foreach (Thing thing in cell.GetThingList(map).ToList())
        {
            if (thing.def.category is ThingCategory.Building or ThingCategory.Plant)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
