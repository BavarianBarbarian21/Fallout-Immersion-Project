using System.Xml;
using Verse;

namespace FIP.RobCo;

public class PatchOperationReplaceUnlessRestoreMechanoids : PatchOperationReplace
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !OnlyImmersiveMechanoidsRequested() || base.ApplyWorker(xml);
    }

    internal static bool OnlyImmersiveMechanoidsRequested()
    {
        return RobCoMod.Settings?.onlyImmersiveMechanoids ?? true;
    }
}

public class PatchOperationAddUnlessRestoreMechanoids : PatchOperationAdd
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !PatchOperationReplaceUnlessRestoreMechanoids.OnlyImmersiveMechanoidsRequested() || base.ApplyWorker(xml);
    }
}

public class PatchOperationRemoveUnlessRestoreMechanoids : PatchOperationRemove
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return !PatchOperationReplaceUnlessRestoreMechanoids.OnlyImmersiveMechanoidsRequested() || base.ApplyWorker(xml);
    }
}
