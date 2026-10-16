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

Model replays covered four body exports at three pregnancy stages. Tests use production code with Unity/game stubs or extracted lifecycle methods. Pose-only updates made no normal uploads in the replay; no game frame-rate benchmark was performed. Public replay commands accept a directory of matching local exports; private model files and exports are not distributed.

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

The preceding 0.2.2 body-change build was loaded and used locally. This exact 0.2.3 English/defaults build has not had a separate in-game visual or frame-rate pass. Static patch checks do not replace runtime Harmony validation.

Game assemblies, game model files, personal logs, saved settings and backups are not included in the public package. The bundled user-supplied presets define the body/color differences and are included so users can replace them.
