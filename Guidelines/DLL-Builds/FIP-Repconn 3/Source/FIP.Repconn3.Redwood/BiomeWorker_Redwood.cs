using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_Redwood : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return BiomeBandScoring.IsRedwoodEnvelope(tile) ? 24f : -100f;
    }
}