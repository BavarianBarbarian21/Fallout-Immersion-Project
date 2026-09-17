using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Verse;

namespace FIP.YesMan;

internal static class ActiveContentScanner
{
    private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static List<string> ResolveContentRoots(ModContentPack mod, List<string> warnings)
    {
        string rootDir = mod.RootDir;
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);
        AddDirectory(roots, rootDir, rootDir);

        bool discoveredRuntimeRoots = false;
        foreach (FieldInfo field in GetAllFields(mod.GetType()))
        {
            if (field.Name.IndexOf("folder", StringComparison.OrdinalIgnoreCase) < 0
                && field.Name.IndexOf("load", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            object value;
            try
            {
                value = field.GetValue(mod);
            }
            catch
            {
                continue;
            }

            discoveredRuntimeRoots |= AddPathsFromValue(roots, rootDir, value);
        }

        foreach (PropertyInfo property in mod.GetType().GetProperties(AllInstance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0
                || (property.Name.IndexOf("folder", StringComparison.OrdinalIgnoreCase) < 0
                    && property.Name.IndexOf("load", StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }

            object value;
            try
            {
                value = property.GetValue(mod, null);
            }
            catch
            {
                continue;
            }

            discoveredRuntimeRoots |= AddPathsFromValue(roots, rootDir, value);
        }

        if (!discoveredRuntimeRoots)
        {
            string versionRoot = Path.Combine(rootDir, "1.6");
            if (Directory.Exists(versionRoot))
            {
                roots.Add(Path.GetFullPath(versionRoot));
            }

            if (Directory.Exists(Path.Combine(rootDir, "LoadFolders")))
            {
                warnings.Add("Aktive LoadFolders konnten für " + mod.PackageId + " nicht per Laufzeitdaten bestimmt werden; nur Modwurzel und 1.6 werden als sicherer Fallback gescannt.");
            }
        }

        return roots.OrderBy(path => PathDepth(path)).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void AddKeyedAndStrings(
        ModContentPack mod,
        IEnumerable<string> roots,
        IDictionary<string, YesManTextRecord> keyed,
        IDictionary<string, YesManTextRecord> strings,
        List<string> warnings)
    {
        HashSet<string> visitedFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            string englishRoot = Path.Combine(root, "Languages", "English");
            string keyedRoot = Path.Combine(englishRoot, "Keyed");
            if (Directory.Exists(keyedRoot))
            {
                foreach (string file in Directory.GetFiles(keyedRoot, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (!visitedFiles.Add(Path.GetFullPath(file)))
                    {
                        continue;
                    }

                    try
                    {
                        XDocument document = XDocument.Load(file, LoadOptions.PreserveWhitespace);
                        XElement languageData = document.Root;
                        if (languageData == null)
                        {
                            continue;
                        }

                        foreach (XElement element in languageData.Elements())
                        {
                            string key = element.Name.LocalName;
                            keyed[key] = new YesManTextRecord
                            {
                                id = "Keyed|" + key,
                                kind = "Keyed",
                                key = key,
                                path = RelativeLanguagePath(englishRoot, file),
                                text = element.Value,
                                sourcePackageId = mod.PackageId
                            };
                        }
                    }
                    catch (Exception exception)
                    {
                        warnings.Add("Keyed-Datei konnte nicht gelesen werden: " + file + " (" + exception.Message + ")");
                    }
                }
            }

            string stringsRoot = Path.Combine(englishRoot, "Strings");
            if (!Directory.Exists(stringsRoot))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(stringsRoot, "*.txt", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!visitedFiles.Add(Path.GetFullPath(file)))
                {
                    continue;
                }

                try
                {
                    string relativePath = RelativeLanguagePath(englishRoot, file).Replace('\\', '/');
                    string normalizedText = File.ReadAllText(file).Replace("\r\n", "\n").Replace('\r', '\n');
                    string id = "Strings|" + relativePath;
                    strings[id] = new YesManTextRecord
                    {
                        id = id,
                        kind = "Strings",
                        key = relativePath,
                        path = relativePath,
                        text = normalizedText,
                        sourcePackageId = mod.PackageId
                    };
                }
                catch (Exception exception)
                {
                    warnings.Add("Strings-Datei konnte nicht gelesen werden: " + file + " (" + exception.Message + ")");
                }
            }
        }
    }

    private static bool AddPathsFromValue(HashSet<string> roots, string rootDir, object value)
    {
        if (value == null || value is string)
        {
            return value is string path && AddDirectory(roots, rootDir, path);
        }

        if (value is IEnumerable enumerable)
        {
            bool added = false;
            foreach (object item in enumerable)
            {
                if (item is string itemPath)
                {
                    added |= AddDirectory(roots, rootDir, itemPath);
                    continue;
                }

                if (item == null)
                {
                    continue;
                }

                foreach (FieldInfo field in GetAllFields(item.GetType()))
                {
                    if (field.FieldType != typeof(string)
                        || (field.Name.IndexOf("folder", StringComparison.OrdinalIgnoreCase) < 0
                            && field.Name.IndexOf("path", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    try
                    {
                        added |= AddDirectory(roots, rootDir, field.GetValue(item) as string);
                    }
                    catch
                    {
                    }
                }
            }

            return added;
        }

        return false;
    }

    private static bool AddDirectory(HashSet<string> roots, string rootDir, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string path = Path.IsPathRooted(candidate) ? candidate : Path.Combine(rootDir, candidate);
        try
        {
            path = Path.GetFullPath(path);
            string normalizedRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Directory.Exists(path)
                || (!path.Equals(Path.GetFullPath(rootDir), StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            roots.Add(path);
            return !path.Equals(Path.GetFullPath(rootDir), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<FieldInfo> GetAllFields(Type type)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(AllInstance | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static string RelativeLanguagePath(string englishRoot, string file)
    {
        Uri rootUri = new(englishRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(new Uri(file)).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static int PathDepth(string path) => path.Count(character => character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar);
}
