# Building and testing

The runtime targets the legacy .NET 3.5/Mono profile shipped with COM3D2. Use a current .NET SDK with a C# compiler supporting records and file-scoped namespaces; the included test harness targets .NET 10.

Game assemblies are referenced locally and are not distributed here. Point `GameDir` at an existing COM3D2 installation containing `COM3D2x64_Data/Managed` and `BepInEx/core`.

```powershell
$gameDir = 'YOUR_GAME_DIRECTORY'
dotnet build .\COM3D2.Pregnancy.Plugin.csproj -c Release -p:GameDir="$gameDir"
```

The default output is `bin/Release` inside the checkout. Copy the resulting DLL and optional PDB to `BepInEx/plugins` to install it. Referenced game assemblies are not copied into the output.

The root project explicitly compiles `src/**/*.cs`, so the test stubs are not included in the runtime DLL. The SDK generates assembly metadata.

## Self-contained checks

Run these commands from the repository root:

```powershell
dotnet run --project .\tests\NativeRuntimeTests -c Release
dotnet run --project .\tests\NativeBufferTests -c Release
dotnet run --project .\tests\BodyRuntimeTests -c Release
dotnet run --project .\tests\EventTests -c Release
dotnet run --project .\tests\AdapterTests -c Release
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --refresh-tests .\refresh-results.json
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --skirt-tests
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --normal-tests
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --curved-binding-tests .\curved-results.json
```

Native runtime checks link the production transaction scheduler; buffer checks link the production native-morph integration. Body and event tests exercise the production overlay and simulation logic with game/Unity stubs.

Adapter checks link the production math, native-morph integration, calibration, clothing, skirt and normal modules. They use synthetic meshes, Unity/TMorph stubs and explicit surrounding harness code. `ControllerSlice.cs`, `LifecycleSlice.cs` and `SelectionSlice.cs` contain extracted helpers; they are not the production transaction scheduler. The refresh suite checks mask-independent topology, native slot visibility, cache reuse, genuine topology invalidation and per-maid isolation.

## Checks against installed game assemblies

Use the same `$gameDir` as for the runtime build. The appearance checks reference the installed ExSaveData DLL with two small API stubs. They cover per-slot/per-maid data, missing-field migration, external XML updates and accepted/cancelled edits. The profile reader references the local game assemblies; the defaults/migration test references local BepInEx and Unity assemblies.

```powershell
dotnet build .\tests\AppearanceTests -c Release -m:1 -p:GameDir="$gameDir"
dotnet run --project .\tests\AppearanceTests -c Release --no-build
dotnet run --project .\tests\BodyProfileTests -c Release -p:GameDir="$gameDir" -- .\presets\base.preset .\presets\reference.preset
dotnet run --project .\tests\ConfigDefaultsTests -c Release -p:GameDir="$gameDir"
pwsh -File .\tests\Validate-NativeAppearancePatches.ps1 -GameDir "$gameDir" -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
pwsh -File .\tests\Validate-BodyChangePatches.ps1 -GameDir "$gameDir" -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
pwsh -File .\tests\Validate-MakerPatches.ps1 -GameDir "$gameDir" -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
pwsh -File .\tests\Validate-EventPatches.ps1 -GameDir "$gameDir" -PluginDll .\bin\Release\COM3D2.Pregnancy.Plugin.dll
```

These checks cover native preset differences including Hara exclusion, slider/color calculations, legacy configuration migration, unrestricted integer settings and all 51 logged shape defaults. `expected-defaults.json` contains only expected parameter values.

The patch scripts inspect Harmony targets, parameter signatures and reflected fields in the installed game. They also check that body overlays do not call persistent appearance mutation APIs and that maker controls write through the appearance-preview interface.

Build and check results are recorded in [VERIFICATION.md](VERIFICATION.md). When updating extracted helpers, review them against their fixture setup; the older `sync_body_lifecycle.py` script does not generate the native transaction harness.

## Optional model and dump replays

Normal/boundary replays accept a locally owned body model and a directory of matching ALGrowth body CSV exports:

```powershell
dotnet run --project .\tests\AdapterTests -c Release -- --bake-normal-tests BODY_MODEL DUMP_DIRECTORY .\bake-results.json
dotnet run --project .\tests\AdapterTests -c Release --no-build -- --normal-model-lifecycle-tests BODY_MODEL DUMP_DIRECTORY .\lifecycle-results.json
```

Every `.csv` file in the dump directory is replayed, so use a directory containing only compatible exports. These checks cover protected boundaries, authored normal detail, UV seams and morph/reset behavior.

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
