# Building and testing

The runtime targets the legacy .NET 3.5/Mono profile shipped with COM3D2. Use a current .NET SDK with a C# compiler supporting records and file-scoped namespaces; the included test harness targets .NET 10.

Game assemblies are referenced locally and are not distributed here. Point `GameDir` at an existing COM3D2 installation containing `COM3D2x64_Data/Managed` and `BepInEx/core`.

```powershell
dotnet build .\COM3D2.Pregnancy.Plugin.csproj -c Release -t:Rebuild -p:GameDir="E:\com3d2" -p:OutputPath=".\bin\Release\"
```

Pass `OutputPath` as shown to build into the checkout. The historical project default writes directly to the game's plugin directory when no override is supplied.

The root project explicitly compiles `src/**/*.cs`, so the test stubs are not included in the runtime DLL. The obsolete nested project and hand-written duplicate assembly attributes have been removed; the SDK generates assembly metadata.

## Self-contained checks

```powershell
dotnet run --project .\tests\AdapterTests -c Release
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --breast-clothing-tests
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --refresh-tests refresh-results.json
```

These checks use Unity stubs and exercise the actual production math and adapter files. They do not launch the game or measure game FPS. `ControllerSlice.cs`, `LifecycleSlice.cs` and `SelectionSlice.cs` supply the surrounding game-code harness. The back-category checks use `ClassificationSlice.cs`, extracted from the production classifier with method-name prefixes to isolate older fixtures.

## Normal and boundary checks

```powershell
dotnet run --project .\tests\AdapterTests -c Release -- --normal-tests
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --normal-lifecycle-tests
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --curved-binding-tests curved-results.json
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --bake-normal-tests BODY_MODEL DUMP_DIRECTORY bake-results.json
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --normal-model-lifecycle-tests BODY_MODEL DUMP_DIRECTORY lifecycle-results.json
```

The last two commands require a locally owned body model and a directory containing matching ALGrowth body CSV exports. Every `.csv` file in that directory is replayed; use a separate directory containing only compatible exports. Checks cover protected boundaries, authored normal detail, UV seams, native morph changes, repeat Apply/reset and unchanged normal uploads during pose changes. Results are recorded in [VERIFICATION.md](VERIFICATION.md).

## V14 body, defaults and event checks

Run these from the repository root. The profile reader uses your local game assemblies; the defaults/migration test uses your local BepInEx and Unity assemblies. Override `GameDir` with `-p:GameDir="YOUR_GAME_DIRECTORY"` when needed. Patch validation scripts accept `-GameDir` too.

```powershell
dotnet run --project .\tests\BodyProfileTests -c Release -- .\presets\base.preset .\presets\reference.preset
dotnet run --project .\tests\BodyRuntimeTests -c Release
dotnet run --project .\tests\ConfigDefaultsTests -c Release
dotnet run --project .\tests\EventTests -c Release
dotnet run --project .\tests\AdapterTests -c Release -- --body-change-routing
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --refresh-tests refresh-results.json
dotnet run --project .\tests\AdapterTests -c Release --no-build
pwsh -File .\tests\Validate-BodyChangePatches.ps1 -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
pwsh -File .\tests\Validate-EventPatches.ps1 -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
```

These checks cover native preset differences, slider/color calculations and restoration, repeated application, childbirth counting, per-maid isolation, legacy configuration migration, unrestricted integer settings and all 51 logged shape defaults. `expected-defaults.json` contains only the expected parameter values; personal logs are not distributed.

The patch scripts inspect signatures against the installed game and check that the body-change runtime does not call persistent appearance mutation APIs. They do not prove that Harmony is installed successfully in a running game. `sync_body_lifecycle.py` refreshes the extracted body-change lifecycle methods used by the adapter harness when those production methods change.

Build/test results for this update are recorded in `VERIFICATION.md`. The preceding body-change build was loaded and used locally; the exact English/defaults build has not had a separate in-game visual pass.

## Optional model and dump replays

The other study commands in `tests/AdapterTests/Program.cs` require locally owned model files and prepared vertex data. Game models and private dumps are deliberately excluded. For `--clothing-pose-study`, the input directory contains `inputs.json` entries with `model` and `stage`, plus `<model>-body.csv` and `<model>-original.csv` (three comma-separated coordinates per vertex). The model directory contains the corresponding `<model>.model` and `LOmobchara_extra_v1_beta.model`.

```powershell
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --clothing-pose-study INPUT_DIRECTORY MODEL_DIRECTORY poses.json MODEL_NAME
```

The pose replay verifies sampled body positions against rendered body skinning, all three support-face corners, the four-weight skin equation, culling bounds, no per-frame clothing vertex uploads, and reuse of unchanged support poses.

### V13 focused replay

```powershell
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --leg-navel-tests results.json BODY_MODEL DRESS652_MODEL SKIRT_DUMP_DIRECTORY BODY_ORIGINAL_CSV
```

Use the 5,502-vertex `dress652_onep.model` matching the skirt diagnostic export. `SKIRT_DUMP_DIRECTORY` contains the plugin's `vertices.csv` and `parameters.txt`; `BODY_ORIGINAL_CSV` contains the matching body's original vertices (one header line, then three comma-separated coordinates per vertex). This replay checks unchanged lower-leg coordinates/weights and native poses, minority calf weights, skirt ownership, source skin rebinding, accessory update order, and the back-category Apply/cache/visibility flow. It requires the matching local dataset; the self-contained tests above do not.
