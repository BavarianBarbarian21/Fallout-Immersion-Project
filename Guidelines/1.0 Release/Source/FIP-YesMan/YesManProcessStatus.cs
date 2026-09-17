using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FIP.YesMan;

[Serializable]
public sealed class YesManProcessStatus
{
    public int schemaVersion = 1;
    public string id;
    public string title;
    public string state;
    public string phase;
    public long current;
    public long total;
    public string currentItem;
    public string message;
    public string error;
    public string startedUtc;
    public string updatedUtc;
    public string completedUtc;
    public double elapsedSeconds;
    public double etaSeconds = -1d;
    public int processId;
    public long added;
    public long changed;
    public long removed;
    public long unchanged;
    public long apiRequests;
    public long cacheHits;
}

internal static class YesManStatusStore
{
    public const string CoreExport = "core-dlc";
    public const string ModListExport = "mod-list";
    public const string EnglishBaseline = "english-baseline";
    public const string Translation = "translation";

    public static YesManProcessStatus Start(string id, string title, string phase)
    {
        YesManProcessStatus status = new()
        {
            id = id,
            title = title,
            state = "running",
            phase = phase,
            startedUtc = DateTime.UtcNow.ToString("O"),
            updatedUtc = DateTime.UtcNow.ToString("O"),
            processId = System.Diagnostics.Process.GetCurrentProcess().Id
        };
        Save(status);
        return status;
    }

    public static void Report(YesManProcessStatus status, string phase, long current, long total, string currentItem = null)
    {
        if (status == null)
        {
            return;
        }

        status.phase = phase;
        status.current = current;
        status.total = total;
        status.currentItem = currentItem ?? string.Empty;
        status.updatedUtc = DateTime.UtcNow.ToString("O");
        UpdateTiming(status);
        Save(status);
    }

    public static void Complete(YesManProcessStatus status, string message)
    {
        status.state = "completed";
        status.current = status.total > 0 ? status.total : status.current;
        status.message = message;
        status.updatedUtc = DateTime.UtcNow.ToString("O");
        status.completedUtc = status.updatedUtc;
        status.etaSeconds = 0d;
        UpdateTiming(status);
        Save(status);
    }

    public static void Fail(YesManProcessStatus status, Exception exception)
    {
        if (status == null)
        {
            return;
        }

        status.state = "failed";
        status.error = exception?.ToString() ?? "Unbekannter Fehler";
        status.message = exception?.Message ?? "Unbekannter Fehler";
        status.updatedUtc = DateTime.UtcNow.ToString("O");
        status.completedUtc = status.updatedUtc;
        status.etaSeconds = -1d;
        UpdateTiming(status);
        Save(status);
    }

    public static YesManProcessStatus Load(string id)
    {
        string path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<YesManProcessStatus>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch
        {
            return null;
        }
    }

    public static string PathFor(string id) => Path.Combine(YesManPaths.StatusRoot, id + ".status.json");

    public static void Save(YesManProcessStatus status)
    {
        Directory.CreateDirectory(YesManPaths.StatusRoot);
        string path = PathFor(status.id);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(status, true) + Environment.NewLine, new UTF8Encoding(false));
        if (File.Exists(path))
        {
            string backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Replace(temporary, path, backup);
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }
        else
        {
            File.Move(temporary, path);
        }
    }

    private static void UpdateTiming(YesManProcessStatus status)
    {
        if (!TryUtc(status.startedUtc, out DateTime started))
        {
            return;
        }

        status.elapsedSeconds = Math.Max(0d, (DateTime.UtcNow - started).TotalSeconds);
        if (status.state == "running" && status.current > 0 && status.total > status.current)
        {
            status.etaSeconds = status.elapsedSeconds / status.current * (status.total - status.current);
        }
    }

    public static bool TryUtc(string value, out DateTime result) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
