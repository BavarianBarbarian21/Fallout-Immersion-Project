# Feil's Fauna Fixes

**Feil's Fauna Fixes** (`feilsfaunafixes`) ist ein Code-/Content-Mod für Vintage Story 1.22.7. Er erweitert die vorhandenen Futtertröge um sichtbar ansteigendes Wasser, gibt Hühnern, Schafen und allen Ziegen einen persistenten Hydrationsvorrat und ergänzt passive Eier, Schafwolle, Dung, Hornverarbeitung, haltbare Kekse sowie eigene kletternde Kleintiere.

## Installation

1. Vintage Story 1.22.7 beenden.
2. `feilsfaunafixes_1.01.zip` unverändert in den Benutzerordner `VintagestoryData/Mods` kopieren.
3. Das Spiel starten und den Mod im Modmanager aktivieren.

Die ZIP muss nicht entpackt werden. Für eine Entwicklungsversion kann stattdessen der komplette Ordner mit `modinfo.json`, `feilsfaunafixes.dll` und `assets/` nach `Mods` kopiert werden.

Die technische Mod-ID lautet `feilsfaunafixes`. Es gibt keine Kompatibilitäts- oder Migrationslogik für die frühere Entwicklungs-ID.

## Gameplay

- Ein voller normaler Wassereimer füllt einen kleinen leeren Trog vollständig (8 Einheiten) oder einen großen leeren Trog zur Hälfte (8 von 16). Eine Wasserfläche steigt im kleinen Trog in acht und im großen Trog in sechzehn sichtbaren Stufen bis knapp unter den Rand. Wasserstand und Kapazität erscheinen zusätzlich in der Blockinfo. Wasser und Futter schließen einander aus.
- Hühner und Schafe suchen selbstständig wassergefüllte Tröge auf. Ihr Hydrations- und gegebenenfalls Wollzustand wird am Tier angezeigt und gespeichert.
- Hydrierte Hennen können ohne Futter langsamer ausschließlich unbefruchtete Eier legen. Gefütterte Hennen verwenden weiterhin den Vanilla-Lege- und Befruchtungsprozess.
- Hydrierte Schafe bauen Wolle auf. Bei 100 %: schleichen, eine Vanilla-Schere halten und per Rechtsklick scheren. Das ergibt standardmäßig eine `Raw Wool`; die Schere verliert Haltbarkeit. Eine Wolle lässt sich direkt zu `game:cloth-plain` verarbeiten.
- Alle erwachsenen Vanilla-Ziegenvarianten suchen Wasser und produzieren während hydrierter Zeit standardmäßig alle zwei Spieltage Dung. Normale Ziegen erzeugen ein Stück; die technisch ebenfalls als `goat-*` geführten Moschusochsen und Goldtakine erzeugen zwei. Der Dung wird als echter Bodenspeicher zuerst auf dem Tierfeld und danach im nächstmöglichen freien Feld des umliegenden 3×3-Bereichs platziert. Gras, Feldfrüchte, lose Steine, Wasser und andere belegte Felder werden niemals ersetzt; ohne freien, tragfähigen Platz entfällt die Produktion. Dung ist ein normales, nicht knappbares Item ohne Steinverhalten. Auf dem Boden verwendet er dieselbe 32-teilige Stapellagerung wie Torf, brennt sowohl als Item als auch im Stapel mit den Feuerholzwerten und bleibt fassgeeignet.
- 64 Dung können wie 64 Verrottetes 480 Ingame-Stunden in einem versiegelten Fass zu 16 Kompost verarbeitet werden. Dung bleibt dabei ein eigenes Material; das Vanilla-Rezept für Verrottetes bleibt unverändert bestehen.
- Natürliche Vanilla-Horn-/Geweihvarianten `game:antler-*-01` bis `-09` lassen sich mit einem Hammer in gemeinsame Hornstücke zerlegen. Das Horn wird verbraucht, der Hammer nur beschädigt.
- 1–4 Hornstücke können wie Rohfett im normalen Kochtopf erhitzt und als Hirschhornsalz entnommen werden.
- Ein normaler Vanilla-Teig und ein Hirschhornsalz ergeben vier rohe Kekse. Diese werden im normalen Ofen zu Keksen und bei Überbacken zu verbrannten Keksen. Gebackene Kekse bleiben standardmäßig 3,5-mal so lange frisch wie vergleichbares Brot.
- Eichhörnchen spawnen in gemäßigten, ausreichend bewaldeten Gebieten. Das eigene Cuboidmodell orientiert sich in Proportionen und vierbeiniger Gangart am Vanilla-Waschbären, besitzt aber den kleinen Kopf, die Ohren und den großen gebogenen Eichhörnchenschwanz. Beim Klettern wird der gesamte Körper nach oben beziehungsweise nach unten zur Wand gedreht und die vier Beine greifen diagonal gegeneinander.
- Bei der Flucht vor Spielern oder Raubtieren suchen Eichhörnchen ausschließlich in Fluchtrichtung einen natürlichen, senkrechten Baumstamm beliebiger Baumart mit mindestens sechs zusammenhängenden Stammblöcken. Stümpfe und Hauswände gelten nicht als Zuflucht; auch für eine Birke gelten genau dieselben Höhenregeln. Ist kein geeigneter Baum vor ihnen, übernimmt die normale Bodenflucht. Sie klettern ausschließlich senkrecht an der Stammseite bis neben den höchsten zusammenhängenden Stammblock, können normale und asthaltige Blattblöcke als Baumkronenraum einplanen und halten dort in Kletterposition. Sobald die Bedrohung fort ist, klettern sie kontrolliert dieselbe Stammseite hinunter. Fallschaden ist vollständig ausgeschaltet.
- Bei jeder aktiven normalen Wegfindung können Eichhörnchen bis zu drei Blöcke dicke und fünf Blöcke hohe Mauern oder Zäune vollständig in den Phasen hoch, darüber und hinunter überqueren, sofern das Hindernis tatsächlich zum Ziel hin liegt sowie Oberkante und Gegenseite frei sind. Ohne aktives Ziel wird dieser Klettermodus nicht gestartet; bei Annäherung eines Feindes wird eine solche Querung sofort zugunsten der Flucht abgebrochen. Lücken von höchstens zwei freien Blöcken werden bei aktiver Vorwärtsbewegung mit einem echten Sprung überwunden, wenn dahinter eine freie, feste Landefläche liegt.
- Nach einer Baumflucht bleibt die Kletterposition zunächst erhalten. Die Nahrungssuche besitzt Vorrang vor dem Abstieg: Gibt es ein Beeren- oder Fruchtziel, übernimmt zuerst dessen Wegfindung. Nur wenn weder Flucht noch Nahrungssuche aktiv ist, startet der eigene Abstiegstask und klettert bis zum Stammfuß hinunter.
- Die fünf Entscheidungsstufen sind ausdrücklich als Taskprioritäten abgebildet: Baumflucht `5`, normale Bodenflucht `4`, hungrige Nahrungssuche `3`, notwendiger Abstieg aus einer verbliebenen Kletterposition `2` und normales Umherwandern/Idle `1`. „Feind in der Nähe“ ist dabei nur die Ausführungsbedingung der beiden Fluchttasks. Der Bodenfluchttask prüft nicht auf das Fehlen eines Baums; er kommt automatisch nur dann zum Zug, wenn der höher priorisierte Baumfluchttask seine eigenen Bedingungen nicht erfüllt.
- Eichhörnchen fressen reife alte und neue Beerenbüsche sowie reife Fruchtbaumteile. Nach einer Portion gelten sie – entsprechend dem Vanilla-Rhythmus des Waschbären – zufällig 6 bis 12 Ingame-Stunden als satt und suchen in dieser Zeit kein weiteres Futter. Es werden nur die Früchte beziehungsweise der Reifezustand entfernt; Busch, Ast und Baum bleiben erhalten.
- Ein geschlachtetes Eichhörnchen ergibt genau ein Wildfleisch, eine kleine Rohhaut und ein platzierbares Eichhörnchengerippe. Das Gerippe verwendet die kleine dreidimensionale Vanilla-Kadaverform, kann aufgehoben und erneut als Dekoration platziert werden und erzeugt keine einzelnen Knochen.

## Konfiguration

Beim ersten Start wird `feilsfaunafixes.json` im Vintage-Story-Konfigurationsordner erzeugt. Dort liegen Trogkapazitäten, Tierverbrauch, Hydrationsreserve, Wasser-Ei-Faktor, Wollrate, Schurertrag, Dungintervall, normale und große Ziegen-Dungmenge, Hornstufen, Kochtopfzeit, Keks-Haltbarkeit, Fluchtdistanz, Klettergeschwindigkeit, minimale Fluchtbaumhöhe, maximale Sprunglücke, minimale/maximale Eichhörnchen-Sattzeit und der standardmäßig deaktivierte Debugmodus.

Änderungen gelten nach einem vollständigen Neustart der Welt/des Servers. Für Multiplayer müssen die Gameplaywerte serverseitig geändert werden.

## Bauen

`build.ps1` verwendet die lokal installierten Vintage-Story-1.22.7-Assemblies und eine vorhandene .NET-10-Laufzeit. Optional kann `VINTAGE_STORY` auf einen abweichenden Installationsordner gesetzt werden. Der Build legt `feilsfaunafixes.dll` im Projektstamm ab.

## Empfohlene Ingame-Tests

- Kleinen und großen Trog mit vollen und teilweise gefüllten Eimern testen; Futter-/Wasserausschluss und Save/Load prüfen.
- Hennen jeweils trocken, nur hydriert sowie gefüttert mit/ohne Hahn beobachten; Eierattribute kontrollieren.
- Ein Schaf hydriert und trocken über Kalenderzeit beobachten; volle Wolle scheren und Scherenhaltbarkeit prüfen.
- Eine normale Ziege sowie Moschusochse und Goldtakin am Trog beobachten; Dungmengen 1/2/2, geschützte 3×3-Platzierung, Torf-Stapelung, fehlendes Knapping, Brennen und Fassrezept prüfen.
- Alle Hornstufen mit einem Hammer verarbeiten, Hornstücke im Topf erhitzen und jede Teigvariante backen/überbacken.
- Eichhörnchen neben einem einblockigen Stumpf, einer niedrigen Birke und einem mindestens sechs Blöcke hohen Baum spawnen und danach verfolgen: Nur der hohe Baum darf als Fluchtziel dienen; Blätter dürfen die Flucht nicht beenden. Danach Futter hinter Zaun und Mauer sowie eine ein- und zweiblockige Lücke testen. Das Tier darf nach ziellosem Wandern nicht an Hauswänden hängen bleiben. Bei Eichhörnchen und Affe zusätzlich den Tod aus Leerlauf, Lauf, Flucht und Kletterzustand prüfen; außerdem Beer-/Obstfraß, Schlachten, Gerippeaufnahme und erneute Platzierung testen.

Technische Referenzen, genaue Defaultwerte und die Grenzen des automatisierten Tests stehen in `IMPLEMENTATION_NOTES.md`.
