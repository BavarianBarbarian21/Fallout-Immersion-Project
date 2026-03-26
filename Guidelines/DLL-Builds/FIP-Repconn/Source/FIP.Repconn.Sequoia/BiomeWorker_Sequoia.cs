using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Sequoia : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Mountainous or Hilliness.Impassable
            && ((tile.temperature > 15.5f && tile.temperature <= 23f) || (tile.temperature > 23f && tile.temperature <= 32f))
            ? 24f
            : -100f;
    }
}