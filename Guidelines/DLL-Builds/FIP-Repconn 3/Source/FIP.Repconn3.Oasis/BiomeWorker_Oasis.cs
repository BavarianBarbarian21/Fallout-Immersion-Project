using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn3;

public sealed class BiomeWorker_Oasis : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeBandScoring.IsMojaveEnvelope(tile) || BiomeBandScoring.HasRiver(tile))
        {
            return -100f;
        }

        return BiomeBandScoring.SelectChanceScore(planetTile.tileId, 30051, 0.01f, 33f, -100f);
    }
}