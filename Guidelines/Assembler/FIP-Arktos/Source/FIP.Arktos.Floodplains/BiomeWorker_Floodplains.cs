using System;
using System.Collections;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;

namespace FIP.Arktos;

public sealed class BiomeWorker_Floodplains : BiomeWorker
{
    private static readonly FieldInfo PotentialRiversField = typeof(SurfaceTile).GetField("potentialRivers", BindingFlags.Instance | BindingFlags.NonPublic);

    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        var hasRiver = HasRiver(tile);
        var isLowElevation = tile != null && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills;
        var isMediumElevation = tile != null && tile.hilliness == Hilliness.LargeHills;
        var isTemperate = tile != null && tile.temperature > 8f && tile.temperature <= 23f;
        var isCentralValleyEnvelope = tile != null && !tile.WaterCovered && isLowElevation && isTemperate;
        var isBrahminWoodsEnvelope = tile != null && !tile.WaterCovered && isMediumElevation && isTemperate;

        if (tile == null || tile.WaterCovered || !hasRiver || (!isCentralValleyEnvelope && !isBrahminWoodsEnvelope))
        {
            return -100f;
        }

        unchecked
        {
            var hash = planetTile.tileId;
            hash = (hash * 397) ^ 30021;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            var value = (uint)hash & 0x00FFFFFFu;
            return value / 16777215f <= 0.05f ? 35f : -100f;
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