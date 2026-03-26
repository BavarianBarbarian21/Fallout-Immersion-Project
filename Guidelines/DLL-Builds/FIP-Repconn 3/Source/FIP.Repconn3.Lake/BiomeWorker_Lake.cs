using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_Lake : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeBandScoring.IsPreserveEnvelope(tile) || BiomeBandScoring.HasRiver(tile))
        {
            return -100f;
        }

        return BiomeBandScoring.SelectChanceScore(planetTile.tileId, 30012, 0.01f, 33f, -100f);
    }
}