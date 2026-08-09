using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.Lucky38
{
    [StaticConstructorOnStartup]
    internal static class Lucky38TradingCompatibility
    {
        private const string HarmonyId = "FIP.Lucky38.VanillaTradingExpanded";
        private const string TradingManagerTypeName = "VanillaTradingExpanded.TradingManager";
        private const string BankTypeName = "VanillaTradingExpanded.Bank";
        private const string BankWindowTypeName = "VanillaTradingExpanded.Window_Bank";
        private const string TradingUtilsTypeName = "VanillaTradingExpanded.Utils";
        private const string TradingModTypeName = "VanillaTradingExpanded.VanillaTradingExpandedMod";
        private const string CapsDefName = "FCP_Currency_Caps";

        private static readonly string[] ApprovedListedSecurities =
        {
            "Friendly Lending Company",
            "Gun Runners",
            "Crimson Caravan Company",
            "Cassidy Caravans",
            "Far Go Traders",
            "Happy Trails Caravan Company",
            "Mojave Express",
            "Water Merchants",
            "Griffin Wares",
            "Durable Dunn's Caravan Outfit",
            "Blue Ridge Caravan Company",
            "Stockton's Caravan Company",
            "Westside Cooperative",
            "Littlehorn & Associates",
            "Talon Company",
            "Shark Club",
            "Desperado"
        };

        private static readonly FieldInfo SilverField = AccessTools.Field(typeof(ThingDefOf), nameof(ThingDefOf.Silver));
        private static readonly MethodInfo CurrencyGetter = AccessTools.Method(typeof(Lucky38TradingCompatibility), nameof(GetTradingCurrency));

        static Lucky38TradingCompatibility()
        {
            Type tradingManagerType = AccessTools.TypeByName(TradingManagerTypeName);
            if (tradingManagerType == null)
            {
                return;
            }

            Harmony harmony = new Harmony(HarmonyId);
            PatchCompanyNames(harmony, tradingManagerType);
            PatchCompanySetting(harmony);
            PatchTradingCurrency(harmony);
            LongEventHandler.ExecuteWhenFinished(AddCapsToTradingExclusions);
        }

        private static void PatchCompanyNames(Harmony harmony, Type tradingManagerType)
        {
            MethodInfo startup = AccessTools.Method(tradingManagerType, "Startup");
            if (startup == null)
            {
                Log.Warning("[FIP - Lucky 38] Vanilla Trading Expanded Startup method was not found; listed security names were not patched.");
                return;
            }

            harmony.Patch(
                startup,
                prefix: new HarmonyMethod(typeof(Lucky38TradingCompatibility), nameof(EnforceApprovedSecurityCount)),
                postfix: new HarmonyMethod(typeof(Lucky38TradingCompatibility), nameof(RenameListedSecurities)));
        }

        private static void PatchCompanySetting(Harmony harmony)
        {
            Type tradingModType = AccessTools.TypeByName(TradingModTypeName);
            MethodInfo settingsWindow = AccessTools.Method(tradingModType, "DoSettingsWindowContents");
            MethodInfo writeSettings = AccessTools.Method(tradingModType, "WriteSettings");

            if (settingsWindow != null)
            {
                harmony.Patch(
                    settingsWindow,
                    postfix: new HarmonyMethod(typeof(Lucky38TradingCompatibility), nameof(CapCompanySetting)));
            }

            if (writeSettings != null)
            {
                harmony.Patch(
                    writeSettings,
                    prefix: new HarmonyMethod(typeof(Lucky38TradingCompatibility), nameof(CapCompanySetting)));
            }
        }

        private static void EnforceApprovedSecurityCount(object __instance)
        {
            CapCompanySetting();

            if (__instance == null)
            {
                return;
            }

            IList companies = AccessTools.Field(__instance.GetType(), "companies")?.GetValue(__instance) as IList;
            if (companies == null)
            {
                return;
            }

            while (companies.Count > ApprovedListedSecurities.Length)
            {
                companies.RemoveAt(companies.Count - 1);
            }
        }

        private static void CapCompanySetting()
        {
            Type tradingModType = AccessTools.TypeByName(TradingModTypeName);
            object settings = AccessTools.Field(tradingModType, "settings")?.GetValue(null);
            FieldInfo maximumCompanyCount = settings == null
                ? null
                : AccessTools.Field(settings.GetType(), "maxCompanyCount");

            if (maximumCompanyCount?.GetValue(settings) is int configuredCount)
            {
                maximumCompanyCount.SetValue(settings, Math.Max(1, Math.Min(configuredCount, ApprovedListedSecurities.Length)));
            }
        }

        private static void RenameListedSecurities(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            FieldInfo companiesField = AccessTools.Field(__instance.GetType(), "companies");
            IList companies = companiesField?.GetValue(__instance) as IList;
            if (companies == null)
            {
                return;
            }

            for (int index = 0; index < companies.Count; index++)
            {
                object company = companies[index];
                if (company == null)
                {
                    continue;
                }

                Type companyType = company.GetType();
                FieldInfo nameField = AccessTools.Field(companyType, "name");
                nameField?.SetValue(company, ApprovedListedSecurities[index]);
            }
        }

        private static string GetApprovedSecurityName(int loadId)
        {
            int positiveId = loadId % ApprovedListedSecurities.Length;
            if (positiveId < 0)
            {
                positiveId += ApprovedListedSecurities.Length;
            }

            string approvedName = ApprovedListedSecurities[positiveId];
            int issue = Math.Abs(loadId / ApprovedListedSecurities.Length);
            return issue == 0 ? approvedName : approvedName + " — issue " + (issue + 1);
        }

        private static void PatchTradingCurrency(Harmony harmony)
        {
            Type bankType = AccessTools.TypeByName(BankTypeName);
            Type bankWindowType = AccessTools.TypeByName(BankWindowTypeName);

            PatchCurrencyMethod(harmony, AccessTools.Method(bankType, "WithdrawSilver"));
            PatchCurrencyMethod(harmony, AccessTools.Method(bankType, "TakeLoan"));
            PatchCurrencyMethod(harmony, AccessTools.Method(bankWindowType, "DoWindowContents"));
            PatchCurrencyMethod(harmony, AccessTools.PropertyGetter(bankWindowType, "AvailableSilver"));
        }

        private static void PatchCurrencyMethod(Harmony harmony, MethodInfo original)
        {
            if (original == null)
            {
                return;
            }

            harmony.Patch(
                original,
                transpiler: new HarmonyMethod(typeof(Lucky38TradingCompatibility), nameof(ReplaceSilverWithTradingCurrency)));
        }

        private static IEnumerable<CodeInstruction> ReplaceSilverWithTradingCurrency(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, SilverField))
                {
                    CodeInstruction replacement = new CodeInstruction(OpCodes.Call, CurrencyGetter);
                    replacement.labels.AddRange(instruction.labels);
                    replacement.blocks.AddRange(instruction.blocks);
                    yield return replacement;
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        public static ThingDef GetTradingCurrency()
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(CapsDefName) ?? ThingDefOf.Silver;
        }

        private static void AddCapsToTradingExclusions()
        {
            ThingDef caps = DefDatabase<ThingDef>.GetNamedSilentFail(CapsDefName);
            Type utilsType = AccessTools.TypeByName(TradingUtilsTypeName);
            object exclusions = AccessTools.Field(utilsType, "tradeableItemsToIgnore")?.GetValue(null);
            if (caps == null || exclusions == null)
            {
                return;
            }

            exclusions.GetType().GetMethod("Add", new[] { typeof(ThingDef) })?.Invoke(exclusions, new object[] { caps });
        }
    }
}
