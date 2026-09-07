using System.Xml;
using Verse;

namespace FIP.HHTools;

// Gate the original XML operations before inheritance and recipe generation.
// Disabled options leave the source definitions untouched until the next load.
public sealed class PatchOperationIfImmersive : PatchOperationSequence
{
    public string setting;

    protected override bool ApplyWorker(XmlDocument xml)
    {
        HHToolsModSettings settings = HHToolsMod.Settings;
        bool enabled = setting switch
        {
            "buildings" => settings?.onlyImmersiveBuildings ?? true,
            "weapons" => settings?.onlyImmersiveWeapons ?? true,
            "apparel" => settings?.onlyImmersiveApparel ?? true,
            "textiles" => settings?.onlyImmersiveTextiles ?? true,
            "ideologyOrigins" => settings?.onlyImmersiveIdeologyOrigins ?? true,
            _ => throw new System.InvalidOperationException("Unknown H&H removal setting: " + setting)
        };
        return !enabled || base.ApplyWorker(xml);
    }
}
