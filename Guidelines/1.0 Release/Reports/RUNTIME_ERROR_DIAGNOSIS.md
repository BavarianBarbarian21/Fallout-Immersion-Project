# FIP 1.0 – Diagnose des All-Mods-Starts

Stand: 2026-08-07

## Ergebnis

Die im bereitgestellten RimWorld-Log enthaltene rote Fehlerkette wurde von vier ungültigen Big-MT-Definitionen ausgelöst. Sie war kein Autosort-Fehler. Die sechs Meldungen aus `TranslationReport.txt` waren Folgefehler: Weil RimWorld die Big-MT-Fraktion und beide PawnKinds zuvor nicht erzeugen konnte, konnten deren englische DefInjected-Einträge anschließend nicht zugeordnet werden.

Die korrigierten Release-Dateien wurden auch gezielt nach `D:\Steam\steamapps\common\RimWorld\Mods` synchronisiert und per SHA-256 mit der validierten Release-Kopie verglichen.

## Behobene Ursachen

1. `BigMT_WestTek_Entities.xml` enthielt `minAge` innerhalb eigener `PawnKindLifeStage`-Einträge. Dieses Feld ist dort in RimWorld 1.6 ungültig. Die unnötigen eigenen LifeStages wurden entfernt; die menschliche Rasse liefert ihre normalen LifeStages.
2. Dieselbe Datei verwendete ungültige `WorkTags` wie `Smithing`, `Tailoring`, `Art` und `Construction`. Die gültigen Enum-Namen lauten unter anderem `Crafting`, `Artistic` und `Constructing`.
3. `BigMT_WestTek_Faction.xml` verwendete für `PawnGenOption` die falsche Listenstruktur mit `<li><kindDef>…`. RimWorld 1.6 erwartet in `options` die PawnKind-DefNames als Dictionary-Schlüssel mit dem Gewicht als Wert.
4. `BigMT_WestTek_Research.xml` referenzierte den nicht vorhandenen `ResearchProjectDef` `HoldingPlatform`. Die gültige Anomaly-Forschung heißt `EntityContainment`.

Big MT deklariert außerdem optionale `loadAfter`-Kanten zu Anomaly, Vanilla Anomaly Expanded – Insanity und FIP WestTek. Das erzeugt keine harten Requirements; die Inhalte bleiben weiterhin über `LoadFolders.xml` optional.

## Zusätzlich behobener LoadFolder-Fehler

Ein Lucky-38-Patch für `VFEPD_EspressoMachine` lag in einer Kombination, die nur Fallout Collaboration Project – Plants und Vanilla Brewing Expanded – Coffees and Teas verlangte. Dadurch konnte der Patch ohne Vanilla Furniture Expanded – Props and Decor aktiv werden.

Der Props-and-Decor-Anteil besitzt jetzt einen eigenen LoadFolder mit der exakten Dreifachbedingung:

`Rick.FCP.Plants, VanillaExpanded.VBrewECandT, VanillaExpanded.VFEPropsandDecor`

## Audit der installierten Ladereihenfolge

- 135 aktive Mods und 135 aufgelöste `About.xml`-Metadaten
- 0 Verletzungen deklarierter Dependencies, `loadAfter`, `loadBefore` oder Force-Varianten
- 0 fehlende belastbare FIP/FCP-Reihenfolgekanten
- 3 belastbare, nicht deklarierte Assembly-Reihenfolgekanten in Drittmods
- 27 optionale Patchbeziehungen mit umgekehrter aktueller Reihenfolge; im gelieferten Lauf trat keine `PatchOperation`-Fehlermeldung auf, deshalb sind sie Hinweise und keine nachgewiesenen Loaderfehler
- 1 privat gebündelte Harmony-Kopie in Dubs Bad Hygiene

### Belastbare Metadatenlücken in Drittmods

1. Vanilla Persona Weapons Expanded referenziert die Assembly von Vanilla Psycasts Expanded, deklariert aber kein entsprechendes `loadAfter`. Die aktuelle Autosort-Reihenfolge lädt Persona Weapons sogar vor Psycasts. Das ist der einzige der drei Fälle, der gegenwärtig falsch herum steht.
2. Vanilla Temperature Expanded referenziert die Assembly von Vanilla Factions Expanded – Tribals ohne direkte Metadatenkante. Die aktuelle Reihenfolge ist zufällig sicher: Tribals steht davor.
3. Vanilla Hair Expanded referenziert `0Harmony` ohne direkte Metadatenkante. Die aktuelle Reihenfolge ist sicher: Harmony steht an Position 1.

Dubs Bad Hygiene liefert eine eigene `0Harmony.dll` mit. Die offizielle Harmony-Mod wird aktuell zuerst geladen. Das ist kein im bereitgestellten Log nachgewiesener Fehler, bleibt aber ein Bibliotheks-Konfliktrisiko außerhalb von FIP/FCP.

## Validierung

Der Abschlussvalidator bestand nach dem ersten Hotfix 65 von 65 Prüfungen; nach Ergänzung der beim zweiten Smoke-Start sichtbar gewordenen Runtime-Schemafelder besteht er 67 von 67 Prüfungen:

- 1.510 XML-Dateien wohlgeformt
- Big-MT-LifeStage-, WorkTag-, PawnGenOption- und Forschungs-Schemata geprüft
- alle LoadFolder-Kombinationen simuliert
- keine FIP/FCP-Def-, Sprach-, Assembly- oder konkrete Patchziel-Kollision
- 22 Projekte mit 0 Warnungen und 0 Fehlern gebaut

Ein neuer RimWorld-Prozess wurde nicht automatisch gestartet. Zur Runtime-Bestätigung muss RimWorld nach dem Dateisync vollständig beendet und neu gestartet werden; nur ein frischer `Player.log` kann beweisen, dass keine weitere dynamische Modinteraktion auftritt.

## Zweiter Smoke-Start

Der zweite bereitgestellte Startlog bestätigt, dass alle oben beschriebenen Primär- und Folgefehler verschwunden sind. Er zeigte noch 13 ausschließlich Big MT zugeordnete Konfigurationsmeldungen:

- sechs `SkillRange`-Feldfehler, weil RimWorld 1.6 `<range>8~14</range>` statt getrennter Felder `minLevel` und `maxLevel` erwartet;
- vier fehlende `initialWillRange`-/`initialResistanceRange`-Werte der beiden menschlichen PawnKinds;
- drei Pflichtangaben der menschlichen Fraktion: Backstory-Filter, Raid-Loot-Kurve und maximale Pawnkostenkurve.

Die Skillbereiche wurden auf das Vanilla-1.6-Schema umgestellt. Crazed Super Mutants verwenden die offiziellen Anomaly-Mutant-Grundwerte für Wille und Widerstand; Nightkin besitzen entsprechend ihrer Beschreibung höhere Werte. Die Fraktion erbt nun von `FactionBase` und definiert zusätzlich einen Anomaly-kompatiblen Cult-Backstory-Filter sowie explizite Raidkurven.
