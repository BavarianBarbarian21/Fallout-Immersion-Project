using System;
using System.Collections;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Bay : BiomeWorker
{
    private static readonly FieldInfo PotentialRiversField = typeof(SurfaceTile).GetField("potentialRivers", BindingFlags.Instance | BindingFlags.NonPublic);

    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        var hasRiver = HasRiver(tile);
        var isBayEnvelope = tile != null
            && !tile.WaterCovered
            && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills
            && IsCoastal(tile)
            && tile.temperature > -5f
            && tile.temperature <= 32f;

        if (!isBayEnvelope || hasRiver)
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

    private static bool HasRiver(Tile tile)
    {
        if (tile is not SurfaceTile surfaceTile)
        {
            return false;
        }

        if (PotentialRiversField?.GetValue(surfaceTile) is ICollection potentialRivers)
        {
            return potentialRivers.Count > 0;
        }

        try
        {
            return surfaceTile.Rivers != null && surfaceTile.Rivers.Count > 0;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }
}