# V17 verification

Plugin version: **0.3.0**. Checked on 2026-11-22. The .NET 3.5/Mono production Release build completed with **zero warnings and zero errors**. Commands are in [BUILDING.md](BUILDING.md).

| Check | Result |
| --- | ---: |
| Appearance: save-slot isolation, XML updates, migration and edit commit/cancel | 22 passed |
| Pregnancy events and simulation | 51 passed |
| Native output buffers, original geometry and slot visibility | 30 passed |
| Body-overlay lifecycle and native input preservation | 680 passed |
| Supplied presets, Hara exclusion, formula and native APIs | 37 passed |
| Native construction order, completion and exception recovery | 44 passed |
| Original topology, masks and geometry-cache reuse | 36,791 passed |
| General adapter geometry | 4,274 passed |
| Skirt and clothing contact | 6,287 passed |
| Normal transport | 31 passed |
| Curved binding | 23,237 passed |

The native transaction and buffer harnesses link their production source files. Appearance, body and event harnesses also use production logic with game/API stubs. Profile checks parse the supplied preset pair using local game assemblies. The five adapter suites use synthetic meshes, production geometry modules and explicit Unity/TMorph stubs or surrounding helper slices; their total is **70,620** assertions.

The topology suite checks identical output for initially masked and unmasked geometry, cache reuse when only render masks change, invalidation when original topology changes, and exclusion of hidden alternative bodies from clothing calibration. Native-order checks cover one completion after native phase 3, body replacement, maid removal, slot changes and recovery after interrupted transactions. Buffer checks verify that original native geometry stays clean while the final output survives native visibility resets.

| Installed-game API inspection | Result |
| --- | ---: |
| Native lifecycle/morph Harmony targets | 9 matched |
| Native reflected fields | 7 matched |
| Body-change Harmony targets | 4 matched |
| Native-maker Harmony targets | 5 matched |
| Conception-event Harmony targets | 7 matched |

Inspection also checked the body-overlay persistence boundary and accepted-edit/preview handling. The production build is warning-free; the adapter harness has two existing unused-field warnings.

DLL SHA-256: `D44C174425F45C4D7BB68BCE2217F117718F7A6EE56C1CF602CD2FFC9852D158`.

# V16 verification

Checked on 2026-11-16. The production rebuild completed with zero warnings and zero errors. Runtime lifecycle checks: **652 passed**. Profile/math/native API checks: **37 passed**. All **4** body-change Harmony targets were verified against the installed game.

The release DLL is byte-identical to the installed color-target build from 2026-10-17. Production source matches its archived source.

DLL SHA-256: `C10C1B2483A658E2DF788DE6F9CE806C977F2FE930F04511BBDD6F0D4523F095`.

# V15 verification

Checked on 2026-10-17. The .NET 3.5 production Release build completed with zero warnings and zero errors. The release uses the same DLL subsequently confirmed in game by the user.

| Check | Result |
| --- | ---: |
| Bake-time normals and protected boundaries | 290,974 passed |
| Model-based normal lifecycle | 484,764 passed |
| Self-contained normal lifecycle | 21,009 passed |
| Refresh and multi-maid behavior | 31,531 passed |
| General adapter behavior | 4,274 passed |
| Curved boundary binding | 63,631 passed |

Model replays covered four body exports at three pregnancy stages. Tests use production code with Unity/game stubs or extracted lifecycle methods. Pose-only updates made no normal uploads in the replay. Public replay commands accept a directory of matching local exports; private model files and exports are not distributed.

DLL SHA-256: `C159E1EBF084F03CD521429F3CCBADE3C1BC1A146F47CDF3CB0CFF3EB37FE1EE`.

# V14 verification

Plugin version: **0.2.3**. Checked on 2026-10-13 against a local COM3D2 installation.

The production Release build targets the game's legacy .NET 3.5/Mono profile and completed with **zero warnings and zero errors**. Build and check commands are in [BUILDING.md](BUILDING.md).

| Check | Result |
| --- | ---: |
| Native preset differences, formula, limits, colors and game APIs | 36 passed |
| Runtime application, repeated application and restoration | 22 passed |
| Configuration migration and all 51 logged defaults | 131 passed |
| Event targets, probability, cycle, context and childbirth counts | 48 passed |
| Body/belly routing and postpartum behavior | 2,630 passed |
| Refresh and multi-maid behavior | 31,531 passed |
| General adapter behavior | 4,274 passed |
| **Total assertions** | **38,672 passed** |

Four body-change Harmony targets and seven conception-event targets matched the installed game signatures. The body-change runtime inspection found no persistent appearance field writes or calls to the checked persistent appearance mutation APIs. Fast-executor fields and removal of the old button hook were also verified.

The profile tests parse the included preset pair using local game assemblies. Configuration tests use the local BepInEx configuration library. Runtime/event/adapter harnesses use production code with game/Unity stubs or extracted lifecycle methods. Test-harness builds reported unused stub parameter/field warnings; these are separate from the clean production build.

Game assemblies, game model files, personal logs, saved settings and backups are not included in the public package. The bundled user-supplied presets define the body/color differences and are included so users can replace them.
