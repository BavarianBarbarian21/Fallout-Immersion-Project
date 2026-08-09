using System;
using System.IO;
using System.Xml;
using Verse;

namespace FIP.RobCo;

public class PatchOperationReplaceUnlessRestoreMechanoids : PatchOperationReplace
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return RestoreMechanoidsRequested() || base.ApplyWorker(xml);
    }

    internal static bool RestoreMechanoidsRequested()
    {
        try
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "Mod_FIP.RobCo.xml");
            if (!File.Exists(path))
            {
                return false;
            }

            XmlDocument document = new();
            document.Load(path);
            string value = document.SelectSingleNode("//restoreMechanoids")?.InnerText;
            return bool.TryParse(value, out bool restore) && restore;
        }
        catch (Exception exception)
        {
            Log.Warning("[FIP - RobCo] Could not read Restore Mechanoids setting during patching: " + exception.Message);
            return false;
        }
    }
}

public class PatchOperationAddUnlessRestoreMechanoids : PatchOperationAdd
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return PatchOperationReplaceUnlessRestoreMechanoids.RestoreMechanoidsRequested() || base.ApplyWorker(xml);
    }
}

public class PatchOperationRemoveUnlessRestoreMechanoids : PatchOperationRemove
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return PatchOperationReplaceUnlessRestoreMechanoids.RestoreMechanoidsRequested() || base.ApplyWorker(xml);
    }
}
