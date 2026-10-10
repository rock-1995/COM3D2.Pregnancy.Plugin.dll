# COM3D2 Pregnancy Plugin

A pregnancy and belly deformation plugin for COM3D2. Maids can conceive during play and progress through pregnancy as game days pass. You can also set progress directly and adjust the belly shape, navel, and clothing response.

[Latest stable release](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [Changelog](CHANGELOG.md) · [Build instructions](BUILDING.md)

## Features

- **Pregnancy and cycles** support fixed probability, a seven-day cycle, and a 28-day cycle. Set the base conception chance and duration, or adjust a maid's pregnancy state, progress, and cycle position.
- **Progressive belly growth** lets you set the timing of the subtle, visible, and mid-stage shapes, as well as size, width, height, projection, and sag.
- **Belly sag** moves the projecting abdomen down and preserves the navel's local orientation. The original surface sliding and redistribution controls are available under `Skin slide / density`.
- **Progressive navel eversion** starts at a chosen stage and reaches full strength at 100% progress. Adjust protrusion depth, position, range, and proportions. Navel accessories follow the deformed navel.
- **Clothing tracking** supports underwear, swimwear, outerwear, skirts, trousers, and the Back (背中) category. Skirts have draping support. Areas weighted to the calves and unaffected by skirt bones retain normal motion, reducing unwanted hem lift.
- **Pose and multi-maid support** lets the belly and clothing follow poses such as bending. Each maid in the scene retains her applied shape when you switch maids or apply a shape to someone else.
- **Automatic updates and AYS compatibility** refreshes the shape when clothing visibility changes and reuses calculations that are still valid. The plugin can be used with AddYotogiSlider (AYS) body morphs.

## Installation and upgrades

Requires **BepInEx** and **ExSaveData / ExternalSaveData**. The latter reads and writes per-maid pregnancy state, growth progress, and cycle data.

1. Close the game and back up the existing DLL and settings.
2. Download `COM3D2.Pregnancy.Plugin.dll` from [Releases](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest).
3. Put the DLL in the game's `BepInEx/plugins/` directory. Replace the old DLL when upgrading, and keep one copy in the plugin search path.
4. Start the game, bring a maid into the scene, and press **F8**.

The PDB is optional and used for debugging. Upgrading preserves saved settings. To load this version's built-in shape settings, click `Reset Defaults` and then `Apply Belly`.

## Quick start

### Set the belly shape manually

1. Press **F8** to open `COM3D2 Pregnancy`.
2. Select a maid in the current scene from the `Maid` dropdown. Click `R` to refresh the list.
3. Select `Pregnant` and drag `Growth stage` to set progress. `0` is the start and `1` is full term. The corresponding day appears beside the slider.
4. Click **Apply Belly** to preview the result.
5. Adjust the belly or navel controls, then click **Apply Belly** again to apply and save.

For posing or previewing, leave `Pregnant` cleared, set a stage, and click `Apply Belly`. Daily progression and pregnancy-triggered refreshes apply to maids marked as pregnant.

### Progress through pregnancy in-game

Choose a conception mode, base chance, and duration in the plugin settings. The selected mode handles vaginal ejaculation events during Yotogi. If a maid becomes pregnant, her progress updates at the end of each game day.

The default duration is **40 weeks (280 days)**. Pregnancy ends automatically at term and cycle-based modes resume the cycle. You can preview any stage by changing progress in the F8 panel.

### View all maid statuses

Press **F9** to open `Pregnancy - Maid Status`. It shows names, pregnancy days, and cycle days for maids in the save. Pregnant maids are highlighted. Reopen the window to refresh the list.

## Conception modes and general settings

New installations default to **`Simple`**. Existing installations use the saved mode.

| Mode | Behavior |
| --- | --- |
| `Simple` | Applies the base conception probability when the event is detected, regardless of cycle timing. |
| `SevenDay` | Uses a seven-day cycle with day 3 as the fertile window. The event sets a coefficient that decays daily. At the end of each day, the calculation applies cycle timing and the base chance. |
| `TwentyEightDay` | Uses a 28-day cycle. Day 14 is the main fertile window and day 15 has half its cycle coefficient. The end-of-day calculation also applies the decaying post-event coefficient. |

In cycle modes, pregnancy pauses cycle progression and sets the cycle position to the post-ovulation stage. In the F8 panel, `Cycle Coefficient` adjusts the current cycle position and displays the corresponding day.

General settings are stored in `BepInEx/config/com.pregnancymod.com3d2.cfg`. Configure them in BepInEx ConfigurationManager, or close the game and edit the file.

| Setting | Purpose | Default |
| --- | --- | --- |
| `Toggle UI Key` | Open or close the main panel | `F8` |
| `Maid List UI Key` | Open or close the maid status list | `F9` |
| `Pregnancy Weeks` | Total pregnancy duration in weeks | `40` |
| `Fertility Rate` | Base conception chance. `0.3` is 30%. Cycle modes also apply the timing and decay coefficients. | `0.3` |
| `Fertility Cycle Mode` | Conception mode | `Simple` |
| `Morph Trigger Mode` | Deformation refresh mode | `VisibilityChange` |

`VisibilityChange` requests a refresh when a pregnant maid's body or clothing visibility changes. In `ManualOnly` mode, use `Apply Belly` to refresh.

## Main panel controls

| Control | Behavior |
| --- | --- |
| `Maid` | Selects a maid in the scene and loads her state and applied shape. |
| `R` | Rescans the scene and reloads the controls, discarding unapplied shape edits. |
| `Pregnant` | Sets the selected maid's pregnancy state while retaining the growth stage. |
| `Growth stage` | Sets pregnancy progress. Click `Apply Belly` to update the shape. |
| `Cycle Coefficient` | Sets cycle position on a scale of `0–1`. |
| `Apply Belly` | Applies the current stage and shape to the selected maid and saves the shape settings. |
| `Reset Belly` | Clears the selected maid's current deformation while retaining pregnancy state, progress, and the saved global shape settings. |
| `Reset Defaults` | Restores the built-in shape settings. Click `Apply Belly` again to apply and save them. |

Drag a slider, type a value, or use `− / +` to step through values. Sliders show suggested ranges and text fields also accept finite values outside those ranges. Use `.` as the decimal separator. Click `Apply Belly` when done.

To clear pregnancy state and the current shape together, clear `Pregnant`, set `Growth stage` to `0`, and click `Apply Belly`. If a maid remains pregnant after `Reset Belly`, the shape can return after an automatic refresh.

## Belly shape parameters

### Growth timing and overall shape

| Control | Effect |
| --- | --- |
| `Subtle belly at progress` / `Visible belly at progress` / `Mid belly at progress` | Sets when each shape appears without changing the pregnancy duration. Use `0 < Subtle < Visible < Mid < 1`. |
| `Forward fullness` / `Belly width` | Forward projection and side-to-side width. |
| `Upper abdomen reach` / `Vertical influence range` | Upper-abdomen reach and vertical influence range. |
| `Whole-abdomen smoothing` | Overall belly smoothing. |
| `Skin slide / density (legacy sag)` | Surface sliding and redistribution from the earlier sag control. |
| `Belly sag` | Moves more prominent areas farther down. `0` disables sag. |
| `Second-stage volume` | Volume of the early visible stage. |
| `Late lower-pole lift` / `Late upper settling` | How much the lower abdomen lifts and the upper abdomen settles late in pregnancy. |
| `Late whole-volume forward` / `All-stage axis tilt` | Late-stage forward shift and growth-axis tilt across all stages. |
| `Late volume height / front-back depth / left-right width multiplier` | Late-stage height, front-to-back depth, and side-to-side width. |
| `Skin clearance` | Surface clearance for the belly shape. |

### Navel

| Control | Effect |
| --- | --- |
| `Navel eversion` | Eversion strength. `0` disables it. |
| `Navel change starts at pregnancy progress` | Progress at which the navel starts changing. The default is **`0.4` (40%)**, and the effect reaches full strength at `1`. |
| `Navel protrusion depth` | Depth of the protruding navel. |
| `Navel vertical offset` | Vertical offset. Negative values move the navel down. |
| `Navel patch radius` | Size of the affected area around the navel. |
| `Navel proportion retention` | How closely the local navel proportions are retained. |
| `Preview full navel response at current belly size` | Previews the full navel effect at the current size, bypassing gradual progression. Disabled by default. |
| `Navel visible preset` | Sets a prominent navel and enables full preview. To resume gradual response, clear the preview option and click Apply. |
| `Navel off` | Sets eversion and proportion retention to `0`. Click Apply to use these values. |

Distance controls marked `/ torso span` are relative to the character's torso size. `Navel stage response` displays the amount of navel change at the current stage.

### Clothing, thigh guard, and bending poses

| Control | Effect |
| --- | --- |
| `Clothing displacement` | How strongly clothing follows belly deformation. |
| `Thigh Guard Speed` | How quickly protection changes in the transition from lower abdomen to thighs. `0` disables it. |
| `Inner Thigh Guard` | Protection strength near the inner thighs. `0` disables it. |
| `Thigh Guard Smooth` | Smooths protected areas when at least one thigh guard control is greater than `0`. |
| `Virtual axis strength` | Extra belly tracking when bending. `0` uses normal bone tracking. |
| `Blend start` / `Full blend` | Transition range between normal pose tracking and belly tracking. |
| `Lower start / transition width / curve bias` | Start, width, and curve of the transition from the lower abdomen to the pelvis. |
| `Upper start / transition width / curve bias / join / activation` | Transition and attachment between the upper abdomen and torso. |
| `Pull at small angles` / `Pull at large angles` / `Full-pull angle` | Pull strengths at small and large bend angles and the angle that reaches full pull. |
| `Anchor height` / `Anchor forward` | Vertical and forward position of the belly tracking anchor. |
| `Exclude breast-weighted vertices` | Excludes body areas influenced by breast bones. Enabled by default. |
| `Upper abdomen bone-weight filter` | Uses bone weights to limit upper-abdomen deformation. Disabled by default. |
| `Extra shape-field top fade` | Adds falloff at the top of the belly deformation. Disabled by default. |

For pose controls, start with the bending pose you plan to use, then adjust and apply in small steps. Clothing cut and mesh affect the fit. If you see clipping, review clothing displacement, belly size, and pose transition.

## Saved settings

- **General settings** such as shortcuts, conception mode, chance, and duration are stored in `BepInEx/config/com.pregnancymod.com3d2.cfg`.
- **Shape settings** are stored in `BepInEx/plugins/pregnancy_settings.json` when you click `Apply Belly` and loaded at startup. Editing the controls or clicking `Log to BepInEx` does not save the shape.
- **Character state** includes pregnancy state, progress, and cycle data. ExSaveData stores it per maid when you save the game.
- **Multi-maid shapes** are tracked per maid during the session. The JSON file stores the last applied global shape settings.

## Exports and issue reports

The controls at the bottom of the panel log settings and export data for diagnosing clothing problems.

| Control | Output |
| --- | --- |
| `Log to BepInEx` | Records the current shape settings in the BepInEx log. |
| `Dump All Verts` | Exports full vertex diagnostics for the selected maid. |
| `Dump Skirt Verts` | Exports skirt deformation data. |
| `Dump Upper Verts` | Exports upper-clothing deformation data. |
| `Dump Navel Accessory` | Exports navel accessory tracking data. |

Exports are stored under `BepInEx/plugins/`, in `PregnancyDumps`, `PregnancySkirtDumps`, `PregnancyUpperDumps`, or `PregnancyNavelDumps`. The log reports the path. Exports record the current state. To compare deformation, export once before applying and once afterward.

When reporting an issue, include the plugin version, outfit or body model name, growth progress, relevant settings, a screenshot, and any available exports.

## Credits

Thanks to the developers of AddYotogiSlider (AYS) and KK PregnancyPlus. Their work informed some of the techniques used here.
