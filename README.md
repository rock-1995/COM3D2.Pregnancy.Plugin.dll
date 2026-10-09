# COM3D2 Pregnancy Plugin

## Clothing preview — 2026-10-10

[Download the preview DLL](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll-on-going-/releases/tag/clothing-preview-20261010) · [Changes and validation](CHANGELOG.md) · [Build and tests](BUILDING.md)

This snapshot contains the current AL-based body and clothing implementation. Tight clothing now follows the actual animated body surface at the abdomen/chest boundary. Breast guarding is selected from the body's breast-physics region instead of freezing abdominal fabric just because the garment has a breast-bone influence. Skirt drape and pose caching from the preceding local versions are retained.

**Preview limitation:** a small edge-interior intersection remains in one simulated upper-abdomen pose (about 0.4 mm in front-depth projection). Offline mesh and pose tests passed; this specific build has not received an in-game visual pass. It is published as a prerelease, without replacing the existing stable release.

### Installation

Back up your current plugin, close the game, and replace `COM3D2.Pregnancy.Plugin.dll` in `BepInEx/plugins/`. Keep only one copy of the plugin DLL in the plugin search path. Existing settings are retained. The release does not contain game assemblies, model assets, private dumps, or user saves.

The plugin identifier and embedded version are retained from the tested build. Use the release tag and SHA-256 file to identify this preview.

---

## Earlier feature overview

The original project description is preserved below. See the preview notes above for the current deformation status.

The pregnancy mod is basically complete. Here is a breakdown of the features:

Menstrual Cycle Design
There are three modes. Simple Mode uses a fixed pregnancy rate, determined after a "cum-inside" (creampie) event. The 7-day and 28-day modes are more complex:

After a maid is finished inside, the pregnancy coefficient is set to 1, which then decays daily based on the selected mode.

Maids have an "egg coefficient": for the 7-day mode, the 3rd day is the ovulation day (coeff = 1); for the 28-day mode, the 14th day is 1, and the 15th day is 0.5.

At the end of each day, these two coefficients are multiplied by the base pregnancy rate to determine if conception occurs. The cycle then progresses; if the progress coefficient exceeds 1, it resets (subtracts 1).

Pregnancy Progression
Maids who conceive will experience belly deformation, which becomes more intense as the days progress. Similar to the cycle, the progress updates at the end of each day. Once pregnant, the menstrual cycle progress is locked to the state corresponding to "post-ovulation," and the cycle itself is paused.

UI Design
BepinEx Menu (F1): You can set a hotkey for the independent UI, select the menstrual mode, set the deformation trigger mode, adjust pregnancy rates, and define the total duration of the pregnancy (in weeks).

Debug Logs: Available, but they impact performance slightly, so it’s recommended to keep them off.

Independent UI (Default F8): This allows you to toggle pregnancy status for specific maids, adjust their cycle progress, reset belly deformation, and tweak various global deformation parameters.

Default Settings: The defaults are tuned for the largest pregnancy belly I’ve seen, but you can easily scale it using the first multiplier. By default, deformation is applied whenever model visibility changes.

Compatibility
Compatible with AddYotigiSlider (AYS) deformation. Since AYS is quite aggressive and refreshes deformation every frame, I have to stack my pregnancy deformation on top of its output. This might be a bit resource-intensive.

Request for Help from the Experts:
I’m looking for guidance on a few things:

Other Body Changes: For things like breast enlargement, hip widening, or darkening the color of nipples and labia, I want to use the existing parameters from the Maid Editor (by increasing values, adding "tattoos," or adjusting colors). However, I don't know the specific variable names or how to retrieve them.

Menstrual Bleeding: I’m considering using the game’s "virginity blood" assets or tattoo overlays, but I’m running into the same issue as above regarding variable/asset names.

Better Hooks: Currently, I’m mimicking the trigger logic from AYS's BOTE to detect when to set the pregnancy coefficient. If AYS fails to detect a "cum-inside" event, my plugin misses it too. Does anyone know of a better hook?

Special thanks to the developers of AYS and KK PregnancyPlus; I’ve referenced many of their methods.

A Bit of a Rant
I’m feeling a bit sentimental. I didn't know a single line of C#, but I managed to brute-force this into existence using AI. That said, a huge chunk of my time was spent cleaning up the AI's mess. Gemini was stubborn and acted like an idiot, GPT was passive-aggressive and even worse, but in the end, Claude saved me by fixing a massive amount of bugs. It really feels like we're in a new era.
