using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn2;

public class BiomeWorker_WastelandPlanet : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile tileID)
    {
        if (tile.WaterCovered)
        {
            return -100f;
        }

        return 1000000f;
    }
}