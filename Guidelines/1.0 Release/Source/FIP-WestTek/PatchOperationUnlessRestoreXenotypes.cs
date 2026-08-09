using System;
using System.IO;
using System.Xml;
using Verse;

namespace FIP.WestTek;

// These operations make the roster toggle reversible at the source: with
// Restore Xenotypes enabled, WestTek leaves third-party XML untouched.
public class PatchOperationReplaceUnlessRestoreXenotypes : PatchOperationReplace
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return RestoreXenotypesRequested() || base.ApplyWorker(xml);
    }

    internal static bool RestoreXenotypesRequested()
    {
        try
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "Mod_FIP.WestTek.xml");
            if (!File.Exists(path))
            {
                return true;
            }

            XmlDocument document = new();
            document.Load(path);
            string value = document.SelectSingleNode("//restoreXenotypes")?.InnerText;
            return !bool.TryParse(value, out bool restore) || restore;
        }
        catch (Exception exception)
        {
            Log.Warning("[FIP - WestTek] Could not read Restore Xenotypes setting during patching: " + exception.Message);
            return true;
        }
    }
}

public class PatchOperationAddUnlessRestoreXenotypes : PatchOperationAdd
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return PatchOperationReplaceUnlessRestoreXenotypes.RestoreXenotypesRequested() || base.ApplyWorker(xml);
    }
}

public class PatchOperationRemoveUnlessRestoreXenotypes : PatchOperationRemove
{
    protected override bool ApplyWorker(XmlDocument xml)
    {
        return PatchOperationReplaceUnlessRestoreXenotypes.RestoreXenotypesRequested() || base.ApplyWorker(xml);
    }
}
