# Modoptionen: Prüfung und Korrektur vom 08.09.2026

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

Korrigiert: Zwei unabhängige Optionen erfassen nur die manuell ausgewählten
VFEM2-Inhalte. Die Strukturoption versteckt Burgmauern, niedrige Burgmauern,
Burgtore und Burgtüren, Stoff-, Fachwerk- und Kopfsteinpflastermauern sowie die
beiden wandmontierten Waffen. Die Möbeloption versteckt Fellbetten, Herd,
heraldische Teppiche und das Standbanner. Alle anderen VFEM2-Gebäude und Böden
bleiben baubar, darunter Bienenstock, Weinfass, Draught Stations, Chemdrench
sowie normale und feine Ledermatten.

## Alle 25 Optionen in neun Modulen

| Modul | Optionen | Ergebnis / Korrektur |
|---|---:|---|
| Arktos | 5: Core, Biotech, VAE, Royal Animals, Odyssey | Tier- und Ei-Defs sowie Biomeinträge bleiben erhalten. Aktivierte Optionen entfernen Händler-Tags und die Eignung für generische Manhunter-Zufallsereignisse; bekannte allgemeine Fraktions-Tierpools werden gezielt bereinigt. Odyssey entfernt zusätzlich die ausgewählten Tiere und Eier aus den vier allgemeinen seltenen Angelbeute-Pools. Spezifische Ereignisse anderer Mods bleiben unangetastet. Bei deaktivierter Option bleibt der XML-Inhalt des Quellmods vollständig unverändert. |
| Donaustahl | 3: Backstories, Ausrüstungsreliquien, Storyteller | Oracle/MedievalPage sowie Cassandra/Phoebe/Randy bleiben genau benannte Ziele. Reliquien werden anhand tatsächlicher Kleidungs-/Waffeneigenschaften erkannt; Namensbestandteile wie `Gun` dürfen kein anderes Objekt erfassen. Ursprüngliche Chancen bleiben wiederherstellbar. |
| Greenway | 3: Ursprünge, Memes, Fraktionen | Native Ursprünge und native normale Memes haben unabhängige Optionen. Von VMemesE werden nur die ursprünglich unterdrückten Defs Anonymity, Serketist und SecularSpirituality erfasst. Der pauschale Filter auf `vanillaexpanded.*` und alle `VME_Structure_*` ist entfernt. Die drei benannten Ideology-Fraktionen und erforderliche Referenzkorrekturen bleiben optional. |
| H&H Tools | 9: Fraktionen, Szenarien, Wandstrukturen, Möbel, Waffen, Kleidung, Medieval-Ursprünge, Quests, Storyteller | Der frühere globale Gebäudefilter ist durch zwei genaue, unabhängige Listen ersetzt. Hardweave und Hayweave sind vollständig wiederhergestellt und besitzen keinen Entfernungsschalter mehr. Waffen und Kleidung behalten ihre Defs, verlieren bei aktivierter Option aber normale Erzeugungs-Tags und Rezeptzugänge. Der Merchant-Guild-Kompatibilitätspatch verwendet bereits die H&H/FCP-Ausrüstungsgruppen. `generateAllowChance` wird nur für den ursprünglich erfassten TorchBelt geändert. |
| Hubris | 1: Storyteller | Basilicus: ursprüngliche Sichtbarkeit wird gespeichert und wiederhergestellt. Kein zusätzliches Ziel. |
| Lucky 38 | 1: Forschungsbaum | Ursprünglicher Brewing-Tab, die drei betroffenen Forschungspositionen und Schematic-Tablisten bleiben wiederherstellbar. |
| RobCo | 1: Mechanoids | Vorhandene native Mechanitor-/Mechanoid-Ersetzungen sind an die Option gebunden. Eigene bzw. fremde Mech-Defs werden nicht global gesperrt. Rezeptlisten und native Kampf-/Clusterflags werden wiederhergestellt; der Rezeptcache wird verworfen. Die beiden Vanilla-Bossrufgebäude `BurnoutMechlinkBooster` und `MechbandDish` bleiben als Defs vorhanden, werden bei aktivierter Option aber aus dem Baumenü ausgeblendet. |
| WestTek | 1: Xenotypes | Die vorhandenen Fraktions-/Sanguophage-Poolpatches bleiben optional. Die beim Umbau hinzugefügte globale Sperre für sämtliche fremden Xenotypen wurde entfernt. Deaktiviert werden diese XML-Patches vollständig übersprungen; kosmetische/inhaltliche Umgestaltungen bleiben bestehen. |
| Whitespring | 1: Storyteller | Ariadne Archduchess/Damocles: ursprüngliche Sichtbarkeit wird wiederhergestellt. Kein zusätzliches Ziel. |

Big MT, Corvega, FutureTec, Poseidon und Repconn besitzen keine entsprechenden
ModSettings-Schalter. Technische Patches wie die Entfernung des inkompatiblen
Chemshine-Muffalo-ThinkTrees bleiben unabhängig von Inhaltsoptionen.

## Prüfung

- Gemeinsamer Release-Build: erfolgreich, ohne Compilerwarnungen/-fehler.
  Fünf zuvor fehlende Settings-Projekte wurden der Projektmappe hinzugefügt,
  sodass der gemeinsame Build künftig alle neun Settings-Module umfasst.
- `Tools/ModOptionsRegression/Run.ps1`: **294 erfolgreiche Prüfungen** gegen
  die ausgelieferten Assemblies und echte RimWorld-Patchoperationen, einschließlich
  aller 25 Optionen mit deaktivierten/aktivierten gespeicherten Werten,
  inverser Migration alter Restore-Werte und Vorrang neuer Einstellungen.
- Die fünf Arktos-Schalter werden mit unveränderten Biomen, Händler-Tags,
  generischer Manhunter-Eignung, Fraktionspools, Ei-Defs sowie allgemeinen und
  spezifischen Begegnungspools geprüft. Die lokal installierten Medieval-Def-Dateien wurden zusätzlich geprüft: alle
  erfassten Nahkampfwaffen/Kleidungs-/Schilddefs besitzen einen lokalen recipeMaker.
- Release-Struktur-/XML-Prüfung: siehe `MODOPTIONS_RELEASE_VALIDATION.md`.

Grenzen: Die Tests laufen außerhalb Unity mit gezielt aufgebauten Def-Fixtures.
Es wurde kein interaktiver RimWorld-Spielstart, vollständiger Modlistenlauf oder
visueller Architect-Menütest durchgeführt. Installierte Workshop-Mods, persönliche
Einstellungen und Saves wurden nicht verändert. Die aktualisierten DLLs liegen
in den spielbaren Modordnern des Repositorys.
