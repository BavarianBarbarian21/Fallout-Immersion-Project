using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace FIP.YesMan;

internal static class ActiveLanguageSnapshotCollector
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Collect(
        YesManSnapshot snapshot,
        IDictionary<string, YesManTextRecord> keyed,
        IDictionary<string, YesManTextRecord> strings)
    {
        object language = LanguageDatabase.activeLanguage;
        if (language == null)
        {
            snapshot.warnings.Add("Die aktive Sprachdatenbank ist nicht verfügbar; Keyed und Strings konnten nicht exportiert werden.");
            return;
        }

        CollectKeyed(language, snapshot, keyed);
        CollectStrings(language, snapshot, strings);
    }

    private static void CollectKeyed(object language, YesManSnapshot snapshot, IDictionary<string, YesManTextRecord> output)
    {
        FieldInfo field = language.GetType().GetField("keyedReplacements", AllInstance);
        if (field?.GetValue(language) is not IDictionary replacements)
        {
            snapshot.warnings.Add("RimWorlds finale Keyed-Datenbank konnte nicht gelesen werden.");
            return;
        }

        foreach (DictionaryEntry pair in replacements)
        {
            string key = pair.Key?.ToString();
            if (string.IsNullOrEmpty(key) || pair.Value == null)
            {
                continue;
            }

            string value = ReadStringField(pair.Value, "value") ?? string.Empty;
            string sourcePath = ReadStringField(pair.Value, "fileSourceFullPath")
                ?? ReadStringField(pair.Value, "fileSource")
                ?? string.Empty;
            output[key] = new YesManTextRecord
            {
                id = "Keyed|" + key,
                kind = "Keyed",
                key = key,
                path = sourcePath,
                text = value,
                sourcePackageId = FindSourcePackage(sourcePath, snapshot.mods)
            };
        }
    }

    private static void CollectStrings(object language, YesManSnapshot snapshot, IDictionary<string, YesManTextRecord> output)
    {
        FieldInfo field = language.GetType().GetField("stringFiles", AllInstance);
        if (field?.GetValue(language) is not IDictionary stringFiles)
        {
            snapshot.warnings.Add("RimWorlds finale Strings-Datenbank konnte nicht gelesen werden.");
            return;
        }

        foreach (DictionaryEntry pair in stringFiles)
        {
            string resourceKey = pair.Key?.ToString();
            if (string.IsNullOrWhiteSpace(resourceKey) || pair.Value is not IEnumerable values)
            {
                continue;
            }

            List<string> lines = new();
            foreach (object value in values)
            {
                lines.Add(value?.ToString() ?? string.Empty);
            }

            string relativePath = NormalizeStringsPath(resourceKey);
            string id = "Strings|" + relativePath;
            output[id] = new YesManTextRecord
            {
                id = id,
                kind = "Strings",
                key = resourceKey.Replace('\\', '/'),
                path = relativePath,
                text = string.Join("\n", lines),
                sourcePackageId = string.Empty
            };
        }
    }

    private static string NormalizeStringsPath(string resourceKey)
    {
        string path = resourceKey.Replace('\\', '/').TrimStart('/');
        if (!path.StartsWith("Strings/", StringComparison.OrdinalIgnoreCase))
        {
            path = "Strings/" + path;
        }

        if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            path += ".txt";
        }

        return path;
    }

    private static string ReadStringField(object instance, string name)
    {
        FieldInfo field = instance.GetType().GetField(name, AllInstance);
        return field?.GetValue(instance) as string;
    }

    private static string FindSourcePackage(string sourcePath, IEnumerable<YesManModRecord> mods)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            return string.Empty;
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(sourcePath);
        }
        catch
        {
            return string.Empty;
        }

        return mods
            .Where(mod => !string.IsNullOrEmpty(mod.rootDir))
            .Select(mod => new { Mod = mod, Root = SafeFullPath(mod.rootDir) })
            .Where(item => !string.IsNullOrEmpty(item.Root)
                && (normalized.Equals(item.Root, StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith(item.Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.Root.Length)
            .Select(item => item.Mod.packageId)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }
}
