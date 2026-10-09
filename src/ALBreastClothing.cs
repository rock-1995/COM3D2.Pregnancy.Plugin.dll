using System;
using System.Collections.Generic;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;

namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        // Built and queried only when applying a shape. Native chest physics
        // and the body breast-bone exclusion are not changed here.
        private class ClothingRestSurface
        {
            public readonly List<BodySurfaceTriangle> Triangles = new();
            public readonly Dictionary<int, List<int>> Buckets = new();
        }

        private static void AddClothingRestTriangle(BodyAnchorContext context,
            int meshIndex, int a, int b, int c, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var rest = context.ClothingRest;
            int index = rest.Triangles.Count;
            rest.Triangles.Add(new BodySurfaceTriangle { MeshIndex = meshIndex, A = a, B = b, C = c });
            AddBodySurfaceTriangleToBuckets(context, index, p0, p1, p2, rest.Buckets);
        }

        private static bool TryFindClothingRestSurface(BodyAnchorContext context,
            Vector3 original, float maxDistance, out BodySurfaceHit hit)
        {
            hit = new BodySurfaceHit { TriangleIndex = -1 };
            if (context == null || maxDistance <= 0) return false;
            var rest = context.ClothingRest;
            float bestSq = maxDistance * maxDistance;
            var seen = new HashSet<int>();
            BodyAnchorBucketCoords(original, context.CellSize, out int bx, out int by, out int bz);
            int radius = (int)Math.Ceiling(maxDistance / context.CellSize) + 1;
            for (int x = -radius; x <= radius; x++)
                for (int y = -radius; y <= radius; y++)
                    for (int z = -radius; z <= radius; z++)
                    {
                        if (!rest.Buckets.TryGetValue(BodyAnchorBucketKey(bx + x, by + y, bz + z), out var list)) continue;
                        foreach (int index in list)
                        {
                            if (!seen.Add(index)) continue;
                            var tri = rest.Triangles[index];
                            var mesh = context.Meshes[tri.MeshIndex];
                            var a = mesh.Original[tri.A];
                            var ab = mesh.Original[tri.B] - a;
                            var ac = mesh.Original[tri.C] - a;
                            // Normalize length before the barycentric calculation:
                            // millimetre-sized face determinants otherwise hit the
                            // absolute degeneracy tolerance of the old contact query.
                            float scale = Mathf.Sqrt(Mathf.Max(ab.sqrMagnitude, Mathf.Max(ac.sqrMagnitude, (ac - ab).sqrMagnitude)));
                            if (scale <= 0 || !TryClosestPointOnTriangle((original - a) / scale,
                                Vector3.zero, ab / scale, ac / scale, out var closest, out var barycentric)) continue;
                            closest = a + closest * scale;
                            float sq = (original - closest).sqrMagnitude;
                            if (sq >= bestSq) continue;
                            bestSq = sq;
                            hit = new BodySurfaceHit { TriangleIndex = index, Closest = closest,
                                Barycentric = barycentric, Normal = Vector3.Cross(ab, ac).normalized, DistanceSq = sq };
                        }
                    }
            return hit.TriangleIndex >= 0;
        }

        private static bool TryGetBreastClothingTarget(BodyAnchorContext context,
            Vector3 original, float maxDistance, float multiplier, out Vector3 target, out bool excluded)
        {
            target = original;
            excluded = false;
            if (!TryFindClothingRestSurface(context, original, maxDistance, out var hit)) return false;
            return TryGetBreastClothingTarget(context, original, multiplier, hit, out target, out excluded);
        }

        private static bool TryGetBreastClothingTarget(BodyAnchorContext context,
            Vector3 original, float multiplier, BodySurfaceHit hit, out Vector3 target, out bool excluded)
        {
            target = original;
            var triangle = context.ClothingRest.Triangles[hit.TriangleIndex];
            var body = context.Meshes[triangle.MeshIndex];
            // Only the native breast-physics mask defines chest ownership.
            // A mixed triangle is a moving abdomen/chest boundary, not a
            // frozen point merely because its closest vertex belongs to chest.
            excluded = BreastExclusion.Contains(body.BreastExcluded, triangle.A)
                && BreastExclusion.Contains(body.BreastExcluded, triangle.B)
                && BreastExclusion.Contains(body.BreastExcluded, triangle.C);
            if (excluded || (!body.Affected[triangle.A] && !body.Affected[triangle.B] && !body.Affected[triangle.C]))
                return true;
            var am = ScaleAnchorMove(body.Original[triangle.A], body.Morphed[triangle.A], multiplier);
            var bm = ScaleAnchorMove(body.Original[triangle.B], body.Morphed[triangle.B], multiplier);
            var cm = ScaleAnchorMove(body.Original[triangle.C], body.Morphed[triangle.C], multiplier);
            var normal = Vector3.Cross(bm - am, cm - am);
            if (!IsFinite(normal) || normal.sqrMagnitude < 1e-18f) return false;
            normal.Normalize();
            if (Vector3.Dot(hit.Normal, normal) < -.25f) return false;
            var offset = original - hit.Closest;
            var rotated = SurfaceAttachment.RotateOffset(N(offset), N(hit.Normal), N(normal));
            target = am * hit.Barycentric.x + bm * hit.Barycentric.y + cm * hit.Barycentric.z + U(rotated);
            return IsFinite(target);
        }
    }
}
