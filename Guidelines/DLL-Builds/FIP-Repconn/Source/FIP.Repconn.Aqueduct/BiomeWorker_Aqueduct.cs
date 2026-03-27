using System;
using System.Collections;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn;

public sealed class BiomeWorker_Aqueduct : BiomeWorker
{
    private static readonly FieldInfo PotentialRiversField = typeof(SurfaceTile).GetField("potentialRivers", BindingFlags.Instance | BindingFlags.NonPublic);
    private const float HugeRiverChance = 0.92f;
    private const float LargeRiverChance = 0.55f;
    private const float MediumRiverChance = 0.12f;
    private const float SmallRiverChance = 0f;

    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        var riverSize = GetRiverSize(tile);
        var isLowElevation = tile != null && tile.hilliness is Hilliness.Flat or Hilliness.SmallHills;
        var isMediumElevation = tile != null && tile.hilliness == Hilliness.LargeHills;
        var isTemperate = tile != null && tile.temperature > 8f && tile.temperature <= 23f;
        var isCentralValleyEnvelope = tile != null && !tile.WaterCovered && isLowElevation && isTemperate;
        var isBrahminWoodsEnvelope = tile != null && !tile.WaterCovered && isMediumElevation && isTemperate;
        var aqueductChance = GetAqueductChance(riverSize);

        return tile != null
            && !tile.WaterCovered
            && riverSize != RiverSize.None
            && (isCentralValleyEnvelope || isBrahminWoodsEnvelope)
            && RollForTile(planetTile.tileId, 30023) <= aqueductChance
            ? 30f + aqueductChance * 10f
            : -100f;
    }

    private static RiverSize GetRiverSize(Tile tile)
    {
        if (tile is not SurfaceTile surfaceTile)
        {
            return RiverSize.None;
        }

        var riverLinks = surfaceTile.Rivers;
        if ((riverLinks == null || riverLinks.Count == 0) && PotentialRiversField?.GetValue(surfaceTile) is ICollection potentialRivers)
        {
            riverLinks = potentialRivers as System.Collections.Generic.List<SurfaceTile.RiverLink>;
        }

        try
        {
            if (riverLinks == null || riverLinks.Count == 0)
            {
                return RiverSize.None;
            }

            var strongestWidth = 0f;
            foreach (var riverLink in riverLinks)
            {
                if (riverLink.river == null)
                {
                    continue;
                }

                if (riverLink.river.widthOnWorld > strongestWidth)
                {
                    strongestWidth = riverLink.river.widthOnWorld;
                }
            }

            if (strongestWidth >= 0.7f)
            {
                return RiverSize.Huge;
            }

            if (strongestWidth >= 0.5f)
            {
                return RiverSize.Large;
            }

            if (strongestWidth >= 0.3f)
            {
                return RiverSize.Medium;
            }

            return strongestWidth > 0f ? RiverSize.Small : RiverSize.None;
        }
        catch (NullReferenceException)
        {
            return RiverSize.None;
        }
    }

    private static float GetAqueductChance(RiverSize riverSize)
    {
        return riverSize switch
        {
            RiverSize.Huge => HugeRiverChance,
            RiverSize.Large => LargeRiverChance,
            RiverSize.Medium => MediumRiverChance,
            RiverSize.Small => SmallRiverChance,
            _ => 0f,
        };
    }

    private static float RollForTile(int tileId, int salt)
    {
        unchecked
        {
            var hash = tileId;
            hash = (hash * 397) ^ salt;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            var value = (uint)hash & 0x00FFFFFFu;
            return value / 16777215f;
        }
    }

    private enum RiverSize
    {
        None,
        Small,
        Medium,
        Large,
        Huge,
    }
}