# FIP-Entwicklung

Dieser Ordner enthält ausschließlich bearbeitbare Quellprojekte und Entwicklungsdateien.

- `Source/`: C#-Quellcode und Projektdateien.
- `../New-Mods/`: getrennte, spielbare Mod-Arbeitskopien.
- `../Guidelines/Assembler/`: unveränderte historische Referenz; kein Build-Ziel.

Die Quellprojekte schreiben Release-Builds direkt in den jeweiligen Ordner
`New-Mods/<Mod>/LoadFolders/Base/Assemblies`. Dadurch werden weder die alten
Root-Mods noch die historischen Projektordner überschrieben.

`bin`, `obj`, PDB-Dateien und private Kopien externer Bibliotheken gehören nicht
in die Mod-Ausgabe.
