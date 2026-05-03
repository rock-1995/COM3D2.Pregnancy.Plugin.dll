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
