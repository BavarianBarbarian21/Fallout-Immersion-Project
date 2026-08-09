# Verwaltete FIP-Quellprojekte

Dieser Ordner enthält ausschließlich die kanonischen C#-Quellen der aktiven
Arbeitsmods unter `../../New-Mods`. Historische Projektstände unter
`Guidelines/` sind Referenzmaterial und kein Build-Eingang.

## Enthaltene Projekte

- FIP-Arktos: 15 getrennte, save- und reflection-kompatibel beibehaltene
  Assemblies.
- FIP-Greenway: eine Assembly.
- FIP-H&HTools: eine Assembly.
- FIP-Lucky 38: eine Assembly.
- FIP-RobCo: eine Assembly.
- FIP-WestTek: eine Assembly.

Die Projektmappe `FIP.Managed.sln` enthält damit 20 Projekte. Big MT ist nicht
enthalten und bleibt aus dieser Version vollständig ausgeschlossen.

## Gemeinsamer Build

`Directory.Build.props` definiert .NET Framework 4.7.2, die RimWorld-Referenzen
und den Pfad zu `New-Mods`. RimWorld wird unter
`D:\Steam\steamapps\common\RimWorld` oder im üblichen Steam-Standardpfad
gesucht. Bei einer anderen Installation muss `RimWorldInstallDir` gesetzt
werden.

Ein normaler Release-Build schreibt nur die erzeugte FIP-Assembly in den
zugehörigen Ordner unter `New-Mods`. RimWorld-, Unity- und Harmony-DLLs werden
nicht mitkopiert. Harmony ist bei H&HTools, Lucky 38, RobCo und WestTek nur eine
Compile-Referenz; die Laufzeitabhängigkeit wird in `About.xml` deklariert, wo
sie tatsächlich benötigt wird.

Beispiel:

```powershell
dotnet build .\FIP.Managed.sln -c Release
```

Für eine reine Prüfung müssen `OutputPath`, `BaseIntermediateOutputPath` und
`MSBuildProjectExtensionsPath` auf ein temporäres Verzeichnis umgeleitet
werden. Der abschließende Refactoring-Build aller 20 Projekte lief isoliert mit
0 Warnungen und 0 Fehlern.
