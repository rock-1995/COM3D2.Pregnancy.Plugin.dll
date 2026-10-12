# V14 — Body and color changes across pregnancies

Pregnancy can now gradually change more than the plugin's belly shape. The included preset pair adds changes to 11 body sliders and three color groups, and completed births contribute lasting changes.

## What's included

- Gradual body-slider and color changes based on pregnancy progress and completed births.
- An editable `base.preset` / `reference.preset` pair to define your own changes.
- F1 inputs for the births needed for full change and final body-slider limits. Defaults are **5**, **0** and **100**; negative limits and values above 100 are supported.
- Runtime effects that leave character-card appearance data unchanged. Repeated application does not accumulate the same change.
- Conception-event support for Yotogi, story scenes and private-mode interactions.
- English body-change controls/help, with existing Chinese-key settings migrated automatically.
- Updated built-in belly-shape defaults from the latest tuned settings.

## Install and use

1. Close the game and back up the current plugin/settings.
2. Extract the ZIP's `BepInEx` folder into the game directory. Keep `base.preset`, `reference.preset` and matching XML files beside the DLL.
3. Press **F8**, select a maid, set `Growth stage` and click **Apply Belly**. Enable `Pregnant` to progress with in-game days.
4. Press **F1** and find **COM3D2 Pregnancy → Body Changes** for the birth threshold and slider limits. Use **F9** for the maid status list.

The additional change is `(reference - base) × (pregnancy progress + completed births) / x`. At `x` births or more, it uses the full difference. After childbirth, the birth-history portion remains. Saves without a recorded birth count start at zero; earlier births cannot be reconstructed.

Replace the two named presets with your own starting and full-change appearances to customize the effect, then apply again. Body-part/menu selections are ignored. Colors use their native channel units and are not restricted by the body-slider limits.

Existing belly settings are retained. To use the new built-in defaults, choose **Reset Defaults**, then **Apply Belly** in F8. **Reset Belly** removes current runtime appearance effects without erasing pregnancy or childbirth history.

Read the [user guide](README.md) for controls and examples. The packaged plugin version is **0.2.3**; the release label is **V14**. Verification is documented in [BUILDING.md](BUILDING.md). This exact English/defaults build has not had a separate in-game visual pass; the preceding body-change build was loaded and used locally.
