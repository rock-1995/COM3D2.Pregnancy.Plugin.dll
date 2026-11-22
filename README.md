# COM3D2 Pregnancy Plugin

Adds pregnancy progression, an adjustable pregnant belly, clothing deformation, and gradual body/color changes across pregnancies to COM3D2.

[Download the latest release](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [Changelog](CHANGELOG.md) · [Build and tests](BUILDING.md)

## Features

- **Pregnancy as you play.** Supported vaginal ejaculation events in Yotogi, story scenes and private-mode interactions can cause pregnancy. Choose a fixed conception chance or a 7-day/28-day menstrual cycle. Pregnancy advances with in-game days.
- **Shape the pregnant belly.** Adjust fullness, width, height, growth timing, sag, upper/lower contours, movement and navel appearance. Preview any pregnancy stage from the in-game menu.
- **Body changes across pregnancies.** Gradually change sliders such as bust size, waist, hips, arms and legs. Completed births contribute lasting changes, up to your chosen number of births. Pregnancy does not use the native `Hara` abdomen slider.
- **Gradual color changes.** Nipple color moves from each maid's own original color toward the reference preset, with an adjustable progress limit. Other colors use the difference between the two presets. Body-part types and menu selections are not replaced.
- **Clothing and pose support.** Underwear, outerwear, skirts, trousers and Back-category items follow the deformation. Skirts have draping support, and navel accessories follow the changing body. Supports use alongside AddYotogiSlider (AYS) morphs.
- **Separate control for each maid.** Apply a shape to one maid without replacing another maid's applied shape. A separate status window lists the maids in your save.
- **Saved appearance progress.** Edit belly and body-change progress independently in the current game save's plugin XML, or use the two added sliders on the native body-customization page.
- **Native construction integration.** Complete custom deformation during character/clothing construction and reuse it when an already loaded garment is shown or hidden. Loading a new garment still requires a deformation pass.

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

The plugin runs through BepInEx. **BepInEx ConfigurationManager** provides F1, and **ExSaveData** stores pregnancy, childbirth history and the two appearance-progress values with each game save. Existing user settings are retained. The three Chinese body-change setting names from the local build are migrated to English without resetting their values.

## Quick start

1. Press **F8** and choose a maid from the `Maid` list. Click `R` to rescan if needed.
2. Enable `Pregnant` for daily pregnancy progression.
3. Set `Growth stage`: `0` is the start and `1` is full term.
4. Click **Apply Belly** to apply the shape using the maid's saved belly/body appearance progress.
5. Adjust the shape controls and apply again. Select another maid to adjust her separately.

Changing pregnancy status or stage updates the appearance progress from the simulation. For an appearance independent of the current pregnancy state, use the saved progress controls below. Repeated application does not accumulate the change.

## Edit appearance progress in a game save

Close the game and open the selected slot's `SaveData/SaveDataNNN.save.exsave.xml`. Find the target maid's GUID entry, then its existing `<plugin name="com.pregnancymod.com3d2">` node. Add or edit these properties inside that node:

```xml
<prop name="appearanceBellyProgress" value="0.65" />
<prop name="appearanceBodyProgress" value="0.4" />
```

Both values range from `0` to `1`. This example uses 65% belly progress and 40% of the full body/color change. Reload that game save to apply external edits. Do not create a second plugin node when one already exists.

Older saves initialize each missing value from that maid's pregnancy and birth history. Save the game to write the new fields; an explicit `0` is retained. These values belong to the selected game-save slot and maid, including maids outside the current scene. They are not written to global character presets.

Actual pregnancy progression, pregnancy-status changes and childbirth update the appearance values from the simulation again. Repeating the same simulation value does not overwrite an external adjustment.

### Native body-customization controls

The native body-slider page adds **怀孕肚形进度** (belly progress) and **体型改变进度** (body-change progress), each from `0` to `100`. Changing a slider previews the appearance; accepting the completed character edit commits these values. Leaving without accepting discards their preview. Simply opening the page does not change either value. Save the game afterward to retain the accepted values in that slot.

## The three menus

| Shortcut | What it does |
| --- | --- |
| **F1** | Open ConfigurationManager, then find `COM3D2 Pregnancy`. Set conception rules, pregnancy duration, body-change limits and shortcuts. |
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

The simulation uses this formula to update `appearanceBodyProgress`. A manually edited body-progress value directly supplies the factor until the simulation changes it again. Final body-slider values are rounded to integers and limited by your minimum/maximum settings. The pair defines the **difference**; it does not replace each maid with the reference character.

For example, a slider changing from `70` to `95` defines `+25`. With `x = 5`, full term in the first pregnancy adds `+5`. After two births and halfway through the next pregnancy, it adds about `+13` after rounding. At five completed births, it adds the full `+25`.

After childbirth, the pregnancy contribution becomes zero and the completed-birth contribution remains. Saves without a recorded birth count start at zero. The count increases on an actual end-of-pregnancy day transition; toggling `Pregnant` is not counted as childbirth.

### F1 body-change settings

Under **Body Changes**:

| Setting | Default | What it controls |
| --- | ---: | --- |
| `Births for Full Change` | `5` | The value of `x` in the formula. |
| `Body Value Minimum` | `0` | The lowest final value for an affected body slider. |
| `Body Value Maximum` | `100` | The highest final value for an affected body slider. |
| `Nipple Color Change Limit (%)` | `100` | Maximum progress from the maid's own nipple color to the reference color, from `0` to `100`. |

The first three are integer inputs with no additional input-range restriction. You may use negative body-slider limits or limits above `100` with slider-unlock mods. Zero or negative `x` uses the full change. Reversed body-slider bounds are sorted for calculation. Settings take effect on the next deformation.

**Colors use their own channel units**, which can exceed `100`. They follow the saved body-progress factor but are not clamped by the body-slider limits. The supplied pair changes 10 eligible sliders and three color groups: nipple color and small differences in both eye colors. `Hara` differences are excluded from the preset comparison; pregnancy keeps its own belly deformation.

Nipple color starts from each maid's native color setting and approaches the color in `reference.preset`; `base.preset` is not its starting color. The blend is the smaller of the body-progress factor and the nipple limit divided by `100`. A `0%` limit keeps her original color, `50%` stops halfway, and `100%` permits reaching the target. Repeated application never uses the previous tint as a new starting point. Other color groups retain their preset-difference behavior.

### Use your own preset pair

1. Save the starting appearance as a body preset named **base.preset**.
2. Adjust sliders and colors to the desired full-change appearance and save it as **reference.preset**.
3. Put both beside the DLL. Keep any companion XML files with matching names as shown above.
4. Click **Apply Belly** again. Modified preset files are reloaded at the next application.

Native numeric slider and other-color differences are used; the reference nipple color is an absolute target even when it matches the base preset. Menu-file selections, body-part types, clothing selections and extension-only slider values are not applied. Companion XML files stay with the pair; this feature reads the native `.preset` data.

These are runtime appearance effects. The plugin does not rewrite the original character-editor slider/color values or global character presets. Birth history and the two appearance-progress values are stored through ExSaveData with the game save.

## Pregnancy rules

New configurations use **Simple**, a **30%** base conception chance and **40 weeks** of pregnancy. Change them in F1.

| Mode | How it works |
| --- | --- |
| `Simple` | A supported vaginal ejaculation event triggers a check using the base chance. |
| `SevenDay` | A 7-day cycle with day 3 as the fertile day. An event leaves a fertility factor that decays daily; pregnancy is checked at day end. |
| `TwentyEightDay` | A 28-day cycle, with the main fertile day on day 14 and half the egg factor on day 15. Pregnancy is checked at day end using the decaying fertility factor. |

In cycle modes, timing and the fertility factor multiply the base chance. The cycle pauses during pregnancy and resumes after childbirth. The default 40 weeks equals 280 in-game days. Override status, stage or cycle position in F8; apply after changing the stage to update the appearance.

Supported event detection covers Yotogi, story scenes and private-mode interactions. Anal, oral and external effects are excluded; repeated particle pulses for one event count once. Only the target maid is affected. Custom scripts must use a supported event; see [compatibility details](EVENT_COMPATIBILITY.md) for technical limits.

## Shape controls and native refresh

F8 controls cover growth-stage timing, fullness, width, vertical range, upper reach, late-pregnancy volume, smoothing, skin slide, belly sag, upper/lower transitions, clothing displacement, thigh protection and movement in bending poses.

Navel controls adjust strength, depth, start stage, position and radius. The default start is `0.4`; the effect reaches full strength at stage `1`. `Navel visible preset` enables a full-response preview; turn that preview off to return to gradual change.

Drag sliders, type values, or use the small step buttons. Distances marked `/ torso span` are relative to torso size. Check several stages and the poses/clothing you use.

The plugin now applies appearance at the completion of native character/clothing construction. Loaded garments receive their deformation even if currently hidden. Showing or hiding them reuses the result; actual model or native shape changes request a new application. The old `Morph Trigger Mode` setting is no longer used.

## Applying, resetting and saving

- **Apply Belly:** apply the selected shape using the maid's current saved belly/body progress; save global belly-shape settings for the next launch.
- **Reset Belly:** remove the selected maid's current runtime belly, body-slider and color changes. Status and birth history remain; a later application or automatic refresh can restore the effects.
- **Reset Defaults:** load the built-in belly-shape defaults into the editor. Apply to use and save them. This release uses the latest tuned settings, including upper transition width `0.4667925`.
- **Log to BepInEx:** record the editor's current shape parameters; this button does not change defaults.

Global belly-shape settings are stored in `BepInEx/plugins/pregnancy_settings.json`. Per-maid pregnancy, progress, cycle, birth data and the two appearance-progress values use ExSaveData; save the game normally to retain them. The body-change rules use `BepInEx/config/com.pregnancymod.com3d2.cfg`.

To remove the current pregnancy contribution, clear `Pregnant`, set `Growth stage` to `0`, and apply. Recorded births still contribute body/color changes. For independent appearance control, set either saved appearance progress to `0`. Use **Reset Belly** to remove the current appearance temporarily; the saved progress remains available for the next application.

## Reporting a problem

Include the plugin version, body/clothing model, pregnancy stage, relevant settings and a screenshot. F8 also offers vertex, skirt, upper-clothing and navel-accessory exports; their paths are written to the log.

Clothing fit depends on model, shape and pose. The existing small garment edge-interior clipping limitation is documented in the [changelog](CHANGELOG.md). Automated checks and build instructions are in [BUILDING.md](BUILDING.md).

Thanks to the developers of AddYotogiSlider (AYS) and KK PregnancyPlus; this project draws on their work.
