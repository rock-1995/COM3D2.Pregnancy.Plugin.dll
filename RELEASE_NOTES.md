# V17 — 0.3.0

Pregnancy appearance now joins native character/clothing construction. Already loaded garments reuse their completed deformation when shown or hidden; a newly loaded model still needs a deformation pass.

- Edit belly and body-change progress independently in each maid's game-save XML using `appearanceBellyProgress` and `appearanceBodyProgress`, both from `0` to `1`.
- The native body-customization page also has two progress sliders. Accepted edits are stored with the game-save slot, not global character presets.
- Existing saves initialize missing values from pregnancy and birth history. Explicit zero values are retained; future simulation changes update the progress again.
- Pregnancy does not use `Hara`. The old visibility-trigger mode is replaced by native construction handling.

Extract the installation ZIP's `BepInEx` folder into the game directory with the game closed. Keep your customized preset pair and settings when updating. Save the game normally to retain the new progress fields. See the [user guide](README.md) for XML examples and controls.

Full changes are in the [changelog](CHANGELOG.md); test scope and results are in [verification](VERIFICATION.md).
