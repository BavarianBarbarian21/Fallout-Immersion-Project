using RimWorld;
using RimWorld.Planet;

namespace FIP.Arktos;

public sealed class BiomeWorker_Redwood : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Mountainous or Hilliness.Impassable
            && ((tile.temperature > -5f && tile.temperature <= 8f) || (tile.temperature > 8f && tile.temperature <= 15.5f))
            ? 24f
            : -100f;
    }
}