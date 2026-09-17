using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FIP.YesMan;

internal static class DefSnapshotCollector
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const int MaximumDepth = 12;

    private static readonly HashSet<string> FallbackTranslatableFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "label", "labelShort", "labelNoun", "labelNounPretty", "labelPlural", "labelFemale", "labelMale",
        "labelMalePlural", "labelFemalePlural", "description", "descriptionShort", "descriptionHyperlinks", "descriptionExtra",
        "baseDesc", "title", "titleShort", "titleFemale", "titleMale", "titleShortFemale", "baseTitle", "baseTitleFemale",
        "reportString", "gerund", "gerundLabel", "inspectString", "baseInspectLine", "jobString", "commandLabel", "commandDesc",
        "confirmationText", "letterLabel", "letterText", "letterDesc", "beginLetterLabel", "beginLetterText", "arrivedLetterLabel",
        "arrivedLetterText", "customLetterLabel", "customLetterText", "message", "messageSuccess", "messageFailure", "successMessage",
        "successText", "failMessage", "failText", "rejectInputMessage", "pawnsArrivalMessage", "joinText", "failTriggerText",
        "text", "note", "header", "headerTip", "tip", "summary", "customSummary", "pawnSingular", "pawnPlural", "pawnsPlural",
        "leaderPawnSingular", "leaderTitle", "leaderTitleFemale", "royalFavorLabel", "ideoDescription", "fixedName", "customLabel",
        "customDescription", "verb", "verbGerund", "chargeNoun", "skillLabel", "skillDescription", "pawnLabel", "helpText",
        "tooltip", "extraTooltip", "pawnCannotEquipReason", "name", "nameNoun", "nameSuffix", "namePrefix", "rulesStrings"
    };

    public static List<YesManTextRecord> Collect(List<string> warnings)
    {
        Dictionary<string, YesManTextRecord> records = new(StringComparer.Ordinal);
        HashSet<string> visitedDefs = new(StringComparer.Ordinal);
        IEnumerable<Type> defTypes;
        try
        {
            defTypes = GenTypes.AllTypes
                .Where(type => type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(Def).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Geladene Def-Typen konnten nicht ermittelt werden.", exception);
        }

        foreach (Type defType in defTypes)
        {
            IEnumerable defs = GetDefs(defType);
            if (defs == null)
            {
                continue;
            }

            foreach (object item in defs)
            {
                if (item is not Def def || string.IsNullOrEmpty(def.defName))
                {
                    continue;
                }

                Type runtimeType = def.GetType();
                string defTypeName = TranslationDefTypeName(runtimeType);
                string defIdentity = runtimeType.AssemblyQualifiedName + "|" + def.defName;
                if (!visitedDefs.Add(defIdentity))
                {
                    continue;
                }

                HashSet<object> objectStack = new(ReferenceEqualityComparer.Instance);
                CollectObject(def, def, defTypeName, string.Empty, records, objectStack, 0, warnings);
            }
        }

        return records.Values.OrderBy(record => record.id, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable GetDefs(Type defType)
    {
        try
        {
            Type databaseType = typeof(DefDatabase<>).MakeGenericType(defType);
            PropertyInfo property = databaseType.GetProperty("AllDefsListForReading", BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null, null) as IEnumerable;
        }
        catch
        {
            return null;
        }
    }

    private static void CollectObject(
        Def rootDef,
        object current,
        string defTypeName,
        string currentPath,
        IDictionary<string, YesManTextRecord> records,
        HashSet<object> objectStack,
        int depth,
        List<string> warnings)
    {
        if (current == null || depth > MaximumDepth)
        {
            return;
        }

        Type currentType = current.GetType();
        if (!currentType.IsValueType && !objectStack.Add(current))
        {
            return;
        }

        try
        {
            foreach (FieldInfo field in currentType.GetFields(PublicInstance))
            {
                if (field.IsStatic || field.Name == "defName" || field.Name == "modContentPack")
                {
                    continue;
                }

                object value;
                try
                {
                    value = field.GetValue(current);
                }
                catch
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                string path = string.IsNullOrEmpty(currentPath) ? field.Name : currentPath + "." + field.Name;
                if (value is string text)
                {
                    if (IsTranslatable(field) && !string.IsNullOrWhiteSpace(text))
                    {
                        string key = rootDef.defName + "." + path;
                        string id = "DefInjected|" + defTypeName + "|" + key;
                        records[id] = new YesManTextRecord
                        {
                            id = id,
                            kind = "DefInjected",
                            defType = defTypeName,
                            key = key,
                            path = path,
                            text = text,
                            sourcePackageId = rootDef.modContentPack?.PackageId
                        };
                    }

                    continue;
                }

                Type valueType = value.GetType();
                if (IsTerminal(valueType) || value is Def)
                {
                    continue;
                }

                if (value is IList list)
                {
                    for (int index = 0; index < list.Count; index++)
                    {
                        object child = list[index];
                        if (child is string childText)
                        {
                            if (IsTranslatable(field) && !string.IsNullOrWhiteSpace(childText))
                            {
                                string indexedPath = path + "." + index;
                                string key = rootDef.defName + "." + indexedPath;
                                string id = "DefInjected|" + defTypeName + "|" + key;
                                records[id] = new YesManTextRecord
                                {
                                    id = id,
                                    kind = "DefInjected",
                                    defType = defTypeName,
                                    key = key,
                                    path = indexedPath,
                                    text = childText,
                                    sourcePackageId = rootDef.modContentPack?.PackageId
                                };
                            }

                            continue;
                        }

                        if (child == null || IsTerminal(child.GetType()) || child is Def)
                        {
                            continue;
                        }

                        CollectObject(rootDef, child, defTypeName, path + "." + index, records, objectStack, depth + 1, warnings);
                    }

                    continue;
                }

                if (value is IDictionary)
                {
                    continue;
                }

                CollectObject(rootDef, value, defTypeName, path, records, objectStack, depth + 1, warnings);
            }
        }
        catch (Exception exception)
        {
            warnings.Add("Def-Felder konnten nicht vollständig gelesen werden: " + defTypeName + "/" + rootDef.defName + " (" + exception.Message + ")");
        }
        finally
        {
            if (!currentType.IsValueType)
            {
                objectStack.Remove(current);
            }
        }
    }

    private static bool IsTranslatable(FieldInfo field)
    {
        try
        {
            object[] attributes = field.GetCustomAttributes(true);
            if (attributes.Any(attribute =>
                    string.Equals(attribute.GetType().Name, "NoTranslateAttribute", StringComparison.Ordinal)
                    || string.Equals(attribute.GetType().Name, "UnsavedAttribute", StringComparison.Ordinal)))
            {
                return false;
            }

            if (attributes.Any(attribute => string.Equals(attribute.GetType().Name, "MustTranslateAttribute", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        catch
        {
        }

        if (FallbackTranslatableFieldNames.Contains(field.Name))
        {
            return true;
        }

        return false;
    }

    private static bool IsTerminal(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(Type)
            || typeof(Delegate).IsAssignableFrom(type)
            || (type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal));
    }

    private static string TranslationDefTypeName(Type type)
    {
        if (type.Namespace == "Verse" || type.Namespace == "RimWorld" || string.IsNullOrEmpty(type.Namespace))
        {
            return type.Name;
        }

        return type.FullName?.Replace('+', '.') ?? type.Name;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
