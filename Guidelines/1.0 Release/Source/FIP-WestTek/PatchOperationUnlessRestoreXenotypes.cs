using System.Xml;
using Verse;

namespace FIP.WestTek;

// These operations make the roster toggle reversible at the source: when the
// immersive-only roster is disabled, WestTek leaves third-party XML untouched.
public class PatchOperationReplaceUnlessRestoreXenotypes : PatchOperationReplace
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !OnlyImmersiveXenotypesRequested() || base.ApplyWorker(xml);
    }

    internal static bool OnlyImmersiveXenotypesRequested()
    {
        return WestTekMod.Settings?.onlyImmersiveXenotypes ?? true;
    }
}

public class PatchOperationAddUnlessRestoreXenotypes : PatchOperationAdd
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !PatchOperationReplaceUnlessRestoreXenotypes.OnlyImmersiveXenotypesRequested() || base.ApplyWorker(xml);
    }
}

public class PatchOperationRemoveUnlessRestoreXenotypes : PatchOperationRemove
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !PatchOperationReplaceUnlessRestoreXenotypes.OnlyImmersiveXenotypesRequested() || base.ApplyWorker(xml);
    }
}
