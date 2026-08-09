# Fallout Immersion Project – Refactoring-Changelog

Stand: 31. Juli 2026

Ziel: aktive Arbeitskopien unter `New-Mods/`

## 1. Umfang

- 14 spielbare Module und 4 unabhängige Übersetzungsmodule.
- Big MT sowie `FIP-Translation Part 1–4` sind nicht Teil des aktiven Stands.
- Historische Root-Module, `Guidelines/` und alte Übersetzungsbestände blieben
  Referenzmaterial und wurden nicht als Laufzeitquelle verwendet.
- Package IDs, Assemblynamen und öffentliche C#-Typnamen blieben stabil.

## 2. Neues Identitäts- und Duplikat-Refactoring

Der frühere Audit erkannte weder alle inhaltlich doppelten Assets noch
redundante identische Sprachspiegel. Der neue Durchgang hat deshalb
Dateiinhalte und XML-Semantik zusätzlich zu Pfaden geprüft.

### Assets

- 27 redundante Texturen entfernt, darunter:
  - 3 doppelte Arktos-Axolotl-Dateien,
  - 3 doppelte Lucky-38-Dosen,
  - 3 überzählige Hubris-Mothman-Tree-Dateien,
  - 15 RobCo-Kopien, deren Referenzen auf vorhandene lokale Texturfamilien
    vereinheitlicht wurden,
  - 3 WestTek-Platzhalterkopien.
- 349 Textur- und Sounddateien auf das Präfix ihres Besitzermods umbenannt.
- Alle XML- und C#-Referenzen auf diese Assets aktualisiert.
- Insbesondere besitzen die 21 Arktos-Ant-Texturen jetzt `Arktos_*`-Namen.
- Mod-eigene XML-Dateien ohne Besitzerpräfix wurden ebenfalls umbenannt.

### DefNames

- 318 Def-ID-Zuordnungen auf Besitzerpräfixe angewendet.
- 319 direkte oder benannte Deklarationen sowie alle lokalen Referenzen
  aktualisiert.
- 95 Dateien in `New-Mods/` und `Development/Source/` angepasst.
- Fünf NameMaker-Dateien passend zu ihrem neuen Besitzerpräfix umbenannt.
- RobCos `WestTek_Gene_*` wurden zu `RobCo_Gene_*`.
- Greenways `HHTools_Greenway_*` wurden zu `Greenway_*`.
- Arktos’ `HHTools_Arktos_*` wurden zu `Arktos_*`.
- H&HTools’ `FIPD_*` und weitere generische Orts-/Faction-IDs wurden zu
  `HHTools_*`.
- WestTeks generische IDs `Highmate` und `SPECIAL` wurden zu
  `WestTek_Xenotype_SNuffy` und `WestTek_SPECIAL`.
- Greenways direkte Vanilla-Kollision `ConnectedTreeDied` wurde durch einen
  Strukturpatch plus `DefInjected` ersetzt.

Die Änderung ist für betroffene Alt-Spielstände und externe Patches potenziell
save-brechend. Details stehen in `FIP_DEFNAME_MIGRATION.md`.

### LanguageData und Textpatches

- 3.272 exakt redundante englische `DefInjected`-Spiegel entfernt.
- 30 danach leere Sprachdateien entfernt.
- 2 doppelte Poseidon-Chemfuel-Schlüssel bereinigt.
- 14 reine Text-`PatchOperationReplace` entfernt; die vorhandenen
  `DefInjected`-Einträge sind jetzt die einzige Textquelle.
- Strukturelle Patches, die fehlende Felder oder XML-Strukturen anlegen,
  bleiben erhalten.

## 3. Donaustahl-Packtiere

- Die alte Klasse `ArktosPackAnimalCompatibility` aus der Urban-Assembly
  entfernt und die Assembly neu gebaut.
- Den alten einzelnen Arktos-FCP-Biomepatch entfernt.
- Vier klar bedingte Donaustahl-Patches eingeführt:
  - global Muffalo als NPC-Händler-Backup mit Gewicht `1`,
  - mit FCP Animals Brahmin mit Gewicht `100`,
  - Horse für die gemeinsame Arktos-Biomebasis,
  - mit Arktos plus FCP Animals zusätzlich Bighorner und Radstag.
- Vorhandene Brahmin-/Muffalo-Einträge werden vor dem Hinzufügen entfernt;
  dadurch bleibt pro Trägerliste und Biomebene genau ein Eintrag.
- Biome-Vererbung und `Inherit="False"` werden berücksichtigt.
- Donaustahls `loadAfter` auf alle 97 im aktiven Bestand bekannten
  FCP-/Vanilla-Expanded-Pakete sowie alle übrigen spielbaren FIP-Module
  erweitert.
- Ein eigener Regressionstest simuliert vorhandene Duplikate, fehlende
  Carrierlisten, Gewichtung und Parent-/Child-Biome.

## 4. Frühere Struktur- und Abhängigkeitskorrekturen

- Falsche H&H-Workshop-ID korrigiert.
- Greenway-`PublishedFileId.txt` ergänzt.
- Verwaisten Sunset-LoadFolder entfernt.
- Harmony für RobCo und WestTek korrekt als Runtime-Abhängigkeit deklariert,
  private Harmony-Kopien entfernt.
- Fehlende `loadAfter`-Einträge ergänzt.
- Fachlich Greenway- oder WestTek-eigene Integrationen aus H&HTools zu ihren
  Besitzern verschoben.
- Arktos- und Greenway-RulePacks und NameMaker den richtigen Modulen
  zugeordnet.
- Interne Ladezyklen beseitigt; alle 168 LoadFolder-Einträge zeigen auf
  vorhandene Ziele.

## 5. C# und Assemblies

- Kanonische Quellen unter `Development/Source/` eingerichtet.
- 15 Arktos-Projekte sowie Greenway, H&HTools, RobCo und WestTek in
  `FIP.Managed.sln`.
- Obsolete H&HTools-Faction-Felder und Startup-Logs entfernt.
- WestTek-Geneffekte in einem gemeinsamen `TickRare`-Scan zusammengeführt.
- DefOf-Felder und C#-Referenzen auf die neuen RobCo-/WestTek-DefNames
  aktualisiert.
- Alle 19 Projekte anschließend als Release neu gebaut:
  0 Warnungen, 0 Fehler.
- Keine privaten Harmony-, PDB- oder MDB-Dateien in den Laufzeitmods.

## 6. Übersetzungen

- Vier unabhängige Pakete für Chinese, Japanese, Korean und Russian.
- 97 zuvor widersprüchliche Schlüsselgruppen auf 0 reduziert.
- Leere oder generierte TODO-Werte entfernt; unbekannte Übersetzungen wurden
  nicht erfunden.
- 184.304 sprachübergreifende Platzhaltervergleiche, 0 Fehler.
- Fünf bewusst fehlende Werte fallen auf den Text des Quellmods zurück.
- Big-MT-Inhalte und Part-1–4-Abhängigkeiten aus dem aktiven Satz entfernt.

## 7. Sounds und Bilder

- 14 sicher ersetzbare oder unreferenzierte RobCo-Sounds entfernt;
  176 Audiodateien verbleiben.
- 45 große PNGs verlustfrei und pixelidentisch neu komprimiert.
- Nach der zusätzlichen Duplikatbereinigung verbleiben 492 dekodierbare Bilder.
- 11 bytegleiche inner-moduläre Hashgruppen sind explizit als technisch
  notwendige Richtungs-, Varianten- oder Gewichtungsslots freigegeben.
- 8 modübergreifende Gruppen bleiben zur Unabhängigkeit der Module lokal.

## 8. Abschlussprüfung

`Development/Tools/validate_refactor.py` prüft jetzt zusätzlich:

- Besitzerpräfixe für Defs, Assets und XML-Dateien,
- globale doppelte direkte Defs,
- doppelte LanguageData-Schlüssel,
- redundante englische DefInjected-Spiegel,
- reine Text-Replaces,
- alle bytegleichen Assetgruppen innerhalb eines Mods gegen eine exakte
  Ausnahmeliste.

Der Abschlusslauf meldet 0 unerwartete Befunde. Ein echter RimWorld-Lauf steht
weiterhin aus.
