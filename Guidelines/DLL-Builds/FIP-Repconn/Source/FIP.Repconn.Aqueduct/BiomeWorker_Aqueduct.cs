using System;
using System.Collections;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Aqueduct : BiomeWorker
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

        return tile != null
            && !tile.WaterCovered
            && hasRiver
            && (isCentralValleyEnvelope || isBrahminWoodsEnvelope)
            ? 30f
            : -100f;
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