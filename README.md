# COM3D2 Pregnancy Plugin

Adds pregnancy progression, an adjustable pregnant belly, clothing deformation, and gradual body/color changes across pregnancies to COM3D2.

[Download the latest release](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [Changelog](CHANGELOG.md) · [Build and tests](BUILDING.md)

## Features

- **Pregnancy as you play.** Supported vaginal ejaculation events in Yotogi, story scenes and private-mode interactions can cause pregnancy. Choose a fixed conception chance or a 7-day/28-day menstrual cycle. Pregnancy advances with in-game days.
- **Shape the pregnant belly.** Adjust fullness, width, height, growth timing, sag, upper/lower contours, movement and navel appearance. Preview any pregnancy stage from the in-game menu.
- **Body changes across pregnancies.** Gradually change sliders such as bust size, waist, hips, arms, legs and the character editor's abdomen slider. Completed births contribute lasting changes, up to your chosen number of births.
- **Gradual color changes.** Differences between two presets also drive color changes. Color channels are interpolated; body-part types and menu selections are not replaced.
- **Clothing and pose support.** Underwear, outerwear, skirts, trousers and Back-category items follow the deformation. Skirts have draping support, and navel accessories follow the changing body. Supports use alongside AddYotogiSlider (AYS) morphs.
- **Separate control for each maid.** Apply a shape to one maid without replacing another maid's applied shape. A separate status window lists the maids in your save.

## Installation

1. Close the game and back up your existing pregnancy plugin and settings.
2. Download the release ZIP and extract its `BepInEx` folder into the game directory.
3. Keep one `COM3D2.Pregnancy.Plugin.dll` in the plugin search path. Keep these preset files beside it:

```text
BepInEx/plugins/
  COM3D2.Pregnancy.Plugin.dll
  base.preset
  base.preset.expreset.xml
  reference.preset
  reference.preset.expreset.xml
```

The PDB is optional. The preset pair is required for the additional body/color changes. If the pair is missing or cannot be read, the plugin logs a warning and keeps the original belly feature available.

The plugin runs through BepInEx. **BepInEx ConfigurationManager** provides F1, and **ExSaveData** stores pregnancy and childbirth history with the game save. Existing user settings are retained. The three Chinese body-change setting names from the local build are migrated to English without resetting their values.

## Quick start

1. Press **F8** and choose a maid from the `Maid` list. Click `R` to rescan if needed.
2. Enable `Pregnant` for daily pregnancy progression, or leave it unchecked for a manual preview.
3. Set `Growth stage`: `0` is the start and `1` is full term.
4. Click **Apply Belly** to apply the belly, clothing, body-slider and color changes for that stage.
5. Adjust the shape controls and apply again. Select another maid to adjust her separately.

The additional body/color changes use the existing deformation triggers. Repeatedly applying the same stage does not accumulate the change.

## The three menus

| Shortcut | What it does |
| --- | --- |
| **F1** | Open ConfigurationManager, then find `COM3D2 Pregnancy`. Set conception rules, pregnancy duration, automatic refresh, body-change limits and shortcuts. |
| **F8** | Select a maid; edit pregnancy status, progress and cycle position; adjust and apply the shape. |
| **F9** | Open `Pregnancy - Maid Status` to check pregnancy/cycle days for the maids in your save, including those outside the current scene. |

F8 and F9 are configurable defaults. Press the shortcut again to close its window. Reopen F9 to refresh its list; use F8 to edit a maid who is present in the scene.

## Body and color changes across pregnancies

The plugin compares **base.preset** with **reference.preset**:

```text
change = reference value - base value
before x completed births: factor = (pregnancy progress + completed births) / x
at x or more completed births: factor = 1
final value = the maid's own value + change * factor
```

Pregnancy progress is between `0` and `1`. Final body-slider values are rounded to integers and limited by your minimum/maximum settings. The pair defines the **difference**; it does not replace each maid with the reference character.

For example, a slider changing from `70` to `95` defines `+25`. With `x = 5`, full term in the first pregnancy adds `+5`. After two births and halfway through the next pregnancy, it adds about `+13` after rounding. At five completed births, it adds the full `+25`.

After childbirth, the pregnancy contribution becomes zero and the completed-birth contribution remains. Saves without a recorded birth count start at zero. The count increases on an actual end-of-pregnancy day transition; toggling `Pregnant` is not counted as childbirth.

### F1 body-change settings

Under **Body Changes**:

| Setting | Default | What it controls |
| --- | ---: | --- |
| `Births for Full Change` | `5` | The value of `x` in the formula. |
| `Body Value Minimum` | `0` | The lowest final value for an affected body slider. |
| `Body Value Maximum` | `100` | The highest final value for an affected body slider. |

All three are integer inputs with no additional input-range restriction. You may use negative limits or limits above `100` with slider-unlock mods. Zero or negative `x` uses the full difference. Reversed minimum/maximum values are sorted for calculation. Settings take effect on the next deformation.

**Colors use their own channel units**, which can exceed `100`. They follow the same pregnancy/birth factor but are not clamped by the body-slider limits. The supplied pair changes 11 sliders, including the character editor's `Hara` abdomen slider, and three color groups: nipple color and small differences in both eye colors. The existing pregnancy-belly deformation remains separate.

### Use your own preset pair

1. Save the starting appearance as a body preset named **base.preset**.
2. Adjust sliders and colors to the desired full-change appearance and save it as **reference.preset**.
3. Put both beside the DLL. Keep any companion XML files with matching names as shown above.
4. Click **Apply Belly** again. Modified preset files are reloaded at the next application.

Only differing native numeric sliders and color channels are used. Menu-file selections, body-part types, clothing selections and extension-only slider values are not applied. Companion XML files stay with the pair; this feature reads the native `.preset` data.

These are runtime appearance effects. The plugin does not rewrite character presets or saved character-editor slider/color data. Birth history is stored separately through ExSaveData.

## Pregnancy rules

New configurations use **Simple**, a **30%** base conception chance and **40 weeks** of pregnancy. Change them in F1.

| Mode | How it works |
| --- | --- |
| `Simple` | A supported vaginal ejaculation event triggers a check using the base chance. |
| `SevenDay` | A 7-day cycle with day 3 as the fertile day. An event leaves a fertility factor that decays daily; pregnancy is checked at day end. |
| `TwentyEightDay` | A 28-day cycle, with the main fertile day on day 14 and half the egg factor on day 15. Pregnancy is checked at day end using the decaying fertility factor. |

In cycle modes, timing and the fertility factor multiply the base chance. The cycle pauses during pregnancy and resumes after childbirth. The default 40 weeks equals 280 in-game days. Override status, stage or cycle position in F8; apply after changing the stage to update the appearance.

Supported event detection covers Yotogi, story scenes and private-mode interactions. Anal, oral and external effects are excluded; repeated particle pulses for one event count once. Only the target maid is affected. Custom scripts must use a supported event; see [compatibility details](EVENT_COMPATIBILITY.md) for technical limits.

## Shape controls and automatic refresh

F8 controls cover growth-stage timing, fullness, width, vertical range, upper reach, late-pregnancy volume, smoothing, skin slide, belly sag, upper/lower transitions, clothing displacement, thigh protection and movement in bending poses.

Navel controls adjust strength, depth, start stage, position and radius. The default start is `0.4`; the effect reaches full strength at stage `1`. `Navel visible preset` enables a full-response preview; turn that preview off to return to gradual change.

Drag sliders, type values, or use the small step buttons. Distances marked `/ torso span` are relative to torso size. Check several stages and the poses/clothing you use.

In F1, `Morph Trigger Mode = VisibilityChange` refreshes eligible maids when body/clothing visibility changes, including remaining body/color changes after childbirth. `ManualOnly` disables automatic triggers; use **Apply Belly** yourself.

## Applying, resetting and saving

- **Apply Belly:** apply the selected stage and shape; save global belly-shape settings for the next launch.
- **Reset Belly:** remove the selected maid's current runtime belly, body-slider and color changes. Status and birth history remain; a later application or automatic refresh can restore the effects.
- **Reset Defaults:** load the built-in belly-shape defaults into the editor. Apply to use and save them. This release uses the latest tuned settings, including upper transition width `0.4667925`.
- **Log to BepInEx:** record the editor's current shape parameters; this button does not change defaults.

Global belly settings are stored in `BepInEx/plugins/pregnancy_settings.json`. Per-maid pregnancy, progress, cycle and birth data use ExSaveData; save the game normally to retain them. The body-change rules use `BepInEx/config/com.pregnancymod.com3d2.cfg`.

To remove the current pregnancy contribution, clear `Pregnant`, set `Growth stage` to `0`, and apply. Recorded births still contribute body/color changes. Use **Reset Belly** to remove the appearance temporarily.

## Reporting a problem

Include the plugin version, body/clothing model, pregnancy stage, relevant settings and a screenshot. F8 also offers vertex, skirt, upper-clothing and navel-accessory exports; their paths are written to the log.

Clothing fit depends on model, shape and pose. The existing small garment edge-interior clipping limitation is documented in the [changelog](CHANGELOG.md). Automated checks and build instructions are in [BUILDING.md](BUILDING.md).

Thanks to the developers of AddYotogiSlider (AYS) and KK PregnancyPlus; this project draws on their work.
