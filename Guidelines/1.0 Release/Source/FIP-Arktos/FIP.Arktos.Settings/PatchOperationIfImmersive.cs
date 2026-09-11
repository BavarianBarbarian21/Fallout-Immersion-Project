using System.Xml;
using Verse;

namespace FIP.Arktos;

public sealed class PatchOperationIfImmersive : PatchOperationSequence
{
    public string setting;

    protected override bool ApplyWorker(XmlDocument xml)
    {
        ArktosSettings settings = ArktosSettingsMod.Settings;
        bool enabled = setting switch
        {
            "native" => settings?.onlyImmersiveNativeWildlife ?? true,
            "biotech" => settings?.onlyImmersiveBiotechWildlife ?? true,
            "vanillaAnimalsExpanded" => settings?.onlyImmersiveVanillaAnimalsExpandedWildlife ?? true,
            "royalAnimals" => settings?.onlyImmersiveRoyalAnimalsWildlife ?? true,
            "odyssey" => settings?.onlyImmersiveOdysseyWildlife ?? true,
            _ => throw new System.InvalidOperationException("Unknown Arktos removal setting: " + setting)
        };
        return !enabled || base.ApplyWorker(xml);
    }
}
