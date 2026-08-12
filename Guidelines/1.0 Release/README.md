# FIP 1.0 Release – Entwicklungsunterlagen

Die spielbaren FIP-1.0-Module liegen als kanonische Release-Dateien direkt im Repository-Root. Dieser Ordner enthält die zugehörigen Entwicklungsunterlagen, Berichte, C#-Quellen und Prüfwerkzeuge. Historische Vorstufen und die früheren Modstände liegen unter `Outdated/`.

Enthalten sind:

- alle spielbaren FIP-Module außer dem eingestellten FIP Sunset,
- die vier Übersetzungsmodule als nachgelagerte Refactoring-Phase,
- FIP Big MT mit sicherem Basisordner und optionalen Anomaly-, Insanity- und WestTek-Integrationen,
- der vollständige Arbeitsauftrag in `REFACTOR_PROMPT.md`.

Big MT verwendet absichtlich die ehemalige Sunset-Package-ID `FIP.Sunset` und die Workshop-ID `3760676309`. Der bedingungslos geladene `BigMT`-Ordner bleibt ohne Anomaly sicher; sämtliche Anomaly-Inhalte liegen in exakt bedingten LoadFolders.

Die technischen Ergebnisse und die bewusst akzeptierten Kombinationen sind in `Reports/FINAL_VALIDATION.md` dokumentiert. `Tools/Validate-Release.ps1` prüft standardmäßig die Root-Module und verwendet die Quellen aus diesem Ordner. Die übrigen Skripte dokumentieren den abgeschlossenen Umbau und sind nicht als wiederholbare Migration der aktuellen Root-Struktur gedacht.
