namespace FeilsFaunaFixes;

public sealed class FaunaConfig
{
    public bool Debug { get; set; } = false;
    public float SmallTroughCapacity { get; set; } = 8f;
    public float LargeTroughCapacity { get; set; } = 16f;
    public float BucketLitres { get; set; } = 10f;
    public float ChickenHydrationUsePerDay { get; set; } = 0.35f;
    public float SheepHydrationUsePerDay { get; set; } = 0.5f;
    public float DungAnimalHydrationUsePerDay { get; set; } = 1f;
    public float AnimalHydrationReserve { get; set; } = 2.5f;
    public float WaterEggProductionMultiplier { get; set; } = 0.6f;
    public float ShearingsPerYear { get; set; } = 2.5f;
    public int WoolPerShear { get; set; } = 1;
    public float DungProductionIntervalDays { get; set; } = 2f;
    public int DungPerProduction { get; set; } = 1;
    public int LargeGoatDungPerProduction { get; set; } = 2;
    public Dictionary<string, int> HornPieceYields { get; set; } = new()
    {
        ["01"] = 1, ["02"] = 2, ["03"] = 3, ["04"] = 4,
        ["05"] = 5, ["06"] = 6, ["07"] = 7, ["08"] = 8, ["09"] = 8
    };
    public float HartshornCookingHours { get; set; } = 6f;
    public float CookieShelfLifeMultiplier { get; set; } = 3.5f;
    public float SquirrelFleeDistance { get; set; } = 14f;
    public float SquirrelClimbSpeed { get; set; } = 0.045f;
    public int SquirrelMaxClimbHeight { get; set; } = 16;
    public int SquirrelMinimumEscapeTreeHeight { get; set; } = 6;
    public int SquirrelMaximumJumpGap { get; set; } = 2;
    public float SquirrelForageCooldownMinHours { get; set; } = 6f;
    public float SquirrelForageCooldownMaxHours { get; set; } = 12f;
}
