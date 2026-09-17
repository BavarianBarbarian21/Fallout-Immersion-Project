using System;
using System.Diagnostics;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.YesMan;

public sealed class YesManMod : Mod
{
    private Vector2 scrollPosition;
    private string status = "Noch kein Export in dieser Sitzung.";

    public YesManMod(ModContentPack content) : base(content)
    {
        YesManPaths.ModRoot = content.RootDir;
    }

    public override string SettingsCategory() => "FIP - Yes Man";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Rect viewRect = new(0f, 0f, inRect.width - 20f, 1020f);
        Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
        Listing_Standard listing = new();
        listing.Begin(viewRect);

        Text.Font = GameFont.Medium;
        listing.Label("FIP - Yes Man Kontrollzentrum");
        Text.Font = GameFont.Small;
        listing.Label("Exporte, Baseline und Übersetzung bleiben über dauerhafte Statusdateien nachvollziehbar. RimWorld muss für die Exporte auf Englisch laufen und nach jeder Änderung der Modliste neu gestartet worden sein.");
        listing.GapLine();

        DrawJob(listing, YesManStatusStore.CoreExport, "1. Core + DLC", YesManPaths.CoreSnapshot);

        if (listing.ButtonText("1. Core + alle DLCs exportieren"))
        {
            RunExport(SnapshotRole.CoreDlc);
        }

        listing.Label("Diesen Export mit ausschließlich Core, allen DLCs und Yes Man erstellen.");
        listing.GapLine();

        DrawJob(listing, YesManStatusStore.ModListExport, "2. Vollständige Modliste", YesManPaths.ModListSnapshot);

        if (listing.ButtonText("2. Vollständige Modliste exportieren"))
        {
            RunExport(SnapshotRole.ModList);
        }

        listing.Label("Diesen Export mit der gewünschten finalen Modliste erstellen.");
        listing.GapLine();

        DrawJob(listing, YesManStatusStore.EnglishBaseline, "3. Englische Baseline", Path.Combine(YesManPaths.GeneratedBaseline, "manifest.json"));

        if (listing.ButtonText("3. Englische Baseline erzeugen"))
        {
            RunBuild();
        }

        listing.Label("Vergleicht beide Snapshots und entfernt ausschließlich Einträge, deren Schlüssel und englischer Text gegenüber Core + DLC unverändert sind.");
        listing.GapLine();

        Text.Font = GameFont.Medium;
        listing.Label("Übersetzung");
        Text.Font = GameFont.Small;
        DrawJob(listing, YesManStatusStore.Translation, "Simplified Chinese", null);
        listing.Label("Der künftige Übersetzungs-Worker meldet hier Gesamtfortschritt, Sprache, Datei, ETA und Fehler. Der Zustand bleibt auch nach einem Neustart erhalten.");
        Rect translationButtons = listing.GetRect(30f);
        float translationHalf = (translationButtons.width - 8f) / 2f;
        if (Widgets.ButtonText(new Rect(translationButtons.x, translationButtons.y, translationHalf, 30f), "Volumen schätzen"))
        {
            StartTranslationWorker(true);
        }
        if (Widgets.ButtonText(new Rect(translationButtons.x + translationHalf + 8f, translationButtons.y, translationHalf, 30f), "Simplified Chinese starten / fortsetzen"))
        {
            StartTranslationWorker(false);
        }
        Rect translationControlButtons = listing.GetRect(30f);
        if (Widgets.ButtonText(new Rect(translationControlButtons.x, translationControlButtons.y, translationHalf, 30f), "Abbruch anfordern"))
        {
            RequestTranslationCancellation();
        }
        if (Widgets.ButtonText(new Rect(translationControlButtons.x + translationHalf + 8f, translationControlButtons.y, translationHalf, 30f), "Übersetzungsstatus öffnen"))
        {
            OpenFolder(YesManPaths.StatusRoot);
        }
        listing.GapLine();

        Rect folderButtons = listing.GetRect(30f);
        float half = (folderButtons.width - 8f) / 2f;
        if (Widgets.ButtonText(new Rect(folderButtons.x, folderButtons.y, half, 30f), "Exportordner öffnen"))
        {
            OpenFolder(YesManPaths.ExportRoot);
        }
        if (Widgets.ButtonText(new Rect(folderButtons.x + half + 8f, folderButtons.y, half, 30f), "Statusordner öffnen"))
        {
            OpenFolder(YesManPaths.StatusRoot);
        }

        Rect baselineButtons = listing.GetRect(30f);
        if (Widgets.ButtonText(new Rect(baselineButtons.x, baselineButtons.y, half, 30f), "Englische Baseline öffnen"))
        {
            OpenFolder(YesManPaths.GeneratedBaseline);
        }
        if (Widgets.ButtonText(new Rect(baselineButtons.x + half + 8f, baselineButtons.y, half, 30f), "Exportpfad kopieren"))
        {
            GUIUtility.systemCopyBuffer = YesManPaths.ExportRoot;
            status = "Exportpfad wurde kopiert.";
        }

        Rect languagePackButtons = listing.GetRect(30f);
        if (Widgets.ButtonText(new Rect(languagePackButtons.x, languagePackButtons.y, languagePackButtons.width, 30f), "Erzeugte Sprachpakete öffnen"))
        {
            OpenFolder(YesManPaths.GeneratedLanguagePacks);
        }

        listing.Gap();
        listing.Label("Pfad: " + YesManPaths.ExportRoot);
        listing.GapLine();
        Text.Font = GameFont.Medium;
        listing.Label("Status");
        Text.Font = GameFont.Small;
        listing.Label(status);

        listing.End();
        Widgets.EndScrollView();
    }

    private void RunExport(SnapshotRole role)
    {
        string id = role == SnapshotRole.CoreDlc ? YesManStatusStore.CoreExport : YesManStatusStore.ModListExport;
        string title = role == SnapshotRole.CoreDlc ? "Core + DLC exportieren" : "Vollständige Modliste exportieren";
        YesManProcessStatus process = YesManStatusStore.Start(id, title, "Geladene Inhalte erfassen");
        try
        {
            SnapshotResult result = YesManSnapshotService.Export(role, process);
            status = result.Message;
            YesManStatusStore.Complete(process, result.Message);
            Messages.Message(result.Message, MessageTypeDefOf.TaskCompletion, false);
        }
        catch (Exception exception)
        {
            YesManStatusStore.Fail(process, exception);
            status = "Export fehlgeschlagen: " + exception.Message;
            Log.Error("[FIP - Yes Man] Export failed: " + exception);
            Messages.Message(status, MessageTypeDefOf.RejectInput, false);
        }
    }

    private void RunBuild()
    {
        YesManProcessStatus process = YesManStatusStore.Start(YesManStatusStore.EnglishBaseline, "Englische Baseline erzeugen", "Snapshots prüfen");
        try
        {
            SnapshotResult result = YesManDeltaBuilder.Build(process);
            status = result.Message;
            YesManStatusStore.Complete(process, result.Message);
            Messages.Message(result.Message, MessageTypeDefOf.TaskCompletion, false);
        }
        catch (Exception exception)
        {
            YesManStatusStore.Fail(process, exception);
            status = "Baseline fehlgeschlagen: " + exception.Message;
            Log.Error("[FIP - Yes Man] Baseline build failed: " + exception);
            Messages.Message(status, MessageTypeDefOf.RejectInput, false);
        }
    }

    private static void DrawJob(Listing_Standard listing, string id, string fallbackTitle, string artifactPath)
    {
        YesManProcessStatus process = YesManStatusStore.Load(id);
        bool artifactExists = !string.IsNullOrEmpty(artifactPath) && (File.Exists(artifactPath) || Directory.Exists(artifactPath));
        string title = process?.title ?? fallbackTitle;
        string state = process?.state ?? (artifactExists ? "completed" : "pending");
        string stateText = state == "running" ? "LÄUFT" : state == "completed" ? "FERTIG" : state == "failed" ? "FEHLER" : state == "cancelled" ? "ABGEBROCHEN" : "AUSSTEHEND";

        Rect heading = listing.GetRect(24f);
        Widgets.Label(new Rect(heading.x, heading.y, heading.width * 0.7f, heading.height), title);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = state == "failed" ? Color.red : state == "completed" ? Color.green : state == "running" ? Color.yellow : Color.gray;
        Widgets.Label(new Rect(heading.x + heading.width * 0.7f, heading.y, heading.width * 0.3f, heading.height), stateText);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        float fraction = process != null && process.total > 0 ? Mathf.Clamp01((float)process.current / process.total) : state == "completed" ? 1f : 0f;
        Rect bar = listing.GetRect(24f);
        Widgets.FillableBar(bar, fraction);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(bar, process != null && process.total > 0 ? process.current + " / " + process.total + "  (" + (fraction * 100f).ToString("F1") + " %)" : stateText);
        Text.Anchor = TextAnchor.UpperLeft;

        if (process != null)
        {
            string detail = string.IsNullOrEmpty(process.phase) ? process.message : process.phase;
            if (!string.IsNullOrEmpty(process.currentItem))
            {
                detail += " — " + process.currentItem;
            }
            if (process.state == "running")
            {
                detail += " | Laufzeit " + FormatDuration(process.elapsedSeconds);
                if (process.etaSeconds >= 0d)
                {
                    detail += " | Restzeit ca. " + FormatDuration(process.etaSeconds);
                }
                if (YesManStatusStore.TryUtc(process.updatedUtc, out DateTime updated) && (DateTime.UtcNow - updated).TotalMinutes > 2d)
                {
                    detail += " | WARNUNG: seit über 2 Minuten kein Lebenszeichen";
                }
            }
            listing.Label(detail.NullOrEmpty() ? "Keine Detailinformationen." : detail);
            if (process.state == "failed" && !string.IsNullOrEmpty(process.message))
            {
                listing.Label("Fehler: " + process.message);
            }
            if (id == YesManStatusStore.Translation && (process.added + process.changed + process.removed + process.unchanged > 0))
            {
                listing.Label("Update: +" + process.added + " neu, " + process.changed + " geändert, -" + process.removed + " entfernt, " + process.unchanged + " unverändert.");
                listing.Label("Webanfragen: " + process.apiRequests + " | aus Cache übernommen: " + process.cacheHits);
            }
        }
        else if (artifactExists)
        {
            listing.Label("Vorhanden: " + File.GetLastWriteTime(artifactPath).ToString("g"));
        }
        else
        {
            listing.Label("Noch nicht gestartet.");
        }
        listing.Gap();
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 0d || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return "unbekannt";
        }
        TimeSpan value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1d ? $"{(int)value.TotalHours}h {value.Minutes}m" : value.TotalMinutes >= 1d ? $"{value.Minutes}m {value.Seconds}s" : $"{value.Seconds}s";
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            status = "Ordner geöffnet: " + path;
        }
        catch (Exception exception)
        {
            status = "Ordner konnte nicht geöffnet werden: " + exception.Message;
        }
    }

    private void StartTranslationWorker(bool estimateOnly)
    {
        try
        {
            if (!File.Exists(YesManPaths.TranslationLauncher))
            {
                throw new FileNotFoundException("Der Übersetzungs-Worker ist noch nicht installiert.", YesManPaths.TranslationLauncher);
            }

            YesManProcessStatus existing = YesManStatusStore.Load(YesManStatusStore.Translation);
            if (existing?.state == "running" && existing.processId > 0)
            {
                try
                {
                    using Process running = Process.GetProcessById(existing.processId);
                    if (!running.HasExited)
                    {
                        status = "Die Übersetzung läuft bereits (Prozess " + existing.processId + ").";
                        return;
                    }
                }
                catch
                {
                }
            }

            YesManProcessStatus processStatus = YesManStatusStore.Start(YesManStatusStore.Translation, estimateOnly ? "Übersetzungsvolumen schätzen" : "Simplified Chinese übersetzen", "Worker wird gestartet");
            string workerArguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + YesManPaths.TranslationLauncher + "\" -ExportRoot \"" + YesManPaths.ExportRoot + "\"";
            workerArguments += estimateOnly ? " -Provider none -EstimateOnly" : " -Provider argos";
            ProcessStartInfo startInfo = new()
            {
                FileName = "powershell.exe",
                Arguments = workerArguments,
                WorkingDirectory = Path.GetDirectoryName(YesManPaths.TranslationLauncher),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process child = Process.Start(startInfo);
            if (child == null)
            {
                throw new InvalidOperationException("Der Übersetzungsprozess konnte nicht gestartet werden.");
            }

            processStatus.processId = child.Id;
            processStatus.message = "Übersetzungs-Worker gestartet.";
            processStatus.updatedUtc = DateTime.UtcNow.ToString("O");
            YesManStatusStore.Save(processStatus);
            status = (estimateOnly ? "Volumenschätzung" : "Übersetzung") + " gestartet (Prozess " + child.Id + ").";
        }
        catch (Exception exception)
        {
            status = "Übersetzung konnte nicht gestartet werden: " + exception.Message;
            Log.Error("[FIP - Yes Man] Translation worker launch failed: " + exception);
            Messages.Message(status, MessageTypeDefOf.RejectInput, false);
        }
    }

    private void RequestTranslationCancellation()
    {
        try
        {
            YesManProcessStatus process = YesManStatusStore.Load(YesManStatusStore.Translation);
            if (process?.state != "running")
            {
                status = "Es läuft derzeit keine Übersetzung.";
                return;
            }
            Directory.CreateDirectory(YesManPaths.StatusRoot);
            File.WriteAllText(YesManPaths.TranslationCancelRequest, DateTime.UtcNow.ToString("O"));
            status = "Abbruch angefordert. Der Worker beendet sich nach der aktuellen API-Anfrage und behält seinen Cache.";
        }
        catch (Exception exception)
        {
            status = "Abbruch konnte nicht angefordert werden: " + exception.Message;
        }
    }
}

public enum SnapshotRole
{
    CoreDlc,
    ModList
}

public sealed class SnapshotResult
{
    public string Message;
}

public static class YesManPaths
{
    public static string ModRoot { get; set; }
    public static string ExportRoot => Path.Combine(GenFilePaths.ConfigFolderPath, "FIP-YesMan", "Exports");
    public static string CoreSnapshot => Path.Combine(ExportRoot, "core-dlc.snapshot.json");
    public static string ModListSnapshot => Path.Combine(ExportRoot, "modlist.snapshot.json");
    public static string GeneratedBaseline => Path.Combine(ExportRoot, "FIP-English Language Pack");
    public static string GeneratedLanguagePacks => Path.Combine(GenFilePaths.ConfigFolderPath, "FIP-Languages");
    public static string StatusRoot => Path.Combine(ExportRoot, "Status");
    public static string TranslationLauncher => Path.Combine(ModRoot ?? string.Empty, "Tools", "Start-FIPTranslation.ps1");
    public static string TranslationCancelRequest => Path.Combine(StatusRoot, "translation.cancel.request");
}
