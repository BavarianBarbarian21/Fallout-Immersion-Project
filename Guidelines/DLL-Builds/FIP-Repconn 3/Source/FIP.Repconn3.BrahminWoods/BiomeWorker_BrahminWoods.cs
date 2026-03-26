using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_BrahminWoods : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return BiomeBandScoring.IsBrahminWoodsEnvelope(tile) ? 21f : -100f;
    }
}