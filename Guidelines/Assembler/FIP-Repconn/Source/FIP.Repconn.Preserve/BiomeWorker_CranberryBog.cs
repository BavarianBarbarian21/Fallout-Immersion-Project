using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_CranberryBog : BiomeWorker_Glowforest
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile != null
            && tile.temperature > -5f
            && tile.temperature <= 8f
            ? base.GetScore(biome, tile, planetTile)
            : -100f;
    }
}