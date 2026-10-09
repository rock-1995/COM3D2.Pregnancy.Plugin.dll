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
