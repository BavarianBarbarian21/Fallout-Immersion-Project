# Mod option regression runner

Build `Source/FIP.Managed.sln` in Release first, then run `Run.ps1` on Windows.
Override `RepositoryRoot`, `RimWorldManagedDir` and `ReferenceAssemblyDirectory`
when needed. The runner uses the SDK compiler and cached .NET 4.7.2 reference
assemblies without restoring packages. The csproj also supports normal SDK builds.

Tests execute the **shipped mod DLLs and RimWorld's real patch operations**.
Fixtures cover all 25 option serialization/migration rules; H&H wall scope and
restoration, source XML gates and independent crafting toggles; the five Arktos
source gates for biome, trader, faction, egg and encounter lists, including exact
restoration when disabled; Greenway's independent meme/origin/faction options; Donaustahl relic and
backstory scope; storyteller visibility; RobCo native flags, recipe restoration
and source gates; WestTek source gates; Lucky 38 research tab restoration.

The runner is not a Unity/game launch. It supplies in-memory Def fixtures, minimal
preference/DefOf initialization and patch-object fields from the real XML files.
It skips Unity resource constructors for ThingDef fixtures. It does not emulate
the full XML inheritance/load-order pipeline or render the Architect menu.
No installed mods, game settings or saves are changed.
