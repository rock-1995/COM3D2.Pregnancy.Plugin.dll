# COM3D2 Pregnancy Plugin

Adds pregnancy progression and customizable belly deformation to COM3D2. Maids can become pregnant after Yotogi, with visible changes as game days pass. You can also open the panel at any time, select a maid, and set a growth stage directly.

[Latest stable release](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [Changelog](CHANGELOG.md) · [Build instructions](BUILDING.md)

## Features

**Let pregnancy progress in-game.** The plugin detects conception events during Yotogi and applies your configured probability. Use a fixed chance or a seven- or 28-day cycle to factor in timing. You can set the pregnancy duration. Growth progresses daily after conception.

**Customize the shape.** Adjust the size, width, height, projection, and timing of each stage, from a slight curve to a full-term belly. Adjust the upper and lower contours, sag, surface transitions, and navel details independently of growth progress.

**Use it with clothing and poses.** Underwear, swimwear, outerwear, skirts, trousers, and items in the Back (背中) category adjust with the body. Skirts have draping support and navel accessories follow the body. The belly and clothing follow poses such as bending, with movement and transition controls. The plugin can be used alongside AddYotogiSlider (AYS) body morphs.

**Adjust each maid in the scene.** Maids have individual pregnancy states and progress. Select and apply a shape to each maid in a multi-maid scene without clearing previously applied shapes. The status list shows pregnancy or cycle days for maids in the save.

## Installation

Requires a working **BepInEx** installation and **ExSaveData / ExternalSaveData** for per-maid save data.

1. Close the game. When upgrading, back up your existing DLL and settings.
2. Download `COM3D2.Pregnancy.Plugin.dll` from [Releases](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) and place it in the game's `BepInEx/plugins/` directory. Keep one copy of the DLL in the plugin directory.
3. Start the game, bring a maid into the scene, and press **F8** to open the panel.

Only the DLL is required for normal use. The PDB is optional and used for debugging. Upgrading preserves saved settings.

## Controls

### F1 — Gameplay settings

With **BepInEx ConfigurationManager** installed, press **F1** and find `COM3D2 Pregnancy`. Set the conception chance, choose fixed or cycle-based probability, set pregnancy duration, and control automatic updates after clothing changes. You can also change the F8 and F9 shortcuts and toggle troubleshooting logs.

These settings apply to every maid. If you do not have ConfigurationManager, close the game and edit the plugin configuration file directly.

### F8 — Adjust the selected maid

Press **F8** to open `COM3D2 Pregnancy`. Use the top selector to choose a maid in the scene, then set her pregnancy state, growth stage, and cycle position. The lower controls adjust the belly, clothing, and pose response.

Click **Apply Belly** to apply and save the shape. **Reset Belly** clears the active deformation, and **Reset Defaults** restores the built-in shape values. Scroll to the bottom for logging and data exports. After swapping maids, click `R` to rescan the scene. This reloads the values and replaces any unapplied edits.

### F9 — View maid status

Press **F9** to open `Pregnancy - Maid Status`. It lists maids in the save, including those outside the active scene. Pregnant maids are highlighted and show elapsed and total pregnancy days. Other maids show their cycle day.

To edit a maid, bring her into the scene and use F8. Reopen the status window to refresh the list, or click `X` to close it. In `Simple` mode the list still shows cycle days, while conception uses a fixed chance.

Press F8 or F9 again to close its window. These are the default shortcuts. Your saved shortcuts take precedence.

## Quick start

Press **F8** to open the panel and select a maid from the `Maid` dropdown.

1. Select **Pregnant** to set her pregnancy state.
2. Move the **Growth stage** slider. `0` is the start and `1` is full term. The corresponding day appears beside the slider.
3. Click **Apply Belly** to see changes to the body and clothing.
4. Adjust the shape controls below and click **Apply Belly** again.

For photos or a preview, set a stage and click Apply with `Pregnant` cleared. Daily progression applies to maids marked as pregnant.

## Conception and pregnancy

A new configuration defaults to **Simple mode, a 30% base conception chance, and a 40-week pregnancy**. Each value can be adjusted. Existing installations use the saved values.

| Mode | Conception |
| --- | --- |
| `Simple` | Applies the base conception chance when the plugin detects vaginal ejaculation during Yotogi. |
| `SevenDay` | Uses a seven-day cycle, with day 3 as the fertile window. The event sets a coefficient that decays daily. At the end of each day, cycle timing and the base chance are applied. |
| `TwentyEightDay` | Uses a 28-day cycle with day 14 as the main fertile window. Day 15 has half the cycle coefficient. Conception is evaluated at the end of each day with the decaying post-event coefficient. |

Pregnancy progresses at the end of each game day. The default 40 weeks are 280 days, and the duration can be adjusted. In cycle modes the cycle pauses during pregnancy and resumes once pregnancy ends automatically at term.

You can adjust pregnancy state, growth progress, and cycle position at any time with the F8 panel's `Pregnant`, `Growth stage`, and `Cycle Coefficient` controls. Click `Apply Belly` after changing the visual stage.

Adjust general options in BepInEx ConfigurationManager, or close the game and edit `BepInEx/config/com.pregnancymod.com3d2.cfg`.

<details>
<summary>General settings</summary>

| Setting | Purpose | Default |
| --- | --- | --- |
| `Toggle UI Key` | Main panel shortcut | `F8` |
| `Maid List UI Key` | Maid status shortcut | `F9` |
| `Pregnancy Weeks` | Pregnancy length in weeks | `40` |
| `Fertility Rate` | Base conception chance. `0.3` is 30%. Cycle modes also apply the timing and decay coefficients. | `0.3` |
| `Fertility Cycle Mode` | Selects one of the three conception modes | `Simple` |
| `Morph Trigger Mode` | Deformation refresh mode | `VisibilityChange` |

`VisibilityChange` refreshes the deformation of pregnant maids when body or clothing visibility changes, such as when changing clothes. In `ManualOnly` mode, click `Apply Belly` to refresh.

</details>

## Shape controls

Pick a stage where the shape is easy to see, adjust the size and contour, then check a few other stages. You can drag sliders, type values, or step through values using `− / +`. Click **Apply Belly** after each edit.

- **Size and contour** adjust projection, width, vertical range, upper-abdomen reach, and late-stage height, depth, and width.
- **Growth timing** sets the stages at which subtle, visible, and mid-stage shapes appear. This affects appearance and leaves the total duration as configured.
- **Shape details** control lower-abdomen lift, upper-abdomen settling, overall tilt, sag, and smoothing. The navel has separate position, range, and eversion controls.
- **Clothing and movement** control how much clothing moves, how the belly follows a bending pose, and how the deformation blends into the pelvis and upper body. Adjust these in the pose you plan to use.

Sliders show suggested ranges. The text fields also accept values outside those ranges. Use `.` for decimal values. To start over, click **Reset Defaults**, then **Apply Belly**.

<details>
<summary>Shape parameters</summary>

| Control | Effect |
| --- | --- |
| `Subtle belly at progress` / `Visible belly at progress` / `Mid belly at progress` | Timing of the three growth stages. Use `0 < Subtle < Visible < Mid < 1`. |
| `Forward fullness` / `Belly width` | Forward projection and side-to-side width. |
| `Upper abdomen reach` / `Vertical influence range` | Upper-abdomen reach and vertical influence range. |
| `Second-stage volume` | Volume of the early visible stage. |
| `Late volume height / front-back depth / left-right width multiplier` | Late-stage height, front-to-back depth, and side-to-side width. |
| `Late lower-pole lift` / `Late upper settling` | Lower-abdomen lift and upper settling late in pregnancy. |
| `Late whole-volume forward` / `All-stage axis tilt` | Late-stage forward shift and growth-axis tilt across all stages. |
| `Belly sag` | Moves more prominent areas farther down and retains the local navel orientation. `0` disables sag. |
| `Skin slide / density (legacy sag)` | Surface sliding and redistribution. |
| `Whole-abdomen smoothing` / `Skin clearance` | Overall smoothing and surface clearance. |
| `Navel eversion` / `Navel protrusion depth` | Eversion strength and protrusion depth. Set eversion to `0` to disable it. |
| `Navel change starts at pregnancy progress` | The navel starts changing at this progress value. The default is `0.4`, with the full effect at `1`. |
| `Navel vertical offset` / `Navel patch radius` / `Navel proportion retention` | Vertical offset, affected area, and proportion retention. A negative offset moves the navel down. |
| `Clothing displacement` | How strongly clothing follows belly deformation. |
| `Thigh Guard Speed` / `Inner Thigh Guard` | Protection for the lower-abdomen transition and inner thighs. `0` disables the corresponding control. |
| `Thigh Guard Smooth` | Smooths protected areas when at least one thigh guard control is greater than `0`. |

`Navel visible preset` sets a prominent navel and enables `Preview full navel response at current belly size`. Clear the preview option and click Apply to resume gradual progression. `Navel off` sets eversion and proportion retention to `0`. Click Apply to use it.

Distance controls marked `/ torso span` are relative to the character's torso size.

</details>

<details>
<summary>Pose tracking and deformation range</summary>

| Control | Effect |
| --- | --- |
| `Virtual axis strength` | Extra belly tracking in poses such as bending. `0` uses normal bone tracking. |
| `Blend start` / `Full blend` | Transition range between normal pose tracking and belly tracking. |
| `Lower start / transition width / curve bias` | Start, width, and curve of the lower-abdomen transition to the pelvis. |
| `Upper start / transition width / curve bias / join / activation` | Transition and attachment between the upper abdomen and torso. |
| `Pull at small angles` / `Pull at large angles` / `Full-pull angle` | Pull strength at small and large bend angles, and the angle at which it reaches full strength. |
| `Anchor height` / `Anchor forward` | Vertical and forward position of the belly tracking anchor. |
| `Exclude breast-weighted vertices` | Excludes body areas influenced by breast bones. Enabled by default. |
| `Upper abdomen bone-weight filter` | Uses bone weights to limit upper-abdomen deformation. Disabled by default. |
| `Extra shape-field top fade` | Adds falloff at the top of the belly deformation. Disabled by default. |

Clothing cut and mesh affect the fit. If you see clipping, review belly size, clothing displacement, and the pose transition together.

</details>

## Applying, resetting, and saving

**Apply Belly** applies the current settings and saves the shape to `BepInEx/plugins/pregnancy_settings.json` for the next launch. Editing fields and clicking `Log to BepInEx` do not save the shape.

**Reset Belly** clears the selected maid's active deformation. Pregnancy state and progress are retained, so an automatic refresh can restore the belly later. To clear the pregnancy and shape together, clear `Pregnant`, set `Growth stage` to `0`, and click `Apply Belly`. **Reset Defaults** restores the shape controls, which are applied and saved on the next Apply.

ExSaveData stores pregnancy state, progress, and cycle data per maid when you save the game. Applied shapes are tracked individually in a multi-maid scene. The JSON file stores the last applied global shape. Upgrading preserves saved settings.

<details>
<summary>Logging and issue reports</summary>

Click `Log to BepInEx` to record the current shape settings. Use the export controls when diagnosing clothing or accessory problems.

| Button | Export |
| --- | --- |
| `Dump All Verts` | Full vertex diagnostic data |
| `Dump Skirt Verts` | Skirt data |
| `Dump Upper Verts` | Upper-clothing data |
| `Dump Navel Accessory` | Navel accessory tracking data |

Exports go in the applicable `Pregnancy*Dumps` directory under `BepInEx/plugins/`. The log reports the path. Each export captures the current state without changing deformation. Make separate exports before and after applying the shape for comparison.

When reporting an issue, include the plugin version, outfit or body model name, growth progress, relevant settings, a screenshot, and any available exports.

</details>

Thanks to the developers of AddYotogiSlider (AYS) and KK PregnancyPlus. Their work informed some of the techniques used here.
