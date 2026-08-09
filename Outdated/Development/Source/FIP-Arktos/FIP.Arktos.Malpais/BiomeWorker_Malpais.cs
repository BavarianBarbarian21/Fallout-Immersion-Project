using RimWorld;
using RimWorld.Planet;

namespace FIP.Arktos;

public sealed class BiomeWorker_Malpais : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Mountainous or Hilliness.Impassable
            && tile.temperature > 26f
            && tile.temperature <= 32f
            ? 18f
            : -100f;
    }
}