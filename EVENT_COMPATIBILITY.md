# Conception events: story and private-mode compatibility

This documents the event-detection update introduced in the local 0.2.1 build and included in V14.

## Supported events

Yotogi, story scenes and private-mode interactions can trigger conception when their scripts emit the supported vaginal ejaculation effect. Detection follows the script's resolved target maid. Anal, oral and external effects are excluded, and repeated particle pulses within one action segment count as one event.

Simple mode checks the existing conception probability immediately. The 7-day and 28-day modes retain the existing behavior: set the fertility factor to 1, then check at day end. Already-pregnant maids are ignored.

## How detection works

The earlier implementation wrapped `YotogiCommandFactory.SetCommandCallback`, used insertion state and button names, and then iterated over active maids. Story scenes without Yotogi buttons could not trigger it, and multi-maid scenes could select the wrong target.

The shared entry point in the inspected game is `BaseKagManager.TagAddPrefabChara(KagTagSupport)`. Action scripts call `Maid.AddPrefab` with resource `Particle/pSeieki_naka` and target bone `_IK_vagina`. The resource is also used with `_IK_anal`, head and other bones, so both resource and bone must match. `TexMulAdd` in `seieki.ks` applies surface stains and is not a conception event.

Evidence from local scripts, with line numbers after CRLF normalization:

| Context | Script evidence |
| --- | --- |
| Story | `a1_kszk_g_02010.ks:905` in `script.arc` calls the ejaculation label of `sex_OM_seijyoui`, which emits the matching resource/bone effect. |
| Private-mode interaction | `general/privatemaidmode/a1_muku/a1_pmh_0006.ks:156` in `script_gp002.arc` calls the ejaculation label of `sex_deep_seijyoui`; lines 325, 328 and 331 emit three matching pulses. |
| Yotogi action | `crc_sex_bed_haimenkijyoui.ks:272/275/278` in `script.arc` emits three matching pulses in one action segment. |

`PrivateMaidTouchManager.OnFinish` enters an ADV script. `MotionKagManager` inherits the shared entry point and can map script `maid=0` to a designated main maid. Detection uses the game's target resolution rather than assuming `maid=0` means the first maid in the current list.

The old button wrapper, button-name matching and global insertion state were removed. Duplicate suppression uses script execution context, target maid and action segment, with no fixed cooldown. Observed scripts contain 1, 3, 4 or 9 pulses per segment. Entering a new script or label allows a new event.

Both normal KAG and fast script execution have context handling. If fast-executor context cannot be identified, the event is skipped with a warning rather than guessing the script or maid. Event logging uses `Pregnancy.Events`.

## Verification

The original event update compiled against local game assemblies with zero warnings/errors. All seven event-related Harmony targets and parameters were checked, fast-executor reflection fields were present, and the old button hook was absent. Its 30 offline assertions covered target selection, irrelevant effects, probability boundaries, subsequent attempts, already-pregnant maids, cycle modes, both executors and exception cleanup. V14 adds childbirth-count tests; current results are in [VERIFICATION.md](VERIFICATION.md).

A read-only inspection covered 23,323 script files across `script.arc`, `script_gp001.arc`, `script_gp002.arc` and `script_personal_om011_gp002.arc`. A replay of the base archive matched 1,814 particle calls across 360 action scripts and 598 valid segments, excluding 9,110 other effect calls. GP archives mainly supply story/interaction entry points that call these actions. This sampling does not establish support for every DLC.

The inspected `Assembly-CSharp.dll` SHA-256 was `3BADC798C17BFBD23837C509B63374669EF658EE53BE47E5D329DDBC6DB243BC`.

## In-game checks

1. Confirm the installed plugin version and `Conception uses vaginal script effects` in the BepInEx log, with no Harmony patch errors.
2. Use a maid who is not pregnant and trigger a supported event in a story/private-mode scene. In cycle modes, check that the F9 fertility factor becomes 1. In Simple mode, results follow the configured probability. Restore the original probability after any temporary test change.
3. Look for `Pregnancy.Events` / `Conception effect:`. Consecutive pulses in the same segment should produce one entry; a separate event should produce another.
4. In multi-maid scenes, confirm that only the target maid changes. Unrelated body parts and external effects should not produce a conception-event entry.

Game configuration and saves were not changed to force the offline tests. Game scripts, resources and assemblies are not distributed. Offline checks do not replace an in-game event test.

## Limits

Text-only scenes, static CGs, scripts that skip effect instructions, and mods using a different entry point, resource or bone are not detected. A missed scene needs its script or log examined; arbitrary texture loads or resources merely containing `naka` are not a fallback.

Each action segment counts once. A custom script with several independent events in one segment, without re-entering a label, needs additional event boundaries. Replays and recollections that actually execute the same effect instructions also count as events. This update does not change the save/recollection system's semantics.
