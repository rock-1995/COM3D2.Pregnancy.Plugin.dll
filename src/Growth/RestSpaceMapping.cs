using System;
using System.Collections.Generic;
using System.Linq;
using COM3D2.Pregnancy.Plugin.Growth.Numerics;

namespace COM3D2.Pregnancy.Plugin.Growth;

// All matrix arithmetic stays in managed .NET. AL's generated Unity
// Matrix4x4.GetDeterminant wrapper contains invalid IL for a value-type this.
internal static class RestSpaceMapping
{
    private struct Match { public int Source, Target; }
    internal static bool TryCreate(string[] sourceNames, Matrix4x4[] sourcePoses,
        string[] targetNames, Matrix4x4[] targetPoses, out Matrix4x4 map,
        out Matrix4x4 inverse, out int sharedBones, out float maxError)
    {
        map = inverse = Matrix4x4.Identity;
        sharedBones = 0;
        maxError = 0;
        if (sourceNames.Length != sourcePoses.Length || targetNames.Length != targetPoses.Length) return false;
        var targets = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < targetNames.Length; i++)
            if (!string.IsNullOrEmpty(targetNames[i])) { if (!targets.ContainsKey(targetNames[i])) targets.Add(targetNames[i], i); }
        var matches = new List<Match>();
        for (int i = 0; i < sourceNames.Length; i++)
            if (!string.IsNullOrEmpty(sourceNames[i]) && targets.TryGetValue(sourceNames[i], out int target)) matches.Add(new Match { Source=i, Target=target });
        sharedBones = matches.Count;
        if (sharedBones == 0) return false;
        var first = matches[0];
        if (!Matrix4x4.Invert(targetPoses[first.Target], out var targetInverse)) return false;
        // System.Numerics uses row vectors. Unity adapters transpose at the boundary.
        map = sourcePoses[first.Source] * targetInverse;
        if (!Finite(map) || !Matrix4x4.Invert(map, out inverse) || !Finite(inverse)) return false;
        float extent = 1;
        foreach (var pair in matches)
        {
            if (!Matrix4x4.Invert(sourcePoses[pair.Source], out var sourceRest) ||
                !Matrix4x4.Invert(targetPoses[pair.Target], out var targetRest)) return false;
            Vector3 source = Vector3.Transform(Vector3.Zero, sourceRest);
            Vector3 expected = Vector3.Transform(Vector3.Zero, targetRest);
            float error = Vector3.Distance(Vector3.Transform(source, map), expected);
            if (!Scalar.IsFinite(error)) return false;
            maxError = Scalar.Max(maxError, error);
            extent = Scalar.Max(extent, expected.Length());
        }
        return maxError <= extent * 0.01f;
    }

    internal static bool TryCreateAliased(string[] sourceNames, Matrix4x4[] sourcePoses,
        string[] targetNames, Matrix4x4[] targetPoses, out Matrix4x4 map,
        out Matrix4x4 inverse, out int sharedBones, out float maxError)
    {
        map = inverse = Matrix4x4.Identity;
        sharedBones = 0; maxError = float.PositiveInfinity;
        if (sourceNames.Length != sourcePoses.Length || targetNames.Length != targetPoses.Length) return false;
        var targets = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < targetNames.Length; i++)
            if (!string.IsNullOrEmpty(targetNames[i])) { if (!targets.ContainsKey(targetNames[i])) targets.Add(targetNames[i], i); }
        var source = new List<Vector3>();
        var target = new List<Vector3>();
        var used = new HashSet<int>();
        for (int i = 0; i < sourceNames.Length; i++)
        {
            string name = RigBoneNames.Canonical(sourceNames[i]);
            if (string.IsNullOrEmpty(name) || !targets.TryGetValue(name, out int j) || !used.Add(j)) continue;
            if (!Matrix4x4.Invert(sourcePoses[i], out var s) || !Matrix4x4.Invert(targetPoses[j], out var t)) return false;
            source.Add(s.Translation); target.Add(t.Translation);
        }
        sharedBones = source.Count;
        if (!RestPoseFit.TryFit(source.ToArray(), target.ToArray(), out map, out maxError) || !Finite(map)) return false;
        return Matrix4x4.Invert(map, out inverse) && Finite(inverse);
    }

    private static bool Finite(Matrix4x4 m)
        => Scalar.IsFinite(m.M11) && Scalar.IsFinite(m.M12) && Scalar.IsFinite(m.M13) && Scalar.IsFinite(m.M14) &&
           Scalar.IsFinite(m.M21) && Scalar.IsFinite(m.M22) && Scalar.IsFinite(m.M23) && Scalar.IsFinite(m.M24) &&
           Scalar.IsFinite(m.M31) && Scalar.IsFinite(m.M32) && Scalar.IsFinite(m.M33) && Scalar.IsFinite(m.M34) &&
           Scalar.IsFinite(m.M41) && Scalar.IsFinite(m.M42) && Scalar.IsFinite(m.M43) && Scalar.IsFinite(m.M44);
}
