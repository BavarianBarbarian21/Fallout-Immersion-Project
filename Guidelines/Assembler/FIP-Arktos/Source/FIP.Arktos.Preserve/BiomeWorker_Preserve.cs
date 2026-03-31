using RimWorld;
using RimWorld.Planet;

namespace FIP.Arktos;

public sealed class BiomeWorker_Preserve : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills or Hilliness.LargeHills
            && tile.temperature > -5f
            && tile.temperature <= 8f
            ? 20f
            : -100f;
    }
}