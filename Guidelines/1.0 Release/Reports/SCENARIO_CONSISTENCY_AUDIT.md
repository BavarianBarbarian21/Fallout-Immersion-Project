# FIP 1.0 – Szenario-Konsistenzprüfung

Stand: 2026-08-07

## Ursache der Regression

Das Sanguophage-Szenario besaß in WestTek zwar vier korrekte English-DefInjected-Einträge für Titel, Beschreibung, Zusammenfassung und Startdialog. RimWorld übernahm beim eingebauten Biotech-Szenario jedoch nur die drei längeren Texte zuverlässig; der sichtbare Titel blieb `The Sanguophage`. Die aktive Reihenfolge war korrekt: Biotech, Vanilla Races Expanded – Sanguophage, danach FIP WestTek. Kein späterer Mod schrieb denselben `ScenarioDef`-Sprachschlüssel.

Die Schwachstelle war damit die alleinige Abhängigkeit von English DefInjected für sichtbare Szenariofelder. Die Refaktorierung hatte diese alte Struktur unverändert übernommen, aber nicht als zusammengehörigen Laufzeitvertrag abgesichert.

## Korrektur

Alle sichtbaren FIP-Szenariotexte besitzen nun zwei identische Ebenen:

1. direkte `PatchOperationReplace`-Operationen am jeweiligen `ScenarioDef`;
2. English-DefInjected-Einträge für die reguläre Sprachschicht.

Dadurch kann ein einzelnes Feld nicht mehr den Vanilla-Wert behalten, während die übrigen Felder bereits die FIP-Umschreibung zeigen. Nichtenglische DefInjected-Übersetzungen können die direkt gesetzten englischen Ausgangstexte weiterhin normal überschreiben.

## Geprüfte Szenarioverträge

| ScenarioDef | Sichtbare Identität | Abgesicherte Felder |
|---|---|---|
| `Crashlanded` | airship/aircraft | Beschreibung, Zusammenfassung, Startdialog |
| `LostTribe` | tribe | Beschreibung, Zusammenfassung, Startdialog |
| `NakedBrutality` | naked wastelander | Beschreibung, Zusammenfassung, Startdialog |
| `TheRichExplorer` | vault dweller | Beschreibung, Zusammenfassung, Startdialog |
| `Mechanitor` | RobCo mechanic | Titel, Beschreibung, Zusammenfassung, Startdialog |
| `Sanguophage` | Wendigo | Titel, Beschreibung, Zusammenfassung, Startdialog |
| `TheGravship` | Helixen | Titel, Beschreibung, Zusammenfassung, Startdialog |
| `VFED_NewSafehaven` | Brotherhood expedition | Titel, Beschreibung, Zusammenfassung, Startdialog |
| `VFEE_NewFamily` | Whitespring splinter cell | Titel, Beschreibung, Zusammenfassung, Startdialog |

Die beiden deaktivierten Szenarien `VFEM2_NewKingdom` und `VFES_Bandits` bleiben außerhalb der sichtbaren Verträge, weil H&H Tools sie absichtlich deaktiviert und nur ihre Deaktivierungsbezeichnung und -beschreibung setzt. Das neue WestTek-Szenario `WestTek_ForcedEvolution` ist eine direkte FIP-Def und hatte bereits vollständig zusammenhängende Ausgangsfelder.

## Zusätzlich gefundene Lücken

- RobCo änderte vorher nur den Mechanitor-Startdialog und die Startmechs. Titel, Beschreibung und Zusammenfassung blieben Vanilla. Das Szenario heißt nun `The RobCo Mechanic` und beschreibt in allen vier Feldern dieselben vier RobCo-Servicebots.
- Beide Whitespring-Szenarien hatten Titel, Beschreibung und Startdialog, aber keine FIP-Zusammenfassung. Die fehlenden Zusammenfassungen wurden ergänzt.
- Repconns `The Helixen` sowie die vier H&H-Tools-Basisszenarien waren sprachlich vollständig, besitzen nun aber ebenfalls direkte Fallback-Patches.

## Dauerhafte Validatorregeln

`Tools/Validate-Release.ps1` bricht den Release künftig ab, wenn:

- eines der erwarteten sichtbaren Felder fehlt oder doppelt vorkommt;
- die Felder eines Szenarios nicht dieselbe thematische Identität enthalten;
- ein English-DefInjected-Szenariofeld keinen direkten `ScenarioDef`-Fallback besitzt.

Der vollständige Lauf besteht nach dieser Änderung 70 von 70 Prüfungen: 1.513 XML-Dateien, 0 ungültige Dateien, 0 modulübergreifende Sprach- oder konkrete Patchziel-Kollisionen und ein Managed-Build mit 0 Warnungen und 0 Fehlern.
