using RimWorld;
using RimWorld.Planet;

namespace FIP.Repconn2;

public sealed class BiomeWorker_ManualOnly : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        return tile.WaterCovered ? -100f : -1000f;
    }
}

public sealed class BiomeWorker_Redwood : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsHighElevation(tile))
        {
            return -100f;
        }

        if (!BiomeScoring.IsBorealOrTemperate(tile.temperature) || tile.rainfall < 700f)
        {
            return -100f;
        }

        return 25f + BiomeScoring.Clamp01(tile.rainfall / 2000f) * 3f;
    }
}

public sealed class BiomeWorker_Sequoia : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsHighElevation(tile))
        {
            return -100f;
        }

        if (!BiomeScoring.IsTemperateOrArid(tile.temperature) || tile.rainfall < 450f)
        {
            return -100f;
        }

        return 24f + BiomeScoring.Clamp01(tile.rainfall / 1800f) * 2.5f;
    }
}

public sealed class BiomeWorker_CentralValley : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsLowElevation(tile))
        {
            return -100f;
        }

        if (!BiomeScoring.IsTemperateOrArid(tile.temperature) || tile.rainfall < 600f)
        {
            return -100f;
        }

        return 22f + BiomeScoring.Clamp01(tile.rainfall / 2000f) * 4f;
    }
}

public sealed class BiomeWorker_BrahminWoods : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsMediumElevation(tile))
        {
            return -100f;
        }

        if (!BiomeScoring.IsBorealOrTemperate(tile.temperature) || tile.rainfall < 500f)
        {
            return -100f;
        }

        return 21f + BiomeScoring.Clamp01(tile.rainfall / 1800f) * 3f;
    }
}

public sealed class BiomeWorker_Mojave : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsLowOrMediumElevation(tile))
        {
            return -100f;
        }

        if (!BiomeScoring.IsArid(tile.temperature) || tile.rainfall > 900f)
        {
            return -100f;
        }

        return 23f - BiomeScoring.Clamp01(tile.rainfall / 1000f) * 3f;
    }
}

public sealed class BiomeWorker_Canyon : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsMojaveEnvelope(tile) || !BiomeScoring.HasRiver(tile))
        {
            return -100f;
        }

        return BiomeScoring.WeightedRiverVariantScore(planetTile.tileId, 49185, 0.5f, 28f, 18f);
    }
}

public sealed class BiomeWorker_Aqueduct : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.HasRiver(tile) || !BiomeScoring.IsAqueductEnvelope(tile))
        {
            return -100f;
        }

        return BiomeScoring.WeightedRiverVariantScore(planetTile.tileId, 43293, 0.5f, 27f, 17f);
    }
}

public sealed class BiomeWorker_Divide : BiomeWorker
{
    public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
    {
        if (!BiomeScoring.IsLand(tile) || !BiomeScoring.IsHot(tile.temperature))
        {
            return -100f;
        }

        return 26f - BiomeScoring.Clamp01(tile.rainfall / 2000f) * 2f;
    }
}

internal static class BiomeScoring
{
    private const float ArcticMax = -5f;
    private const float BorealMax = 8f;
    private const float TemperateMax = 20f;
    private const float AridMax = 32f;

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

    public static bool IsLowOrMediumElevation(Tile tile)
    {
        return IsLowElevation(tile) || IsMediumElevation(tile);
    }

    public static bool IsBorealOrTemperate(float temperature)
    {
        return temperature > ArcticMax && temperature <= TemperateMax;
    }

    public static bool IsTemperateOrArid(float temperature)
    {
        return temperature > BorealMax && temperature <= AridMax;
    }

    public static bool IsArid(float temperature)
    {
        return temperature > TemperateMax && temperature <= AridMax;
    }

    public static bool IsHot(float temperature)
    {
        return temperature > AridMax;
    }

    public static bool IsMojaveEnvelope(Tile tile)
    {
        return IsLand(tile) && IsLowOrMediumElevation(tile) && IsArid(tile.temperature) && tile.rainfall <= 900f;
    }

    public static bool IsAqueductEnvelope(Tile tile)
    {
        if (!IsLand(tile) || tile.rainfall < 550f)
        {
            return false;
        }

        if (IsLowElevation(tile))
        {
            return IsTemperateOrArid(tile.temperature);
        }

        return IsMediumElevation(tile) && IsBorealOrTemperate(tile.temperature);
    }

    public static bool HasRiver(Tile tile)
    {
        return tile is SurfaceTile surfaceTile && surfaceTile.Rivers != null && surfaceTile.Rivers.Count > 0;
    }

    public static float WeightedRiverVariantScore(int tileId, int salt, float chance, float boostedScore, float fallbackScore)
    {
        return Roll01(tileId, salt) <= chance ? boostedScore : fallbackScore;
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