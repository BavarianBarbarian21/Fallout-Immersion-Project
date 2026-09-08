using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using RimWorld;
using Verse;

internal static class Bootstrap
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: ModOptionsRegression.exe <repository> <RimWorld Managed directory>"); return 2; }
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string path = Path.Combine(args[1], new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Launch(args[0]); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Launch(string path) => Program.Execute(path);
}

internal static class Program
{
    private static string root;
    private static int checks;
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    internal static void Execute(string path)
    {
        root = Path.GetFullPath(path);
        Run();
        Console.WriteLine($"PASS: {checks} mod-option regression checks against the shipped assemblies.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        // The player normally initializes preferences before XML patching.
        var prefs = typeof(Prefs).GetField("data", All);
        prefs.SetValue(null, FormatterServices.GetUninitializedObject(prefs.FieldType));
        foreach (string path in new[] {
            "FIP-Arktos/LoadFolders/Arktos/Assemblies/FIP_Arktos_Settings.dll",
            "FIP-Donaustahl/LoadFolders/Donaustahl/Assemblies/FIP_Donaustahl_Settings.dll",
            "FIP-Greenway/LoadFolders/Greenway/Assemblies/FIP_Greenway.dll",
            "FIP-H&HTools/LoadFolders/HHTools/Assemblies/FIP_HHTools.dll",
            "FIP-Hubris/LoadFolders/Hubris/Assemblies/FIP_Hubris_Settings.dll",
            "FIP-Lucky 38/LoadFolders/Lucky38/Assemblies/FIP_Lucky38_Settings.dll",
            "FIP-RobCo/LoadFolders/RobCo/Assemblies/FIP_RobCo.dll",
            "FIP-WestTek/LoadFolders/WestTek/Assemblies/FIP_WestTek.dll",
            "FIP-Whitespring/LoadFolders/Whitespring/Assemblies/FIP_Whitespring_Settings.dll"
        }) Assembly.LoadFrom(Path.Combine(root, path));
        TestSettingsMigration();
        TestHHTools();
        TestArktos();
        TestGreenway();
        TestDonaustahlAndStorytellers();
        TestWestTekAndRobCoGates();
        TestRobCoRuntime();
        TestLucky38();
    }

    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(string type, string method, params object[] args) => Type(type).GetMethod(method, All).Invoke(null, args);
    private static object Settings(string modType, string settingsType, string field = "Settings")
    {
        object value = Activator.CreateInstance(Type(settingsType));
        Type(modType).GetField(field, All).SetValue(null, value);
        return value;
    }
    private static void Set(object value, string field, bool enabled) => value.GetType().GetField(field).SetValue(value, enabled);
    private static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); checks++; }
    private static T Add<T>(T def) where T : Def, new() { DefDatabase<T>.Add(def); return def; }
    // BuildableDef's constructor loads Unity shaders. Test fixtures only need
    // the explicitly supplied fields; the appliers themselves are unmodified.
    private static ThingDef Thing(string name, Action<ThingDef> configure = null)
    {
        var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        def.defName = name;
        configure?.Invoke(def);
        return def;
    }
    private static XmlDocument Xml(string text) { var xml = new XmlDocument(); xml.LoadXml(text); return xml; }
    private static void Patch(XmlDocument data, XmlNode operation)
    {
        var patch = ReadPatch(operation);
        Check(patch.Apply(data), "XML patch succeeds: " + operation.Attributes["Class"].Value);
    }
    private static PatchOperation ReadPatch(XmlNode node)
    {
        string name = node.Attributes["Class"].Value;
        var patch = (PatchOperation)Activator.CreateInstance(Type(name.Contains(".") ? name : "Verse." + name));
        // Populate the real patch objects without booting RimWorld's global XML
        // inheritance/type discovery (which requires a running Unity player).
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            FieldInfo field = null;
            for (var type = patch.GetType(); type != null && field == null; type = type.BaseType) field = type.GetField(child.Name, All);
            if (field == null) throw new Exception("Unsupported patch field: " + child.Name);
            object value;
            if (child.Name == "operations") value = child.ChildNodes.Cast<XmlNode>().Where(c => c.NodeType == XmlNodeType.Element).Select(ReadPatch).ToList();
            else if (child.Name == "match" || child.Name == "nomatch") value = ReadPatch(child);
            else if (child.Name == "value")
            {
                value = Activator.CreateInstance(field.FieldType);
                field.FieldType.GetField("node", All).SetValue(value, child);
            }
            else if (field.FieldType.IsEnum) value = Enum.Parse(field.FieldType, child.InnerText);
            else value = child.InnerText;
            field.SetValue(patch, value);
        }
        return patch;
    }
    private static void PatchFile(XmlDocument data, string relativePath)
    {
        var xml = new XmlDocument(); xml.Load(Path.Combine(root, relativePath));
        foreach (XmlNode operation in xml.SelectNodes("/Patch/Operation")) Patch(data, operation);
    }

    private static void TestSettingsMigration()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("FIP"))
            .SelectMany(a => a.GetTypes()).Where(t => typeof(ModSettings).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        int optionCount = 0;
        foreach (Type type in types)
        foreach (FieldInfo field in type.GetFields().Where(f => f.Name.StartsWith("onlyImmersive")))
        {
            optionCount++;
            string legacy = "restore" + field.Name.Substring("onlyImmersive".Length);
            foreach (bool enabled in new[] { false, true })
            {
                var settings = (ModSettings)Activator.CreateInstance(type);
                Scribe.mode = LoadSaveMode.LoadingVars;
                Scribe.loader.curXmlParent = Xml($"<settings><{field.Name}>{enabled}</{field.Name}></settings>").DocumentElement;
                settings.ExposeData();
                Check((bool)field.GetValue(settings) == enabled, type.Name + "." + field.Name + " persists");
                Scribe.loader.curXmlParent = Xml($"<settings><{legacy}>{!enabled}</{legacy}></settings>").DocumentElement;
                settings.ExposeData();
                Check((bool)field.GetValue(settings) == enabled, type.Name + "." + field.Name + " migrates inverse Restore value");
                Scribe.loader.curXmlParent = Xml($"<settings><{field.Name}>{enabled}</{field.Name}><{legacy}>{enabled}</{legacy}></settings>").DocumentElement;
                settings.ExposeData();
                Check((bool)field.GetValue(settings) == enabled, "new setting takes precedence over legacy");
            }
            Scribe.mode = LoadSaveMode.Inactive;
            Scribe.loader.curXmlParent = null;
        }
        Check(optionCount == 25, "all 25 options covered");
    }

    private static void TestHHTools()
    {
        var legacySettings = (ModSettings)Activator.CreateInstance(Type("FIP.HHTools.HHToolsModSettings"));
        Scribe.mode = LoadSaveMode.LoadingVars;
        Scribe.loader.curXmlParent = Xml("<settings><onlyImmersiveBuildings>false</onlyImmersiveBuildings></settings>").DocumentElement;
        legacySettings.ExposeData();
        Check(!(bool)legacySettings.GetType().GetField("onlyImmersiveWallStructures").GetValue(legacySettings)
            && !(bool)legacySettings.GetType().GetField("onlyImmersiveFurniture").GetValue(legacySettings), "legacy building option migrates to both precise building options");
        Scribe.mode = LoadSaveMode.Inactive;
        Scribe.loader.curXmlParent = null;

        var settings = Settings("FIP.HHTools.HHToolsMod", "FIP.HHTools.HHToolsModSettings");
        var structure = Add(new DesignationCategoryDef { defName = "Structure" });
        var walls = new[] { "Wall", "Door", "FCP_ConcreteWall", "OtherMod_Wall", "VFEM2_Palisade", "VFEM2_ArcheryTarget", "VFEM2_TrainingDummy", "VFEM2_Apiary", "VFEM2_AlchemicalWorkbench", "VFEM2_CobblestoneWall_Granite", "VFEM2_Hearth", "VFEM2_FurBed" }
            .Select(n => Add(Thing(n, d => { d.category = ThingCategory.Building; d.designationCategory = structure; }))).ToList();
        var faction = Add(new FactionDef { defName = "OutlanderCivil", displayInFactionSelection = true, startingCountAtWorldCreation = 3, requiredCountAtGameStart = 1, maxConfigurableAtWorldCreation = 7, settlementGenerationWeight = 0.6f });
        var scenario = Add(new ScenarioDef { defName = "VFEM2_NewKingdom", scenario = new Scenario { showInUI = true } });
        var quest = Add(new QuestScriptDef { defName = "VFEM2_OpportunitySite_Skirmish", rootSelectionWeight = 1.7f });
        var teller = Add(new StorytellerDef { defName = "VFEM_MaynardMedieval", listVisible = true });
        Call("FIP.HHTools.HHToolsRestoreApplier", "Apply", settings);
        Check(walls.Take(9).All(w => w.designationCategory == structure), "unrelated and explicitly retained Medieval buildings remain buildable");
        Check(walls.Skip(9).All(w => w.designationCategory == null), "selected Medieval walls and furniture hidden");
        Check(!faction.displayInFactionSelection && faction.startingCountAtWorldCreation == 0 && !scenario.scenario.showInUI && quest.rootSelectionWeight == 0 && !teller.listVisible, "H&H named content hidden");

        Set(settings, "onlyImmersiveWallStructures", false);
        Call("FIP.HHTools.HHToolsRestoreApplier", "Apply", settings);
        Check(walls[9].designationCategory == structure && walls[10].designationCategory == null && walls[11].designationCategory == null, "wall option restores walls without restoring furniture");

        Set(settings, "onlyImmersiveWallStructures", true);
        Set(settings, "onlyImmersiveFurniture", false);
        Call("FIP.HHTools.HHToolsRestoreApplier", "Apply", settings);
        Check(walls[9].designationCategory == null && walls[10].designationCategory == structure && walls[11].designationCategory == structure, "furniture option restores furniture without restoring walls");

        foreach (string field in new[] { "onlyImmersiveWallStructures", "onlyImmersiveFurniture", "onlyImmersiveFactions", "onlyImmersiveScenarios", "onlyImmersiveQuests", "onlyImmersiveStorytellers" }) Set(settings, field, false);
        Call("FIP.HHTools.HHToolsRestoreApplier", "Apply", settings);
        Check(walls.All(w => w.designationCategory == structure), "building categories restored exactly");
        Check(faction.displayInFactionSelection && faction.startingCountAtWorldCreation == 3 && faction.requiredCountAtGameStart == 1 && faction.maxConfigurableAtWorldCreation == 7 && faction.settlementGenerationWeight == 0.6f && scenario.scenario.showInUI && quest.rootSelectionWeight == 1.7f && teller.listVisible, "H&H source values restored");

        const string path = "FIP-H&HTools/LoadFolders/Medieval2/Patches/FIP-H&HTools/HHTools_OptionalMedievalContent.xml";
        const string norse = "FIP-H&HTools/LoadFolders/Medieval2_Ideology/Patches/FIP-H&HTools/HHTools_VFEMedieval2_Ideology_Compat.xml";
        string source = "<Defs>" + string.Join("", new[] { "VFEM2_MeleeWeapon_Sword", "VFEM2_Apparel_Helmet", "VFEM2_Apparel_TorchBelt", "FCP_Gun", "Apparel_Duster" }.Select(n => $"<ThingDef><defName>{n}</defName><generateCommonality>0.7</generateCommonality><generateAllowChance>0.8</generateAllowChance><recipeMaker><recipeUsers><li>Smithy</li></recipeUsers></recipeMaker></ThingDef>"))
            + "<PawnKindDef><defName>VFEM2_TestPawn</defName><weaponTags><li>VFEM2_Warbow</li><li>Gun</li></weaponTags><apparelRequired><li>VFEM2_Apparel_Helmet</li><li>Apparel_Pants</li></apparelRequired></PawnKindDef>"
            + "<ThingDef><defName>VFEM2_Hardweave</defName><thingCategories><li>Textiles</li></thingCategories></ThingDef><VFEMedieval.CobblestoneWallTemplateDef><designatorDropdown>Walls</designatorDropdown></VFEMedieval.CobblestoneWallTemplateDef><MemeDef><defName>VFEM2_Structure_Norse</defName><randomizationSelectionWeightFactor>1</randomizationSelectionWeightFactor></MemeDef></Defs>";
        foreach (bool enabled in new[] { false, true, false })
        {
            foreach (string field in new[] { "onlyImmersiveWeapons", "onlyImmersiveApparel", "onlyImmersiveWallStructures", "onlyImmersiveIdeologyOrigins" }) Set(settings, field, enabled);
            var xml = Xml(source); PatchFile(xml, path); PatchFile(xml, norse);
            if (!enabled) { Check(xml.OuterXml == Xml(source).OuterXml, "disabled H&H source gates leave all XML unchanged"); continue; }
            Check(xml.SelectNodes("/Defs/ThingDef[starts-with(defName,'VFEM2_')]/recipeMaker/recipeUsers/li").Count == 0, "Medieval crafting recipe users removed before generation");
            Check(xml.SelectNodes("/Defs/ThingDef[defName='FCP_Gun' or defName='Apparel_Duster']/recipeMaker/recipeUsers/li").Count == 2, "unrelated crafting intact");
            Check(xml.SelectSingleNode("/Defs/ThingDef[defName='VFEM2_MeleeWeapon_Sword']/generateAllowChance").InnerText == "0.8", "weapon allow chance not broadened beyond original patch");
            Check(xml.SelectSingleNode("/Defs/ThingDef[defName='VFEM2_Apparel_TorchBelt']/generateAllowChance").InnerText == "0", "TorchBelt original special rule");
            Check(xml.SelectSingleNode("/Defs/ThingDef[defName='VFEM2_Hardweave']/thingCategories/li").InnerText == "Textiles", "Medieval textiles remain fully available");
            Check(xml.SelectSingleNode("/Defs/PawnKindDef[defName='VFEM2_TestPawn']/weaponTags/li[.='VFEM2_Warbow']") == null, "direct Medieval weapon tag removed from pawn generation");
            Check(xml.SelectSingleNode("/Defs/PawnKindDef[defName='VFEM2_TestPawn']/weaponTags/li[.='Gun']") != null, "unrelated pawn weapon tag retained");
            Check(xml.SelectSingleNode("/Defs/PawnKindDef[defName='VFEM2_TestPawn']/apparelRequired/li[.='VFEM2_Apparel_Helmet']") == null, "required Medieval apparel removed from pawn generation");
            Check(xml.SelectSingleNode("/Defs/PawnKindDef[defName='VFEM2_TestPawn']/apparelRequired/li[.='Apparel_Pants']") != null, "vanilla required apparel retained");
            Check(xml.SelectSingleNode("/Defs/MemeDef/hiddenInChooseMemes").InnerText == "true", "Norse origin gate");
        }
        Set(settings, "onlyImmersiveWeapons", true);
        var mixed = Xml(source); PatchFile(mixed, path);
        Check(mixed.SelectNodes("/Defs/ThingDef[starts-with(defName,'VFEM2_Apparel_')]/recipeMaker/recipeUsers/li").Count == 2, "weapons switch leaves apparel independent");
    }

    private static void TestArktos()
    {
        var settings = Settings("FIP.Arktos.ArktosSettingsMod", "FIP.Arktos.ArktosSettings");
        string[] options = { "onlyImmersiveNativeWildlife", "onlyImmersiveBiotechWildlife", "onlyImmersiveVanillaAnimalsExpandedWildlife", "onlyImmersiveRoyalAnimalsWildlife", "onlyImmersiveOdysseyWildlife" };
        string[] files =
        {
            "FIP-Arktos/LoadFolders/Arktos/Patches/FIP-Arktos/Nature/Arktos_VanillaAnimalRemovalPatch.xml",
            "FIP-Arktos/LoadFolders/Biotech/Patches/FIP-Arktos/Nature/Arktos_BiotechAnimalRemovalPatch.xml",
            "FIP-Arktos/LoadFolders/Animals/Patches/FIP-Arktos/Compatch/Arktos_VAE_AnimalRemovalPatch.xml",
            "FIP-Arktos/LoadFolders/RoyalAnimals/Patches/FIP-Arktos/Compatch/Arktos_VAE_RoyalAnimalRemovalPatch.xml",
            "FIP-Arktos/LoadFolders/Odyssey/Patches/FIP-Arktos/Nature/Arktos_OdysseyAnimalRemovalPatch.xml"
        };
        const string source = "<Defs>"
            + "<BiomeDef><defName>TestBiome</defName><wildAnimals><Elephant>1</Elephant><Toxalope>1</Toxalope><AEXP_Lion>1</AEXP_Lion><VAERoy_RoyalTiger>1</VAERoy_RoyalTiger><Tiger>1</Tiger></wildAnimals></BiomeDef>"
            + "<ThingDef><defName>Elephant</defName><tradeTags><li>AnimalCommon</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>Toxalope</defName><tradeTags><li>AnimalUncommon</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>AEXP_Lion</defName><tradeTags><li>AnimalUncommon</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>VAERoy_RoyalTiger</defName><tradeTags><li>AnimalExotic</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>Tiger</defName><tradeTags><li>AnimalExotic</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>OtherMod_Animal</defName><tradeTags><li>AnimalCommon</li></tradeTags><race><canArriveManhunter>true</canArriveManhunter></race></ThingDef>"
            + "<ThingDef><defName>EggChickenFertilized</defName><tradeability>All</tradeability><comps><li><hatcherPawn>Chicken</hatcherPawn></li></comps></ThingDef>"
            + "<ThingDef><defName>EggEmuUnfertilized</defName><tradeability>All</tradeability></ThingDef>"
            + "<ThingDef><defName>AEXP_EggCrocodileFertilized</defName><tradeability>All</tradeability><comps><li><hatcherPawn>AEXP_Crocodile</hatcherPawn></li></comps></ThingDef>"
            + "<ThingDef><defName>AEXP_EggPlatypusUnfertilized</defName><tradeability>All</tradeability></ThingDef>"
            + "<ThingDef><defName>VAERoy_EggMegaChickenFertilized</defName><tradeability>All</tradeability><comps><li><hatcherPawn>VAERoy_Megachicken</hatcherPawn></li></comps></ThingDef>"
            + "<ThingDef><defName>VAERoy_EggMegaChickenUnfertilized</defName><tradeability>All</tradeability></ThingDef>"
            + "<ThingDef><defName>EggFlamingoFertilized</defName><tradeability>All</tradeability></ThingDef>"
            + "<ThingDef><defName>EggAlligatorUnfertilized</defName><tradeability>All</tradeability></ThingDef>"
            + "<ThingSetMakerDef><defName>RareFishingCatches_Hot</defName><items><li>EggFlamingoFertilized</li><li><thingSetMaker><pawnKind>Crow</pawnKind></thingSetMaker></li></items></ThingSetMakerDef>"
            + "<ThingSetMakerDef><defName>DedicatedCrowEvent</defName><items><li><thingSetMaker><pawnKind>Crow</pawnKind></thingSetMaker></li></items></ThingSetMakerDef>"
            + "<FactionDef><defName>TestFaction</defName><carriers><Elephant>1</Elephant><OtherMod_Animal>1</OtherMod_Animal></carriers></FactionDef>"
            + "</Defs>";

        foreach (string option in options) Set(settings, option, false);
        for (int i = 0; i < files.Length; i++)
        {
            var disabled = Xml(source);
            PatchFile(disabled, files[i]);
            Check(disabled.OuterXml == Xml(source).OuterXml, "disabled Arktos gate leaves source XML unchanged: " + options[i]);
        }

        for (int i = 0; i < files.Length; i++)
        {
            foreach (string option in options) Set(settings, option, false);
            Set(settings, options[i], true);
            var enabled = Xml(source);
            PatchFile(enabled, files[i]);
            Check(enabled.SelectNodes("/Defs/BiomeDef/wildAnimals/*").Count == 5, "Arktos leaves biome spawn lists untouched: " + options[i]);
            Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='OtherMod_Animal']/tradeTags/li") != null, "Arktos preserves unrelated trader tags: " + options[i]);

            if (i == 0)
            {
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Elephant']/tradeTags/li") == null, "native option removes original trader tag");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Elephant']/race/canArriveManhunter").InnerText == "false", "native option blocks generic manhunter selection");
                Check(enabled.SelectSingleNode("/Defs/FactionDef/carriers/Elephant") == null, "native option removes original faction dictionary target");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='EggChickenFertilized']/tradeability").InnerText == "None", "native option keeps egg def while preventing trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='EggEmuUnfertilized']/tradeability").InnerText == "None", "native option also prevents unfertilized egg trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='AEXP_Lion']/tradeTags/li") != null, "native option leaves VAE independent");
            }
            else if (i == 1)
            {
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Toxalope']/tradeTags/li") == null, "Biotech option removes trader tag");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Toxalope']/race/canArriveManhunter").InnerText == "false", "Biotech option blocks generic manhunter selection");
            }
            else if (i == 2)
            {
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='AEXP_Lion']/tradeTags/li") == null, "VAE option removes lion trader tag");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='AEXP_Lion']/race/canArriveManhunter").InnerText == "false", "VAE option blocks lion in generic manhunter incidents");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='AEXP_EggCrocodileFertilized']/tradeability").InnerText == "None", "VAE option keeps egg def while preventing trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='AEXP_EggPlatypusUnfertilized']/tradeability").InnerText == "None", "VAE option also prevents unfertilized egg trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Elephant']/tradeTags/li") != null, "VAE option leaves native wildlife independent");
            }
            else if (i == 3)
            {
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='VAERoy_RoyalTiger']/tradeTags/li") == null, "Royal Animals option removes trader tag");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='VAERoy_RoyalTiger']/race/canArriveManhunter").InnerText == "false", "Royal Animals option blocks generic manhunter selection");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='VAERoy_EggMegaChickenFertilized']/tradeability").InnerText == "None", "Royal Animals option keeps egg def while preventing trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='VAERoy_EggMegaChickenUnfertilized']/tradeability").InnerText == "None", "Royal Animals option also prevents unfertilized egg trader stock");
            }
            else
            {
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='Tiger']/tradeTags/li") == null, "Odyssey option removes trader tag");
                Check(enabled.SelectSingleNode("/Defs/ThingSetMakerDef[defName='RareFishingCatches_Hot']//li[normalize-space(.)='EggFlamingoFertilized']") == null, "Odyssey option removes egg from generic rare fishing catches");
                Check(enabled.SelectSingleNode("/Defs/ThingSetMakerDef[defName='RareFishingCatches_Hot']//li[thingSetMaker/pawnKind='Crow']") == null, "Odyssey option removes animal from generic rare fishing catches");
                Check(enabled.SelectSingleNode("/Defs/ThingSetMakerDef[defName='DedicatedCrowEvent']//li[thingSetMaker/pawnKind='Crow']") != null, "Odyssey option preserves dedicated animal content");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='EggFlamingoFertilized']/tradeability").InnerText == "None", "Odyssey option keeps egg def while preventing trader stock");
                Check(enabled.SelectSingleNode("/Defs/ThingDef[defName='EggAlligatorUnfertilized']/tradeability").InnerText == "None", "Odyssey option also prevents unfertilized egg trader stock");
            }
        }
    }

    private static ModContentPack Package(string id)
    {
        var pack = (ModContentPack)FormatterServices.GetUninitializedObject(typeof(ModContentPack));
        typeof(ModContentPack).GetField("packageIdInt", All).SetValue(pack, id);
        return pack;
    }
    private static void TestGreenway()
    {
        var native = Package("ludeon.rimworld.ideology"); var ve = Package("vanillaexpanded.vmemese");
        var origin = Add(new MemeDef { defName = "Structure_Animist", category = MemeCategory.Structure, modContentPack = native, randomizationSelectionWeightFactor = 0.6f });
        var meme = Add(new MemeDef { defName = "Cannibal", category = MemeCategory.Normal, modContentPack = native, randomizationSelectionWeightFactor = 0.8f });
        var anonymity = Add(new MemeDef { defName = "VME_Anonymity", category = MemeCategory.Normal, modContentPack = ve, randomizationSelectionWeightFactor = 0.7f });
        var serket = Add(new MemeDef { defName = "VME_Structure_Serketist", category = MemeCategory.Structure, modContentPack = ve, randomizationSelectionWeightFactor = 0.9f });
        var unrelated = Add(new MemeDef { defName = "VME_InsectoidSupremacy", category = MemeCategory.Normal, modContentPack = ve, randomizationSelectionWeightFactor = 1f });
        var otherOrigin = Add(new MemeDef { defName = "VME_Structure_Authoritarianism", category = MemeCategory.Structure, modContentPack = ve, randomizationSelectionWeightFactor = 1f });
        var referenceFaction = Add(new FactionDef { defName = "TestMemeFaction", allowedMemes = new List<MemeDef> { anonymity, unrelated }, structureMemeWeights = new List<MemeWeight> { new MemeWeight { meme = serket, selectionWeight = 0.3f }, new MemeWeight { meme = otherOrigin, selectionWeight = 0.7f } } });
        foreach (bool hideOrigins in new[] { false, true }) foreach (bool hideMemes in new[] { false, true })
        {
            Call("FIP.Greenway.GreenwayVanillaMemeApplier", "Apply", hideMemes);
            Call("FIP.Greenway.GreenwayVanillaIdeologyOriginApplier", "Apply", hideOrigins);
            Call("FIP.Greenway.GreenwayVanillaExpandedFactionMemeApplier", "Apply", hideMemes, hideOrigins);
            Check(origin.hiddenInChooseMemes == hideOrigins && serket.hiddenInChooseMemes == hideOrigins, "origins independent from memes");
            Check(meme.hiddenInChooseMemes == hideMemes && anonymity.hiddenInChooseMemes == hideMemes, "only original meme targets hidden");
            Check(!unrelated.hiddenInChooseMemes && !otherOrigin.hiddenInChooseMemes && unrelated.randomizationSelectionWeightFactor == 1f, "other VE content retained");
            Check(referenceFaction.allowedMemes.Contains(anonymity) == !hideMemes && referenceFaction.allowedMemes.Contains(unrelated), "faction meme references follow only their own switch");
            Check(referenceFaction.structureMemeWeights.Any(w => w.meme == serket) == !hideOrigins && referenceFaction.structureMemeWeights.Any(w => w.meme == otherOrigin && w.selectionWeight == 0.7f), "faction origins retain unrelated weights");
        }
        Call("FIP.Greenway.GreenwayVanillaMemeApplier", "Apply", false); Call("FIP.Greenway.GreenwayVanillaIdeologyOriginApplier", "Apply", false);
        Check(origin.randomizationSelectionWeightFactor == 0.6f && meme.randomizationSelectionWeightFactor == 0.8f, "Greenway restores original weights");
        var faction = Add(new FactionDef { defName = "TribeCannibal", hidden = false, displayInFactionSelection = true, maxConfigurableAtWorldCreation = 4 });
        Call("FIP.Greenway.GreenwayVanillaIdeologyFactionApplier", "Apply", true);
        Check(faction.hidden && !faction.displayInFactionSelection, "Greenway faction hidden");
        Call("FIP.Greenway.GreenwayVanillaIdeologyFactionApplier", "Apply", false);
        Check(!faction.hidden && faction.displayInFactionSelection && faction.maxConfigurableAtWorldCreation == 4, "Greenway faction restored");
    }

    private static void TestDonaustahlAndStorytellers()
    {
        var settings = Settings("FIP.Donaustahl.DonaustahlSettingsMod", "FIP.Donaustahl.DonaustahlSettings");
        var teller = Add(new StorytellerDef { defName = "Cassandra", listVisible = true });
        var story = Add(new BackstoryDef { defName = "VBE_Oracle", shuffleable = true });
        var relic = Add(Thing("TestApparel", d => { d.apparel = new ApparelProperties(); d.relicChance = 0.37f; }));
        var untouched = Add(Thing("TestGunDisplayCase", d => { d.category = ThingCategory.Building; d.relicChance = 0.23f; }));
        foreach (bool enabled in new[] { true, false })
        {
            foreach (string field in new[] { "onlyImmersiveStorytellers", "onlyImmersiveBackstories", "onlyImmersiveEquipmentRelics" }) Set(settings, field, enabled);
            Call("FIP.Donaustahl.DonaustahlRestoreApplier", "Apply", settings);
            Check(teller.listVisible == !enabled && story.shuffleable == !enabled && relic.relicChance == (enabled ? 0f : 0.37f) && untouched.relicChance == 0.23f, "Donaustahl restores only its three target groups");
        }
        foreach (var entry in new[] {
            new[] { "FIP.Hubris.HubrisSettingsMod", "FIP.Hubris.HubrisSettings", "VPE_Basilicus" },
            new[] { "FIP.Whitespring.WhitespringSettingsMod", "FIP.Whitespring.WhitespringSettings", "VFEE_AriadneArchduchess" }
        })
        {
            var value = Settings(entry[0], entry[1], "settings"); var def = Add(new StorytellerDef { defName = entry[2], listVisible = true });
            Call(entry[0], "CaptureAndApply"); Check(!def.listVisible, "storyteller hidden: " + def.defName);
            Set(value, "onlyImmersiveStorytellers", false); Call(entry[0], "CaptureAndApply"); Check(def.listVisible, "storyteller restored: " + def.defName);
        }
    }

    private static void TestWestTekAndRobCoGates()
    {
        foreach (var entry in new[] {
            new[] { "FIP.WestTek.WestTekMod", "FIP.WestTek.WestTekModSettings", "onlyImmersiveXenotypes", "FIP.WestTek" },
            new[] { "FIP.RobCo.RobCoMod", "FIP.RobCo.RobCoModSettings", "onlyImmersiveMechanoids", "FIP.RobCo" }
        })
        {
            var settings = Settings(entry[0], entry[1]);
            string suffix = entry[2] == "onlyImmersiveXenotypes" ? "Xenotypes" : "Mechanoids";
            foreach (bool enabled in new[] { true, false }) foreach (string op in new[] { "Replace", "Add", "Remove" })
            {
                Set(settings, entry[2], enabled);
                var data = Xml("<Defs><ThingDef><defName>Target</defName><value>source</value></ThingDef></Defs>"); string before = data.OuterXml;
                string xpath = op == "Add" ? "Defs/ThingDef" : "Defs/ThingDef/value";
                var patch = Xml($"<Operation Class='{entry[3]}.PatchOperation{op}UnlessRestore{suffix}'><xpath>{xpath}</xpath>" + (op == "Remove" ? "" : "<value><value>changed</value></value>") + "</Operation>");
                Patch(data, patch.DocumentElement); Check((data.OuterXml != before) == enabled, entry[3] + " " + op + " respects disabled source state");
            }
        }
        Check(!Type("FIP.WestTek.WestTekMod").Assembly.GetTypes().Any(t => t.Name == "WestTekXenotypeRosterApplier"), "WestTek adds no global third-party blacklist");
    }

    private static void TestLucky38()
    {
        var brewing = Add(new ResearchTabDef { defName = "VCE_Brewing" });
        var cooking = Add(new ResearchTabDef { defName = "VCE_Cooking" });
        var research = Add(new ResearchProjectDef { defName = "VBE_LiquorBrewing", tab = brewing, researchViewX = 7, researchViewY = 3 });
        Call("FIP.Lucky38.Lucky38ResearchTreeApplier", "Apply", true);
        Check(research.tab == cooking && research.researchViewX == 3 && DefDatabase<ResearchTabDef>.GetNamedSilentFail("VCE_Brewing") == null, "Lucky38 original brewing integration");
        Call("FIP.Lucky38.Lucky38ResearchTreeApplier", "Apply", false);
        Check(research.tab == brewing && research.researchViewX == 7 && research.researchViewY == 3 && DefDatabase<ResearchTabDef>.GetNamedSilentFail("VCE_Brewing") == brewing, "Lucky38 restores source tab and coordinates");
    }

    private static void TestRobCoRuntime()
    {
        var vanillaRecipe = Add(new RecipeDef { defName = "MakeMilitor" });
        var robcoRecipe = Add(new RecipeDef { defName = "RobCo_Eyebot_Recipe" });
        var bench = Add(Thing("MechGestator", d => d.recipes = new List<RecipeDef> { vanillaRecipe }));
        var cache = typeof(ThingDef).GetField("allRecipesCached", All);
        cache.SetValue(bench, new List<RecipeDef> { vanillaRecipe });
        var race = Add(Thing("TestMechRace", d => d.race = new RaceProperties()));
        // Set a concrete flesh def without invoking RimWorld's DefOf bootstrap.
        var mechFlesh = Add(new FleshTypeDef { defName = "Mechanoid" });
        var binding = typeof(DefOfHelper).GetField("bindingNow", All);
        binding.SetValue(null, true);
        try { typeof(FleshTypeDefOf).GetField("Mechanoid").SetValue(null, mechFlesh); }
        finally { binding.SetValue(null, false); }
        typeof(RaceProperties).GetField("fleshType", All).SetValue(race.race, mechFlesh);
        var native = Add(new PawnKindDef { defName = "TestNativeMech", race = race, modContentPack = Package("ludeon.rimworld.biotech"), isFighter = true, allowInMechClusters = true });
        var other = Add(new PawnKindDef { defName = "OtherModMech", race = race, modContentPack = Package("other.mechs"), isFighter = true, allowInMechClusters = true });
        Call("FIP.RobCo.RobCoDefSettingsApplier", "Apply", true);
        Check(bench.recipes.SequenceEqual(new[] { robcoRecipe }) && !native.isFighter && !native.allowInMechClusters && other.isFighter, "RobCo suppresses original native mech sources only");
        Check(cache.GetValue(bench) == null, "RobCo invalidates crafting cache");
        Call("FIP.RobCo.RobCoDefSettingsApplier", "Apply", false);
        Check(bench.recipes.SequenceEqual(new[] { vanillaRecipe }) && native.isFighter && native.allowInMechClusters, "RobCo restores source recipes and native flags");
    }
}
