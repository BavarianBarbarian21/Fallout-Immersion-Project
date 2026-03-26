using System;
using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Bay : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        var isBayEnvelope = tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills
            && IsCoastal(tile)
            && tile.temperature > -5f
            && tile.temperature <= 32f;

        if (!isBayEnvelope)
        {
            return -100f;
        }

        unchecked
        {
            var hash = planetTile.tileId;
            hash = (hash * 397) ^ 30041;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            var value = (uint)hash & 0x00FFFFFFu;
            return value / 16777215f <= 0.5f ? 29f : -100f;
        }
    }

    private static bool IsCoastal(Tile tile)
    {
        try
        {
            return tile.IsCoastal;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}