using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Divide : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills or Hilliness.LargeHills
            && tile.temperature > 32f
            ? 22f
            : -100f;
    }
}