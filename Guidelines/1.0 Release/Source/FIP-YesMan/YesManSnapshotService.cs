using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.YesMan;

internal static class YesManSnapshotService
{
    public static SnapshotResult Export(SnapshotRole role, YesManProcessStatus progress = null)
    {
        string language = ActiveLanguageName();
        if (language.IndexOf("English", StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException("RimWorld läuft nicht auf Englisch (erkannte Sprache: " + language + ").");
        }

        Directory.CreateDirectory(YesManPaths.ExportRoot);
        YesManSnapshot snapshot = new()
        {
            role = role == SnapshotRole.CoreDlc ? "CoreDlc" : "ModList",
            generatedUtc = DateTime.UtcNow.ToString("O"),
            gameVersion = VersionControl.CurrentVersionStringWithRev,
            language = language
        };

        Dictionary<string, YesManTextRecord> keyed = new(StringComparer.Ordinal);
        Dictionary<string, YesManTextRecord> strings = new(StringComparer.OrdinalIgnoreCase);
        List<ModContentPack> runningMods = LoadedModManager.RunningModsListForReading.ToList();

        for (int index = 0; index < runningMods.Count; index++)
        {
            ModContentPack mod = runningMods[index];
            YesManStatusStore.Report(progress, "Aktive Mod-Inhalte erfassen", index, Math.Max(1, runningMods.Count + 4), mod.Name);
            List<string> roots = ActiveContentScanner.ResolveContentRoots(mod, snapshot.warnings);
            snapshot.mods.Add(new YesManModRecord
            {
                loadOrder = index,
                packageId = mod.PackageId,
                name = mod.Name,
                version = ReadVersion(mod),
                rootDir = mod.RootDir,
                activeContentRoots = roots
            });

        }

        YesManStatusStore.Report(progress, "Keyed und Strings erfassen", runningMods.Count, runningMods.Count + 4);
        ActiveLanguageSnapshotCollector.Collect(snapshot, keyed, strings);

        if (role == SnapshotRole.CoreDlc)
        {
            List<string> unexpected = snapshot.mods
                .Where(mod => !IsOfficialOrExporter(mod.packageId))
                .Select(mod => mod.packageId)
                .ToList();
            if (unexpected.Count > 0)
            {
                snapshot.warnings.Add("Core/DLC-Snapshot enthält zusätzliche Mods: " + string.Join(", ", unexpected));
            }
        }

        YesManStatusStore.Report(progress, "Geladene Defs erfassen", runningMods.Count + 1, runningMods.Count + 4);
        snapshot.defInjected = DefSnapshotCollector.Collect(snapshot.warnings);
        snapshot.keyed = keyed.Values.OrderBy(record => record.id, StringComparer.Ordinal).ToList();
        snapshot.strings = strings.Values.OrderBy(record => record.id, StringComparer.Ordinal).ToList();

        string destination = role == SnapshotRole.CoreDlc ? YesManPaths.CoreSnapshot : YesManPaths.ModListSnapshot;
        YesManStatusStore.Report(progress, "Snapshot schreiben", runningMods.Count + 3, runningMods.Count + 4, destination);
        WriteJson(destination, snapshot);

        string message = "Yes Man: " + snapshot.role + " exportiert – "
            + snapshot.defInjected.Count + " DefInjected, "
            + snapshot.keyed.Count + " Keyed, "
            + snapshot.strings.Count + " Strings-Dateien. "
            + snapshot.warnings.Count + " Warnungen.";
        Log.Message("[FIP - Yes Man] " + message + " Path: " + destination);
        return new SnapshotResult { Message = message };
    }

    public static void WriteJson<T>(string path, T value)
    {
        SnapshotJsonSerializer.Write(path, value);
    }

    private static string ActiveLanguageName()
    {
        object language = LanguageDatabase.activeLanguage;
        if (language == null)
        {
            return "Unknown";
        }

        foreach (string memberName in new[] { "folderName", "FolderName", "friendlyNameNative", "FriendlyNameNative" })
        {
            Type type = language.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(language) is string fieldValue && !string.IsNullOrEmpty(fieldValue))
            {
                return fieldValue;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(language, null) is string propertyValue && !string.IsNullOrEmpty(propertyValue))
            {
                return propertyValue;
            }
        }

        return language.ToString();
    }

    private static string ReadVersion(ModContentPack mod)
    {
        object metadata = mod.ModMetaData;
        if (metadata == null)
        {
            return string.Empty;
        }

        foreach (string memberName in new[] { "ModVersion", "modVersion", "Version", "version" })
        {
            PropertyInfo property = metadata.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object value = property?.GetValue(metadata, null);
            if (value != null)
            {
                return value.ToString();
            }

            FieldInfo field = metadata.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            value = field?.GetValue(metadata);
            if (value != null)
            {
                return value.ToString();
            }
        }

        return string.Empty;
    }

    private static bool IsOfficialOrExporter(string packageId)
    {
        return !string.IsNullOrEmpty(packageId)
            && (packageId.StartsWith("Ludeon.RimWorld", StringComparison.OrdinalIgnoreCase)
                || packageId.Equals("FIP.YesMan", StringComparison.OrdinalIgnoreCase));
    }
}
