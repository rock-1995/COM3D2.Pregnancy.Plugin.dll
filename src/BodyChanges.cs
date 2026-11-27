using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    internal static class BodyChanges
    {
        internal struct SourceValue
        {
            internal int Value;
            internal bool Temporary;
        }

        internal sealed class State
        {
            internal Maid Maid;
            internal MaidParts Parts;
            internal TBody Body;
            internal BodyChangeProfile Profile;
            internal double Factor;
            internal int Lower, Upper;
            internal int NippleColorLimit;
            internal readonly Dictionary<string, int> Applied = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, SourceValue> Sources = new Dictionary<string, SourceValue>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, int> PreservedNative = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<MaidParts.PARTS_COLOR, Texture2D> Textures = new Dictionary<MaidParts.PARTS_COLOR, Texture2D>();
            internal readonly Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor> TextureColors = new Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor>();
            internal readonly Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor> AppliedColors = new Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor>();
            internal readonly Dictionary<string, BodyChangeProfile.Slider> PendingSliders = new Dictionary<string, BodyChangeProfile.Slider>(StringComparer.OrdinalIgnoreCase);
            internal readonly HashSet<MaidParts.PARTS_COLOR> PendingColors = new HashSet<MaidParts.PARTS_COLOR>();
            internal readonly HashSet<string> NativeSliders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal readonly HashSet<MaidParts.PARTS_COLOR> NativeColors = new HashSet<MaidParts.PARTS_COLOR>();
            internal bool NativeBuild;
        }

        static readonly Dictionary<Maid, State> States = new Dictionary<Maid, State>();
        static readonly Dictionary<MaidParts, State> PartStates = new Dictionary<MaidParts, State>();
        static readonly BepInEx.Logging.ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("PregnancyBodyChanges");
        static BodyChangeProfile _profile;
        static string _stamp;
        internal static bool Applying;

        internal static bool HasHistory(Maid maid) { return PregnancyManager.GetBirthCount(maid) > 0; }

        static void ReloadIfChanged()
        {
            string folder = Path.GetDirectoryName(typeof(PregnancyPlugin).Assembly.Location);
            string basePath = Path.Combine(folder, "base.preset");
            string referencePath = Path.Combine(folder, "reference.preset");
            string stamp = Stamp(basePath) + "/" + Stamp(referencePath);
            if (stamp == _stamp) return;
            _stamp = stamp;
            try
            {
                _profile = BodyChangeProfile.Load(basePath, referencePath);
                Log.LogInfo("Loaded base/reference: " + _profile.Sliders.Count + " sliders, " + _profile.Colors.Count + " color groups. Menu selections ignored.");
            }
            catch (Exception e)
            {
                _profile = null;
                Log.LogWarning("Body changes unavailable; original belly feature remains active: " + e.Message);
            }
        }

        static string Stamp(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length + ":" + info.LastWriteTimeUtc.Ticks : "missing";
        }

        internal static State Prepare(Maid maid, float progress)
        {
            return PrepareFactor(maid, (float)BodyChangeMath.Factor(PregnancyManager.GetBirthCount(maid), progress,
                PregnancyPlugin.CfgBodyChangeBirths.Value));
        }

        // bodyProgress is the independent appearance factor, not pregnancy time.
        internal static State PrepareFactor(Maid maid, float bodyProgress)
        {
            return PrepareFactor(maid, bodyProgress, true);
        }

        static State PrepareFactor(Maid maid, float bodyProgress, bool refreshColors, bool preserveNative = false)
        {
            ReloadIfChanged();
            State previous;
            States.TryGetValue(maid, out previous);
            double factor = Math.Max(0d, Math.Min(1d, bodyProgress));
            var next = new State {
                Maid = maid, Body = maid.body0, Parts = maid.Parts, Profile = _profile,
                Factor = factor, Lower = PregnancyPlugin.CfgBodyChangeLower.Value, Upper = PregnancyPlugin.CfgBodyChangeUpper.Value,
                NippleColorLimit = PregnancyPlugin.CfgNippleColorLimit.Value
            };
            bool same = previous != null && previous.Profile == _profile && previous.Body == maid.body0
                && previous.Parts == maid.Parts && previous.Factor == factor && previous.Lower == next.Lower && previous.Upper == next.Upper
                && previous.NippleColorLimit == next.NippleColorLimit;
            if (!Active(next) && previous == null) return null;
            var targets = Targets(previous, next);
            foreach (var entry in targets)
            {
                MaidProp prop = maid.GetProp(entry.Value.Id);
                if (prop == null) continue;
                int value = TargetValue(prop, entry.Key, next);
                if (preserveNative && CanPreserveNative(previous, next, entry.Key, prop))
                    next.PreservedNative[entry.Key] = value = previous.Applied[entry.Key];
                int old;
                same &= previous != null && previous.Applied.TryGetValue(entry.Key, out old) && old == value;
            }
            if (same)
            {
                if (refreshColors && Active(previous)) RefreshColors(previous);
                return null;
            }
            return next;
        }

        static int BaseValue(MaidProp prop)
        {
            return UsesTemporary(prop) ? prop.temp_value : prop.value;
        }

        static bool UsesTemporary(MaidProp prop) { return prop.boTempDut || (!prop.boDut && prop.boTempExecuted); }

        static SourceValue Source(MaidProp prop) { return new SourceValue { Value = BaseValue(prop), Temporary = UsesTemporary(prop) }; }

        static bool CanPreserveNative(State previous, State next, string tag, MaidProp prop)
        {
            if (previous == null || previous.Body != next.Body || previous.Profile != next.Profile
                || previous.Factor != next.Factor || previous.Lower != next.Lower || previous.Upper != next.Upper
                || !previous.Applied.ContainsKey(tag)) return false;
            SourceValue old;
            return previous.Sources.TryGetValue(tag, out old) && old.Value == BaseValue(prop) && old.Temporary == UsesTemporary(prop);
        }

        static int Value(MaidProp prop, long delta, State state)
        {
            return BodyChangeMath.Add(BaseValue(prop), delta, state.Factor, state.Lower, state.Upper);
        }

        static bool Active(State state) { return state != null && state.Profile != null && state.Factor != 0d; }

        static int TargetValue(MaidProp prop, string tag, State state)
        {
            BodyChangeProfile.Slider slider;
            return Active(state) && state.Profile.Sliders.TryGetValue(tag, out slider)
                ? Value(prop, slider.Delta, state) : BaseValue(prop);
        }

        static Dictionary<string, BodyChangeProfile.Slider> Targets(State previous, State next)
        {
            var result = new Dictionary<string, BodyChangeProfile.Slider>(StringComparer.OrdinalIgnoreCase);
            if (previous != null && previous.Profile != null)
                foreach (var entry in previous.Profile.Sliders)
                    if (Active(previous) || previous.Applied.ContainsKey(entry.Key)) result[entry.Key] = entry.Value;
            if (Active(next)) foreach (var entry in next.Profile.Sliders) result[entry.Key] = entry.Value;
            return result;
        }

        internal static bool NeedsGeometryApply(State next)
        {
            State previous;
            States.TryGetValue(next.Maid, out previous);
            foreach (var entry in Targets(previous, next))
            {
                MaidProp prop = next.Maid.GetProp(entry.Value.Id);
                int old;
                int target;
                if (prop == null) continue;
                if (!next.PreservedNative.TryGetValue(entry.Key, out target)) target = TargetValue(prop, entry.Key, next);
                if (previous == null || previous.Body != next.Body
                    || !previous.Applied.TryGetValue(entry.Key, out old) || old != target) return true;
            }
            return false;
        }

        // Install before native ProcItem calls. Values remain owned by MaidProp;
        // only the arguments and color textures supplied to the renderer change.
        internal static bool PrepareNative(Maid maid, float bodyProgress)
        {
            State next = PrepareFactor(maid, bodyProgress, false, true);
            State previous;
            States.TryGetValue(maid, out previous);
            bool changed = next != null && NeedsGeometryApply(next);
            if (next == null) next = previous;
            if (next == null) return false;
            Install(next, true);
            next.NativeBuild = true;
            return changed;
        }

        internal static void CompleteNative(Maid maid, bool rebuildBody = false)
        {
            State state;
            if (!States.TryGetValue(maid, out state) || !state.NativeBuild) return;
            try { ApplyPending(state, rebuildBody); }
            finally { state.NativeBuild = false; }
        }

        static void Install(State next, bool preserveNative = false)
        {
            State previous;
            States.TryGetValue(next.Maid, out previous);
            var targets = Targets(previous, next);
            var preserved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (preserveNative)
                foreach (var entry in targets)
                {
                    MaidProp prop = next.Maid.GetProp(entry.Value.Id);
                    if (prop != null && CanPreserveNative(previous, next, entry.Key, prop)) preserved[entry.Key] = previous.Applied[entry.Key];
                }
            var colors = new HashSet<MaidParts.PARTS_COLOR>();
            if (previous != null) foreach (var color in previous.PendingColors) colors.Add(color);
            if (Active(previous)) foreach (var entry in previous.Profile.Colors) colors.Add(entry.Key);
            if (Active(next)) foreach (var entry in next.Profile.Colors) colors.Add(entry.Key);
            if (previous != null && !object.ReferenceEquals(previous, next))
            {
                if (previous.Body == next.Body)
                    foreach (var entry in previous.Applied) next.Applied[entry.Key] = entry.Value;
                if (previous.Body == next.Body && previous.Parts == next.Parts)
                    foreach (var entry in previous.AppliedColors) next.AppliedColors[entry.Key] = entry.Value;
                Release(previous);
            }
            next.PendingSliders.Clear();
            foreach (var entry in targets) next.PendingSliders[entry.Key] = entry.Value;
            next.PreservedNative.Clear();
            foreach (var entry in preserved)
            {
                next.PreservedNative[entry.Key] = entry.Value;
                next.Sources[entry.Key] = previous.Sources[entry.Key];
            }
            next.PendingColors.Clear();
            foreach (var color in colors) next.PendingColors.Add(color);
            next.NativeSliders.Clear();
            next.NativeColors.Clear();
            States[next.Maid] = next;
            if (next.Parts != null) PartStates[next.Parts] = next;
        }

        internal static void Apply(State next)
        {
            Install(next);
            ApplyPending(next, false);
        }

        static void ApplyPending(State next, bool rebuildBody)
        {
            bool applying = Applying;
            Applying = true;
            try
            {
                bool geometryChanged = false;
                foreach (var entry in next.PendingSliders)
                {
                    if (next.NativeSliders.Contains(entry.Key)) continue;
                    var slider = entry.Value;
                    MaidProp prop = next.Maid.GetProp(slider.Id);
                    if (prop == null) continue;
                    int value;
                    if (rebuildBody || !next.PreservedNative.TryGetValue(entry.Key, out value)) value = TargetValue(prop, entry.Key, next);
                    int old;
                    if (!rebuildBody && next.Applied.TryGetValue(entry.Key, out old) && old == value) continue;
                    if (slider.Type == 1) next.Body.VertexMorph_FromProcItem(slider.Name, value / 100f);
                    else if (slider.Type == 2) next.Body.BoneMorph_FromProcItem(slider.Name, value / 100f);
                    next.Applied[entry.Key] = value;
                    geometryChanged = true;
                }
                if (geometryChanged) next.Body.ItemScaleReset();
                foreach (var color in next.PendingColors)
                {
                    if (next.NativeColors.Contains(color)) continue;
                    long[] delta;
                    MaidParts.PartsColor previous;
                    if (!rebuildBody && Active(next) && next.Profile.Colors.TryGetValue(color, out delta)
                        && next.AppliedColors.TryGetValue(color, out previous)
                        && previous.Equals(EffectiveColor(next, color, delta))) continue;
                    next.Body.UpdateInfinityColor(color);
                }
            }
            finally
            {
                Applying = applying;
                foreach (var entry in next.PendingSliders)
                {
                    MaidProp prop = next.Maid.GetProp(entry.Value.Id);
                    if (prop != null) next.Sources[entry.Key] = Source(prop);
                }
                next.PendingSliders.Clear();
                next.PendingColors.Clear();
                next.PreservedNative.Clear();
            }
        }

        // This hook transforms the runtime argument, never the serializable MaidProp.
        // AYS and WideSlider can continue supplying their own live values.
        internal static void Transform(TBody body, string tag, ref float value)
        {
            if (Applying || body == null || body.maid == null) return;
            State state;
            BodyChangeProfile.Slider slider;
            if (!States.TryGetValue(body.maid, out state)) return;
            bool target = state.PendingSliders.ContainsKey(tag) || (state.Profile != null && state.Profile.Sliders.ContainsKey(tag));
            if (!target) return;
            if (Active(state) && state.Profile.Sliders.TryGetValue(tag, out slider))
            {
                int result = BodyChangeMath.Add(value * 100f, slider.Delta, state.Factor, state.Lower, state.Upper);
                value = result / 100f;
            }
            state.Applied[tag] = (int)Math.Round(value * 100d, MidpointRounding.AwayFromZero);
            if (state.NativeBuild) state.NativeSliders.Add(tag);
        }

        static void RefreshColors(State state)
        {
            foreach (var entry in state.Profile.Colors)
            {
                MaidParts.PartsColor effective = EffectiveColor(state, entry.Key, entry.Value);
                MaidParts.PartsColor previous;
                if (!state.AppliedColors.TryGetValue(entry.Key, out previous) || !previous.Equals(effective))
                    state.Body.UpdateInfinityColor(entry.Key);
            }
        }

        internal static void ColorApplied(TBody body, MaidParts.PARTS_COLOR kind)
        {
            State state;
            if (body == null || body.maid == null || !States.TryGetValue(body.maid, out state)
                || state.Body != body || state.Parts == null) return;
            if (!state.PendingColors.Contains(kind)
                && (state.Profile == null || !state.Profile.Colors.ContainsKey(kind))) return;
            // Reading a palette (or coloring one newly loaded texture) does not
            // update the already rendered textures. Only the native whole-group
            // update also refreshes those targets and recomposites their layers.
            long[] delta;
            state.AppliedColors[kind] = Active(state) && state.Profile.Colors.TryGetValue(kind, out delta)
                ? EffectiveColor(state, kind, delta) : state.Parts.GetPartsColor(kind);
            if (state.NativeBuild) state.NativeColors.Add(kind);
        }

        internal static void ColorTexture(MaidParts parts, MaidParts.PARTS_COLOR kind, ref Texture2D texture)
        {
            State state;
            long[] delta;
            if (!PartStates.TryGetValue(parts, out state)) return;
            if (!Active(state) || !state.Profile.Colors.TryGetValue(kind, out delta)) return;
            MaidParts.PartsColor effective = EffectiveColor(state, kind, delta);
            Texture2D result;
            MaidParts.PartsColor previous;
            state.Textures.TryGetValue(kind, out result);
            if (result == null || !state.TextureColors.TryGetValue(kind, out previous) || !previous.Equals(effective))
            {
                if (result == null)
                {
                    result = new Texture2D(256, 1, TextureFormat.RGBA32, false);
                    result.wrapMode = TextureWrapMode.Clamp;
                    state.Textures[kind] = result;
                }
                UTY.UpdateColorTableTexture(effective, ref result);
                state.Textures[kind] = result;
                state.TextureColors[kind] = effective;
            }
            texture = result;
        }

        internal static Texture2D RenderColorTexture(MaidParts parts, MaidParts.PARTS_COLOR kind)
        {
            Texture2D texture = parts.GetPartsColorTableTex(kind);
            ColorTexture(parts, kind, ref texture);
            return texture;
        }

        static MaidParts.PartsColor EffectiveColor(State state, MaidParts.PARTS_COLOR kind, long[] delta)
        {
            // GetPartsColor returns the maid's native setting; our replacement
            // texture never writes back to it, so repeat Apply cannot accumulate.
            var original = state.Parts.GetPartsColor(kind);
            return kind == MaidParts.PARTS_COLOR.NIPPLE && state.Profile.NippleTarget.HasValue
                ? BodyChangeProfile.BlendColor(original, state.Profile.NippleTarget.Value, state.Factor, state.NippleColorLimit)
                : BodyChangeProfile.AddColor(original, delta, state.Factor);
        }

        internal static void Reset(Maid maid)
        {
            State state;
            if (States.TryGetValue(maid, out state)) Apply(new State { Maid = maid, Body = maid.body0, Parts = maid.Parts });
        }

        static void Release(State state)
        {
            if (!object.ReferenceEquals(state.Parts, null)) PartStates.Remove(state.Parts);
            foreach (Texture2D texture in state.Textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            state.Textures.Clear();
        }

        internal static void Forget(Maid maid)
        {
            State state;
            if (States.TryGetValue(maid, out state)) { Release(state); States.Remove(maid); }
        }
    }

    [HarmonyPatch(typeof(TBody), "VertexMorph_FromProcItem")]
    class Patch_BodyChangeVertex
    {
        static void Prefix(TBody __instance, string tag, ref float f) { BodyChanges.Transform(__instance, tag, ref f); }
    }

    [HarmonyPatch(typeof(TBody), "BoneMorph_FromProcItem")]
    class Patch_BodyChangeBone
    {
        static void Prefix(TBody __instance, string tag, ref float f) { BodyChanges.Transform(__instance, tag, ref f); }
    }

    [HarmonyPatch(typeof(InfinityColorTextureCache), "UpdateTexture",
        new[] { typeof(Texture), typeof(MaidParts.PARTS_COLOR), typeof(RenderTexture) })]
    class Patch_BodyChangeColor
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.Method(typeof(MaidParts), "GetPartsColorTableTex");
            var render = AccessTools.Method(typeof(BodyChanges), "RenderColorTexture");
            foreach (var instruction in instructions)
            {
                // Replace the palette read at its actual rendering call site.
                // This does not depend on the tiny getter remaining uninlined.
                if (instruction.Calls(getter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = render;
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(Maid), "Uninit")]
    class Patch_BodyChangeUnload
    {
        static void Prefix(Maid __instance) { NativeAppearanceRuntime.Forget(__instance); }
    }

    [HarmonyPatch(typeof(TBody), "UpdateInfinityColor")]
    class Patch_BodyChangeColorApplied
    {
        static void Postfix(TBody __instance, MaidParts.PARTS_COLOR f_eColorType)
        { BodyChanges.ColorApplied(__instance, f_eColorType); }
    }
}
