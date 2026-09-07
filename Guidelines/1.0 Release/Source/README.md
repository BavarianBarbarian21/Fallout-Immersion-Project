# Verwaltete FIP-Quellprojekte

Dieser Ordner enthält ausschließlich die kanonischen C#-Quellen der aktiven
Release-Module im Repository-Root. Historische Projektstände unter
`Outdated/` sind Referenzmaterial und kein Build-Eingang.

## Enthaltene Projekte

- FIP-Arktos: 15 getrennte, save- und reflection-kompatibel beibehaltene
  Assemblies sowie die ModSettings-Assembly.
- Donaustahl, Hubris und Whitespring: jeweils eine ModSettings-Assembly.
- FIP-Greenway: eine Assembly.
- FIP-H&HTools: eine Assembly.
- FIP-Lucky 38: Settings- und Harmony-Assembly.
- FIP-RobCo: Basis- und Harmony-Assembly.
- FIP-WestTek: Basis- und Harmony-Assembly.

Die Projektmappe `FIP.Managed.sln` enthält auch alle neun ModSettings-Module.
Big MT hat hier kein C#-Projekt; sein spielbares Modul liegt im Repository-Root.

## Gemeinsamer Build

`Directory.Build.props` definiert .NET Framework 4.7.2, die RimWorld-Referenzen
und den Pfad zum Repository-Root. RimWorld wird unter
`D:\Steam\steamapps\common\RimWorld` oder im üblichen Steam-Standardpfad
gesucht. Bei einer anderen Installation muss `RimWorldInstallDir` gesetzt
werden.

Ein normaler Release-Build schreibt nur die erzeugte FIP-Assembly in den
zugehörigen FIP-Ordner im Repository-Root. RimWorld-, Unity- und Harmony-DLLs werden
nicht mitkopiert. Harmony ist bei H&HTools, Lucky 38, RobCo und WestTek nur eine
Compile-Referenz; die Laufzeitabhängigkeit wird in `About.xml` deklariert, wo
sie tatsächlich benötigt wird.

Beispiel:

```powershell
dotnet build .\FIP.Managed.sln -c Release
```

Für eine reine Prüfung müssen `OutputPath`, `BaseIntermediateOutputPath` und
`MSBuildProjectExtensionsPath` auf ein temporäres Verzeichnis umgeleitet
werden. Die Modoptionen können nach dem Build mit
`../Tools/ModOptionsRegression/Run.ps1` gegen die erzeugten Assemblies geprüft
werden. Umfang und Grenzen stehen in der dortigen README.
