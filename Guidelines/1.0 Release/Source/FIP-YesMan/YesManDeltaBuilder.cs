using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEngine;

namespace FIP.YesMan;

internal static class YesManDeltaBuilder
{
    public static SnapshotResult Build(YesManProcessStatus progress = null)
    {
        if (!File.Exists(YesManPaths.CoreSnapshot) || !File.Exists(YesManPaths.ModListSnapshot))
        {
            throw new InvalidOperationException("Beide Snapshots werden benötigt. Zuerst Core + DLC und danach die vollständige Modliste exportieren.");
        }

        YesManStatusStore.Report(progress, "Snapshots lesen", 0, 7, YesManPaths.CoreSnapshot);
        YesManSnapshot core = SnapshotJsonSerializer.Read<YesManSnapshot>(YesManPaths.CoreSnapshot);
        YesManSnapshot full = SnapshotJsonSerializer.Read<YesManSnapshot>(YesManPaths.ModListSnapshot);
        ValidateSnapshot(core, "Core/DLC");
        ValidateSnapshot(full, "Modliste");

        List<string> warnings = new();
        if (!string.Equals(core.gameVersion, full.gameVersion, StringComparison.Ordinal))
        {
            warnings.Add("Die RimWorld-Versionen der Snapshots unterscheiden sich: " + core.gameVersion + " / " + full.gameVersion);
        }

        if (!string.Equals(core.language, full.language, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Die Snapshot-Sprachen unterscheiden sich: " + core.language + " / " + full.language);
        }

        YesManStatusStore.Report(progress, "Unveränderte Core/DLC-Texte abziehen", 1, 7);
        int excluded = 0;
        List<YesManTextRecord> defInjected = Difference(core.defInjected, full.defInjected, ref excluded);
        List<YesManTextRecord> keyed = Difference(core.keyed, full.keyed, ref excluded);
        List<YesManTextRecord> strings = Difference(core.strings, full.strings, ref excluded);

        YesManStatusStore.Report(progress, "Ausgabeordner vorbereiten", 2, 7, YesManPaths.GeneratedBaseline);
        PrepareCleanOutput(YesManPaths.GeneratedBaseline);
        YesManStatusStore.Report(progress, "Mod-Metadaten schreiben", 3, 7);
        WriteAbout(full);
        YesManStatusStore.Report(progress, "DefInjected schreiben", 4, 7, defInjected.Count + " Einträge");
        WriteDefInjected(defInjected);
        YesManStatusStore.Report(progress, "Keyed schreiben", 5, 7, keyed.Count + " Einträge");
        WriteKeyed(keyed);
        YesManStatusStore.Report(progress, "Strings schreiben", 6, 7, strings.Count + " Dateien");
        WriteStrings(strings);

        YesManDeltaManifest manifest = new()
        {
            generatedUtc = DateTime.UtcNow.ToString("O"),
            coreSnapshot = YesManPaths.CoreSnapshot,
            modListSnapshot = YesManPaths.ModListSnapshot,
            coreGameVersion = core.gameVersion,
            modListGameVersion = full.gameVersion,
            defInjectedCount = defInjected.Count,
            keyedCount = keyed.Count,
            stringsFileCount = strings.Count,
            excludedUnchangedCount = excluded,
            sourcePackages = defInjected.Concat(keyed).Concat(strings)
                .Select(record => record.sourcePackageId)
                .Where(packageId => !string.IsNullOrEmpty(packageId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(packageId => packageId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            warnings = warnings.Concat(core.warnings ?? new List<string>()).Concat(full.warnings ?? new List<string>()).ToList()
        };

        YesManSnapshot delta = new()
        {
            role = "EnglishDelta",
            generatedUtc = manifest.generatedUtc,
            gameVersion = full.gameVersion,
            language = full.language,
            mods = full.mods,
            defInjected = defInjected,
            keyed = keyed,
            strings = strings,
            warnings = manifest.warnings
        };

        YesManSnapshotService.WriteJson(Path.Combine(YesManPaths.GeneratedBaseline, "manifest.json"), manifest);
        YesManSnapshotService.WriteJson(Path.Combine(YesManPaths.GeneratedBaseline, "english-delta.snapshot.json"), delta);
        WriteReport(manifest);

        string message = "Yes Man: Englische Baseline erzeugt – " + defInjected.Count + " DefInjected, "
            + keyed.Count + " Keyed und " + strings.Count + " Strings-Dateien; " + excluded + " unveränderte Core/DLC-Einträge ausgeschlossen.";
        Verse.Log.Message("[FIP - Yes Man] " + message + " Path: " + YesManPaths.GeneratedBaseline);
        return new SnapshotResult { Message = message };
    }

    private static List<YesManTextRecord> Difference(List<YesManTextRecord> baseline, List<YesManTextRecord> full, ref int excluded)
    {
        Dictionary<string, YesManTextRecord> baselineById = (baseline ?? new List<YesManTextRecord>())
            .Where(record => record != null && !string.IsNullOrEmpty(record.id))
            .GroupBy(record => record.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        List<YesManTextRecord> result = new();
        foreach (YesManTextRecord record in (full ?? new List<YesManTextRecord>()).Where(record => record != null).OrderBy(record => record.id, StringComparer.Ordinal))
        {
            if (baselineById.TryGetValue(record.id, out YesManTextRecord original)
                && string.Equals(original.text, record.text, StringComparison.Ordinal))
            {
                excluded++;
                continue;
            }

            result.Add(record);
        }

        return result;
    }

    private static void ValidateSnapshot(YesManSnapshot snapshot, string label)
    {
        if (snapshot == null || snapshot.schemaVersion != 1)
        {
            throw new InvalidOperationException(label + "-Snapshotformat ist ungültig oder wird von dieser Yes-Man-Version nicht unterstützt.");
        }

        if (snapshot.mods == null || snapshot.defInjected == null || snapshot.keyed == null || snapshot.strings == null || snapshot.warnings == null)
        {
            throw new InvalidOperationException(label + "-Snapshot ist unvollständig. Er wurde wahrscheinlich mit der fehlerhaften älteren Yes-Man-Version gespeichert und muss neu exportiert werden.");
        }

        if (snapshot.mods.Count == 0)
        {
            throw new InvalidOperationException(label + "-Snapshot enthält keine geladenen Mods und muss neu exportiert werden.");
        }
    }

    private static void PrepareCleanOutput(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);
    }

    private static void WriteDefInjected(IEnumerable<YesManTextRecord> records)
    {
        foreach (IGrouping<string, YesManTextRecord> group in records.GroupBy(record => record.defType).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            string directory = Path.Combine(YesManPaths.GeneratedBaseline, "Languages", "English", "DefInjected", group.Key);
            Directory.CreateDirectory(directory);
            // The Def type is already the containing directory name. Repeating a
            // namespaced type in the file name can exceed RimWorld/Mono's legacy
            // Windows MAX_PATH limit even though the directory exists.
            string path = Path.Combine(directory, "FIP.xml");
            WriteLanguageData(path, group.OrderBy(record => record.key, StringComparer.Ordinal));
        }
    }

    private static void WriteAbout(YesManSnapshot full)
    {
        string aboutDirectory = Path.Combine(YesManPaths.GeneratedBaseline, "About");
        Directory.CreateDirectory(aboutDirectory);

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };

        using XmlWriter writer = XmlWriter.Create(Path.Combine(aboutDirectory, "About.xml"), settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("ModMetaData");
        writer.WriteElementString("packageId", "FIP.English");
        writer.WriteElementString("name", "FIP - English Language Pack");
        writer.WriteElementString("author", "Feil");
        writer.WriteElementString("description", "Internal English language pack generated by FIP - Yes Man. This development artifact is the canonical source for FIP language packs and is not intended for publication.");
        writer.WriteStartElement("supportedVersions");
        writer.WriteElementString("li", "1.6");
        writer.WriteEndElement();

        List<string> packages = (full.mods ?? new List<YesManModRecord>())
            .Select(mod => mod.packageId)
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId)
                && !packageId.Equals("FIP.YesMan", StringComparison.OrdinalIgnoreCase)
                && !packageId.Equals("FIP.English", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (packages.Count > 0)
        {
            writer.WriteStartElement("loadAfter");
            foreach (string package in packages)
            {
                writer.WriteElementString("li", package);
            }
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();

        string sourceIcon = string.IsNullOrEmpty(YesManPaths.ModRoot)
            ? string.Empty
            : Path.Combine(YesManPaths.ModRoot, "About", "ModIcon.png");
        if (File.Exists(sourceIcon))
        {
            File.Copy(sourceIcon, Path.Combine(aboutDirectory, "ModIcon.png"), true);
        }
    }

    private static void WriteKeyed(IEnumerable<YesManTextRecord> records)
    {
        List<YesManTextRecord> entries = records.OrderBy(record => record.key, StringComparer.Ordinal).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        string directory = Path.Combine(YesManPaths.GeneratedBaseline, "Languages", "English", "Keyed");
        Directory.CreateDirectory(directory);
        WriteLanguageData(Path.Combine(directory, "FIP-YesMan_Keyed.xml"), entries);
    }

    private static void WriteLanguageData(string path, IEnumerable<YesManTextRecord> records)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };
        using XmlWriter writer = XmlWriter.Create(path, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("LanguageData");
        foreach (YesManTextRecord record in records)
        {
            writer.WriteStartElement(record.key);
            writer.WriteString(record.text ?? string.Empty);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteStrings(IEnumerable<YesManTextRecord> records)
    {
        foreach (YesManTextRecord record in records)
        {
            string relative = (record.path ?? record.key ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains(".."))
            {
                continue;
            }

            string path = Path.Combine(YesManPaths.GeneratedBaseline, "Languages", "English", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, (record.text ?? string.Empty).Replace("\n", Environment.NewLine), new UTF8Encoding(false));
        }
    }

    private static void WriteReport(YesManDeltaManifest manifest)
    {
        StringBuilder report = new();
        report.AppendLine("FIP - Yes Man English baseline");
        report.AppendLine("Generated UTC: " + manifest.generatedUtc);
        report.AppendLine("Core/DLC game version: " + manifest.coreGameVersion);
        report.AppendLine("Mod-list game version: " + manifest.modListGameVersion);
        report.AppendLine("DefInjected: " + manifest.defInjectedCount);
        report.AppendLine("Keyed: " + manifest.keyedCount);
        report.AppendLine("Strings files: " + manifest.stringsFileCount);
        report.AppendLine("Excluded unchanged Core/DLC entries: " + manifest.excludedUnchangedCount);
        report.AppendLine();
        report.AppendLine("Source packages:");
        foreach (string package in manifest.sourcePackages)
        {
            report.AppendLine("- " + package);
        }
        report.AppendLine();
        report.AppendLine("Warnings:");
        if (manifest.warnings.Count == 0)
        {
            report.AppendLine("- none");
        }
        else
        {
            foreach (string warning in manifest.warnings)
            {
                report.AppendLine("- " + warning);
            }
        }

        File.WriteAllText(Path.Combine(YesManPaths.GeneratedBaseline, "report.txt"), report.ToString(), new UTF8Encoding(false));
    }

}
