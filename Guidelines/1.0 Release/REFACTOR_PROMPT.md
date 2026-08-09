# Arbeitsauftrag: Fallout Immersion Project 1.0 vollständig refaktorieren

Du arbeitest ausschließlich in:

`Guidelines/1.0 Release/`

Die Verzeichnisse `New-Mods/`, die bisherigen FIP-Ordner im Repository-Root und andere Quellen sind nur Referenzmaterial. Verändere sie nicht. Bewahre vorhandene Änderungen in der 1.0-Arbeitskopie und arbeite inkrementell. Erstelle vor größeren Umbauten eine Bestandsaufnahme und validiere nach jeder abgeschlossenen Phase.

## Ziel

Erstelle aus der vorhandenen Arbeitskopie eine wartbare, konfliktarme und veröffentlichungsfähige FIP-Version 1.0 für RimWorld 1.6. Jeder Inhalt soll einen klaren fachlichen Eigentümer haben. Optionale Integrationen müssen ohne die Zielmods sicher inaktiv bleiben. Mehrfachreferenzen sind erlaubt, wenn eine echte Kombination mehrerer Mods benötigt wird, aber ein konkreter Patch darf niemals auf mehrere FIP-Module aufgeteilt oder doppelt implementiert werden.

## Verbindliche FIP-Rollen

- **FIP Arktos – Immersive Biomes:** Biome, Natur, Tiere, Pflanzen, Fische, Ruinen und Landmarks.
- **FIP Corvega – Immersive Transportation:** Vehicle Framework und Vanilla Vehicles Expanded samt Erweiterungen.
- **FIP Donaustahl – Immersive RimWorld:** RimWorld-Core-Retheme, allgemeine Vanilla-Systeme, Books, Skills, Outposts, Events, Backstories, Traits, Hair und Textures.
- **FIP Future-Tec – Immersive Quests:** Vanilla Quests Expanded und andere Quest-Rethemes.
- **FIP Greenway – Immersive Ideology:** Ideology, Memes, Dryads, Rollen und ideologische Integrationen.
- **FIP H&H Tools – Immersive Factions:** gemeinsame Fraktionen, Faction Equipment, Medieval 2, Tribals, Settlers und Vanilla Base Generation Expanded.
- **FIP Hubris – Immersive Psycasts:** Psycasts und Psycast-Erweiterungen. Royalty darf optional unterstützt werden, ist aber kein Requirement.
- **FIP Lucky 38 – Immersive Hospitality:** Hospitality, Casino, Spa, Gastronomy, Cooking, Brewing, Händler und Service-Mechanoids.
- **FIP Poseidon – Immersive Energy:** Energie, Industrie, Gas, Chemfuel, Temperatur, Recycling und Produktions-/Versorgungsgebäude.
- **FIP Repconn – Immersive Gravships:** Odyssey, Gravships und gravshipspezifische Kombinationen wie Gravship plus Skills.
- **FIP RobCo – Immersive Mechanoids:** Biotech-Mechanoids, Mechanitors, Roboter und synthetische Pawns.
- **FIP Big MT – Immersive Anomalies:** Anomaly, Vanilla Anomaly Expanded – Insanity und Big-MT-Experimente.
- **FIP WestTek – Immersive Xenotypes:** Biotech-Gene, Xenotypes, Mutanten, Vanilla Genetics Expanded und Sanguophages.
- **FIP Whitespring – Immersive Royalty:** Royalty, Empire, Deserters und Persona Weapons.

FIP Sunset wird vollständig eingestellt. Seine Medieval-2-, Settlers- und Tribals-Inhalte gehen in H&H Tools auf.

## Package- und Workshop-Identität

- Der 1.0-Mod **FIP Big MT** verwendet absichtlich die ehemalige Sunset-Package-ID `FIP.Sunset`.
- Big MT übernimmt außerdem Sunsets Workshop-ID `3760676309`.
- In der 1.0-Ausgabe darf kein eigenständiger FIP-Sunset-Mod mehr existieren.
- Die alte WIP-ID `FIP.BigMT` darf in der 1.0-Ausgabe nicht als aktive Package-ID verwendet werden.
- Der Anzeigename bleibt `FIP - Big MT`; Package-ID und Anzeigename dürfen sich deshalb bewusst unterscheiden.
- Prüfe alle internen und übersetzungsbezogenen Verweise auf die neue Bedeutung von `FIP.Sunset`.

## Harte Requirements

Am Ende der Refaktorierung sind genau diese fachlichen Requirements erlaubt:

1. **FIP Greenway** benötigt RimWorld – Ideology.
2. **FIP Repconn** benötigt RimWorld – Odyssey.
3. **FIP RobCo** benötigt RimWorld – Biotech.
4. **FIP WestTek** benötigt RimWorld – Biotech.
5. **FIP Whitespring** benötigt RimWorld – Royalty.

Keine anderen DLCs, externen Contentmods oder FIP-Module dürfen harte fachliche Requirements sein. Insbesondere:

- Arktos darf H&H Tools nicht hart benötigen.
- Greenway darf H&H Tools nicht hart benötigen.
- WestTek darf H&H Tools nicht hart benötigen.
- Hubris darf Royalty nicht hart benötigen.
- Big MT darf Anomaly nicht hart benötigen; ohne Anomaly muss der Mod über seinen sicheren Basisordner trotzdem fehlerfrei laden.
- Corvega darf Vehicle Framework oder die Vehicles-Expanded-Module nicht hart benötigen; alle Fahrzeuginhalte sind optional.

### Technische Harmony-Abhängigkeit

Harmony ist eine Library und besitzt keinen FIP-Inhalt. FIP darf Harmony weder verändern noch eine eigene `0Harmony.dll` mitliefern. Der Zielzustand soll keine zusätzliche harte Harmony-Abhängigkeit haben. Entferne eine bestehende Harmony-Dependency jedoch niemals, solange eine immer geladene Assembly direkt `HarmonyLib` referenziert.

Falls Harmony optional werden soll:

1. Trenne Harmony-abhängigen Code in eine eigene Assembly.
2. Lade diese Assembly ausschließlich über einen lesbar benannten `IfModActive="brrainz.harmony"`-LoadFolder.
3. Stelle sicher, dass die immer geladene Basis-Assembly keine `HarmonyLib`-Typreferenz mehr enthält.
4. Erst danach darf der harte Harmony-Eintrag entfernt werden.

Lucky 38, RobCo und WestTek müssen darauf ausdrücklich geprüft werden. Es dürfen keine `Unpatch`, `UnpatchAll` oder Eingriffe in fremde Harmony-Patches eingeführt werden. Jeder FIP-Mod verwendet eine eindeutige Harmony-ID.

## Libraries und Frameworks

- **Harmony:** technische Library, kein fachlicher Patch-Eigentümer.
- **Vanilla Expanded Framework:** technische Library, kein fachlicher Patch-Eigentümer. Die Benutzung eines `VEF.*`-Typs ist noch kein Patch des Framework-Kerns.
- **Vehicle Framework:** fachlich Corvega zugeordnet, aber optional und kein hartes Requirement.
- **Vanilla Base Generation Expanded:** fachlich H&H Tools zugeordnet, aber optional und kein hartes Requirement.

Ein Framework darf nur dann direkt gepatcht werden, wenn ein konkreter Framework-Def oder eine Framework-Funktion tatsächlich geändert werden muss. Eine bloße API-/Klassennutzung zählt nicht als eigener Framework-Patch.

## LoadFolder-Regeln

### Sicherer Basisordner

Jeder spielbare FIP-Mod besitzt einen immer geladenen Basisordner. Dieser heißt nach Möglichkeit wie der Mod und nicht technisch `Base`:

- `LoadFolders/Arktos`
- `LoadFolders/BigMT`
- `LoadFolders/Corvega`
- `LoadFolders/Donaustahl`
- `LoadFolders/FutureTec`
- `LoadFolders/Greenway`
- `LoadFolders/HHTools`
- `LoadFolders/Hubris`
- `LoadFolders/Lucky38`
- `LoadFolders/Poseidon`
- `LoadFolders/Repconn`
- `LoadFolders/RobCo`
- `LoadFolders/WestTek`
- `LoadFolders/Whitespring`

Der Basisordner ist der erste, bedingungslose Eintrag in `LoadFolders.xml`. Bei einem Pflicht-DLC muss dessen Name nicht im Basisordner wiederholt werden: RobCos Basisordner heißt beispielsweise `RobCo`, nicht `Biotech`.

Jeder Basisordner enthält mindestens eine gültige, sicher ladbare Datei. Falls ein Mod wie Corvega oder der anfängliche Big-MT-Platzhalter noch keinen unabhängigen Basisinhalt besitzt, verwende einen harmlosen, eindeutig benannten Keyed-Eintrag als Dummy. Erzeuge dafür keinen gameplay- oder save-relevanten Dummy-Def.

Der Basisordner darf keine Defs, Typen oder Assemblies optionaler Mods referenzieren. Inhalte für optionale Mods gehören vollständig in bedingte LoadFolder.

### Lesbare Ordnernamen

LoadFolder-Namen müssen von Menschen lesbar und fachlich verständlich sein. Verwende keine Package-IDs und keine generierten Namen wie:

- `Equipment_OskarPotocki_VanillaFactionsExpanded_SettlersModule`
- `VFFE.plantsEV`
- `Rick_FCP_Core_Tools_VanillaExpanded_VCookE`

Verwende stattdessen beispielsweise:

- `Settlers`
- `Medieval2`
- `Plants_Cooking`
- `MechanoidWaiter_RobCo`
- `Empire_Donaustahl`
- `Anomaly_Insanity`

Ordnernamen sollen kurz, stabil und trotzdem eindeutig sein. Kombinationen werden mit Unterstrichen aus lesbaren Mod- oder Feature-Namen gebildet.

### Exakte Bedingungen

- Ein einzelner optionaler Mod verwendet `IfModActive`.
- Eine Kombination mehrerer Mods verwendet `IfModActiveAll` mit allen tatsächlich benötigten Package-IDs.
- Bedingungen dürfen nicht künstlich verbreitert werden.
- Jeder eingetragene LoadFolder muss physisch existieren.
- Ein bedingter Ordner darf nur Inhalte enthalten, die genau zu seiner Bedingung passen.
- `loadAfter` ist nur eine Reihenfolgeangabe, keine Ownership und kein Requirement.

Beispiel Lucky 38:

Der Mechanoid-Waiter-Retheme wird nur geladen, wenn **Mechanoid Waiter und FIP RobCo gleichzeitig** aktiv sind. Der komplette Patch liegt in Lucky 38, beispielsweise in `LoadFolders/MechanoidWaiter_RobCo`. RobCo enthält keine zweite Hälfte und keine Kopie dieses Patches.

## Ownership von Kombinationspatches

Jeder konkrete Patch besitzt genau einen FIP-Eigentümer. Ein Mod darf in den Bedingungen verschiedener FIP-Module vorkommen, wenn unterschiedliche fachliche Kombinationen existieren. Das ist keine unerwünschte Dopplung, solange nicht derselbe Def-Pfad oder Sprachschlüssel mehrfach verändert wird.

Entscheide den Eigentümer nach dem Feature, das angepasst wird:

- Mechanoid Waiter plus RobCo: vollständig Lucky 38, weil das Service-/Hospitality-Verhalten angepasst wird.
- Gravship plus Skills: vollständig Repconn, weil Gravship-Expertisen zu Hellion-Expertisen werden. Vanilla Skills Expanded bleibt als allgemeines System Donaustahl zugeordnet.
- Greenway plus Books/Memes: vollständig Greenway, wenn der gepatchte Inhalt ein Ideology-Meme ist. Vanilla Books Expanded bleibt als allgemeines System Donaustahl zugeordnet.
- Empire plus Donaustahl: vollständig Whitespring. Whitespring besitzt sowohl die normale Plasteel-Fassung als auch die Donaustahl-abhängige Saturnite-Fassung. Beide Sprachvarianten müssen gegenseitig ausschließend sein.
- Medieval 2, Tribals und Settlers: vollständig H&H Tools; alle ehemaligen Sunset-Patches werden dorthin migriert.

Prüfe nicht nur doppelte Package-IDs, sondern insbesondere doppelte Ziele aus:

- Def-Typ plus DefName plus Feld/XPath
- DefInjected-Sprachschlüssel
- Keyed-Sprachschlüssel
- Texturpfad
- Harmony-Zielmethode

## Texturen und andere Assets

- Sämtliche Texturen eines FIP-Mods liegen immer im bedingungslos geladenen mod-eigenen Basisordner.
- Optionale LoadFolder enthalten keine `Textures`-Verzeichnisse.
- Verschiebe vorhandene optionale Texturen in den jeweiligen Modulordner und aktualisiere alle `texPath`, `uiIconPath`, GraphicData- und Codepfade.
- Entferne nur nachweislich identische Duplikate.
- Prüfe Groß-/Kleinschreibung, Richtungsvarianten, MultiTextures und alle referenzierten Dateien.
- Kern-Assemblies dürfen im Modulordner liegen. Assemblies, die Typen optionaler Mods referenzieren, müssen dagegen in einen passenden bedingten LoadFolder oder technisch entkoppelt werden.

## Inhaltliche Migrationen

### Sunset nach H&H Tools

Migriere alle Sunset-Inhalte nach H&H Tools:

- Medieval-2-Rethemes und Patches
- Settlers-Rethemes und Patches
- Tribals-Rethemes
- Ideology-Kombinationen
- FCP-Animals-Kombinationen
- H&H-Tools-Kombinationen
- Archery-Target- und Training-Dummy-Texturen

Kombinationen, die nur deshalb `HHTools` im Namen oder in der Bedingung hatten, weil Sunset ein separater Mod war, werden nach der Integration vereinfacht. Entferne anschließend veraltete `Sunset_`-Dateinamen und Texturpfade, sofern keine echte Save-Kompatibilität dagegen spricht. Sunset besitzt derzeit keine eigenen direkten Defs, daher ist das Save-Risiko der Migration gering.

### Big MT

Der vorhandene Big-MT-Dummy ist die sichere Ausgangsbasis. Ergänze später optionale, lesbar benannte LoadFolder für:

- `Anomaly`
- `Anomaly_Insanity`
- gegebenenfalls `Anomaly_WestTek`
- gegebenenfalls `Anomaly_HHTools`

Übernimm Anomaly-/Insanity-Inhalte aus H&H Tools nur als vollständige, fachlich Big MT gehörende Patches. Big MT muss ohne Anomaly weiterhin laden können.

### Empire

Alle Vanilla-Factions-Expanded-Empire-Patches gehören Whitespring. Donaustahls bisherige Saturnite-Empire-Dateien überschreiben dieselben zehn Sprachschlüssel wie Whitespring und werden deshalb als bedingte Whitespring-Variante konsolidiert. Donaustahl behält die allgemeine Plasteel-zu-Saturnite-Umbenennung, aber keine eigene Empire-Patchhälfte.

## FCP, Anthrosonae, Artefacts Expanded und Combat Extended

- FCP-Integrationen bleiben optional und gehören jeweils dem fachlich passenden FIP-Mod. FCP selbst erhält keinen FIP-Eigentümer.
- Anthrosonae ist nur Inspirations-/Referenzmaterial und wird nicht als Abhängigkeit übernommen.
- Artefacts Expanded wird nicht als Ownership-Ziel behandelt.
- Combat Extended und seine Module gehören nicht zur FIP-Refaktorierung. Entferne keine CE-Mods aus der privaten Spielinstallation, aber erzeuge keine FIP-Patches oder Requirements dafür.

## Übersetzungen

Refaktoriere zuerst alle englischen Quellmodule. Bearbeite die vier Übersetzungsmodule erst, wenn Package-IDs, DefNames, Keyed-Einträge, Ordnernamen und Ownership stabil sind.

Danach:

- entferne veraltete Sunset-Verweise und behandle `FIP.Sunset` als Big MT,
- aktualisiere verschobene DefInjected- und Keyed-Einträge,
- entferne Übersetzungen nicht mehr vorhandener Defs,
- erzeuge keine Übersetzungsduplikate für denselben Schlüssel,
- lasse Load-Reihenfolgen nur auf tatsächlich übersetzte Quellmods zeigen.

## Vorgehensweise

1. Inventarisiere jeden Mod: Package-ID, Requirements, LoadFolder, Defs, Patches, Languages, Assemblies, Texturen und externe Ziele.
2. Erzeuge eine Zielmatrix `externe Mod → fachlicher FIP-Eigentümer → konkrete Patchdateien`.
3. Finde identische Ziele und entscheide bewusst zwischen Zusammenführung, Varianten oder legitimen Kombinationspatches.
4. Migriere Sunset vollständig nach H&H Tools und aktiviere Big MT unter der Sunset-Identität.
5. Korrigiere Requirements und entkopple alle optionalen FIP-/DLC-/Mod-Verweise aus den Basisordnern.
6. Benenne Basis- und optionale LoadFolder lesbar um.
7. Zentralisiere sämtliche Texturen im jeweiligen Modulordner und repariere Referenzen.
8. Refaktoriere Harmony-Code nur mit Build- und Laufzeitprüfung.
9. Aktualisiere die Übersetzungsmodule zuletzt.
10. Validere jede Modkombination und dokumentiere alle bewussten Ausnahmen.

## Abnahmekriterien

- Alle XML-Dateien sind wohlgeformt.
- Alle Package-IDs der 1.0-Ausgabe sind eindeutig; `FIP.Sunset` gehört ausschließlich Big MT.
- Es existiert kein spielbarer FIP-Sunset-Ordner mehr.
- Jeder spielbare FIP-Mod besitzt einen existierenden, nichtleeren, immer geladenen Modulordner.
- Jeder in `LoadFolders.xml` genannte Pfad existiert.
- Kein Basisordner referenziert einen optionalen Mod.
- Die einzigen fachlichen Requirements sind Greenway→Ideology, Repconn→Odyssey, RobCo→Biotech, WestTek→Biotech und Whitespring→Royalty.
- Harmony wird nicht mitgeliefert oder manipuliert; optionale Harmony-Nutzung kann ohne Loaderfehler fehlen.
- Alle Texturen liegen im jeweiligen immer geladenen Modulordner.
- Keine optionalen LoadFolder enthalten Texturen.
- Alle Textur- und UI-Pfade zeigen auf vorhandene Dateien.
- Keine konkreten Patchziele oder Sprachschlüssel werden unbeabsichtigt von mehreren FIP-Mods überschrieben.
- Lucky 38 enthält den vollständigen Mechanoid-Waiter-plus-RobCo-Patch; RobCo enthält keine zweite Hälfte.
- Whitespring enthält sämtliche Empire-Patches einschließlich der Donaustahl-abhängigen Saturnite-Variante.
- H&H Tools enthält sämtliche ehemaligen Sunset-Inhalte.
- Big MT lädt ohne Anomaly fehlerfrei und aktiviert Anomaly-/Insanity-Inhalte nur über exakte optionale LoadFolder.
- Corvega lädt ohne Vehicle Framework und ohne Vehicles Expanded fehlerfrei.
- C#-Projekte bauen reproduzierbar und ihre Assemblies referenzieren keine fehlenden optionalen Libraries.
- Teste mindestens: Minimalbestand jedes FIP-Mods, jede einzelne optionale Integration, jede Mehrfachkombination sowie den vollständigen vorgesehenen Modsatz.

Arbeite bis zu einem tatsächlich validierten 1.0-Zustand. Berichte nach jeder Phase knapp: verschobene Ownership, geänderte Requirements, umbenannte LoadFolder, behobene Zielkollisionen, Build-/Validator-Ergebnisse und verbleibende Risiken.
