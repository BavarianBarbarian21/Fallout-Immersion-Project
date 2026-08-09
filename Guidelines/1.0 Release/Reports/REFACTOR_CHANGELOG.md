# FIP 1.0 – Refactor-Changelog

## Ergebnis

Die Release-Arbeitskopie enthält 14 spielbare FIP-Module und vier Übersetzungsmodule. FIP Sunset existiert nicht mehr als eigener spielbarer Mod. FIP Big MT verwendet absichtlich `FIP.Sunset` und die Workshop-ID `3760676309`.

Die ursprünglichen FIP-Ordner im Repository-Root, `New-Mods/` und sonstige Referenzquellen wurden nicht verändert.

## Requirements

Es bleiben exakt fünf harte fachliche Abhängigkeiten:

- Greenway → RimWorld Ideology
- Repconn → RimWorld Odyssey
- RobCo → RimWorld Biotech
- WestTek → RimWorld Biotech
- Whitespring → RimWorld Royalty

Arktos, Corvega, Donaustahl, FutureTec, H&H Tools, Hubris, Lucky 38, Poseidon und Big MT besitzen keine harten Content-Requirements. Harmony ist nirgends ein hartes Requirement.

## Struktur und LoadFolders

- Jedes Gameplay-Modul besitzt einen nichtleeren, bedingungslos geladenen Modulordner als ersten Eintrag.
- Es existiert kein technisch benannter `Base`-Ordner mehr.
- Corvega und Big MT besitzen harmlose Keyed-Marker statt gameplay- oder save-relevanter Dummy-Defs.
- Sämtliche optionalen Inhalte liegen in physischen, exakt bedingten und lesbar benannten LoadFolders.
- Rohbezeichnungen wie `VPsycastsE`, `FCPPlants_*` und `VanillaFoodVarietyExpanded` wurden zu `Psycasts`, `FCP_Plants_*` und `FoodVariety` bereinigt.
- Statische Tests decken Minimalbestand, jede einzelne optionale Integration, das Weglassen jedes Bestandteils einer Mehrfachbedingung und den vollständigen Bedingungssatz ab.

## Ownership-Migrationen

- Alle ehemaligen Sunset-Inhalte für Medieval 2, Settlers und Tribals einschließlich Kombinationen, Sprachen und Texturen liegen vollständig in H&H Tools.
- Anomaly-, Insanity- und WestTek-Anomaly-Inhalte liegen vollständig in Big MT. Der `BigMT`-Basisordner enthält keine Anomaly-Defs.
- WestTeks ehemaliger Horax-Cult-Anomaly-Patch liegt als vollständige `Anomaly_WestTek`-Kombination in Big MT.
- Sämtliche Empire-Patches liegen in Whitespring. Normale Materialtexte und Donaustahl/Saturnite-Texte werden gegenseitig ausschließend geladen.
- Donaustahl behält allgemeine Saturnite-Terminologie, aber keine Empire-Patchhälfte.
- Mechanoid Waiter plus RobCo liegt vollständig in Lucky 38; RobCo enthält keine zweite Patchhälfte.
- WestTek besitzt eigene NameMaker-Defs und Namenslisten und benötigt H&H Tools deshalb nicht mehr.
- Psycast-/Anima-Sprachschlüssel gehören Hubris; Empire-Schlüssel Whitespring; verschobene RobCo-, Repconn- und H&H-Tools-Schlüssel besitzen jeweils nur noch einen Eigentümer.
- Combat Extended wurde nicht als Requirement oder Patchziel übernommen.

## Texturen und Assets

- Alle 489 Texturdateien liegen im bedingungslos geladenen Ordner ihres jeweiligen FIP-Moduls.
- Kein optionaler LoadFolder besitzt ein `Textures`-Verzeichnis.
- Alle 291 FIP-eigenen XML-Texturverweise beziehungsweise 133 eindeutigen virtuellen Pfade lösen mit exakter Groß-/Kleinschreibung auf.
- Der fehlerhafte doppelte Whitespring-Pfad des Circle-of-Steel-Icons wurde korrigiert.
- Sunset-Texturen in H&H Tools wurden auf H&H-Tools-Pfade umgestellt; veraltete `Sunset_`-Dateinamen und Pfade wurden entfernt.

## Harmony und Assemblies

- Lucky 38, RobCo und WestTek laden Harmony-Code ausschließlich über `LoadFolders/Harmony` mit `IfModActive="brrainz.harmony"`.
- RobCo und WestTek besitzen getrennte Basis- und Harmony-Projekte. Ihre immer geladenen Basis-DLLs referenzieren `0Harmony` nicht.
- Lucky 38 besitzt ausschließlich eine optionale Harmony-DLL.
- Es wird keine private `0Harmony.dll` mitgeliefert.
- Die Harmony-IDs `FIP.Lucky38.VanillaTradingExpanded`, `FIP.RobCo.SyntheticPawns` und `FIP.WestTek` sind eindeutig.
- Es existieren keine `Unpatch`- oder `UnpatchAll`-Aufrufe.
- Alle 22 Projekte der Solution bauen in Release mit 0 Warnungen und 0 Fehlern. Die 22 ausgelieferten Assembly-Identitäten sind eindeutig.

## Übersetzungen

- Chinesisch (vereinfacht und traditionell), Japanisch, Koreanisch und Russisch wurden nach Abschluss der englischen Ownership-Migration aktualisiert.
- Verschobene WestTek-NameMaker und Namenslisten verwenden die neuen WestTek-Schlüssel und -Pfade.
- Veraltete Sunset-Szenarioverweise wurden auf H&H Tools umgestellt.
- `FIP.Sunset` ist in allen vier About-Dateien ausdrücklich als Big-MT-Identität dokumentiert.
- Harmony wurde aus den `loadAfter`-Listen entfernt, da die Library keinen übersetzten FIP-Inhalt besitzt.
- Die vier `loadAfter`-Mengen sind angeglichen. Alle fünf Sprachvarianten sind frei von doppelten Keyed- beziehungsweise DefInjected-Signaturen.

## Kollisionsprüfung

- 1.252 direkte Def-Identitäten: keine modulübergreifende Dopplung.
- 3.693 englische Spracheinträge: keine modulübergreifende Dopplung.
- 8.105 konkrete XPath-plus-Feld-Signaturen: keine modulübergreifende Dopplung.
- Zwei bewusste gemeinsame Root-XPaths bleiben: `Ancients` und `AncientsHostile`. H&H Tools ergänzt dort Benennungsfelder, WestTek dagegen `xenotypeSet`; die konkreten Felder überschneiden sich nicht.

## Validierung

Der reproduzierbare Abschlussvalidator liegt in `Tools/Validate-Release.ps1`. Der letzte Lauf bestand 70 von 70 Prüfungen:

- 1.513 wohlgeformte XML-Dateien, 0 ungültige Dateien
- eindeutige Package- und Assembly-Identitäten
- exakt fünf erlaubte harte Requirements
- vollständige LoadFolder- und Condition-Simulationen
- keine fehlenden oder falsch geschriebenen FIP-Texturpfade
- keine unbeabsichtigten Def-, Sprach- oder Patchziel-Kollisionen
- Big-MT-Runtime-Schema, Anomaly-Forschungsreferenz und optionale `loadAfter`-Kanten geprüft
- Big-MT-SkillRange-, Gefangenen- und FactionDef-Pflichtfelder geprüft
- neun sichtbare Szenarien als vollständige Textverträge geprüft; kein Titel, keine Beschreibung, Zusammenfassung oder Startmeldung kann einzeln auf Vanilla zurückfallen
- Lucky-38-Props-and-Decor-Patch nur bei exakt passender Dreifachbedingung aktiv
- vollständiger Release-Build mit 0 Warnungen und 0 Fehlern

Der einzige nicht automatisierbare Release-Schritt ist ein manueller RimWorld-1.6-Smoke-Start mit dem tatsächlich vorgesehenen installierten Modsatz. Er ist vor dem Workshop-Publishing weiterhin empfohlen.
