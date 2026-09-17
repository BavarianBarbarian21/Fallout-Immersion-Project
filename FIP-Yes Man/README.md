# FIP - Yes Man

Development-only RimWorld 1.6 mod for producing the canonical English source
corpus used by the FIP translation pipeline.

## Workflow

1. Enable Core, every DLC, and FIP - Yes Man. Set RimWorld to English, restart,
   then use `Options > Mod settings > FIP - Yes Man > Export Core + DLC`.
2. Enable the intended release mod list, keep every DLC and FIP - Yes Man
   active, restart, then use `Export full mod list`.
3. Select `Build English baseline`.

Snapshots created by builds from before the large-export serializer fix are
incomplete even if their status card shows the correct entry counts. Such files
are rejected explicitly. Recreate both the Core/DLC and full-mod-list exports
once after installing the fixed DLL; the lost entry lists cannot be recovered
from the old status files.

Exports are written to RimWorld's configuration folder under
`FIP-YesMan/Exports` so Workshop and local mod directories remain untouched.

The generated baseline contains entries which are new in the full mod list or
whose final English value differs from Core plus DLC. Entries which are
identical to Core plus DLC are excluded. The comparison always uses an entry's
identity as well as its text; equal words belonging to different keys are not
collapsed.

The mod records only the content which RimWorld actually loaded. Inactive mods
and inactive conditional load folders are therefore normally absent. A warning
is included in the snapshot if active content folders cannot be discovered and
the scanner has to fall back to the mod root and its `1.6` directory.

The result is a complete internal mod named `FIP-English Language Pack`. It contains an
`About.xml`, the final English `DefInjected`, `Keyed`, and `Strings` corpus, its
snapshot, manifest, and report. It must not be published as an end-user mod.

Run `Tools/TranslationSync/New-FIPTranslation.ps1` after exporting. The script
syncs the English Language Pack into the repository root and creates a separate
language-pack working copy. The default target is `FIP-Japanese Language Pack`. Its language
directory is renamed from `English` to `Japanese`; all values intentionally
remain English until the translation stage replaces them.

Run `Tools/TranslationSync/Initialize-FIPTranslationMods.ps1` to rebuild all
five release packages at once. `ChineseSimplified` and `ChineseTraditional`
receive separate language packs, as do Japanese, Korean, and Russian.

## Kontrollzentrum und Fortschritt

Die Mod-Einstellungen zeigen für Core/DLC-Export, Modlisten-Export, englische
Baseline und den späteren Übersetzungs-Worker jeweils Status, Fortschrittsbalken,
Phase, aktuellen Eintrag, Laufzeit und ETA. Schaltflächen öffnen direkt den
Export-, Baseline- und Statusordner.

Der Zustand wird dauerhaft unter `FIP-YesMan/Exports/Status` gespeichert. Ein
externer Übersetzungs-Worker schreibt `translation.status.json` atomar nach dem
Schema der übrigen `*.status.json`-Dateien. Yes Man liest diese Datei live ein.
Bleibt bei einem als `running` markierten Prozess das Feld `updatedUtc` länger
als zwei Minuten unverändert, zeigt das Dashboard ein fehlendes Lebenszeichen
an. Dadurch bleibt auch nach einem Neustart sichtbar, ob ein Lauf fertig,
fehlgeschlagen oder vermutlich hängen geblieben ist.

`Simplified Chinese starten / fortsetzen` startet den separat laufenden Worker
unter `Tools/Start-FIPTranslation.ps1` in einem unsichtbaren Prozess. Der Worker
läuft damit unabhängig vom geöffneten Einstellungsfenster weiter. Solange der
Worker noch nicht mitgeliefert wird, meldet der Knopf klar, dass er nicht
installiert ist, anstatt einen scheinbar laufenden Auftrag zu erzeugen.

Der Yes-Man-Standardlauf verarbeitet vorerst ausschließlich Simplified Chinese.
Die übrigen Sprachdefinitionen bleiben für einen späteren Ausbau im Worker
erhalten. Der mitgelieferte Worker unterstützt außerdem einen reinen `-EstimateOnly`-Lauf
ohne Netzwerkzugriff sowie fortsetzbare Übersetzungen mit Cache. Der Anbieter
`none` führt bewusst keine Übersetzung aus; `google-gtx` ist als kostenloser,
aber inoffizieller Webanbieter verfügbar. Fertige Pakete landen standardmäßig
im kurzen Ordner `FIP-Languages` direkt unter RimWorlds Konfigurationsordner.
Der kürzere Stamm verhindert die Windows-Pfadgrenze bei langen, namespaced
Def-Typen. Das Kontrollzentrum kann diesen Ordner direkt öffnen.

Der Übersetzungsweg ist verbindlich kostenfrei. Der Worker enthält keine
kostenpflichtigen Anbieter, keine Kaufoptionen und keine API-Schlüssel für
abrechenbare Dienste. Ein weiterer Anbieter darf nur ergänzt werden, wenn er
lokal/offline oder ohne Gebühren nutzbar ist. Wegen der fehlenden Garantie des
inoffiziellen Google-Endpunkts bleiben Cache, Wiederholungen, Abbruch und
Fortsetzen zwingende Bestandteile des Workflows.

Der Worker sendet nicht jeden XML-Knoten oder jede Textzeile einzeln. Er fasst
die noch nicht gecachten Texte jeder Quelldatei in geschützte Batches zusammen
(standardmäßig höchstens 100 Texte beziehungsweise 2.500 Zeichen). Technische
Platzhalter werden vor dem Versand maskiert, Batch-Grenzen nach der Antwort
validiert und beschädigte Batches automatisch geteilt. Zwischen Webanfragen
liegt standardmäßig eine kurze Pause; HTTP 429 führt zu exponentiell längeren
Wartezeiten. Dadurch kann ein abgebrochener Lauf seinen bisherigen
Einzeltext-Cache weiterverwenden, benötigt für den Rest aber wesentlich weniger
Webanfragen.

Im Kontrollzentrum kann vorab ein reiner Volumenlauf gestartet werden. Ein
laufender Übersetzungsprozess lässt sich außerdem kontrolliert abbrechen. Der
Worker beendet sich nach der aktuellen Anfrage; Cache und bereits vollständig
geschriebene Dateien bleiben erhalten und werden beim Fortsetzen wiederverwendet.

## Inkrementelle Updates

Nach jedem erfolgreichen Sprachlauf speichert der Worker einen Hash-Index des
englischen Quellenbestands. Ein späterer Yes-Man-Export wird dagegen verglichen
und als neu, geändert, entfernt oder unverändert klassifiziert. Nur Texte ohne
passenden Cache-Eintrag erzeugen neue Webanfragen. Neue Defs und neue Mods werden
ergänzt, geänderte Werte ersetzt und entfernte Einträge aus den neuen Paketen
entfernt. Das Dashboard zeigt die vier Änderungszahlen sowie Webanfragen und
Cache-Treffer sichtbar an.

Alle neuen Pakete entstehen zuerst vollständig in einem Staging-Ordner. Erst
nach erfolgreicher QA wird der gesamte Satz aktiviert. Bei Fehler oder Abbruch
bleibt daher die letzte vollständige Ausgabe unangetastet. Der Vergleichsplan
liegt unter `Translation/last-update-plan.json`; der letzte erfolgreiche
Quellenindex unter `Translation/previous-source-index.json`.
