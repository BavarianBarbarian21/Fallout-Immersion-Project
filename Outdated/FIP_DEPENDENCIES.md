# Fallout Immersion Project – Abhängigkeiten

Stand: 2. August 2026
Quelle: aktive Arbeitsmods unter `New-Mods/`

Big MT ist vollständig aus der neuen Version entfernt. Es besitzt keine aktive
Package-ID, Abhängigkeit, Ladereihenfolge oder Übersetzungsschicht.

## 1. Begriffe

- **Pflicht**: Eintrag unter `modDependencies`; der Mod soll ohne dieses Paket
  nicht aktiviert werden.
- **Optional**: Inhalt wird nur über `IfModActive`, `IfModActiveAll` oder
  `MayRequire` wirksam.
- **`loadAfter`**: Reihenfolgehinweis, keine Pflichtabhängigkeit.
- **Base**: immer geladene Schicht; darf nur Pflicht-, Vanilla- oder sichere
  `MayRequire`-Annahmen enthalten.

Keines der 18 aktiven Module deklariert `loadBefore` oder `incompatibleWith`.
Alle 14 `LoadFolders.xml` enthalten zusammen 184 gültige Einträge.

## 2. Pflichtabhängigkeiten der spielbaren Module

| Modul | FIP-Pflicht | DLC-Pflicht | externe Pflicht | Begründung |
|---|---|---|---|---|
| Arktos | `FIP.HHTools` | – | – | verwendet die gemeinsame H&HTools-Naming-/Faction-Basis |
| Corvega | – | – | VVE, VVE Tier 3, VVE Upgrades | überschreibt Fahrzeuge und Upgrades dieser drei Module |
| Donaustahl | – | – | – | alle Integrationen sind Overrides oder optional |
| FutureTec | – | – | – | jede Questfamilie ist optional bedingt |
| Greenway | `FIP.HHTools` | Ideology | – | Ideology-Defs plus gemeinsame H&HTools-Faction-Basis |
| H&HTools | – | – | – | eigenständig ladbares Framework |
| Hubris | – | Royalty | – | ersetzt Royalty-Psycast-/Anima-Systeme |
| Lucky 38 | – | – | `brrainz.harmony` | optionale Trading-Laufzeitintegration für Firmenlisten und FCP-Kronkorken |
| Poseidon | – | – | – | alle Energie-/Produktionsintegrationen sind optional |
| Repconn | – | Odyssey | – | Odyssey-Gravship ist die inhaltliche Basis |
| RobCo | – | Biotech | `brrainz.harmony` | Mech-/Gene-Inhalte und Laufzeitpatches |
| Sunset | – | – | – | alle Faction-Zielmods sind optional |
| WestTek | `FIP.HHTools` | Biotech | `brrainz.harmony` | gemeinsame Faction-/Naming-Basis, Gene und Laufzeitpatches |
| Whitespring | – | Royalty | – | Royalty-Titel- und Empire-Grundsystem |

Corvegas vollständige Pflicht-Package-IDs:

- `OskarPotocki.VanillaVehiclesExpanded`
- `OskarPotocki.VanillaVehiclesExpandedTier3`
- `OskarPotocki.VanillaVehiclesExpandedUpgrades`

## 3. FIP-interne optionale Verbindungen

| Besitzer | optionales Ziel | Form und Begründung |
|---|---|---|
| Donaustahl | H&HTools, Hubris, RobCo, WestTek, Whitespring | Reihenfolge für Immersions-/Sprach-Overrides; keine Pflicht |
| H&HTools | Greenway, WestTek | einzelne `MayRequire`-Verweise in gemeinsamer abstrakter Faction-Hierarchie; keine Ladepflicht |
| Lucky 38 | H&HTools, RobCo | Trading-Banken für demokratische H&HTools-Siedler; RobCo nur in Service-Kombinationen |
| Sunset | H&HTools | nur Medieval2-/Settlers-Kombinationen |
| WestTek | Greenway | eigener `IfModActive="FIP.Greenway"`-Ordner |
| Whitespring | H&HTools, WestTek | eigene und kombinierte Faction-/Equipment-Schichten |

H&HTools besitzt keine Greenway- oder WestTek-spezifischen Patch-LoadFolder
mehr. Diese Inhalte liegen bei Greenway beziehungsweise WestTek. Dadurch sind
die Abhängigkeitskanten gerichtet und der interne Ladegraph ist zyklusfrei.

## 4. Optionale DLC-Schichten

| Modul | DLC | Form |
|---|---|---|
| Arktos | Odyssey | eigener LoadFolder |
| Greenway | Odyssey | eigener LoadFolder |
| H&HTools | Anomaly, Biotech, Ideology, Odyssey, Royalty | getrennte Feature-/Equipment-Schichten |
| Poseidon | Anomaly, Royalty | getrennte LoadFolder |
| RobCo | Royalty | eigener LoadFolder |
| Sunset | Ideology | nur zusammen mit Medieval2 |
| WestTek | Anomaly, Odyssey, Royalty | getrennte LoadFolder |

Pflicht-DLCs stehen in Abschnitt 2. DLC-Bedingungen benötigen keinen
zusätzlichen `loadAfter`-Eintrag, weil RimWorld seine DLCs vor Mods lädt.

## 5. Externe optionale Integrationen

| Modul | Pakete/Familien | Grund |
|---|---|---|
| Arktos | FCP Plants, Animals, Ballistic Weapons, Pre-War Food; VCEF; Ancient Urban Ruins; VExplorationE; VE-Pflanzen/-Tiere | Biome-, Tier-, Ruinen- und Ressourcenpatches |
| Donaustahl | Vanilla Aspirations, Backstories, Events, Social Interactions, Traits, Books | Text-, Backstory-, Event- und Trait-Overrides |
| FutureTec | VQE Ancients, Cryptoforge, Deadlife, Generator; VFE Core | getrennte Quest-Rethemes und gemeinsame Zielbasis |
| Greenway | FCP Chems, VE Dryads, VIEHAR, VMemesE | Natur-, Dryad- und Ideology-Inhalte |
| H&HTools | FCP-Factions/Waffen/Tiere; VFE Medieval2/Tribals/Settlers; VE Equipment/Psycasts/Quests/Gravship | Equipment-Tags, Factions und Naming-Integration |
| Hubris | VPsycastsE, Hemosage, Puppeteer | getrennte Psycast-Schichten |
| Lucky 38 | Hospitality, Casino, Spa, Storefront, Vending, Hygiene, Waiter, Cash Register, Gastronomy, Therapy, Spaceports, Food/Brewing/Plants, Vanilla Trading Expanded, FCP Core Tools | Service-, Casino-, Food- und Pflanzen-Rethemes; Fallout-Handelsnamen, Nachrichten, Banken und Kronkorkenwährung |
| Poseidon | Helixien Gas, Recycling, Temperature, VChemfuelE, VFE Art/Factory/Medical/Power/Production/Security/Spacer, VNutrientE | Energie-, Industrie- und Versorgungssysteme |
| Repconn | VE Gravship, VE Skills | Gravship und Gravship-plus-Skills |
| Sunset | VFE Core, Settlers, Tribals, Medieval2, FCP Animals | Faction-, Tier- und Equipment-Rethemes |
| WestTek | FCP Ghouls, VCookE, VGeneticsE, VRE Sanguophage | Ghoul-, Genetik-, Koch- und Sanguophage-Inhalte |
| Whitespring | VFE Empire, VFE Deserters, FCP BOS, FCP Enclave, VPersonaWeaponsE | Enclave-, Deserter-, Faction- und Persona-Integration |

Jede tatsächlich bedingte Nicht-DLC-Package-ID ist entweder Pflichtabhängigkeit
oder in `loadAfter` enthalten. Der Abschlussvalidator meldet 0 fehlende
Reihenfolgebegründungen.

## 6. LoadFolder-Begründung pro Modul

- **Arktos:** `Base` plus Odyssey, VCEF, Ancient Urban Ruins, Exploration,
  FCP Animals/Plants und zwei genaue AUR-FCP-Kombinationen.
- **Corvega:** ein Base-Ordner; alle drei Zielmods sind Pflicht.
- **Donaustahl:** Base plus sechs einzeln bedingte Vanilla-Expanded-
  Überschreibungsschichten.
- **FutureTec:** Base plus je eine Schicht pro Questfamilie.
- **Greenway:** Base plus Chems, Odyssey, Dryads, VIEHAR und VMemesE.
- **H&HTools:** Base/Core-Equipment plus getrennte DLC-, FCP-, VFE-, Quest- und
  exakte Mehrfachkombinationen. Die Breite ist fachlich durch die
  Equipment-/Faction-Frameworkrolle begründet.
- **Hubris:** Base plus Hemosage, Puppeteer und VPsycastsE.
- **Lucky 38:** Base plus einzelne Serviceziele, genaue Food-/Pflanzen-/
  RobCo-Kombinationen sowie getrennte Trading-, Trading-H&HTools- und
  Trading-FCP-Schichten. Mehrfachbedingungen dürfen nicht verbreitert werden.
- **Poseidon:** Base plus DLC-, Energie-, Industrie- und Versorgungssysteme.
- **Repconn:** Odyssey-Base plus Gravship und Gravship-plus-Skills.
- **RobCo:** Biotech-Base plus optionale Royalty-Schicht.
- **Sunset:** Base plus Settlers, Tribals, Medieval2 und exakte H&HTools-/
  FCP-Animals-Kombinationen. Der verwaiste Odyssey-Ordner ist entfernt.
- **WestTek:** Biotech-Base plus Anomaly, Greenway, Odyssey, Royalty,
  Genetics/Cooking, Sanguophage und FCP Ghouls.
- **Whitespring:** Royalty-Base plus Empire, Deserters, WestTek, H&HTools,
  FCP BOS/Enclave und Persona Weapons.

## 7. Übersetzungsmodule

### Sprachspezifische Pakete

Chinese, Japanese, Korean und Russian besitzen keine Pflichtabhängigkeiten.
Sie laden nach den tatsächlich übersetzten Quellmods. Verweise auf
`FIP.Translation.Part1` bis `.Part4` wurden entfernt; die vier veralteten
Sammelpakete gehören nicht mehr zum aktiven Modsatz.

## 8. Validierter Zustand

- 18 eindeutige Package IDs,
- 0 fehlende LoadFolder-Ziele,
- 0 bedingte Nicht-DLC-Pakete ohne Pflichtabhängigkeit oder `loadAfter`,
- 0 FIP-interne Ladezyklen,
- 0 aktive Big-MT-Abhängigkeiten,
- keine privaten Harmony-Kopien,
- kein `loadBefore` und kein `incompatibleWith`.

Die tatsächliche Existenz externer Defs und Patchziele muss zusätzlich mit den
konkret installierten Fremdmod-Versionen in RimWorld geprüft werden.
