using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.HHTools;

public class GenStep_HHToolsHomestead : GenStep
{
    public override int SeedPart => 91724441;

    public override void Generate(Map map, GenStepParams parms)
    {
        int centerX = map.Size.x / 2;
        int centerZ = map.Size.z / 2;
        TerrainDef wood = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
        TerrainDef soil = DefDatabase<TerrainDef>.GetNamed("Soil");
        TerrainDef packedDirt = DefDatabase<TerrainDef>.GetNamed("PackedDirt");

        CellRect farmhouse = new(centerX - 16, centerZ - 12, 32, 24);
        IntVec3 frontDoor = new(centerX, 0, farmhouse.minZ);
        IntVec3 backDoor = new(centerX, 0, farmhouse.maxZ);
        HHToolsLocationMapUtility.BuildShell(
            farmhouse,
            map,
            wood,
            [frontDoor, backDoor]);

        int bedroomWallX = centerX + 4;
        HHToolsLocationMapUtility.BuildWallLine(
            HHToolsLocationMapUtility.VerticalLine(
                bedroomWallX,
                farmhouse.minZ + 1,
                farmhouse.maxZ - 1),
            map,
            [new IntVec3(bedroomWallX, 0, centerZ)]);
        HHToolsLocationMapUtility.BuildWallLine(
            HHToolsLocationMapUtility.HorizontalLine(
                centerZ,
                bedroomWallX + 1,
                farmhouse.maxX - 1),
            map,
            [new IntVec3(centerX + 10, 0, centerZ)]);
        HHToolsLocationMapUtility.BuildWallLine(
            HHToolsLocationMapUtility.HorizontalLine(
                centerZ - 2,
                farmhouse.minX + 1,
                bedroomWallX - 1),
            map,
            [new IntVec3(centerX - 5, 0, centerZ - 2)]);

        CellRect porch = new(centerX - 8, farmhouse.minZ - 5, 16, 5);
        HHToolsLocationMapUtility.PaintTerrain(porch, map, wood);
        CellRect driveway = new(centerX - 3, 8, 7, farmhouse.minZ - 13);
        HHToolsLocationMapUtility.PaintTerrain(driveway, map, packedDirt);

        CellRect westField = new(centerX - 35, centerZ - 15, 15, 31);
        CellRect eastField = new(centerX + 20, centerZ - 15, 15, 31);
        HHToolsLocationMapUtility.PaintTerrain(westField, map, soil);
        HHToolsLocationMapUtility.PaintTerrain(eastField, map, soil);

        HHToolsLocationMapUtility.TrySpawn("AncientBed", new IntVec3(centerX + 9, 0, centerZ - 6), map);
        HHToolsLocationMapUtility.TrySpawn("AncientBed", new IntVec3(centerX + 9, 0, centerZ + 6), map);
        HHToolsLocationMapUtility.TrySpawn("AncientStove", new IntVec3(centerX - 11, 0, centerZ + 5), map);
        HHToolsLocationMapUtility.TrySpawn("AncientKitchenSink", new IntVec3(centerX - 7, 0, centerZ + 8), map);
        HHToolsLocationMapUtility.TrySpawn("AncientRefrigerator", new IntVec3(centerX - 12, 0, centerZ + 9), map);
        HHToolsLocationMapUtility.TrySpawn("AncientLamp", new IntVec3(centerX - 7, 0, centerZ - 7), map);
        HHToolsLocationMapUtility.TrySpawn("AncientCrate", new IntVec3(centerX + 1, 0, centerZ + 7), map);
        HHToolsLocationMapUtility.TrySpawn("AncientPostbox", new IntVec3(centerX + 5, 0, farmhouse.minZ - 8), map);
        HHToolsLocationMapUtility.TrySpawn("AncientRustedTruck", new IntVec3(centerX - 2, 0, 25), map, Rot4.East);
        HHToolsLocationMapUtility.SpawnLoot("MealSurvivalPack", 5, new IntVec3(centerX - 1, 0, centerZ + 8), map);
        HHToolsLocationMapUtility.SpawnLoot("MedicineIndustrial", 3, new IntVec3(centerX + 12, 0, centerZ + 3), map);

        MapGenerator.PlayerStartSpot = new IntVec3(centerX, 0, 8);
        MapGenerator.UsedRects.Add(farmhouse.ExpandedBy(38));
    }
}

public class GenStep_HHToolsChurch : GenStep
{
    public override int SeedPart => 91724451;

    public override void Generate(Map map, GenStepParams parms)
    {
        int centerX = map.Size.x / 2;
        int centerZ = map.Size.z / 2;
        TerrainDef wood = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor");
        TerrainDef packedDirt = DefDatabase<TerrainDef>.GetNamed("PackedDirt");
        TerrainDef paved = DefDatabase<TerrainDef>.GetNamed("PavedTile");

        CellRect church = new(centerX - 14, centerZ - 19, 28, 38);
        HHToolsLocationMapUtility.BuildShell(
            church,
            map,
            wood,
            [
                new IntVec3(centerX - 1, 0, church.minZ),
                new IntVec3(centerX, 0, church.minZ),
                new IntVec3(church.maxX, 0, centerZ + 10)
            ]);
        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(centerX - 1, church.minZ + 1, 2, church.Height - 2),
            map,
            paved);

        for (int z = church.minZ + 8; z <= church.maxZ - 9; z += 5)
        {
            HHToolsLocationMapUtility.TrySpawn(
                "DiningChair",
                new IntVec3(centerX - 7, 0, z),
                map,
                Rot4.North);
            HHToolsLocationMapUtility.TrySpawn(
                "DiningChair",
                new IntVec3(centerX - 4, 0, z),
                map,
                Rot4.North);
            HHToolsLocationMapUtility.TrySpawn(
                "DiningChair",
                new IntVec3(centerX + 4, 0, z),
                map,
                Rot4.North);
            HHToolsLocationMapUtility.TrySpawn(
                "DiningChair",
                new IntVec3(centerX + 7, 0, z),
                map,
                Rot4.North);
        }

        HHToolsLocationMapUtility.TrySpawn(
            "Table1x2c",
            new IntVec3(centerX - 1, 0, church.maxZ - 6),
            map,
            Rot4.East);
        HHToolsLocationMapUtility.TrySpawn(
            "SteleLarge",
            new IntVec3(centerX, 0, church.maxZ - 3),
            map);
        HHToolsLocationMapUtility.TrySpawn(
            "AncientLamp",
            new IntVec3(centerX - 8, 0, church.maxZ - 4),
            map);
        HHToolsLocationMapUtility.TrySpawn(
            "AncientLamp",
            new IntVec3(centerX + 8, 0, church.maxZ - 4),
            map);

        CellRect[] houses =
        [
            new(centerX - 38, centerZ - 15, 12, 11),
            new(centerX + 27, centerZ - 15, 12, 11),
            new(centerX - 38, centerZ + 7, 12, 11),
            new(centerX + 27, centerZ + 7, 12, 11)
        ];

        for (int index = 0; index < houses.Length; index += 1)
        {
            CellRect house = houses[index];
            bool westHouse = house.maxX < centerX;
            IntVec3 door = westHouse
                ? new IntVec3(house.maxX, 0, house.CenterCell.z)
                : new IntVec3(house.minX, 0, house.CenterCell.z);
            HHToolsLocationMapUtility.BuildShell(house, map, wood, [door]);
            HHToolsLocationMapUtility.TrySpawn(
                "AncientBed",
                new IntVec3(house.minX + 3, 0, house.minZ + 4),
                map);
            HHToolsLocationMapUtility.TrySpawn(
                "AncientCrate",
                new IntVec3(house.maxX - 2, 0, house.maxZ - 2),
                map);
        }

        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(centerX - 2, 8, 5, church.minZ - 8),
            map,
            packedDirt);
        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(centerX - 31, centerZ - 2, 17, 5),
            map,
            packedDirt);
        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(church.maxX + 1, centerZ - 2, 17, 5),
            map,
            packedDirt);

        for (int x = centerX - 10; x <= centerX + 10; x += 5)
        {
            HHToolsLocationMapUtility.TrySpawn(
                "Grave",
                new IntVec3(x, 0, church.maxZ + 7),
                map,
                Rot4.North);
        }

        HHToolsLocationMapUtility.SpawnLoot(
            "MedicineIndustrial",
            4,
            new IntVec3(centerX + 10, 0, church.maxZ - 5),
            map);
        HHToolsLocationMapUtility.SpawnLoot(
            "Silver",
            120,
            new IntVec3(centerX - 10, 0, church.maxZ - 5),
            map);

        MapGenerator.PlayerStartSpot = new IntVec3(centerX, 0, 8);
        MapGenerator.UsedRects.Add(
            new CellRect(centerX - 43, centerZ - 25, 86, 56));
    }
}

public class GenStep_HHToolsWarehouse : GenStep
{
    public override int SeedPart => 91724461;

    public override void Generate(Map map, GenStepParams parms)
    {
        int centerX = map.Size.x / 2;
        int centerZ = map.Size.z / 2;
        TerrainDef concrete = DefDatabase<TerrainDef>.GetNamed("Concrete");
        TerrainDef paved = DefDatabase<TerrainDef>.GetNamed("PavedTile");

        CellRect warehouse = new(centerX - 28, centerZ - 20, 56, 40);
        HHToolsLocationMapUtility.BuildShell(
            warehouse,
            map,
            concrete,
            [
                new IntVec3(centerX - 9, 0, warehouse.minZ),
                new IntVec3(centerX - 8, 0, warehouse.minZ),
                new IntVec3(centerX + 9, 0, warehouse.minZ),
                new IntVec3(centerX + 10, 0, warehouse.minZ),
                new IntVec3(centerX, 0, warehouse.maxZ)
            ]);

        CellRect loadingApron = new(centerX - 23, warehouse.minZ - 9, 46, 9);
        HHToolsLocationMapUtility.PaintTerrain(loadingApron, map, concrete);
        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(centerX - 3, warehouse.minZ + 1, 6, warehouse.Height - 2),
            map,
            paved);
        HHToolsLocationMapUtility.PaintTerrain(
            new CellRect(centerX - 5, 8, 10, warehouse.minZ - 17),
            map,
            paved);

        int[] rowX =
        [
            warehouse.minX + 6,
            warehouse.minX + 14,
            warehouse.maxX - 14,
            warehouse.maxX - 6
        ];
        foreach (int x in rowX)
        {
            for (int z = warehouse.minZ + 6; z <= warehouse.maxZ - 6; z += 8)
            {
                HHToolsLocationMapUtility.TrySpawn(
                    ((z - warehouse.minZ - 6) / 8) % 2 == 0
                        ? "AncientLargeCrate"
                        : "AncientCrate",
                    new IntVec3(x, 0, z),
                    map,
                    Rot4.North);
                HHToolsLocationMapUtility.TrySpawn(
                    "AncientBarrel",
                    new IntVec3(x + 2, 0, z + 2),
                    map);
            }
        }

        HHToolsLocationMapUtility.TrySpawn(
            "AncientLockerBank",
            new IntVec3(warehouse.minX + 3, 0, warehouse.maxZ - 4),
            map,
            Rot4.East);
        HHToolsLocationMapUtility.TrySpawn(
            "AncientEquipmentBlocks",
            new IntVec3(warehouse.maxX - 6, 0, warehouse.maxZ - 5),
            map,
            Rot4.West);
        HHToolsLocationMapUtility.TrySpawn(
            "AncientRustedTruck",
            new IntVec3(centerX - 15, 0, warehouse.minZ - 6),
            map,
            Rot4.North);
        HHToolsLocationMapUtility.TrySpawn(
            "AncientRustedTruck",
            new IntVec3(centerX + 15, 0, warehouse.minZ - 6),
            map,
            Rot4.North);
        HHToolsLocationMapUtility.TrySpawn(
            "TrapSpike",
            new IntVec3(warehouse.minX + 18, 0, warehouse.minZ + 10),
            map);
        HHToolsLocationMapUtility.TrySpawn(
            "TrapSpike",
            new IntVec3(warehouse.maxX - 18, 0, warehouse.maxZ - 10),
            map);

        HHToolsLocationMapUtility.SpawnLoot(
            "ComponentIndustrial",
            8,
            new IntVec3(warehouse.minX + 5, 0, warehouse.maxZ - 5),
            map);
        HHToolsLocationMapUtility.SpawnLoot(
            "Chemfuel",
            35,
            new IntVec3(warehouse.maxX - 5, 0, warehouse.maxZ - 5),
            map);
        HHToolsLocationMapUtility.SpawnLoot(
            "Steel",
            90,
            new IntVec3(warehouse.maxX - 5, 0, warehouse.minZ + 5),
            map);

        MapGenerator.PlayerStartSpot = new IntVec3(centerX, 0, 8);
        MapGenerator.UsedRects.Add(warehouse.ExpandedBy(15));
    }
}

internal static class HHToolsLocationMapUtility
{
    public static void BuildShell(
        CellRect rect,
        Map map,
        TerrainDef floor,
        IEnumerable<IntVec3> doorCells)
    {
        HashSet<IntVec3> doors = doorCells == null ? [] : [.. doorCells];
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
            bool edge = cell.x == rect.minX
                || cell.x == rect.maxX
                || cell.z == rect.minZ
                || cell.z == rect.maxZ;
            if (!edge)
            {
                continue;
            }

            Thing building = doors.Contains(cell)
                ? ThingMaker.MakeThing(doorDef, doorStuff)
                : ThingMaker.MakeThing(wallDef, wallStuff);
            GenSpawn.Spawn(building, cell, map, WipeMode.Vanish);
        }
    }

    public static void BuildWallLine(
        IEnumerable<IntVec3> cells,
        Map map,
        IEnumerable<IntVec3> doorCells)
    {
        HashSet<IntVec3> doors = doorCells == null ? [] : [.. doorCells];
        ThingDef wallDef = ThingDefOf.Wall;
        ThingDef doorDef = ThingDefOf.Door;
        ThingDef wallStuff = GenStuff.DefaultStuffFor(wallDef);
        ThingDef doorStuff = GenStuff.DefaultStuffFor(doorDef);

        foreach (IntVec3 cell in cells)
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            Thing building = doors.Contains(cell)
                ? ThingMaker.MakeThing(doorDef, doorStuff)
                : ThingMaker.MakeThing(wallDef, wallStuff);
            GenSpawn.Spawn(building, cell, map, WipeMode.Vanish);
        }
    }

    public static IEnumerable<IntVec3> VerticalLine(int x, int minZ, int maxZ)
    {
        for (int z = minZ; z <= maxZ; z += 1)
        {
            yield return new IntVec3(x, 0, z);
        }
    }

    public static IEnumerable<IntVec3> HorizontalLine(int z, int minX, int maxX)
    {
        for (int x = minX; x <= maxX; x += 1)
        {
            yield return new IntVec3(x, 0, z);
        }
    }

    public static void PaintTerrain(CellRect rect, Map map, TerrainDef terrain)
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

    public static void TrySpawn(
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

    public static void SpawnLoot(
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
