# FIP 1.0 – ModSettings-Refaktorierung

Alle neuen Schalter sind absichtlich Ladezeit-Einstellungen. Nach einer Änderung
RimWorld neu starten; bei Fraktionen, Szenarien, Raids und Pawn-Generierung eine
neue Welt starten. Die Standardwerte bilden den bisherigen FIP-Zustand ab – mit
der einzigen beabsichtigten Ausnahme **WestTek / Restore Xenotypes**, das
standardmäßig aktiviert ist.

| FIP-Mod | Einstellbare Wiederherstellungen | Standard | Umsetzung und Grenzen |
|---|---|---:|---|
| Arktos | Tiere aus Core, Biotech, Vanilla Animals Expanded, Royal Animals und Odyssey | Aus | Stellt nur normale Nicht-Arktos-Biomspawns wieder her. Arktos-Biome bleiben unverändert; Karawanen-, Handels- und Packtierquellen sowie FIP-Texturen werden nicht verändert. |
| Big MT | Keine | – | Keine Entfernung in diesem Refaktor festgestellt. |
| Corvega | Keine | – | Keine Entfernung in diesem Refaktor festgestellt. |
| Donaustahl | Entfernte Backstories; Ausrüstungsreliquien; Storyteller | Aus | Backstories aus Vanilla/Vanilla Expanded, ursprüngliche Reliktchancen und Cassandra/Phoebe/Randy werden aus den geladenen Quell-Defs wiederhergestellt. |
| FutureTec | Keine | – | Keine Entfernung in diesem Refaktor festgestellt. |
| Greenway | Ideology Origins; Memes; Fraktionen | Aus | Origins werden wieder sichtbar und in Zufallslisten erlaubt. „Restore memes“ umfasst Vanilla und Vanilla Expanded. „Restore factions“ stellt Auswahl- und Weltgenerierungswerte der betroffenen Fraktionen wieder her. |
| H&H Tools | Fraktionen; Szenarien; Gebäude; Waffen; Kleidung; Quests; Storyteller | Aus | Die jeweilige ursprüngliche Def-Konfiguration wird wiederhergestellt. Der technische Chemshine-Muffalo-ThinkTree-Patch bleibt immer aktiv. |
| Hubris | Storyteller | Aus | Basilicus wird wieder sichtbar, ohne FIP-Textpatches. |
| Lucky 38 | Brewing-Forschungsbaum | Aus | Stellt die ursprünglichen Brewing-Tabs, Positionen und Schematic-Tabzuordnung wieder her. |
| Poseidon | Keine | – | Keine Entfernung in diesem Refaktor festgestellt. |
| Repconn | Keine | – | Keine Entfernung in diesem Refaktor festgestellt. |
| RobCo | Mechanoids | Aus | Bei Aktivierung wird der vollständige native Mechanitor-Quellzustand geladen: Gestators/Rezepte, Bedrohungen, Raids, Starts, Quests, Tanks, Royalty-Spawns und Basic Mechtech. Der Schalter zeigt ein vorhandenes RobCo-Mech-Symbol; RobCo-Texturen bleiben erhalten. |
| WestTek | Xenotypes | **An** | Aktiviert: originale Xenotyp-Quellen bleiben intakt. Deaktiviert: nur die explizit erlaubte FIP/FCP-Auswahl (plus Baseliner, Highmate, Sanguophage) bleibt in normalen Xenotyp-, Fraktions- und PawnKind-Pools. Defs werden nicht gelöscht. Anthrosonae wird nicht referenziert und nicht unterdrückt. |
| Whitespring | Storyteller | Aus | Ariadne Archduchess und Damocles werden wieder sichtbar, ohne FIP-Textpatches. |

## Validierung

- Alle XML-Dateien unter `Guidelines/1.0 Release` wurden erfolgreich geparst.
- Die neuen bzw. geänderten Assemblies für Arktos, Donaustahl, Greenway, H&H
  Tools, Hubris, Lucky 38, RobCo, WestTek und Whitespring wurden gegen die
  lokale RimWorld-1.6-API ohne Compilerfehler gebaut.
- Der VGE-Forschungsanschluss wurde noch nicht verändert: Die verlangte Def
  `WestTek_FEVThesis` existiert nicht. Die vorhandene Def
  `WestTek_FlatwormMutation` trägt lediglich das Label „forced evolution
  thesis“. Erst nach einer eindeutigen Zuordnung kann die erste Genetics-
  Expanded-Forschung korrekt daran angehängt werden.
