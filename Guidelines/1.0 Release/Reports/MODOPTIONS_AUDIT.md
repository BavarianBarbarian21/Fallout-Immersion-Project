# Modoptionen: Prüfung und Korrektur vom 07.09.2026

Vertrag: Eine aktivierte Option führt die zugehörige vorhandene FIP-Entfernung
aus. Eine deaktivierte Option lässt die ursprünglichen Inhalte verfügbar.
Es werden keine zusätzlichen Mods pauschal herausgefiltert und keine Defs
zwangsweise hinzugefügt, die ihre Quellmods nicht bereitstellen. Änderungen an
Ladezeit-Optionen benötigen einen Neustart; vorhandene Welten/Pawns werden nicht
rückwirkend neu erzeugt. Texturen, Übersetzungen und technische Kompatibilität
werden dadurch nicht zurückgesetzt.

## Herkunft des Wandfehlers

Der archivierte Sunset-Patch
`Outdated/FIP-Sunset/LoadFolders/Medieval2/Patches/FIP-Sunset/Sunset_VFEMedieval2_Compat.xml`
enthält bereits den XPath
`Defs/ThingDef[designationCategory="Structure" and defName!="VFEM2_Palisade"]/designationCategory`.
Ein Patch im Medieval-LoadFolder wirkt auf den gesamten geladenen XML-Baum,
nicht nur auf die Defs von Medieval. Es fehlte eine Eingrenzung auf die
beabsichtigten Medieval-Gebäude.

Beim ModSettings-Umbau wurde diese Auswahl in `IsTargetBuilding` übernommen.
Die C#-Fassung läuft nach dem Laden der Defs und unabhängig davon, ob Medieval
aktiv ist. Sie erwischte auch geerbte Strukturkategorien und konnte Vanilla-,
FCP- und andere Wände aus dem Baumenü nehmen. Der Fehler ist in der archivierten
XML-Fassung und im C#-Snapshot des Commits `1cfcec1ca` nachweisbar. Daraus lässt
sich kein Auftrag des Modautors ableiten, alle Wände zu entfernen.

Korrigiert: nur Medieval-Gebäude (`VFEM2_`) und die bereits explizit erfassten
Medieval-Heraldik-Gebäude; Palisade, ArcheryTarget und TrainingDummy bleiben
erhalten. Der globale Structure-Filter wurde auch aus der auskommentierten
Vorlage im aktiven Patch entfernt. Der genaue Steam/GitHub-Stand des meldenden
Spielers wurde nicht verglichen.

## Alle 25 Optionen in neun Modulen

| Modul | Optionen | Ergebnis / Korrektur |
|---|---:|---|
| Arktos | 5: Core, Biotech, VAE, Royal Animals, Odyssey | Alle fünf Optionen schalten jetzt die ursprünglichen XML-Entfernungen exakt ein oder aus. Core, VAE und Odyssey bereinigen wie zuvor neben Biomen auch Händler-, Fraktions- oder andere Begegnungslisten; Biotech und Royal Animals bleiben auf Biome begrenzt. Bei deaktivierter Option bleibt der XML-Inhalt des Quellmods vollständig unangetastet. |
| Donaustahl | 3: Backstories, Ausrüstungsreliquien, Storyteller | Oracle/MedievalPage sowie Cassandra/Phoebe/Randy bleiben genau benannte Ziele. Reliquien werden anhand tatsächlicher Kleidungs-/Waffeneigenschaften erkannt; Namensbestandteile wie `Gun` dürfen kein anderes Objekt erfassen. Ursprüngliche Chancen bleiben wiederherstellbar. |
| Greenway | 3: Ursprünge, Memes, Fraktionen | Native Ursprünge und native normale Memes haben unabhängige Optionen. Von VMemesE werden nur die ursprünglich unterdrückten Defs Anonymity, Serketist und SecularSpirituality erfasst. Der pauschale Filter auf `vanillaexpanded.*` und alle `VME_Structure_*` ist entfernt. Die drei benannten Ideology-Fraktionen und erforderliche Referenzkorrekturen bleiben optional. |
| H&H Tools | 9: Fraktionen, Szenarien, Gebäude, Waffen, Kleidung, Textilien, Medieval-Ursprünge, Quests, Storyteller | Gebäudefilter korrigiert. Waffen/Kleidung verwenden wieder die ursprünglichen XML-Eingriffe in `recipeMaker/recipeUsers`, bevor Rezepte erzeugt werden; vorher wurde fälschlich `ThingDef.recipes` am Produkt geleert. `generateAllowChance` wird nur für den ursprünglich erfassten TorchBelt geändert. Textilien und Norse-Ursprung haben jetzt eigene Optionen statt einer unwirksamen Zuordnung zum Gebäudefilter bzw. einer bedingungslosen Sperre. |
| Hubris | 1: Storyteller | Basilicus: ursprüngliche Sichtbarkeit wird gespeichert und wiederhergestellt. Kein zusätzliches Ziel. |
| Lucky 38 | 1: Forschungsbaum | Ursprünglicher Brewing-Tab, die drei betroffenen Forschungspositionen und Schematic-Tablisten bleiben wiederherstellbar. |
| RobCo | 1: Mechanoids | Vorhandene native Mechanitor-/Mechanoid-Ersetzungen sind an die Option gebunden. Eigene bzw. fremde Mech-Defs werden nicht global gesperrt. Rezeptlisten und native Kampf-/Clusterflags werden wiederhergestellt; der Rezeptcache wird jetzt ebenfalls verworfen. |
| WestTek | 1: Xenotypes | Die vorhandenen Fraktions-/Sanguophage-Poolpatches bleiben optional. Die beim Umbau hinzugefügte globale Sperre für sämtliche fremden Xenotypen wurde entfernt. Deaktiviert werden diese XML-Patches vollständig übersprungen; kosmetische/inhaltliche Umgestaltungen bleiben bestehen. |
| Whitespring | 1: Storyteller | Ariadne Archduchess/Damocles: ursprüngliche Sichtbarkeit wird wiederhergestellt. Kein zusätzliches Ziel. |

Big MT, Corvega, FutureTec, Poseidon und Repconn besitzen keine entsprechenden
ModSettings-Schalter. Technische Patches wie die Entfernung des inkompatiblen
Chemshine-Muffalo-ThinkTrees bleiben unabhängig von Inhaltsoptionen.

## Prüfung

- Gemeinsamer Release-Build: erfolgreich, ohne Compilerwarnungen/-fehler.
  Fünf zuvor fehlende Settings-Projekte wurden der Projektmappe hinzugefügt,
  sodass der gemeinsame Build künftig alle neun Settings-Module umfasst.
- `Tools/ModOptionsRegression/Run.ps1`: **288 erfolgreiche Prüfungen** gegen
  die ausgelieferten Assemblies und echte RimWorld-Patchoperationen, einschließlich
  aller 25 Optionen mit deaktivierten/aktivierten gespeicherten Werten,
  inverser Migration alter Restore-Werte und Vorrang neuer Einstellungen.
- Die fünf Arktos-Schalter werden mit Biome-, Händler-, Fraktions-, Ei- und
  Begegnungslisten geprüft, einschließlich vollständiger Wiederherstellung bei
  Deaktivierung. Die lokal installierten Medieval-Def-Dateien wurden zusätzlich geprüft: alle
  erfassten Nahkampfwaffen/Kleidungs-/Schilddefs besitzen einen lokalen recipeMaker.
- Release-Struktur-/XML-Prüfung: siehe `MODOPTIONS_RELEASE_VALIDATION.md`.

Grenzen: Die Tests laufen außerhalb Unity mit gezielt aufgebauten Def-Fixtures.
Es wurde kein interaktiver RimWorld-Spielstart, vollständiger Modlistenlauf oder
visueller Architect-Menütest durchgeführt. Installierte Workshop-Mods, persönliche
Einstellungen und Saves wurden nicht verändert. Die aktualisierten DLLs liegen
in den spielbaren Modordnern des Repositorys.
