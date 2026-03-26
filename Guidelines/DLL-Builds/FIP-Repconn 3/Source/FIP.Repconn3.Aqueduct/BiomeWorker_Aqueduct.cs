using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_Aqueduct : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return BiomeBandScoring.IsAqueductEnvelope(tile) ? 30f : -100f;
    }
}