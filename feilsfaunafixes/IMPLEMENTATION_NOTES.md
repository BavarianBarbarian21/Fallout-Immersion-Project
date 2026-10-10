# Implementation Notes

## Zielversion und Referenzen

Entwickelt und kompiliert gegen die lokal installierte Vintage-Story-Version 1.22.7 und deren `VintagestoryAPI`, `VintagestoryLib`, `VSEssentials` und `VSSurvivalMod` Assemblies. Es wurden keine Spieldateien verändert.

Als technische Referenz dienten insbesondere:

- `BlockTrough`, `BlockTroughDoubleBlock`, `BlockTroughBase` und `BlockEntityTrough` für Inventar, Doppelblockwurzel, Persistenz und POI-Verhalten.
- `BlockLiquidContainerBase` für litergenauen Transfer aus Vanilla-Flüssigkeitsbehältern.
- `BlockEntityTrough.OnTesselation`, `ContainedTextureSource` und die Vanilla-Flüssigkeitsoberflächen für den stufigen Wasserspiegel im bestehenden Trogmodell.
- `AiTaskSeekBlockAndLay` für einen gemeinsamen Hühner-Legeprozess einschließlich Nest, Hahnprüfung, Vanilla-Eiertyp und Domestikationsgeneration.
- `EntityBehaviorMultiply`/Hungerattribute und die aktuelle Hühner-Entitydefinition für Futter- und Befruchtungsbedingungen.
- `ItemShears`, `CollectibleObject.DamageItem` und Entity-Interaction-Help für die Schur.
- `assets/survival/recipes/cooking/oil.json` für Hornstücke im Kochtopf; dessen Rohfettprozess nutzt sechs Ingame-Stunden.
- Vanilla-Antler-Varianten `game:antler-*-01` bis `-09` und das Hammer-Werkzeugtag.
- Vanilla-Dough-/Bread-Baking-Definitionen für sieben Teigvarianten, Backstufen und Haltbarkeit. Der aktuelle Cloth-Code ist `game:cloth-plain`.
- `BlockEntityBerryBush`, `BEBehaviorFruitingBush` und `BlockEntityFruitTreePart` für erntezustandsgetreuen Fraß ohne Blockzerstörung oder Itemduplizierung.
- Hare-, raccoon- und weitere Kleintierdefinitionen für Flucht-AI, Harvest-Drops und Spawnkonventionen.

## Vanilla-Patches

`assets/feilsfaunafixes/patches/troughs.json` tauscht ausschließlich die Laufzeitklassen der bestehenden kleinen und großen Tröge beziehungsweise deren BlockEntity-Klasse aus. Alle Content-Configs und Futterfunktionen bleiben Vanilla.

`assets/feilsfaunafixes/patches/animals.json` ergänzt Huhn, Schaf und die gemeinsame Vanilla-Ziegen-Entity um das Hydrationsbehavior und einen Wasser-Suchtask. Alle erwachsenen `goat-*`-Varianten nehmen an Hydration und Dungproduktion teil. Beim Huhn wird nur der vorhandene Legetask-Code gegen die abgeleitete hydrierte Variante ausgetauscht. Keine vollständige Tierdefinition wird ersetzt.

## Eigene C#-Klassen

- `FeilsFaunaFixesSystem`: Registrierung, Config-Laden, Debugschalter und frühe Anwendung konfigurierbarer Rezept-/Itemwerte.
- `FaunaConfig`: alle zentralen Phase-1-Balancingwerte.
- `BlockFaunaTroughSmall`, `BlockFaunaTroughLarge`, `BlockEntityFaunaTrough`: nativer Eimertransfer, Ausschluss von Mischinhalt, Wasserverbrauch, Tooltip, Save/Load und clientseitige Wasseroberfläche in 8/16 Füllstufen.
- `EntityBehaviorHydration`: persistente Hydration, kalenderbezogenes Wollwachstum, Dungfortschritt der großen Ziegenartigen, Tierinfo, Schurinteraktion und Scherendauer. Fertiger Dung wird als Vanilla-Bodenspeicher im nächstmöglichen freien, tragfähigen Feld des 3×3-Bereichs um das Tier erzeugt. Die Suche prüft Mittelpunkt, Seiten und Diagonalen und verlangt echte Luft in Solid- und Fluid-Layer; vorhandene Blöcke und Pflanzen werden nie ersetzt. Ohne geeigneten Platz wird die jeweilige Produktion verworfen.
- `AiTaskSeekWater`: Trogscan, asynchrone Vanilla-Pfadnavigation mit korrektem Warten auf die fertige Route, Trinkzeit und Reserveauffüllung.
- `AiTaskHydratedLayEgg`: leitet vom Vanilla-Legetask ab; Wasser überbrückt nur dessen Futterprüfung, Wasser-Eier entfernen immer das Kükenattribut.
- `AiTaskSquirrelForage`: sucht ausschließlich reife alte/neue Beerenbüsche und reife Fruchtbaumteile, klettert bei hoch gelegenen Zielen und setzt den vorhandenen Erntezustand ohne Drops zurück. Scheitert der normale Weg an einem höchstens drei Blöcke dicken und fünf Blöcke hohen Hindernis, wird es nur für dieses bestehende Futterziel senkrecht überquert, sofern Oberkante und Gegenseite frei sind.
- `AiTaskSquirrelClimbEscape`: akzeptiert ausschließlich natürliche senkrechte `log-grown-*`-Stämme beliebiger Holzart mit mindestens sechs zusammenhängenden Stammblöcken und verwirft jeden Kandidaten, der hinter dem Tier in Richtung des Verfolgers liegt. Ohne passenden Baum greift der niedrig priorisierte normale Bodentask. Ein einmal erreichter Baum bleibt während der gesamten Bedrohung das bindende Ziel; kleinere Richtungsänderungen des Verfolgers aktivieren nicht zwischendurch die Bodenflucht. Das Tier steigt an derselben Stammseite bis neben den höchsten erkannten Stammblock und hält dort. Nach Fluchtende bleibt die Position für die nachgeordnete Futter-/Abstiegsentscheidung erhalten. Wände und Stümpfe sind keine Fluchtziele.
- `AiTaskSquirrelDescend`: übernimmt eine nach der Flucht erhaltene Kletterposition mit Priorität 2. Damit liegt er ausdrücklich unter Baumflucht 5, Bodenflucht 4 und Nahrungssuche 3, aber über dem normalen Bewegungsband 1. Nur wenn weder Flucht noch Futterziel aktiv ist, steigt das Tier kontrolliert bis zur gespeicherten Stammfußhöhe ab. Bei neuer Gefahr hält es sofort wieder und übergibt zurück an den Fluchttask.
- `EntityBehaviorSquirrelClimber`: synchronisiert Auf-/Abwärtsrichtung, sperrt horizontale Steuerung während des Kletterns, hält nur minimalen Wandkontakt und setzt ausschließlich vertikale Bewegung. Während einer Baumflucht unterstützt es einen ortsfesten Haltezustand. Außerhalb der Flucht darf es nur bei einem aktiven `PathTraverser` und tatsächlicher Frontalkollision eine vollständige Hindernisquerung beginnen; freie Oberkante, feste Gegenseite, begrenzte Höhe/Dicke und eine Verringerung der Zielentfernung sind Pflicht. Bei Auftauchen eines Feindes wird diese Querung abgebrochen. Bei aktiver Vorwärtsbewegung erkennt es Lücken bis zwei freie Blöcke und setzt nur bei freiem Körperraum und fester Landefläche einen physischen Sprungimpuls. Es existieren weder Teleport noch freie diagonale Wandbewegung.

## Defaults

- Trog: 8/16 Wassereinheiten; 10 Liter Eimerbezug.
- Hydrationsreserve: 2,5; Verbrauch Huhn 0,35, Schaf 0,5 sowie Moschusochse/Goldtakin 1 Einheit pro Tag.
- Wasser-Eier: Multiplikator 0,6; ausschließlich unbefruchtet.
- Wolle: 2,5 volle Wachstumszyklen pro tatsächlichem Weltjahr; 1 Wolle pro Schur.
- Dung: 1 Stück je 2 hydrierte Spieltage pro normaler Ziege, 2 Stück bei Moschusochse und Goldtakin; 64 Dung ergeben nach 480 versiegelten Fassstunden 16 Kompost.
- Hornstufen 01–09: 1, 2, 3, 4, 5, 6, 7, 8, 8 Hornstücke.
- Hornstücke im Kochtopf: 6 Ingame-Stunden, analog zur Vanilla-Rohfettdefinition.
- Kekse: vier rohe Kekse pro Teig plus Hirschhornsalz; gebacken 3,5 × 192 = 672 Frischstunden.
- Eichhörnchen: Fluchtdistanz 14 Blöcke, Klettergeschwindigkeit 0,045, minimale Fluchtbaumhöhe 6 Blöcke, maximale Kletterhöhe 16 Blöcke, maximale Sprunglücke 2 Blöcke, Sattzeit nach einer Portion zufällig 6–12 Ingame-Stunden, Waldminimum 0,35, Temperatur −8 bis 30 °C.

## Eigene Assets

Das Eichhörnchen besitzt ein hierarchisches Cuboid-Shape mit waschbärähnlichem horizontalem Rumpf und vierbeiniger Kreuzgang-/Sprungbewegung, aber eigenem kleinen Nagetierkopf, hellem Bauch und Fang, separaten Augen und Nase, Ohren sowie dreiteilig gebogenem buschigem Schwanz. Getrennte `climbup`- und `climbdown`-Animationen drehen den gemeinsamen Modellwurzelknochen um +90° beziehungsweise −90° und bewegen die Beine diagonal gegeneinander. Vier kleine hart gepixelte Materialtexturen werden gezielt einzelnen Modellflächen zugewiesen.

Die Todesanimationen von Eichhörnchen und Affe verwenden ebenfalls den gemeinsamen Modellwurzelknochen und erfassen zusätzlich alle von Lauf-, Fress-, Leerlauf- und Kletteranimationen bewegten Glieder. Mit dem exklusiven, hoch gewichteten `Average`-Blend, `PlayTillEnd` und anschließendem `Hold` können keine fortlaufenden Arm-, Bein- oder Schwanzbewegungen in den Kadaverzustand durchschlagen.

Zusätzlich beendet das clientseitige `EntityBehaviorDeathAnimation` im Todeszustand ausdrücklich alle Leerlauf-, Lauf-, Fress-, Verletzungs- und Kletteranimationen und hält ausschließlich `die` aktiv. Dadurch bleibt die Kadaverpose auch dann statisch, wenn beim Tod noch eine Taskanimation aktiv war.

Die unsichtbaren Modellwurzelelemente von Eichhörnchen und Affe liegen wie bei hierarchischen Vanilla-Entity-Shapes tatsächlich im horizontalen Entity-Mittelpunkt `[8,0,8]`. Sämtliche direkten Kindteile sind relativ zu dieser Wurzel modelliert; Füße beziehungsweise Hände beginnen auf Bodenhöhe. Dadurch fallen Mesh-Mittelpunkt, Auswahlbox und vertikale Drehachse zusammen. Die physische Kollisionsbox bleibt klein, während eine getrennte, das sichtbare Modell umfassende Auswahlbox zuverlässiges Anvisieren und die Namensanzeige ermöglicht.

Das platzierbare `squirrel-skeleton` verwendet die winzige Vanilla-Kadavergeometrie, droppt beim Abbau ausschließlich sich selbst und keine Knochen. Beim Schlachten entstehen exakt ein `game:bushmeat-raw`, eine `game:hide-raw-small` und dieses Gerippe. Ein ungeschlachteter Kadaver verfällt ebenfalls zum gleichen Gerippe.

Wolle, Hornstücke und alle drei Keksstufen verwenden eigene echte Cuboid-Shapes statt flacher gemalter Inventarillustrationen. Das flache, leicht unregelmäßige Keksmodell übernimmt Grundfläche, Bodenhöhe und Darstellungs-Transforms des Vanilla-Brots, damit rohe und gebackene Kekse an dessen Ofenposition ausgerichtet bleiben, besitzt aber eine deutlich eigenständige Geometrie. Ihre Texturen sind kleine, hart gerasterte Materialkacheln nach den lokalen Vanilla-Texturkonventionen. Hornstücke verwenden direkt die Vanilla-Knochenmaterialtextur. Hirschhornsalz verwendet ausdrücklich dieselbe Form, Steinsalztextur und GUI-Transformation wie das aktuelle Vanilla-Salz (`game:item/resource/crushed/normal` und `game:item/resource/crushed/rocksalt`). Die alten illustrativen ImageGen-Icons werden von keinem Item mehr referenziert.

Dung besitzt ein niedriges, aus vier unregelmäßigen Cuboids aufgebautes 3D-Shape. Die 32×32-Materialkachel wurde aus einem eigens erzeugten trockenen, strohigen Dungkonzept auf eine kleine gedeckte Braunpalette reduziert und wird als echte Oberflächentextur statt als gemaltes 3D-Icon verwendet. Dung bleibt eine normale Itemklasse ohne Knapping-Behavior oder Knapping-Rezept. Seine Bodenspeicherdefinition verwendet das Vanilla-Torf-Stapellayout mit 32 Plätzen, Dungtextur und einer Bodenstapel-Brenndauer analog zu Feuerholz.

## Automatische Verifikation

- C#-Kompilierung gegen die 1.22.7-Assemblies und .NET 10 erfolgreich.
- Installierbare ZIP mit `modinfo.json`, DLL, Dokumentation und Assets erzeugt.
- Isolierter dedizierter 1.22.7-Testserver lud den Mod bis `RunGame`: ModSystem registriert, sämtliche eigenen Patches ohne Fehler angewandt, 24 Itemvarianten und die Eichhörnchen-Entity geladen, Kochtopf- und Grid-Rezepte registriert.
- Der Servertest verwendete einen separaten Datenpfad und veränderte weder Vanilla-Dateien noch Benutzerwelten.

## Nicht abschließend ohne manuellen Client-/Ingame-Test verifiziert

- Wassertransfer-Animation und sämtliche konkreten Eimerzustände im Client. Die dynamische Wasseroberfläche ist implementiert, ihre Ausrichtung und Transparenz müssen an sämtlichen Nord-/Süd-/Ost-/West-Varianten praktisch angesehen werden.
- Tatsächliche Langzeit-Dungproduktion, Bodenplatzierung/-aufnahme, Brennverhalten und Fassverarbeitung im laufenden Client; Definition, Persistenz, Rezept und Serverladung sind automatisiert prüfbar.
- Langzeit-Raten und Konkurrenz mehrerer Tiere um denselben Trog unter realer Pfadfindung.
- Sichtbarer Hold-Fortschritt der 1,5-Sekunden-Schur sowie Animation/Meshwechsel am Schaf; der Wollzustand wird als HUD-Prozentwert dargestellt, das Vanilla-Schafmodell selbst wird nicht optisch kahl geschaltet.
- Exakte Befruchtungs-/Domestikationsresultate über mehrere Generationen, obwohl der Vanilla-Legeprozess erhalten bleibt und nur Wasser-Eier ihr Kükenattribut verlieren.
- Backofen-Meshdarstellung aller Teigvarianten und sensorisches Haltbarkeits-/Sättigungsbalancing im Client.
- Eichhörnchen-Animation, Baumkontakt, Halten in Baumkronen, Oberkantenquerung an Mauern/Zäunen, Zweiblock-Sprung, Wegfindung zu komplex verzweigten Obstbäumen und Verhalten an ungewöhnlichen Modblöcken. Normale Vanilla-Blätter besitzen keine Kollisionsbox; normale und asthaltige Blätter werden von der Routenprüfung als betretbarer Baumkronenraum behandelt. Das tatsächliche Zusammenspiel mit sämtlichen Ast-/Modblock-Kollisionsformen benötigt eine praktische Belastungsprobe.
