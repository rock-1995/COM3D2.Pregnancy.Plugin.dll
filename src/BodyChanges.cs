using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    internal static class BodyChanges
    {
        internal sealed class State
        {
            internal Maid Maid;
            internal MaidParts Parts;
            internal TBody Body;
            internal BodyChangeProfile Profile;
            internal double Factor;
            internal int Lower, Upper;
            internal readonly Dictionary<string, int> Applied = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<MaidParts.PARTS_COLOR, Texture2D> Textures = new Dictionary<MaidParts.PARTS_COLOR, Texture2D>();
            internal readonly Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor> TextureColors = new Dictionary<MaidParts.PARTS_COLOR, MaidParts.PartsColor>();
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
            ReloadIfChanged();
            State previous;
            States.TryGetValue(maid, out previous);
            int x = PregnancyPlugin.CfgBodyChangeBirths.Value;
            double factor = BodyChangeMath.Factor(PregnancyManager.GetBirthCount(maid), progress, x);
            var next = new State {
                Maid = maid, Body = maid.body0, Parts = maid.Parts, Profile = _profile,
                Factor = factor, Lower = PregnancyPlugin.CfgBodyChangeLower.Value, Upper = PregnancyPlugin.CfgBodyChangeUpper.Value
            };
            if (_profile == null || factor == 0d)
                return previous == null ? null : next;
            bool same = previous != null && previous.Profile == _profile && previous.Body == maid.body0
                && previous.Parts == maid.Parts && previous.Factor == factor && previous.Lower == next.Lower && previous.Upper == next.Upper;
            foreach (var entry in _profile.Sliders)
            {
                MaidProp prop = maid.GetProp(entry.Value.Id);
                if (prop == null) continue;
                int value = Value(prop, entry.Value.Delta, next);
                int old;
                same &= previous != null && previous.Applied.TryGetValue(entry.Key, out old) && old == value;
            }
            if (same)
            {
                RefreshColors(previous);
                return null;
            }
            return next;
        }

        static int BaseValue(MaidProp prop)
        {
            return prop.boTempDut || (!prop.boDut && prop.boTempExecuted) ? prop.temp_value : prop.value;
        }

        static int Value(MaidProp prop, long delta, State state)
        {
            return BodyChangeMath.Add(BaseValue(prop), delta, state.Factor, state.Lower, state.Upper);
        }

        internal static void Apply(State next)
        {
            State previous;
            States.TryGetValue(next.Maid, out previous);
            var sliders = new Dictionary<string, BodyChangeProfile.Slider>(StringComparer.OrdinalIgnoreCase);
            var colors = new HashSet<MaidParts.PARTS_COLOR>();
            if (previous != null)
            {
                if (previous.Profile != null)
                {
                    foreach (var p in previous.Profile.Sliders) sliders[p.Key] = p.Value;
                    foreach (var p in previous.Profile.Colors) colors.Add(p.Key);
                }
                Release(previous);
            }
            bool active = next.Profile != null && next.Factor != 0d;
            if (active)
            {
                States[next.Maid] = next;
                if (next.Parts != null) PartStates[next.Parts] = next;
                foreach (var p in next.Profile.Sliders) sliders[p.Key] = p.Value;
                foreach (var p in next.Profile.Colors) colors.Add(p.Key);
            }
            else States.Remove(next.Maid);
            Applying = true;
            try
            {
                foreach (var entry in sliders)
                {
                    var slider = entry.Value;
                    MaidProp prop = next.Maid.GetProp(slider.Id);
                    if (prop == null) continue;
                    BodyChangeProfile.Slider current;
                    int value = active && next.Profile.Sliders.TryGetValue(entry.Key, out current)
                        ? Value(prop, current.Delta, next) : BaseValue(prop);
                    if (slider.Type == 1) next.Body.VertexMorph_FromProcItem(slider.Name, value / 100f);
                    else if (slider.Type == 2) next.Body.BoneMorph_FromProcItem(slider.Name, value / 100f);
                    next.Applied[entry.Key] = value;
                }
                if (sliders.Count > 0) next.Body.ItemScaleReset();
                foreach (var color in colors) next.Body.UpdateInfinityColor(color);
            }
            finally { Applying = false; }
        }

        // This hook transforms the runtime argument, never the serializable MaidProp.
        // AYS and WideSlider can continue supplying their own live values.
        internal static void Transform(TBody body, string tag, ref float value)
        {
            if (Applying || body == null || body.maid == null) return;
            State state;
            BodyChangeProfile.Slider slider;
            if (!States.TryGetValue(body.maid, out state) || !state.Profile.Sliders.TryGetValue(tag, out slider)) return;
            int result = BodyChangeMath.Add(value * 100d, slider.Delta, state.Factor, state.Lower, state.Upper);
            value = result / 100f;
            state.Applied[tag] = result;
        }

        static void RefreshColors(State state)
        {
            foreach (var entry in state.Profile.Colors)
            {
                MaidParts.PartsColor effective = BodyChangeProfile.AddColor(state.Parts.GetPartsColor(entry.Key), entry.Value, state.Factor);
                MaidParts.PartsColor previous;
                if (!state.TextureColors.TryGetValue(entry.Key, out previous) || !previous.Equals(effective))
                    state.Body.UpdateInfinityColor(entry.Key);
            }
        }

        internal static void ColorTexture(MaidParts parts, MaidParts.PARTS_COLOR kind, ref Texture2D texture)
        {
            State state;
            long[] delta;
            if (!PartStates.TryGetValue(parts, out state) || !state.Profile.Colors.TryGetValue(kind, out delta)) return;
            MaidParts.PartsColor effective = BodyChangeProfile.AddColor(parts.GetPartsColor(kind), delta, state.Factor);
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

    [HarmonyPatch(typeof(MaidParts), "GetPartsColorTableTex")]
    class Patch_BodyChangeColor
    {
        static void Postfix(MaidParts __instance, MaidParts.PARTS_COLOR f_eColorType, ref Texture2D __result)
        { BodyChanges.ColorTexture(__instance, f_eColorType, ref __result); }
    }

    [HarmonyPatch(typeof(Maid), "Uninit")]
    class Patch_BodyChangeUnload
    {
        static void Prefix(Maid __instance) { BodyChanges.Forget(__instance); }
    }
}
