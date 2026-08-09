# Fallout Immersion Project – verbleibende Entscheidungen und Tests

Stand: 30. Juli 2026

Der statische Audit und der Release-Build sind abgeschlossen. Offen sind nur
Punkte, die einen echten RimWorld-Lauf, eine Savegame-Migration oder eine
spätere Designentscheidung benötigen.

## 1. Erforderliche Ingame-Validierung

RimWorld wurde während dieses Durchgangs nicht gestartet. Zu testen sind:

1. jedes der 14 spielbaren Module mit seinen Pflicht-DLCs und -Mods,
2. alle 14 FIP-Module gemeinsam,
3. relevante optionale FCP-/Vanilla-Expanded-Kombinationen,
4. die vier Sprachpakete mit repräsentativen Quellmods,
5. Darstellung aller Richtungs- und Variantentexturen,
6. Auswahlgewichtung der beiden identischen Robobrain-Angry-Clips,
7. bestehende Spielstände vor und nach der DefName-Migration,
8. `Player.log` auf XML-, Patch-, Def-, Asset- und Assemblyfehler.

Die statische Prüfung meldet keine lokalen Fehler. Sie kann installierte
Fremdmod-Versionen und RimWorlds Laufzeitauflösung nicht vollständig
simulieren.

## 2. DefName-Migration

318 alte IDs wurden auf das Präfix ihres Besitzermods migriert. Das behebt
Namenskonflikte und falsche Eigentümerpräfixe, kann aber alte Spielstände,
gespeicherte Xenotypen und externe Patches betreffen.

Vor einer Veröffentlichung über bestehende Installationen ist zu entscheiden,
wie die Migration ausgeliefert wird:

- dokumentierter Neubeginn ohne Alt-Save-Unterstützung,
- einmalige Savegame-Migration,
- oder gezielte Kompatibilitätspatches für bekannte externe Referenzen.

Die Regeln und Sonderfälle stehen in `FIP_DEFNAME_MIGRATION.md`.

## 3. Arktos-Assemblies

Arktos besitzt weiterhin 15 kleine Assemblies. Eine Konsolidierung könnte
Assemblyqualifikationen, Reflection oder Initialisierungsreihenfolgen ändern.
Sie bleibt deshalb eine getrennte spätere Aufgabe und benötigt Ingame- und
Savegame-Tests.

## 4. Gemeinsame H&HTools-Faction-Basis

`HHTools_Greenway_IdeoBase` liegt weiterhin in H&HTools. Trotz des Namens ist
es ein Framework-Elternknoten für mehrere H&HTools-Faction-Templates. Seine
Greenway-Verweise sind `MayRequire`-gesichert. Ein Umzug würde H&HTools ohne
Greenway brechen.

## 5. Fehlende Übersetzungen

Fünf zuvor leere oder generierte Werte fallen bewusst auf den Text des
Quellmods zurück. Eine redaktionelle Übersetzung durch Muttersprachler ist noch
offen. Neue Werte müssen anschließend erneut auf Platzhalterkonsistenz geprüft
werden.

## 6. Historische Bereiche und Big MT

Historische Root-Module, `Guidelines/`, alte TranslationSync-Zustände und
alternative Projektstände sind kein aktiver Build- oder Runtime-Eingang. Eine
spätere Archivierung oder Bereinigung wäre eine eigene Aufgabe.

Big MT ist vollständig aus der neuen Version ausgeschlossen. Die spätere
Neuentwicklung als Anomaly-DLC-Patch benötigt ein eigenes Design- und
Migrationskonzept.

## 7. Freigabestatus

Statisch und buildseitig ist der Stand ohne Befund. Für eine öffentliche
Freigabe fehlen die Ingame-Prüfungen aus Abschnitt 1 und eine bewusste
Entscheidung zur DefName-Migration.
