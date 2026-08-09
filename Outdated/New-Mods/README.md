# Neue FIP-Mods

`New-Mods/` ist der aktive, vollständig getrennte Arbeitsstand des Fallout
Immersion Project. Jeder Unterordner ist ein eigenständig installierbarer Mod
mit eigener Package ID, eigenen Laufzeitdateien und klar bedingten
LoadFolders.

## Aktiver Stand

Enthalten sind 14 spielbare Module:

- `FIP-Arktos`
- `FIP-Corvega`
- `FIP-Donaustahl`
- `FIP-FutureTec`
- `FIP-Greenway`
- `FIP-H&HTools`
- `FIP-Hubris`
- `FIP-Lucky 38`
- `FIP-Poseidon`
- `FIP-Repconn`
- `FIP-RobCo`
- `FIP-Sunset`
- `FIP-WestTek`
- `FIP-Whitespring`

Hinzu kommen vier unabhängige Übersetzungsmodule:

- `FIP-Translation Chinese`
- `FIP-Translation Japanese`
- `FIP-Translation Korean`
- `FIP-Translation Russian`

Big MT und die veralteten Sammelübersetzungen `FIP-Translation Part 1–4`
gehören nicht zum aktiven Stand. Historische Originale außerhalb von
`New-Mods/` bleiben Referenzmaterial.

Aktueller statischer Prüfstand:

- 18 Module: 14 spielbar und 4 Übersetzungen
- 2.466 Dateien
- 1.416 parsebare XML-Dateien
- 1.230 direkte Defs
- 492 Bilder und 176 Audiodateien
- 19 neu gebaute Laufzeit-DLLs

## Verbindliche Regeln

- Ein Unterverzeichnis enthält genau einen eigenständig installierbaren Mod.
- Jeder spielbare Mod besitzt ein festes Präfix für direkte Defs, Laufzeitassets
  und mod-eigene XML-Dateien.
- Direkte Defs und benannte abstrakte Eltern verwenden ausnahmslos das Präfix
  des Besitzers.
- Ein englischer `DefInjected`-Eintrag darf einen bereits direkt im lokalen Def
  vorhandenen identischen Wert nicht nochmals spiegeln.
- Für reine Textänderungen an vorhandenen Defs wird `DefInjected` verwendet.
  `PatchOperationReplace` ist dafür nicht zulässig; strukturelle Patches bleiben
  normale Patchoperationen.
- Innerhalb eines Mods sind bytegleiche Assetkopien nur in den explizit
  freigegebenen Richtungs-, Varianten- oder Gewichtungs-Slots erlaubt.
- Identische Assets in verschiedenen eigenständigen Mods dürfen lokal bleiben,
  wenn eine Zentralisierung eine neue Pflichtabhängigkeit erzeugen würde.
- Optionale DLC- und Modintegrationen erhalten eigene, bedingte LoadFolders.
- Original und Arbeitskopie eines Mods dürfen wegen identischer Package IDs
  nicht gleichzeitig aktiviert werden.

Die vollständigen Kennzahlen und Grenzen stehen in
`../FIP_AUDIT_REPORT.md`. DefName-Migrationsregeln stehen in
`../FIP_DEFNAME_MIGRATION.md`.
