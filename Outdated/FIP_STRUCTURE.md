# Fallout Immersion Project – Zielstruktur

Stand: 30. Juli 2026

## 1. Repositoryebenen

```text
Fallout Immersion Project/
├─ New-Mods/                  aktive, getrennte RimWorld-Mods
│  └─ FIP-<Modul>/
│     ├─ About/
│     ├─ LoadFolders.xml
│     └─ LoadFolders/
│        ├─ Base/
│        └─ <DLC oder Kompatibilität>/
├─ Development/
│  ├─ Source/                kanonische C#-Quellen
│  └─ Tools/                 Audit- und Refactoring-Werkzeuge
├─ .work/                    ignorierter generierter Arbeitszustand
├─ Guidelines/               historische Referenz
└─ FIP_*.md                  Audit- und Übergabedokumente
```

Jeder aktive Mod besitzt genau ein Verzeichnis unter `New-Mods/`. Historische
Root-Module sind kein Build- oder Runtime-Eingang. Big MT gehört nicht zu
diesem Stand.

## 2. Aktive Module

Die 14 spielbaren Module sind Arktos, Corvega, Donaustahl, FutureTec,
Greenway, H&HTools, Hubris, Lucky 38, Poseidon, Repconn, RobCo, Sunset,
WestTek und Whitespring.

Zusätzlich existieren vier unabhängige Sprachpakete für Chinese, Japanese,
Korean und Russian. `FIP-Translation Part 1–4` ist nicht aktiv.

## 3. Besitz und Präfixe

Für jeden spielbaren Mod gilt ein kanonisches Besitzerpräfix:

| Modul | Präfix |
|---|---|
| Arktos | `Arktos_` |
| Corvega | `Corvega_` |
| Donaustahl | `Donaustahl_` |
| FutureTec | `FutureTec_` |
| Greenway | `Greenway_` |
| H&HTools | `HHTools_` |
| Hubris | `Hubris_` |
| Lucky 38 | `Lucky38_` |
| Poseidon | `Poseidon_` |
| Repconn | `Repconn_` |
| RobCo | `RobCo_` |
| Sunset | `Sunset_` |
| WestTek | `WestTek_` |
| Whitespring | `Whitespring_` |

Das Präfix ist verbindlich für:

- alle direkt deklarierten `defName`s,
- benannte abstrakte Def-Eltern,
- mod-eigene XML-Dateien,
- Laufzeittexturen und Audiodateien.

Vanilla-, DLC- und Fremdmod-Defs werden nicht umbenannt, wenn sie lediglich
Patchziele oder Referenzen sind. Der Besitzer eines eigenen neuen Defs muss
dagegen immer am Namen erkennbar sein.

## 4. LanguageData und Patches

- Jeder Keyed- oder DefInjected-Schlüssel ist innerhalb von Mod, Sprache und
  Bereich genau einmal definiert.
- Ein englischer `DefInjected`-Eintrag entfällt, wenn derselbe sichtbare Wert
  bereits direkt im lokalen Def steht.
- `DefInjected` ist der Standard für reine Änderungen an Label, Beschreibung
  und anderen übersetzbaren Textfeldern.
- `PatchOperationReplace` ist für reine Textänderungen nicht zulässig.
- Patchoperationen bleiben erlaubt, wenn sie Struktur ändern, Listen bearbeiten
  oder ein fehlendes Feld anlegen. Der sichtbare Inhalt dieses Felds wird
  anschließend per `DefInjected` gesetzt.
- Platzhalternamen und -mengen müssen über alle aktiven Sprachen konsistent
  sein.
- Leere oder generierte TODO-Werte werden entfernt, damit der Quelltext als
  Fallback greift.

## 5. Assets

- Ein Mod besitzt alle zwingend benötigten Texturen und Sounds selbst.
- XML-Pfade verwenden dieselbe Groß-/Kleinschreibung wie die Datei.
- Richtungs-, Stack-, Random-, Masken- und Variantenslots bleiben erhalten,
  auch wenn einzelne Dateien bytegleich sind.
- Jede bytegleiche Assetgruppe innerhalb eines Mods muss einzeln im Validator
  freigegeben und technisch begründet sein.
- Modübergreifende lokale Kopien sind erlaubt, wenn Zentralisierung eine neue
  Pflichtabhängigkeit erzeugen würde.
- Dateien werden nur nach Referenz-, C#-, Hash- und Variantenprüfung gelöscht.
- Bildoptimierung bleibt verlustfrei; Abmessungen und Pixelinhalt ändern sich
  nicht.

## 6. LoadFolders und Abhängigkeiten

- `Base` enthält nur Vanilla-, Pflichtabhängigkeits- oder immer verfügbare
  Inhalte.
- `IfModActive` gilt für eine optionale Voraussetzung,
  `IfModActiveAll` für eine vollständige Kombination.
- Jede externe Bedingung steht in `modDependencies` oder `loadAfter`.
- Patches auf Fremddefs liegen in einer passenden bedingten Schicht.
- Interne FIP-Abhängigkeiten dürfen keinen Ladezyklus erzeugen.
- Fachlich modulspezifische RulePacks, NameMaker und Integrationspatches liegen
  bei ihrem Besitzer.

## 7. C# und Build

- Kanonische Quellen liegen unter `Development/Source/`.
- `FIP.Managed.sln` enthält 20 Projekte.
- Ein Release-Build erzeugt pro Projekt nur die FIP-DLL.
- RimWorld-, Unity- und Harmony-Bibliotheken werden nicht mitkopiert.
- `bin`, `obj`, PDBs, Logs und Zwischen-DLLs werden nicht versioniert.
- Assembly-, Namespace- und öffentliche Typnamen bleiben stabil.
- DefOf-Felder müssen exakt zu den kanonischen DefNames passen.

## 8. Freigabe

Ein Stand darf erst veröffentlicht werden, wenn:

1. `Development/Tools/validate_refactor.py` ohne Befund läuft,
2. alle 20 C#-Projekte ohne Warnungen und Fehler bauen,
3. jedes Modul mit seinen Pflichtabhängigkeiten in RimWorld lädt,
4. relevante optionale Kombinationen geprüft sind,
5. `Player.log` keine FIP-bedingten Fehler enthält,
6. Alt-Spielstände für alle DefName-Migrationen geprüft oder migriert wurden.
