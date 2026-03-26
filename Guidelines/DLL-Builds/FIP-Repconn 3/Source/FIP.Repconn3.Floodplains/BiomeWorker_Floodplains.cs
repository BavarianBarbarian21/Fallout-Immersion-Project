using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_Floodplains : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeBandScoring.IsAqueductEnvelope(tile))
        {
            return -100f;
        }

        return BiomeBandScoring.SelectChanceScore(planetTile.tileId, 30021, 0.05f, 35f, -100f);
    }
}