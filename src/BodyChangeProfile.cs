using System;
using System.Collections.Generic;
using System.IO;

namespace COM3D2.Pregnancy.Plugin
{
    internal static class BodyChangeMath
    {
        internal static double Factor(int births, float progress, int x)
        {
            // The threshold branch also handles x <= 0 without division by zero.
            if (births >= x) return 1d;
            return (Math.Max(0d, Math.Min(1d, progress)) + births) / x;
        }

        internal static int Add(double original, long delta, double factor, int lower, int upper)
        {
            double value = original + delta * factor;
            int lo = Math.Min(lower, upper), hi = Math.Max(lower, upper);
            return (int)Math.Round(Math.Max(lo, Math.Min(hi, value)), MidpointRounding.AwayFromZero);
        }
    }

    internal sealed class BodyChangeProfile
    {
        internal sealed class Slider
        {
            internal MPN Id;
            internal string Name;
            internal int Type;
            internal long Delta;
        }

        internal readonly Dictionary<string, Slider> Sliders =
            new Dictionary<string, Slider>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<MaidParts.PARTS_COLOR, long[]> Colors =
            new Dictionary<MaidParts.PARTS_COLOR, long[]>();

        internal static BodyChangeProfile Load(string basePath, string referencePath)
        {
            List<MaidProp> baseProps, referenceProps;
            MaidParts.PartsColor[] baseColors, referenceColors;
            Read(basePath, out baseProps, out baseColors);
            Read(referencePath, out referenceProps, out referenceColors);
            var profile = new BodyChangeProfile();
            foreach (MaidProp original in baseProps)
            {
                if (original.type != 1 && original.type != 2) continue;
                foreach (MaidProp reference in referenceProps)
                {
                    if (!string.Equals(original.name, reference.name, StringComparison.OrdinalIgnoreCase)
                        || original.type != reference.type || original.value == reference.value) continue;
                    profile.Sliders[original.name] = new Slider {
                        Id = (MPN)original.idx, Name = original.name, Type = original.type,
                        Delta = (long)reference.value - original.value
                    };
                    break;
                }
            }
            for (int i = 0; i < Math.Min(baseColors.Length, referenceColors.Length); i++)
            {
                int[] b = ColorValues(baseColors[i]), r = ColorValues(referenceColors[i]);
                var delta = new long[b.Length];
                bool changed = false;
                for (int j = 0; j < delta.Length; j++) { delta[j] = (long)r[j] - b[j]; changed |= delta[j] != 0; }
                if (changed) profile.Colors[(MaidParts.PARTS_COLOR)i] = delta;
            }
            return profile;
        }

        static void Read(string path, out List<MaidProp> props, out MaidParts.PartsColor[] colors)
        {
            // Read data only: never call PresetSet, load menus, or create a thumbnail texture.
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadString() != "CM3D2_PRESET") throw new InvalidDataException("Invalid preset header: " + path);
                int version = reader.ReadInt32();
                if (version < 2 || version >= 30000 || (version >= 1560 && version < 20000))
                    throw new InvalidDataException("Unsupported COM3D2 preset version: " + version);
                reader.ReadInt32(); // Body / Wear / All: numeric differences alone decide what is used.
                int thumbnailBytes = reader.ReadInt32();
                if (thumbnailBytes < 0 || thumbnailBytes > stream.Length - stream.Position)
                    throw new InvalidDataException("Invalid preset thumbnail length");
                stream.Seek(thumbnailBytes, SeekOrigin.Current);
                props = Maid.DeserializePropPre(reader);
                colors = Maid.DeserializeMultiColorPre(reader);
            }
        }

        internal static int[] ColorValues(MaidParts.PartsColor c)
        {
            return new[] { c.m_nMainHue, c.m_nMainChroma, c.m_nMainBrightness, c.m_nMainContrast,
                c.m_nShadowRate, c.m_nShadowHue, c.m_nShadowChroma, c.m_nShadowBrightness, c.m_nShadowContrast };
        }

        internal static MaidParts.PartsColor AddColor(MaidParts.PartsColor original, long[] delta, double factor)
        {
            int[] v = ColorValues(original);
            // Color channels use the game's native units (some exceed 100), not body-slider limits.
            for (int i = 0; i < v.Length; i++)
                v[i] = BodyChangeMath.Add(v[i], delta[i], factor, int.MinValue, int.MaxValue);
            original.m_nMainHue = v[0]; original.m_nMainChroma = v[1];
            original.m_nMainBrightness = v[2]; original.m_nMainContrast = v[3];
            original.m_nShadowRate = v[4]; original.m_nShadowHue = v[5];
            original.m_nShadowChroma = v[6]; original.m_nShadowBrightness = v[7]; original.m_nShadowContrast = v[8];
            return original;
        }
    }
}
