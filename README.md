# COM3D2 Pregnancy Plugin

Adds pregnancy progression and an adjustable pregnant belly to COM3D2. Maids can conceive during Yotogi and gradually show as the days pass. You can also open the in-game controls at any time, choose a maid and a pregnancy stage, and shape the belly to your liking.

[Download the latest release](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [Changelog](CHANGELOG.md) · [Building from source](BUILDING.md)

## What you can do

**Let pregnancy develop as you play.** The plugin detects conception events during Yotogi and checks for pregnancy using your chosen probability. Use a simple fixed chance, or a 7-day or 28-day menstrual cycle that makes timing matter. You can choose how long pregnancy lasts; once a maid is pregnant, her progress advances each day and her belly develops with it.

**Create the look you want.** Choose anything from a slight bump to a full-term belly. Adjust its size, width, height and projection, as well as when each growth stage appears. You can also refine the upper and lower contours, sag, surface transitions and navel details. The same pregnancy stage can look different with different shape settings.

**Use it with clothing and poses.** Underwear, swimwear, outerwear, skirts, trousers and items in the Back (背中) category adjust with the body. Skirts have draping support, and navel accessories follow the body as it changes. The belly and clothing also follow poses such as bending, with controls for movement and transitions. The plugin supports use alongside AddYotogiSlider (AYS) body morphs.

**Adjust several maids separately.** Each maid has her own pregnancy status and progress. In a scene with multiple maids, you can select, adjust and apply a shape to each one while keeping the others' applied shapes. A separate status window lets you check pregnancy or cycle days for the maids in your save.

## Installation

Download `COM3D2.Pregnancy.Plugin.dll` from [Releases](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) and put it in your game's `BepInEx/plugins/` folder.

## The three menus

### F1: Configure the rules

With **BepInEx ConfigurationManager** installed, press **F1** and find `COM3D2 Pregnancy`. This is where you change the conception chance, choose a cycle mode, shorten or extend pregnancy, and decide whether clothing visibility changes should refresh the morph automatically. You can also change the F8 and F9 shortcuts and turn diagnostic logging on or off.

These settings apply to the plugin as a whole and affect all maids. ConfigurationManager provides this menu; without it, you can edit the plugin's configuration file while the game is closed.

### F8: Adjust the selected maid

Press **F8** to open the `COM3D2 Pregnancy` window. Select a maid from the current scene at the top, then set whether she is pregnant, her pregnancy stage and her cycle position. Below that are the shape, clothing and movement controls, so you can adjust them while looking at the character.

Click **Apply Belly** to apply and save your shape edits. Use **Reset Belly** to remove the current deformation, or **Reset Defaults** to start again from the built-in shape settings. Scroll down for more controls and the parameter logging and data export buttons. After changing characters, click `R` to rescan the list. This reloads the values and replaces any shape edits you have not applied.

### F9: Check all maids

Press **F9** to open `Pregnancy - Maid Status`. It lists the maids in your save, including those outside the current scene. Pregnant maids are highlighted and show their current day and total pregnancy length; other maids show their cycle day.

This window is an overview. To edit a maid, bring her into the scene and use F8. The list refreshes when opened, so close and reopen it to update the information. You can also close it with the `X` in the upper-right corner. Cycle days are displayed in `Simple` mode too, but conception still uses the fixed probability in that mode.

Press F8 or F9 again to close its window. These are the default shortcuts; if you have changed them, use your saved bindings.

## Try it out

Press **F8**, choose a maid from the `Maid` dropdown, then:

1. Enable **Pregnant** to mark her as pregnant.
2. Move **Growth stage** to choose her progress. `0` is the beginning and `1` is full term; the corresponding day count appears beside it.
3. Click **Apply Belly** to see the body and clothing change.
4. Adjust the shape controls below and click **Apply Belly** again until you are happy with the result.

For screenshots or a quick preview, you can leave `Pregnant` unchecked and apply a shape at any stage. Only maids marked as pregnant take part in daily pregnancy progression.

## Conception and pregnancy

A new configuration starts with **Simple mode, a 30% base conception chance and a 40-week pregnancy**. All three are adjustable. Existing configurations keep your previously saved values.

| Mode | How conception works |
| --- | --- |
| `Simple` | A detected vaginal creampie event during Yotogi triggers a pregnancy check using the base probability. |
| `SevenDay` | Uses a 7-day cycle, with day 3 as the fertile window. An event leaves a fertility factor that decays each day. Pregnancy is checked at the end of the day using that factor, cycle timing and the base probability. |
| `TwentyEightDay` | Uses a 28-day cycle, with day 14 as the main fertile window and half the cycle factor on day 15. Pregnancy is also checked at day end using the decaying fertility factor. |

Pregnancy advances at the end of each in-game day. The default 40 weeks equals 280 days, and you can make it shorter or longer. In cycle modes, the menstrual cycle pauses during pregnancy. When pregnancy reaches its end, the pregnant status clears and the cycle resumes.

You can override pregnancy status, progress and cycle position at any time using `Pregnant`, `Growth stage` and `Cycle Coefficient` in the F8 window. After changing the stage, click `Apply Belly` to see the new appearance immediately.

Change the general options in BepInEx ConfigurationManager, or close the game and edit `BepInEx/config/com.pregnancymod.com3d2.cfg`.

<details>
<summary>General settings reference</summary>

| Setting | Purpose | Default |
| --- | --- | --- |
| `Toggle UI Key` | Main window shortcut | `F8` |
| `Maid List UI Key` | Maid status window shortcut | `F9` |
| `Pregnancy Weeks` | Total pregnancy length in weeks | `40` |
| `Fertility Rate` | Base conception probability; `0.3` means 30%. Cycle modes also multiply it by the cycle and decaying fertility factors. | `0.3` |
| `Fertility Cycle Mode` | One of the three conception modes above | `Simple` |
| `Morph Trigger Mode` | When deformation is refreshed | `VisibilityChange` |

`VisibilityChange` refreshes a pregnant maid's deformation when her body or clothing visibility changes, such as when dressing or undressing. `ManualOnly` disables those automatic triggers; use `Apply Belly` to apply the deformation yourself.

</details>

## Shaping the belly

Start at a stage where the shape is easy to see, adjust the overall size and outline, then try a few other stages to check the growth progression. You can drag sliders, type values directly, or use `− / +` for small adjustments. Click **Apply Belly** after editing.

- **Size and outline:** Adjust forward projection, width, vertical range and upper-abdomen reach. Late-pregnancy height, depth and width can also be adjusted separately.
- **Growth timing:** Choose when the slight bump, visibly pregnant belly and mid-pregnancy shape appear. You can make the bump show earlier or put more growth later in the pregnancy. This does not change the total pregnancy length.
- **Local shape:** Refine the lower belly, upper settling, tilt, sag and surface smoothing. Navel position, adjustment radius and eversion can be changed too.
- **Clothing and movement:** Adjust clothing displacement, how the belly follows bending poses, and its transitions into the pelvis and upper torso. Check these controls in the pose you intend to use.

Slider ranges are suggestions; the input boxes accept values outside them. Use `.` for decimal numbers. To start over, click **Reset Defaults**, then **Apply Belly**.

<details>
<summary>Shape settings reference</summary>

| Control | What it adjusts |
| --- | --- |
| `Subtle belly at progress` / `Visible belly at progress` / `Mid belly at progress` | The three growth-stage positions. They must satisfy `0 < Subtle < Visible < Mid < 1`. |
| `Forward fullness` / `Belly width` | Forward fullness and left-to-right width. |
| `Upper abdomen reach` / `Vertical influence range` | Upper-abdomen reach and the vertical area affected. |
| `Second-stage volume` | Volume during the earlier visibly pregnant stage. |
| `Late volume height / front-back depth / left-right width multiplier` | Height, depth and width in late pregnancy. |
| `Late lower-pole lift` / `Late upper settling` | How far the bottom lifts and the upper portion settles in late pregnancy. |
| `Late whole-volume forward` / `All-stage axis tilt` | Late-pregnancy forward shift and growth tilt across all stages. |
| `Belly sag` | Downward sag, with more forward growth producing more downward movement while preserving local navel orientation. `0` disables it. |
| `Skin slide / density (legacy sag)` | Surface sliding and density distribution across the abdomen. |
| `Whole-abdomen smoothing` / `Skin clearance` | Overall surface smoothing and clearance. |
| `Navel eversion` / `Navel protrusion depth` | Navel eversion strength and protrusion depth. Setting eversion strength to `0` disables eversion. |
| `Navel change starts at pregnancy progress` | When the navel starts changing, default `0.4`. It progresses from there and reaches the full configured effect at stage `1`. |
| `Navel vertical offset` / `Navel patch radius` / `Navel proportion retention` | Navel height offset, local adjustment radius and proportion retention. A negative offset moves it down. |
| `Clothing displacement` | How much clothing moves with the belly. |
| `Thigh Guard Speed` / `Inner Thigh Guard` | Protection around the lower-abdomen-to-thigh transition and inner thighs. Set either control to `0` to disable that protection. |
| `Thigh Guard Smooth` | Smoothing within the protected thigh regions. At least one thigh protection control must be above `0`. |

`Navel visible preset` selects a pronounced navel effect and enables `Preview full navel response at current belly size`. This preview bypasses gradual pregnancy-based change. To return to gradual change, turn the preview off and apply again. `Navel off` sets eversion strength and proportion retention to `0`; click Apply for it to take effect.

Distances marked `/ torso span` are relative to the character's torso size, not centimetres.

</details>

<details>
<summary>Movement and deformation range</summary>

| Control | What it adjusts |
| --- | --- |
| `Virtual axis strength` | Additional belly following in bending poses. `0` uses the original bone following. |
| `Blend start` / `Full blend` | The transition range from original movement to belly following. |
| `Lower start / transition width / curve bias` | Start, width and curve of the transition between the lower belly and pelvis. |
| `Upper start / transition width / curve bias / join / activation` | The transition and join between the upper belly and torso. |
| `Pull at small angles` / `Pull at large angles` / `Full-pull angle` | Pull strength at different bend angles, and the angle at which full pull is reached. |
| `Anchor height` / `Anchor forward` | Vertical and forward position of the belly's movement anchor. |
| `Exclude breast-weighted vertices` | Excludes body regions controlled by breast bones. Enabled by default. |
| `Upper abdomen bone-weight filter` | Limits upper-abdomen deformation according to bone influence. Disabled by default. |
| `Extra shape-field top fade` | Adds fading at the top of the deformation. Disabled by default. |

Clothing fit depends on the garment's cut and model. If you see local clipping, check the belly size, clothing displacement and pose transitions together.

</details>

## Applying, resetting and saving

**Apply Belly** applies the current settings and saves the shape parameters to `BepInEx/plugins/pregnancy_settings.json`, which is read on the next launch. Editing a value alone does not save it. `Log to BepInEx` only records the parameters in the log.

**Reset Belly** removes the selected maid's current belly deformation but keeps her pregnancy status and progress, so an automatic refresh may bring it back. To clear both pregnancy and its appearance, uncheck `Pregnant`, set `Growth stage` to `0`, then click `Apply Belly`. **Reset Defaults** restores the shape-editing values; click Apply again to use and save them.

Pregnancy status, progress and cycle data are stored per maid through ExSaveData. Save the game normally after changing them. Applied shapes are kept separately for maids during the current session, while the JSON file stores the global shape parameters from the last Apply. Updating the plugin keeps your existing settings.

<details>
<summary>Recording settings and reporting problems</summary>

Click `Log to BepInEx` to write the shape parameters currently shown in the editor to the log. For clothing or accessory problems, these export buttons are also available:

| Button | Export |
| --- | --- |
| `Dump All Verts` | All vertex diagnostic data |
| `Dump Skirt Verts` | Skirt-related data |
| `Dump Upper Verts` | Upper-clothing data |
| `Dump Navel Accessory` | Navel accessory following data |

Files are saved in the relevant `Pregnancy*Dumps` folders under `BepInEx/plugins/`. The exact path is written to the log. Exports capture the current state without changing the deformation. For comparisons, export once without deformation and once with it applied.

When reporting a problem, include the plugin version, clothing or body model name, pregnancy stage, relevant settings and a screenshot. Attach the corresponding exports if available.

</details>

Thanks to the developers of AddYotogiSlider (AYS) and KK PregnancyPlus; this project draws on their work.
