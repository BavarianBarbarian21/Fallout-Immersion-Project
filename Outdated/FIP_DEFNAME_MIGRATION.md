# FIP DefName-Migration

Stand: 30. Juli 2026

Dieser Durchgang hat 318 eindeutige Alt-zu-Neu-Zuordnungen auf 319 direkten
oder benannten Def-Deklarationen angewendet. Alle Referenzen innerhalb von
`New-Mods/` und `Development/Source/` wurden mitgeführt und die betroffenen
Assemblies neu gebaut.

## Transformationsregeln

| Besitzer | Alt | Neu |
|---|---|---|
| Arktos | `HHTools_Arktos_*` | `Arktos_*` |
| Arktos | `ArktosUrban` | `Arktos_Urban` |
| Greenway | `HHTools_Greenway_*` | `Greenway_*` |
| Greenway | `Dryad_Pungaling` | `Greenway_Dryad_Pungaling` |
| Greenway | `Pungaling` | `Greenway_Pungaling` |
| H&HTools | `FIPD_*` | `HHTools_*` |
| RobCo | `WestTek_Gene_*` | `RobCo_Gene_*` |
| RobCo | `PowerClaw` | `RobCo_PowerClaw` |
| RobCo | `ReloadAbilityFromMap` | `RobCo_ReloadAbilityFromMap` |
| RobCo | `ReloadMechAbility` | `RobCo_ReloadMechAbility` |
| RobCo | `Shot_ChargeBlasterCannon` | `RobCo_Shot_ChargeBlasterCannon` |
| WestTek | `SPECIAL` | `WestTek_SPECIAL` |
| WestTek | `Highmate` | `WestTek_Xenotype_SNuffy` |

Zusätzlich erhielten folgende H&HTools-IDs das Präfix `HHTools_`:

- `Cascadia_Seattle`
- `Cascadia_Vancouver`
- `FalloutTribalClan`
- `Texico_Chihuahua`
- `Texico_RioGrande`
- `Texico_Sinaloa`
- `Wasteland_CityRuins`
- `Wasteland_Desert`
- `Wasteland_Forest`

## Sonderfälle

- WestTeks altes eigenes `Highmate` war eine Kollision mit dem
  Vanilla-Xenotyp. Nur WestTeks Deklaration, DefInjected-Schlüssel, DefOf-Feld
  und mod-eigene Referenzen wurden zu `WestTek_Xenotype_SNuffy`. Beabsichtigte
  C#- und XML-Verweise auf Vanilla-`Highmate` bleiben `Highmate`.
- WestTeks eigenes `SPECIAL` wurde zu `WestTek_SPECIAL`; Vanilla-Inhalte wurden
  nicht umbenannt.
- Greenways direkte Deklaration `ConnectedTreeDied` wurde nicht umbenannt,
  sondern entfernt, weil sie eine Vanilla-ID doppelt deklarierte. Greenway
  patcht nur noch die benötigte Struktur des Vanilla-Defs und setzt den Text
  über `DefInjected`.

## Folgen für Spielstände und Fremdpatches

Package IDs, Assemblynamen und öffentliche C#-Typnamen sind unverändert.
Die oben genannten DefNames sind jedoch save- und patchrelevant.

Vor der Übernahme in eine bestehende Installation müssen deshalb mindestens
folgende Fälle geprüft werden:

- Saves mit betroffenen Factions, RulePacks, Orten, Genen oder Xenotypen,
- gespeicherte benutzerdefinierte Xenotypen mit RobCo-Genen,
- externe XML-Patches mit alten `defName`- oder XPath-Verweisen,
- externe C#-Mods mit DefOf-Feldern oder fest codierten alten IDs.

Der aktuelle Stand enthält keine automatische Alt-Save-Migration. Ohne eine
solche Migration ist ein Neubeginn die sichere Variante.

## Reproduzierbarkeit

Die kanonischen Transformationsregeln stehen in
`Development/Tools/refactor_def_prefixes.py`. Der Dry-Run meldet nach
erfolgreicher Migration:

```text
mode=dry-run def_mappings=0 declarations=0 named_parents=0 rewritten_files=0 renamed_files=0
```
