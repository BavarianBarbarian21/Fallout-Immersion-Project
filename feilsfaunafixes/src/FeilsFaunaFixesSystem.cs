using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Newtonsoft.Json.Linq;

namespace FeilsFaunaFixes;

public sealed class FeilsFaunaFixesSystem : ModSystem
{
    public static FaunaConfig Config { get; private set; } = new();
    public static ILogger? Log { get; private set; }

    public override void StartPre(ICoreAPI api)
    {
        try
        {
            Config = api.LoadModConfig<FaunaConfig>("feilsfaunafixes.json") ?? new FaunaConfig();
            api.StoreModConfig(Config, "feilsfaunafixes.json");
        }
        catch (Exception e)
        {
            api.Logger.Warning("[FeilsFaunaFixes] Invalid config, using defaults: {0}", e.Message);
            Config = new FaunaConfig();
        }
    }

    public override void Start(ICoreAPI api)
    {
        Log = api.Logger;
        api.RegisterBlockClass("FaunaTroughSmall", typeof(BlockFaunaTroughSmall));
        api.RegisterBlockClass("FaunaTroughLarge", typeof(BlockFaunaTroughLarge));
        api.RegisterBlockEntityClass("FaunaTrough", typeof(BlockEntityFaunaTrough));
        api.RegisterEntityBehaviorClass("faunahydration", typeof(EntityBehaviorHydration));
        api.RegisterEntityBehaviorClass("faunasquirrelclimber", typeof(EntityBehaviorSquirrelClimber));
        api.RegisterEntityBehaviorClass("faunadeathanimation", typeof(EntityBehaviorDeathAnimation));
        api.RegisterEntityBehaviorClass("faunafoliagephysics", typeof(EntityBehaviorFoliagePhysics));
        AiTaskRegistry.Register<AiTaskSeekWater>("faunaseekwater");
        AiTaskRegistry.Register<AiTaskHydratedLayEgg>("faunahydratedlayegg");
        AiTaskRegistry.Register<AiTaskSquirrelForage>("faunasquirrelforage");
        AiTaskRegistry.Register<AiTaskSquirrelClimbEscape>("faunasquirrelclimbescape");
        AiTaskRegistry.Register<AiTaskSquirrelDescend>("faunasquirreldescend");
    }

    public override double ExecuteOrder() => 0.1;

    public override void AssetsLoaded(ICoreAPI api)
    {
        ApplyConfiguredAssetValues(api, "recipes/grid/hornpieces.json", root =>
        {
            if (root is not JArray recipes) return;
            foreach (JToken recipe in recipes)
            {
                string antlerCode = recipe["ingredients"]?["A"]?["code"]?.Value<string>() ?? "";
                string stage = antlerCode.Split('-').LastOrDefault() ?? "";
                if (Config.HornPieceYields.TryGetValue(stage, out int quantity))
                {
                    recipe["output"]!["quantity"] = Math.Max(1, quantity);
                }
            }
        });

        ApplyConfiguredAssetValues(api, "recipes/cooking/hartshornsalt.json", root =>
        {
            if (root is not JArray recipes) return;
            foreach (JToken recipe in recipes)
            {
                recipe["perishableProps"]!["freshHours"]!["avg"] = Math.Max(0.1f, Config.HartshornCookingHours);
            }
        });

        foreach (string itemPath in new[] { "itemtypes/food/cookie.json" })
        {
            ApplyConfiguredAssetValues(api, itemPath, root =>
            {
                JToken? freshHours = root["transitionableProps"]?[0]?["freshHours"]?["avg"];
                if (freshHours != null) freshHours.Replace(192f * Math.Max(1f, Config.CookieShelfLifeMultiplier));
            });
        }
    }

    private static void ApplyConfiguredAssetValues(ICoreAPI api, string path, Action<JToken> update)
    {
        IAsset? asset = api.Assets.TryGet(new AssetLocation("feilsfaunafixes", path));
        if (asset == null)
        {
            api.Logger.Warning("[FeilsFaunaFixes] Configurable asset not found: {0}", path);
            return;
        }

        try
        {
            JToken root = JToken.Parse(asset.ToText());
            update(root);
            asset.Data = Encoding.UTF8.GetBytes(root.ToString());
            asset.IsPatched = true;
        }
        catch (Exception e)
        {
            api.Logger.Error("[FeilsFaunaFixes] Failed to apply config to {0}: {1}", path, e.Message);
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Logger.Notification("[FeilsFaunaFixes] Loaded successfully.");
    }

    public static void Debug(string message, params object[] args)
    {
        if (Config.Debug) Log?.Debug("[FeilsFaunaFixes] " + message, args);
    }
}
