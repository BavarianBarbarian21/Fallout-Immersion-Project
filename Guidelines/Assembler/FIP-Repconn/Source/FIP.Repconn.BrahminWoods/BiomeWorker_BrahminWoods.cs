using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_BrahminWoods : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness == Hilliness.LargeHills
            && tile.temperature > 8f
            && tile.temperature <= 23f
            ? 21f
            : -100f;
    }
}