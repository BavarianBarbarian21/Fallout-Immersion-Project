# FIP 1.0 Release – refaktorierte Arbeitskopie

Dieser Ordner enthält die vollständig refaktorierte FIP-1.0-Arbeitskopie für RimWorld 1.6. Die Quellordner im Repository-Root und `New-Mods/` dienten ausschließlich als Referenz und blieben unverändert.

Enthalten sind:

- alle spielbaren FIP-Module außer dem eingestellten FIP Sunset,
- die vier Übersetzungsmodule als nachgelagerte Refactoring-Phase,
- FIP Big MT mit sicherem Basisordner und optionalen Anomaly-, Insanity- und WestTek-Integrationen,
- der vollständige Arbeitsauftrag in `REFACTOR_PROMPT.md`.

Big MT verwendet absichtlich die ehemalige Sunset-Package-ID `FIP.Sunset` und die Workshop-ID `3760676309`. Der bedingungslos geladene `BigMT`-Ordner bleibt ohne Anomaly sicher; sämtliche Anomaly-Inhalte liegen in exakt bedingten LoadFolders.

Die technischen Ergebnisse und die bewusst akzeptierten Kombinationen sind in `Reports/FINAL_VALIDATION.md` dokumentiert. `Tools/Validate-Release.ps1` kann die reproduzierbaren Struktur-, XML-, Asset- und Ownership-Prüfungen erneut ausführen.
