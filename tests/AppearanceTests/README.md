# Appearance persistence checks

Requires .NET 10 and your own installed `COM3D2.ExternalSaveData.Managed.dll`. From the repository root:

```powershell
dotnet build .\tests\AppearanceTests -c Release -m:1 -p:GameDir="YOUR_COM3D2_DIRECTORY"
dotnet run --project .\tests\AppearanceTests -c Release --no-build
```

`GameDir` locates `Sybaris/COM3D2.ExternalSaveData.Managed.dll`. You can instead set the `COM3D2_GAME_DIR` environment variable or pass `-p:ExternalSaveAssembly="FULL_PATH_TO_DLL"` to the build command. A missing DLL produces a build error explaining these options.

The tests link the production appearance storage and bridge, then call the installed ExternalSaveData binary's real save/load callbacks. Small test-only `Assembly-CSharp` and `UnityEngine` API stubs provide synthetic maids and file paths. No game engine starts, and no existing game saves are read or changed. Each run creates synthetic slot XML files in a new `fixtures-*` directory under the test output directory.

Checks cover two-slot isolation, maid GUID identity, old-save migration, explicit zero, independent belly/body values, preview confirmation/cancellation, save preparation and external XML edits followed by reload. This verifies serialization behavior with the local DLL; it does not verify in-game Harmony patch installation or rendered geometry.

The dependency DLL, game source, game saves and generated fixtures are not part of the repository. Build outputs include copies of the dependency and test API assemblies solely to run these checks; never install those test outputs into the game.
