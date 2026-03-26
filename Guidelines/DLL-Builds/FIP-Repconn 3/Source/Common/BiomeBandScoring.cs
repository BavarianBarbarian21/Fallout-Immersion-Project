using RimWorld.Planet;

namespace FIP.Repconn3;

internal static class BiomeBandScoring
{
    public const float ArcticMax = -5f;
    public const float BorealMax = 8f;
    public const float TemperateMax = 23f;
    public const float AridMax = 32f;
    public const float TemperateSplit = (BorealMax + TemperateMax) * 0.5f;

    public static bool IsLand(Tile tile)
    {
        return tile != null && !tile.WaterCovered;
    }

    public static bool IsLowElevation(Tile tile)
    {
        return tile.hilliness is Hilliness.Flat or Hilliness.SmallHills;
    }

    public static bool IsMediumElevation(Tile tile)
    {
        return tile.hilliness == Hilliness.LargeHills;
    }

    public static bool IsHighElevation(Tile tile)
    {
        return tile.hilliness is Hilliness.Mountainous or Hilliness.Impassable;
    }

    public static bool IsArctic(float temperature)
    {
        return temperature <= ArcticMax;
    }

    public static bool IsBoreal(float temperature)
    {
        return temperature > ArcticMax && temperature <= BorealMax;
    }

    public static bool IsTemperate(float temperature)
    {
        return temperature > BorealMax && temperature <= TemperateMax;
    }

    public static bool IsLowerTemperateHalf(float temperature)
    {
        return temperature > BorealMax && temperature <= TemperateSplit;
    }

    public static bool IsUpperTemperateHalf(float temperature)
    {
        return temperature > TemperateSplit && temperature <= TemperateMax;
    }

    public static bool IsArid(float temperature)
    {
        return temperature > TemperateMax && temperature <= AridMax;
    }

    public static bool IsDesert(float temperature)
    {
        return temperature > AridMax;
    }

    public static bool IsHot(float temperature)
    {
        return temperature > AridMax;
    }

    public static bool IsLowOrMediumElevation(Tile tile)
    {
        return IsLowElevation(tile) || IsMediumElevation(tile);
    }

    public static bool IsBorealToArid(float temperature)
    {
        return temperature > ArcticMax && temperature <= AridMax;
    }

    public static bool HasRiver(Tile tile)
    {
        return tile is SurfaceTile surfaceTile && surfaceTile.Rivers != null && surfaceTile.Rivers.Count > 0;
    }

    public static bool IsCoastal(Tile tile)
    {
        return tile != null && tile.IsCoastal;
    }

    public static bool IsPreserveEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowOrMediumElevation(tile) && IsBoreal(tile.temperature);
    }

    public static bool IsTemperateLowOrMediumEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowOrMediumElevation(tile) && IsTemperate(tile.temperature);
    }

    public static bool IsCentralValleyEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowElevation(tile) && IsTemperate(tile.temperature);
    }

    public static bool IsBrahminWoodsEnvelope(Tile tile)
    {
        return IsLand(tile) && IsMediumElevation(tile) && IsTemperate(tile.temperature);
    }

    public static bool IsAqueductEnvelope(Tile tile)
    {
        return IsLand(tile) && HasRiver(tile) && (IsCentralValleyEnvelope(tile) || IsBrahminWoodsEnvelope(tile));
    }

    public static bool IsMojaveEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowOrMediumElevation(tile) && IsArid(tile.temperature);
    }

    public static bool IsBayEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowElevation(tile) && IsCoastal(tile) && IsBorealToArid(tile.temperature);
    }

    public static bool IsRedwoodEnvelope(Tile tile)
    {
        return IsLand(tile) && IsHighElevation(tile) && (IsBoreal(tile.temperature) || IsLowerTemperateHalf(tile.temperature));
    }

    public static bool IsSequoiaEnvelope(Tile tile)
    {
        return IsLand(tile) && IsHighElevation(tile) && (IsUpperTemperateHalf(tile.temperature) || IsArid(tile.temperature));
    }

    public static bool IsDivideEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowOrMediumElevation(tile) && IsHot(tile.temperature);
    }

    public static bool IsMesaEnvelope(Tile tile)
    {
        return IsLand(tile) && IsHighElevation(tile) && IsHot(tile.temperature);
    }

    public static float SelectChanceScore(int tileId, int salt, float chance, float selectedScore, float rejectedScore)
    {
        return Roll01(tileId, salt) <= chance ? selectedScore : rejectedScore;
    }

    public static bool RollUnder(int tileId, int salt, float chance)
    {
        return Roll01(tileId, salt) <= chance;
    }

    public static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        return value > 1f ? 1f : value;
    }

    private static float Roll01(int tileId, int salt)
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
}