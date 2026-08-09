# Fallout Immersion Project – technischer Abschlussaudit

Stand: 31. Juli 2026

Aktiver Umfang: `New-Mods/` und `Development/`

Status: statischer Audit und Release-Build bestanden

## 1. Kurzfazit

Der frühere Stand war bei Asset- und Sprachduplikaten nicht streng genug
geprüft. Insbesondere lagen die Axolotl-Texturen in Arktos tatsächlich doppelt
vor und die Ant-Assets hatten kein Arktos-Präfix.

Der aktive Stand ist jetzt gegen diese Fehlerklassen abgesichert:

- keine unerwarteten bytegleichen Assetkopien innerhalb eines Mods,
- keine Asset-, XML- oder direkt deklarierten Def-Namen ohne Besitzerpräfix,
- keine global doppelt direkt deklarierten Defs,
- keine doppelten `LanguageData`-/`DefInjected`-Schlüssel,
- keine englischen `DefInjected`-Einträge, die einen identischen lokalen
  Def-Wert nur spiegeln,
- keine reinen Textänderungen per `PatchOperationReplace`.

Bytegleichheit ist nicht absolut null. Es verbleiben 11 ausdrücklich
freigegebene Gruppen innerhalb eines Mods und 8 Gruppen über Modgrenzen. Die
11 inner-modulären Gruppen sind notwendige RimWorld-Richtungs- oder
Variantenslots sowie eine beabsichtigte Soundgewichtung. Die 8
modübergreifenden Gruppen bleiben lokale Kopien, damit eigenständige Mods keine
neue Pflichtabhängigkeit erhalten. Jede zusätzliche inner-moduläre Gruppe lässt
den Validator fehlschlagen.

## 2. Aktueller Bestand

| Prüfung | Ergebnis |
|---|---:|
| aktive Module | 18 = 14 spielbar + 4 Übersetzung |
| Dateien unter `New-Mods/` | 2.466 |
| Gesamtgröße | 85.119.272 Byte (81,18 MiB) |
| XML-Dateien | 1.416, 0 Parsefehler |
| Patch-XML | 159 |
| XPath-Ausdrücke | 4.515 |
| direkte Defs | 1.230, davon 89 abstrakt |
| global doppelte direkte Defs | 0 |
| Def-/Datei-/Asset-Präfixfehler | 0 |
| `LoadFolders.xml`-Einträge | 168 |
| fehlende Ziele / optionale Reihenfolgefehler | 0 / 0 |
| interne FIP-Ladezyklen | 0 |
| doppelte LanguageData-Schlüssel | 0 |
| redundante englische DefInjected-Spiegel | 0 |
| reine Text-`PatchOperationReplace` | 0 |
| Übersetzungskonflikte / leere Werte | 0 / 0 |
| Platzhaltervergleiche | 184.304, 0 Fehler |
| bewusst auf Quelltext fallende Schlüssel | 5 |
| Bilder | 492, alle dekodierbar |
| Audiodateien | 176, 0 fehlende Soundordner |
| freigegebene / unerwartete inner-moduläre Assetgruppen | 11 / 0 |
| modübergreifende identische Assetgruppen | 8 |
| Laufzeit-DLLs / C#-Projekte | 19 / 19 |
| Release-Build | 0 Warnungen, 0 Fehler |
| fehlende XML-referenzierte FIP-C#-Typen | 0 |
| private Harmony- oder Debug-DLLs | 0 |
| leere Dateien / Verzeichnisse | 0 / 0 |
| aktive Big-MT-Treffer | 0 |

## 3. Korrekturen dieses Durchgangs

### Assets und Dateinamen

- 27 tatsächlich redundante Texturdateien entfernt:
  - 3 verschachtelte Arktos-Axolotl-Kopien,
  - 3 verschachtelte Lucky-38-Dosenkopien,
  - 3 überzählige Hubris-Mothman-Tree-Dateien,
  - 15 durch gemeinsame lokale RobCo-Texturfamilien ersetzte Kopien,
  - 3 WestTek-Platzhalterkopien.
- 349 Laufzeitassets mit dem Präfix ihres Besitzermods versehen und alle
  Referenzen angepasst.
- Alle 21 Arktos-Ant-Texturen heißen jetzt `Arktos_*`.
- Mod-eigene XML-Dateien ohne Besitzerpräfix wurden umbenannt.

### Defs

- 318 alte IDs in 319 direkten oder benannten Deklarationen auf
  Besitzerpräfixe migriert; Referenzen in XML, Sprachen und C# wurden
  mitgeführt.
- Die RobCo-Gene heißen nun `RobCo_Gene_*` statt `WestTek_Gene_*`.
- Die von WestTek versehentlich generisch deklarierten IDs `Highmate` und
  `SPECIAL` heißen jetzt `WestTek_Xenotype_SNuffy` und `WestTek_SPECIAL`;
  echte Verweise auf Vanilla-`Highmate` bleiben unverändert.
- Greenways direkte, mit Vanilla kollidierende Deklaration
  `ConnectedTreeDied` wurde entfernt. Die benötigte Struktur wird gepatcht und
  der Text per `DefInjected` gesetzt.
- Alle 1.230 direkten Defs sowie benannten abstrakten Eltern besitzen jetzt das
  Präfix ihres Eigentümers.

Die Transformationsregeln und Savegame-Auswirkungen sind in
`FIP_DEFNAME_MIGRATION.md` dokumentiert.

### LanguageData und Textpatches

- 3.272 englische `DefInjected`-Einträge entfernt, deren Wert bereits
  identisch im lokalen Def stand.
- 30 dadurch leere Sprachdateien entfernt.
- 2 doppelte Poseidon-Chemfuel-Schlüssel auf eine kanonische Definition
  reduziert.
- 14 reine Text-Replace-Operationen entfernt, weil bereits ein passender
  `DefInjected`-Eintrag existierte.
- Strukturelle Patchoperationen bleiben erhalten. Wo ein Zieldef das benötigte
  Feld noch nicht besitzt, darf ein Patch das leere Feld anlegen; der sichtbare
  Text kommt anschließend aus `DefInjected`.

### Donaustahl-Packtierrichtlinie

- Die reflektionsbasierte Arktos-Klasse, die sämtliche Händler-Packtiere
  dynamisch in Arktos-Biome kopierte, wurde entfernt.
- Donaustahl normalisiert jetzt per XML alle bereits geladenen
  NPC-Händlergruppen auf Brahmin mit Gewicht `100` und Muffalo mit Gewicht `1`.
- Fehlende `carriers`-Listen werden für vorhandene Trader-GroupMaker angelegt.
- Alle Biome erhalten Muffalo; mit FCP Animals erhalten sie zusätzlich Brahmin.
- Alle 22 Arktos-Biome erben von `Arktos_Nature` genau Horse, Muffalo,
  Brahmin, Bighorner und Radstag.
- Parent-/Child-Biome werden vererbungsbewusst gepatcht, damit ein Tier nicht
  zusätzlich im Kinddef landet, wenn es bereits vom Parent geerbt wird.
- Donaustahl besitzt 110 `loadAfter`-Einträge und lädt nach allen 97 im aktiven
  Bestand bekannten FCP-/Vanilla-Expanded-Paketen sowie nach den übrigen
  spielbaren FIP-Modulen.

## 4. Bewusst identische Assets

Die 11 freigegebenen Gruppen innerhalb eines Mods bestehen aus:

- sieben Richtungs- oder Variantenslot-Gruppen in RobCo,
- vier transparente RobCo-West-Slots in einer gemeinsamen Gruppe,
- sieben leere Think-Tank-North-Varianten in einer gemeinsamen Gruppe,
- zwei Lucky-38-Coffee-Workbench-Richtungsslots,
- zwei Robobrain-Angry-Clips zur beabsichtigten Zufallsgewichtung.

Die Zählung erfolgt nach Hashgruppen, nicht nach Dateipaaren. Diese Dateien
können nicht einfach gelöscht werden: RimWorld erwartet die jeweiligen
Dateinamen für Richtungen oder Varianten, oder das Vorhandensein mehrerer Clips
beeinflusst die Auswahl.

Die 8 modübergreifenden Gruppen sind vor allem eigenständige Greenway-,
H&HTools-, RobCo-, WestTek- und Whitespring-Icons beziehungsweise transparente
Platzhalter. Sie werden nicht zentralisiert, weil die Module unabhängig
installierbar bleiben sollen.

## 5. Build und Quellcode

Die kanonische Mappe `Development/Source/FIP.Managed.sln` enthält 19 Projekte:
15 Arktos-Projekte sowie je eines für Greenway, H&HTools, RobCo und WestTek.

Nach der DefName-Migration wurden alle 19 Projekte neu gebaut. Die erzeugten
DLLs liegen in den jeweiligen Modordnern; der Build endete mit 0 Warnungen und
0 Fehlern. Generierte `obj`-Verzeichnisse wurden anschließend entfernt.

## 6. Savegame-Kompatibilität

Package IDs, Assemblynamen und öffentliche C#-Typnamen blieben stabil.
DefNames wurden dagegen bewusst vereinheitlicht. Damit ist dieser Stand ohne
zusätzliche Migration **nicht vollständig savegame-kompatibel** zu Spielständen
oder externen Patches, die eine der 318 alten IDs referenzieren.

Vor einer Veröffentlichung über einen vorhandenen Modstand müssen deshalb
Alt-Spielstände und externe Integrationen anhand von
`FIP_DEFNAME_MIGRATION.md` getestet oder migriert werden. Big-MT-Spielstände
sind weiterhin nicht Ziel dieses Standes.

## 7. Validierungsgrenzen

Es wurde kein RimWorld-Ingame-Lauf durchgeführt. Noch zu prüfen sind:

- Laden jedes Moduls mit seinen Pflichtabhängigkeiten,
- relevante optionale Modkombinationen und konkrete Fremdmod-Versionen,
- bestehende Spielstände nach der DefName-Migration,
- `Player.log` auf XML-, Patch-, Def-, Asset- und Assemblyfehler,
- Darstellung der Texturvarianten und hörbare Soundgewichtung.

Der bestandene statische Check ist reproduzierbar mit:

```powershell
python Development/Tools/validate_refactor.py
```
