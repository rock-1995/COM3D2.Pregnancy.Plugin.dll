using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        // AL 0.2.27 surface attachment and clothing repair equations.
        private static void RuntimeLogInfo(string message) { if (IsDebugMeshLoggingEnabled()) _log.LogInfo(message); }
        private static readonly FieldInfo _clothBodyRendererField = typeof(TMorph).GetField(
            "smr_src", BindingFlags.Instance | BindingFlags.NonPublic);

        private static int[] ClothingBodyTopology(Maid maid, SkinnedMeshRenderer renderer)
        {
            // TMorph collapses hidden skin triangles to (0,0,0). Its original
            // topology remains available even while a coat hides the chest.
            // Read it for clothing contact only; never change the rendered mesh.
            if (maid?.body0?.goSlot == null || renderer?.sharedMesh == null || _clothBodyRendererField == null)
                return null;
            foreach (var skin in maid.body0.goSlot)
            {
                var morph = skin?.morph;
                if (morph?.m_nSubMeshOriTri == null || morph.m_vOriVert == null ||
                    morph.m_vOriVert.Length != renderer.sharedMesh.vertexCount ||
                    !ReferenceEquals(_clothBodyRendererField.GetValue(morph), renderer)) continue;
                var triangles = new List<int>();
                foreach (var submesh in morph.m_nSubMeshOriTri)
                {
                    if (submesh == null || submesh.Length % 3 != 0) return null;
                    foreach (int index in submesh)
                    {
                        if (index < 0 || index >= renderer.sharedMesh.vertexCount) return null;
                        triangles.Add(index);
                    }
                }
                return triangles.Count > 0 ? triangles.ToArray() : null;
            }
            return null;
        }
        private class BodyAnchorContext
        {
            public readonly List<BodyAnchorMesh> Meshes = new();
            public readonly List<BodyAnchorPoint> Points = new();
            public readonly Dictionary<int, List<int>> Buckets = new();
            public readonly List<BodySurfaceTriangle> SurfaceTriangles = new();
            public readonly Dictionary<int, List<int>> SurfaceBuckets = new();
            public readonly ClothingRestSurface ClothingRest = new();
            public float CellSize;
            public float MinMovedSq;
            public int BasisDirect;
            public int BasisSecondOrder;
            public int BasisFailed;
            public int ClothQueries;
            public int ClothHits;
            public int ClothMisses;
            public int ClothRejected;
        }

        private class BodyAnchorMesh
        {
            public MeshRecord Source;
            public Vector3[] Original;
            public Vector3[] Morphed;
            public bool[] Valid;
            public bool[] Affected;
            public bool[] BreastExcluded;
            public float[] AttachmentWeights;
            public BodyAnchorBasis[] Bases;
            public List<int>[] SurfaceTrianglesByVertex;
        }

        private struct BodyAnchorPoint
        {
            public int MeshIndex;
            public int VertexIndex;
            public Vector3 Original;
            public float OriginalUp;
        }

        private struct BodyAnchorBasis
        {
            public bool Valid;
            public int Neighbor1;
            public int Neighbor2;
            public Matrix4x4 OriginalInverse;
        }

        private struct BodySurfaceTriangle
        {
            public int MeshIndex;
            public int A;
            public int B;
            public int C;
            public Vector3 Center;
            public float RadiusSq;
        }

        private struct BodySurfaceHit
        {
            public int TriangleIndex;
            public Vector3 Closest;
            public Vector3 Barycentric;
            public Vector3 Normal;
            public float DistanceSq;
        }

        private static BodyAnchorContext CreateBodyAnchorContext(LocalFrame fr)
        {
            float cell = Mathf.Max(fr.BoneLen * 0.25f, 0.005f);
            float minMoved = Mathf.Max(fr.BoneLen * 0.0001f, 0.000001f);
            return new BodyAnchorContext
            {
                CellSize = cell,
                MinMovedSq = minMoved * minMoved,
            };
        }

        private static void FinalizeBodyAnchorContext(BodyAnchorContext context)
        {
            if (context == null || context.Points.Count == 0) return;

            context.Points.Sort((a, b) => b.OriginalUp.CompareTo(a.OriginalUp));
            context.Buckets.Clear();
            for (int i = 0; i < context.Points.Count; i++)
            {
                var point = context.Points[i];
                BodyAnchorBucketCoords(point.Original, context.CellSize, out int bx, out int by, out int bz);
                int key = BodyAnchorBucketKey(bx, by, bz);
                if (!context.Buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    context.Buckets[key] = list;
                }
                list.Add(i);
            }
        }

        private static void AddBodyAnchorMesh(
            BodyAnchorContext context,
            SkinnedMeshRenderer smr,
            MeshRecord rec,
            bool[] mask,
            Vector3[] morphedVerts,
            LocalFrame fr,
            int[] contactTriangles = null)
        {
            if (context == null || smr == null || smr.sharedMesh == null || rec == null) return;
            if (rec.OrigVerts == null || morphedVerts == null) return;

            Mesh mesh = smr.sharedMesh;
            int count = rec.OrigVerts.Length;
            if (morphedVerts.Length != count) return;

            var contactNeighbors = rec.Neighbors;
            if (contactTriangles != null)
            {
                contactNeighbors = new List<int>[count];
                for (int i = 0; i < count; i++) contactNeighbors[i] = new List<int>();
                for (int i = 0; i + 2 < contactTriangles.Length; i += 3)
                {
                    int a = contactTriangles[i], b = contactTriangles[i + 1], c = contactTriangles[i + 2];
                    if (a == b || b == c || c == a) continue;
                    AddNeighbor(contactNeighbors, a, b); AddNeighbor(contactNeighbors, b, a);
                    AddNeighbor(contactNeighbors, a, c); AddNeighbor(contactNeighbors, c, a);
                    AddNeighbor(contactNeighbors, b, c); AddNeighbor(contactNeighbors, c, b);
                }
            }
            else if (contactNeighbors == null) contactNeighbors = BuildMeshNeighbors(mesh, count);
            if (contactNeighbors == null) return;

            var anchorMesh = new BodyAnchorMesh
            {
                Source = rec,
                Original = new Vector3[count],
                Morphed = new Vector3[count],
                Valid = new bool[count],
                Affected = new bool[count],
                BreastExcluded = rec.BreastExcluded,
                AttachmentWeights = new float[count],
                Bases = new BodyAnchorBasis[count],
                SurfaceTrianglesByVertex = new List<int>[count],
            };

            var meshToReference = MatrixBridge.ToUnity(rec.ToReference);
            for (int i = 0; i < count; i++)
            {
                anchorMesh.Original[i] = meshToReference.MultiplyPoint3x4(rec.OrigVerts[i]);
                anchorMesh.Morphed[i] = meshToReference.MultiplyPoint3x4(morphedVerts[i]);
                anchorMesh.AttachmentWeights[i] = Attachment(rec, i, rec.GrowthContext.Stage);
                anchorMesh.Valid[i] = true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!anchorMesh.Valid[i]) continue;
                if ((anchorMesh.Morphed[i] - anchorMesh.Original[i]).sqrMagnitude < context.MinMovedSq) continue;
                anchorMesh.Affected[i] = true;
            }

            int meshIndex = context.Meshes.Count;
            int added = 0;
            int affected = 0;
            for (int i = 0; i < count; i++)
            {
                if (!anchorMesh.Valid[i]) continue;
                if (anchorMesh.Affected[i]) affected++;
                if (!TryBuildBodyAnchorBasis(context, anchorMesh, contactNeighbors, i, out BodyAnchorBasis basis))
                {
                    context.BasisFailed++;
                    continue;
                }

                anchorMesh.Bases[i] = basis;
                context.Points.Add(new BodyAnchorPoint
                {
                    MeshIndex = meshIndex,
                    VertexIndex = i,
                    Original = anchorMesh.Original[i],
                    OriginalUp = Vector3.Dot(anchorMesh.Original[i] - fr.Center, fr.Up),
                });
                added++;
            }

            int surfaceAdded = 0;
            if (added > 0)
            {
                context.Meshes.Add(anchorMesh);
                surfaceAdded = AddBodySurfaceTriangles(context, mesh, anchorMesh, meshIndex, contactTriangles);
            }

            RuntimeLogInfo($"[VtxMorph] BodyAnchor: anchors={added}/{count} affected={affected} surfaceTri={surfaceAdded} direct={context.BasisDirect} second={context.BasisSecondOrder} fail={context.BasisFailed}");
        }

        private static int AddBodySurfaceTriangles(
            BodyAnchorContext context,
            Mesh mesh,
            BodyAnchorMesh anchorMesh,
            int meshIndex,
            int[] contactTriangles = null)
        {
            if (context == null || mesh == null || anchorMesh == null) return 0;
            if (anchorMesh.Original == null || anchorMesh.Morphed == null || anchorMesh.Valid == null || anchorMesh.Affected == null)
                return 0;

            int[] triangles;
            try { triangles = contactTriangles ?? mesh.triangles; }
            catch { return 0; }
            if (triangles == null || triangles.Length < 3)
                return 0;

            float minEdge = Mathf.Max(context.CellSize * 0.015f, 0.00001f);
            float minArea = minEdge * minEdge;
            float minAreaSq = minArea * minArea;
            int count = anchorMesh.Original.Length;
            int added = 0;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (!IsValidSurfaceTriangleVertex(anchorMesh, a, count)
                    || !IsValidSurfaceTriangleVertex(anchorMesh, b, count)
                    || !IsValidSurfaceTriangleVertex(anchorMesh, c, count))
                    continue;

                Vector3 p0 = anchorMesh.Original[a];
                Vector3 p1 = anchorMesh.Original[b];
                Vector3 p2 = anchorMesh.Original[c];
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                if (!IsFinite(normal) || normal.sqrMagnitude < minAreaSq)
                    continue;

                // Clothing breast ownership needs the unchanged chest too.
                // Keep the existing moved-surface contact index unchanged.
                if (Shape.BreastExclusionEnabled)
                    AddClothingRestTriangle(context, meshIndex, a, b, c, p0, p1, p2);
                if (!anchorMesh.Affected[a] && !anchorMesh.Affected[b] && !anchorMesh.Affected[c])
                    continue;

                Vector3 center = (p0 + p1 + p2) / 3f;
                float radiusSq = (p0 - center).sqrMagnitude;
                radiusSq = Mathf.Max(radiusSq, (p1 - center).sqrMagnitude);
                radiusSq = Mathf.Max(radiusSq, (p2 - center).sqrMagnitude);

                int triIndex = context.SurfaceTriangles.Count;
                context.SurfaceTriangles.Add(new BodySurfaceTriangle
                {
                    MeshIndex = meshIndex,
                    A = a,
                    B = b,
                    C = c,
                    Center = center,
                    RadiusSq = radiusSq,
                });
                AddBodySurfaceTriangleForVertex(anchorMesh, a, triIndex);
                AddBodySurfaceTriangleForVertex(anchorMesh, b, triIndex);
                AddBodySurfaceTriangleForVertex(anchorMesh, c, triIndex);
                AddBodySurfaceTriangleToBuckets(context, triIndex, p0, p1, p2);
                added++;
            }

            return added;
        }

        private static void AddBodySurfaceTriangleForVertex(BodyAnchorMesh mesh, int vertexIndex, int triIndex)
        {
            if (mesh?.SurfaceTrianglesByVertex == null) return;
            if (vertexIndex < 0 || vertexIndex >= mesh.SurfaceTrianglesByVertex.Length) return;
            var list = mesh.SurfaceTrianglesByVertex[vertexIndex];
            if (list == null)
            {
                list = new List<int>(6);
                mesh.SurfaceTrianglesByVertex[vertexIndex] = list;
            }
            list.Add(triIndex);
        }

        private static bool IsValidSurfaceTriangleVertex(BodyAnchorMesh mesh, int index, int count)
        {
            return index >= 0
                && index < count
                && mesh.Valid[index]
                && IsFinite(mesh.Original[index])
                && IsFinite(mesh.Morphed[index]);
        }

        private static void AddBodySurfaceTriangleToBuckets(
            BodyAnchorContext context,
            int triIndex,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Dictionary<int, List<int>> buckets = null)
        {
            if (context == null) return;
            buckets = buckets ?? context.SurfaceBuckets;

            float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x));
            float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y));
            float minZ = Mathf.Min(p0.z, Mathf.Min(p1.z, p2.z));
            float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x));
            float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y));
            float maxZ = Mathf.Max(p0.z, Mathf.Max(p1.z, p2.z));

            BodyAnchorBucketCoords(new Vector3(minX, minY, minZ), context.CellSize, out int minBx, out int minBy, out int minBz);
            BodyAnchorBucketCoords(new Vector3(maxX, maxY, maxZ), context.CellSize, out int maxBx, out int maxBy, out int maxBz);

            for (int x = minBx; x <= maxBx; x++)
                for (int y = minBy; y <= maxBy; y++)
                    for (int z = minBz; z <= maxBz; z++)
                    {
                        int key = BodyAnchorBucketKey(x, y, z);
                        if (!buckets.TryGetValue(key, out var list))
                        {
                            list = new List<int>(4);
                            buckets[key] = list;
                        }
                        list.Add(triIndex);
                    }
        }

        private static bool TryBuildBodyAnchorBasis(
            BodyAnchorContext context,
            BodyAnchorMesh mesh,
            List<int>[] neighbors,
            int index,
            out BodyAnchorBasis basis)
        {
            basis = new BodyAnchorBasis();
            if (context == null || mesh == null || mesh.Original == null || mesh.Morphed == null || mesh.Valid == null)
                return false;
            if (neighbors == null || index < 0 || index >= neighbors.Length || !mesh.Valid[index])
                return false;

            List<int> ns = neighbors[index];
            if (ns == null) return false;

            Vector3 p0 = mesh.Original[index];
            Vector3 p0m = mesh.Morphed[index];
            float minEdge = Mathf.Max(context.CellSize * 0.015f, 0.00001f);
            float minArea = minEdge * minEdge;
            var candidates = new List<int>();
            var seen = new HashSet<int>();

            AddAffectedAnchorCandidates(mesh, ns, index, candidates, seen);
            if (candidates.Count >= 2
                && TryCreateRandomAnchorBasisFromCandidates(mesh, index, candidates, p0, p0m, minEdge, minArea, out basis))
            {
                context.BasisDirect++;
                return true;
            }

            for (int n = 0; n < ns.Count; n++)
            {
                int neighbor = ns[n];
                if (neighbor < 0 || neighbor >= neighbors.Length) continue;
                AddAffectedAnchorCandidates(mesh, neighbors[neighbor], index, candidates, seen);
            }

            if (candidates.Count >= 2
                && TryCreateRandomAnchorBasisFromCandidates(mesh, index, candidates, p0, p0m, minEdge, minArea, out basis))
            {
                context.BasisSecondOrder++;
                return true;
            }

            return false;
        }

        private static bool TryCreateRandomAnchorBasisFromCandidates(
            BodyAnchorMesh mesh,
            int index,
            List<int> candidates,
            Vector3 p0,
            Vector3 p0m,
            float minEdge,
            float minArea,
            out BodyAnchorBasis basis)
        {
            basis = new BodyAnchorBasis();
            if (candidates == null || candidates.Count < 2)
                return false;

            int pairCount = candidates.Count * candidates.Count;
            for (int attempt = 0; attempt < pairCount; attempt++)
            {
                int n1 = candidates[PositiveMod(StableAnchorHash(index, candidates.Count, attempt, 0), candidates.Count)];
                int n2 = candidates[PositiveMod(StableAnchorHash(index, candidates.Count, attempt, 1), candidates.Count)];
                if (n1 == n2) continue;

                if (TryCreateBodyAnchorBasisFromPair(mesh, index, n1, n2, p0, p0m, minEdge, minArea, out basis))
                    return true;
            }

            return false;
        }

        private static void AddAffectedAnchorCandidates(
            BodyAnchorMesh mesh,
            List<int> source,
            int origin,
            List<int> candidates,
            HashSet<int> seen)
        {
            if (mesh == null || mesh.Valid == null || source == null || candidates == null || seen == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                int candidate = source[i];
                if (candidate == origin) continue;
                if (candidate < 0 || candidate >= mesh.Valid.Length) continue;
                if (!mesh.Valid[candidate]) continue;
                if (seen.Add(candidate))
                    candidates.Add(candidate);
            }
        }

        private static bool TryCreateBodyAnchorBasisFromPair(
            BodyAnchorMesh mesh,
            int index,
            int n1,
            int n2,
            Vector3 p0,
            Vector3 p0m,
            float minEdge,
            float minArea,
            out BodyAnchorBasis basis)
        {
            basis = new BodyAnchorBasis();
            if (mesh == null || mesh.Original == null || mesh.Morphed == null) return false;
            if (index < 0 || index >= mesh.Original.Length || n1 < 0 || n1 >= mesh.Original.Length || n2 < 0 || n2 >= mesh.Original.Length)
                return false;

            Vector3 e1 = mesh.Original[n1] - p0;
            Vector3 e2 = mesh.Original[n2] - p0;
            Vector3 e1m = mesh.Morphed[n1] - p0m;
            Vector3 e2m = mesh.Morphed[n2] - p0m;
            float e1Len = e1.magnitude;
            float e2Len = e2.magnitude;
            float e1mLen = e1m.magnitude;
            float e2mLen = e2m.magnitude;
            if (e1Len < minEdge || e2Len < minEdge || e1mLen < minEdge || e2mLen < minEdge)
                return false;

            Vector3 normal = Vector3.Cross(e1, e2);
            Vector3 normalM = Vector3.Cross(e1m, e2m);
            if (normal.magnitude < minArea || normalM.magnitude < minArea)
                return false;
            float sin = normal.magnitude / Mathf.Max(e1Len * e2Len, 1e-9f);
            float sinM = normalM.magnitude / Mathf.Max(e1mLen * e2mLen, 1e-9f);
            if (sin < 0.12f || sinM < 0.12f)
                return false;

            float stretch1 = e1mLen / Mathf.Max(e1Len, 1e-9f);
            float stretch2 = e2mLen / Mathf.Max(e2Len, 1e-9f);
            if (stretch1 < 0.20f || stretch1 > 5.00f || stretch2 < 0.20f || stretch2 > 5.00f)
                return false;
            if (Vector3.Dot(normal.normalized, normalM.normalized) < -0.20f)
                return false;

            basis.Valid = true;
            basis.Neighbor1 = n1;
            basis.Neighbor2 = n2;
            basis.OriginalInverse = BuildBasisMatrix(e1, e2, normal).inverse;
            return true;
        }

        private static bool TryGetBodySurfaceClothTarget(
            BodyAnchorContext context,
            Vector3 original,
            float clothMultiplier,
            LocalFrame fr,
            out Vector3 target)
        {
            target = original;
            if (context == null || context.Meshes.Count == 0 || context.SurfaceTriangles.Count == 0)
                return false;

            context.ClothQueries++;
            if (!TryFindNearestBodySurfaceTriangle(context, original, out BodySurfaceHit hit))
                return BodyAnchorMiss(context);

            float maxDistance = Mathf.Max(fr.BoneLen * 4.0f, 0.035f);
            if (hit.DistanceSq > maxDistance * maxDistance)
                return BodyAnchorMiss(context);

            if (hit.TriangleIndex < 0 || hit.TriangleIndex >= context.SurfaceTriangles.Count)
                return BodyAnchorMiss(context);

            BodySurfaceTriangle tri = context.SurfaceTriangles[hit.TriangleIndex];
            if (tri.MeshIndex < 0 || tri.MeshIndex >= context.Meshes.Count)
                return BodyAnchorMiss(context);

            BodyAnchorMesh mesh = context.Meshes[tri.MeshIndex];
            if (mesh == null || mesh.Original == null || mesh.Morphed == null)
                return BodyAnchorMiss(context);
            if (!IsValidBodyAnchorVertex(mesh, tri.A)
                || !IsValidBodyAnchorVertex(mesh, tri.B)
                || !IsValidBodyAnchorVertex(mesh, tri.C))
                return BodyAnchorMiss(context);

            float mul = clothMultiplier;
            Vector3 a = mesh.Original[tri.A];
            Vector3 b = mesh.Original[tri.B];
            Vector3 c = mesh.Original[tri.C];
            Vector3 am = ScaleAnchorMove(a, mesh.Morphed[tri.A], mul);
            Vector3 bm = ScaleAnchorMove(b, mesh.Morphed[tri.B], mul);
            Vector3 cm = ScaleAnchorMove(c, mesh.Morphed[tri.C], mul);

            Vector3 normalM = Vector3.Cross(bm - am, cm - am);
            float normalMLen = normalM.magnitude;
            if (normalMLen < 1e-9f || !IsFinite(normalM))
                return BodyAnchorMiss(context);
            normalM /= normalMLen;

            if (Vector3.Dot(hit.Normal, normalM) < -0.25f)
                return BodyAnchorMiss(context);

            Vector3 surfaceM =
                am * hit.Barycentric.x +
                bm * hit.Barycentric.y +
                cm * hit.Barycentric.z;
            var offset = original - hit.Closest;
            var rotated = SurfaceAttachment.RotateOffset(
                new NVector(offset.x, offset.y, offset.z),
                new NVector(hit.Normal.x, hit.Normal.y, hit.Normal.z),
                new NVector(normalM.x, normalM.y, normalM.z));
            target = surfaceM + new Vector3(rotated.X, rotated.Y, rotated.Z);
            float distanceFade = 1f - BellyShape.Smooth((Mathf.Sqrt(hit.DistanceSq) / maxDistance - 0.75f) / 0.25f);
            target = Vector3.Lerp(original, target, distanceFade);
            if (!IsFinite(target))
                return BodyAnchorMiss(context);

            context.ClothHits++;
            return true;
        }

        private static bool TryFindNearestBodySurfaceTriangle(
            BodyAnchorContext context,
            Vector3 point,
            out BodySurfaceHit nearest)
        {
            nearest = new BodySurfaceHit { TriangleIndex = -1 };
            if (context == null || context.SurfaceTriangles.Count == 0)
                return false;

            bool found = false;
            float bestSq = float.MaxValue;

            if (!TryFindNearestBodyAnchorPoint(context, point, true, out BodyAnchorPoint anchor))
                return false;

            return TestBodySurfaceTrianglesForVertex(context, point, anchor, ref found, ref bestSq, ref nearest);
        }

        private static bool TryFindNearestBodySurfaceTriangleBroad(
            BodyAnchorContext context,
            Vector3 point,
            out BodySurfaceHit nearest)
        {
            nearest = new BodySurfaceHit { TriangleIndex = -1 };
            if (context == null || context.SurfaceTriangles.Count == 0)
                return false;

            bool found = false;
            float bestSq = float.MaxValue;
            var seen = new HashSet<int>();

            if (context.SurfaceBuckets != null && context.SurfaceBuckets.Count > 0)
            {
                BodyAnchorBucketCoords(point, context.CellSize, out int bx, out int by, out int bz);
                for (int dx = -4; dx <= 4; dx++)
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dz = -4; dz <= 4; dz++)
                        {
                            int key = BodyAnchorBucketKey(bx + dx, by + dy, bz + dz);
                            if (!context.SurfaceBuckets.TryGetValue(key, out var list)) continue;
                            for (int i = 0; i < list.Count; i++)
                            {
                                int triIndex = list[i];
                                if (!seen.Add(triIndex)) continue;
                                TestBodySurfaceTriangle(context, point, triIndex, ref found, ref bestSq, ref nearest);
                            }
                        }
                if (found) return true;
            }

            if (TryFindNearestBodyAnchorPoint(context, point, false, out BodyAnchorPoint anchor))
            {
                TestBodySurfaceTrianglesForVertex(context, point, anchor, ref found, ref bestSq, ref nearest);
                if (found) return true;
            }

            for (int i = 0; i < context.SurfaceTriangles.Count; i++)
                TestBodySurfaceTriangle(context, point, i, ref found, ref bestSq, ref nearest);
            return found;
        }

        private static bool TestBodySurfaceTrianglesForVertex(
            BodyAnchorContext context,
            Vector3 point,
            BodyAnchorPoint anchor,
            ref bool found,
            ref float bestSq,
            ref BodySurfaceHit nearest)
        {
            if (context == null || anchor.MeshIndex < 0 || anchor.MeshIndex >= context.Meshes.Count)
                return false;
            BodyAnchorMesh mesh = context.Meshes[anchor.MeshIndex];
            if (mesh?.SurfaceTrianglesByVertex == null)
                return false;
            if (anchor.VertexIndex < 0 || anchor.VertexIndex >= mesh.SurfaceTrianglesByVertex.Length)
                return false;

            List<int> triangles = mesh.SurfaceTrianglesByVertex[anchor.VertexIndex];
            if (triangles == null || triangles.Count == 0)
                return false;

            for (int i = 0; i < triangles.Count; i++)
                TestBodySurfaceTriangle(context, point, triangles[i], ref found, ref bestSq, ref nearest);
            return found;
        }

        private static void TestBodySurfaceTriangle(
            BodyAnchorContext context,
            Vector3 point,
            int triangleIndex,
            ref bool found,
            ref float bestSq,
            ref BodySurfaceHit nearest)
        {
            if (context == null || triangleIndex < 0 || triangleIndex >= context.SurfaceTriangles.Count)
                return;

            BodySurfaceTriangle tri = context.SurfaceTriangles[triangleIndex];
            if (tri.MeshIndex < 0 || tri.MeshIndex >= context.Meshes.Count)
                return;

            BodyAnchorMesh mesh = context.Meshes[tri.MeshIndex];
            if (mesh == null || mesh.Original == null)
                return;
            if (!IsValidBodyAnchorVertex(mesh, tri.A)
                || !IsValidBodyAnchorVertex(mesh, tri.B)
                || !IsValidBodyAnchorVertex(mesh, tri.C))
                return;

            Vector3 a = mesh.Original[tri.A];
            Vector3 b = mesh.Original[tri.B];
            Vector3 c = mesh.Original[tri.C];
            if (!TryClosestPointOnTriangle(point, a, b, c, out Vector3 closest, out Vector3 barycentric))
                return;

            float sq = (point - closest).sqrMagnitude;
            if (sq >= bestSq)
                return;

            Vector3 normal = Vector3.Cross(b - a, c - a);
            float normalLen = normal.magnitude;
            if (normalLen < 1e-9f || !IsFinite(normal))
                return;
            normal /= normalLen;

            found = true;
            bestSq = sq;
            nearest = new BodySurfaceHit
            {
                TriangleIndex = triangleIndex,
                Closest = closest,
                Barycentric = barycentric,
                Normal = normal,
                DistanceSq = sq,
            };
        }

        private static bool TryClosestPointOnTriangle(
            Vector3 point,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out Vector3 closest,
            out Vector3 barycentric)
        {
            closest = a;
            barycentric = new Vector3(1f, 0f, 0f);

            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = point - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return IsFinite(closest);

            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                closest = b;
                barycentric = new Vector3(0f, 1f, 0f);
                return IsFinite(closest);
            }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float denom = d1 - d3;
                if (Mathf.Abs(denom) < 1e-9f) return false;
                float v = d1 / denom;
                closest = a + ab * v;
                barycentric = new Vector3(1f - v, v, 0f);
                return IsFinite(closest);
            }

            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                closest = c;
                barycentric = new Vector3(0f, 0f, 1f);
                return IsFinite(closest);
            }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float denom = d2 - d6;
                if (Mathf.Abs(denom) < 1e-9f) return false;
                float w = d2 / denom;
                closest = a + ac * w;
                barycentric = new Vector3(1f - w, 0f, w);
                return IsFinite(closest);
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float denom = (d4 - d3) + (d5 - d6);
                if (Mathf.Abs(denom) < 1e-9f) return false;
                float w = (d4 - d3) / denom;
                closest = b + (c - b) * w;
                barycentric = new Vector3(0f, 1f - w, w);
                return IsFinite(closest);
            }

            float sum = va + vb + vc;
            if (Mathf.Abs(sum) < 1e-9f)
                return false;

            float inv = 1f / sum;
            float vFace = vb * inv;
            float wFace = vc * inv;
            float uFace = 1f - vFace - wFace;
            closest = a * uFace + b * vFace + c * wFace;
            barycentric = new Vector3(uFace, vFace, wFace);
            return IsFinite(closest) && IsFinite(barycentric);
        }

        private static bool TryGetBodyAnchorClothTarget(
            BodyAnchorContext context,
            Vector3 original,
            float clothMultiplier,
            out Vector3 target)
        {
            target = original;
            if (context == null || context.Points.Count == 0 || context.Meshes.Count == 0)
                return false;
            context.ClothQueries++;
            if (!TryFindNearestBodyAnchorPoint(context, original, out BodyAnchorPoint point))
                return BodyAnchorMiss(context);
            if (point.MeshIndex < 0 || point.MeshIndex >= context.Meshes.Count)
                return BodyAnchorMiss(context);

            BodyAnchorMesh mesh = context.Meshes[point.MeshIndex];
            int p0Index = point.VertexIndex;
            if (mesh == null || mesh.Bases == null || p0Index < 0 || p0Index >= mesh.Bases.Length)
                return BodyAnchorMiss(context);

            BodyAnchorBasis basis = mesh.Bases[p0Index];
            if (!basis.Valid) return BodyAnchorMiss(context);
            if (!IsValidBodyAnchorVertex(mesh, p0Index)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor1)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor2))
                return BodyAnchorMiss(context);

            float mul = clothMultiplier;
            Vector3 p0 = mesh.Original[p0Index];
            Vector3 p0m = ScaleAnchorMove(mesh.Original[p0Index], mesh.Morphed[p0Index], mul);
            Vector3 originalOffset = original - p0;
            float distance = originalOffset.magnitude;
            if (distance < 1e-7f)
            {
                target = p0m;
                if (!IsFinite(target)) return BodyAnchorMiss(context);
                context.ClothHits++;
                return true;
            }

            Vector3 local = basis.OriginalInverse.MultiplyVector(originalOffset);
            Vector3 n1m = ScaleAnchorMove(mesh.Original[basis.Neighbor1], mesh.Morphed[basis.Neighbor1], mul);
            Vector3 n2m = ScaleAnchorMove(mesh.Original[basis.Neighbor2], mesh.Morphed[basis.Neighbor2], mul);
            Vector3 e1m = n1m - p0m;
            Vector3 e2m = n2m - p0m;
            Vector3 normalM = Vector3.Cross(e1m, e2m);
            Vector3 transformed = BuildBasisMatrix(e1m, e2m, normalM).MultiplyVector(local);
            if (transformed.sqrMagnitude < 1e-10f || !IsFinite(transformed))
                transformed = originalOffset;
            if (transformed.sqrMagnitude < 1e-10f || !IsFinite(transformed))
                return BodyAnchorMiss(context);

            target = p0m + transformed.normalized * distance;
            if (!IsFinite(target)) return BodyAnchorMiss(context);

            context.ClothHits++;
            return true;
        }

        private static bool TryGetBodyAnchorRigidTarget(
            BodyAnchorContext context,
            Vector3 original,
            bool requireAffected,
            out Vector3 target,
            out Quaternion rotationDelta)
        {
            target = original;
            rotationDelta = Quaternion.identity;
            if (context == null || context.Points.Count == 0 || context.Meshes.Count == 0)
                return false;
            context.ClothQueries++;

            if (!TryFindNearestBodyAnchorPoint(context, original, requireAffected, out BodyAnchorPoint point))
                return BodyAnchorMiss(context);
            if (point.MeshIndex < 0 || point.MeshIndex >= context.Meshes.Count)
                return BodyAnchorMiss(context);

            BodyAnchorMesh mesh = context.Meshes[point.MeshIndex];
            int p0Index = point.VertexIndex;
            if (mesh == null || mesh.Bases == null || p0Index < 0 || p0Index >= mesh.Bases.Length)
                return BodyAnchorMiss(context);
            if (requireAffected && (mesh.Affected == null || !mesh.Affected[p0Index]))
                return BodyAnchorMiss(context);

            BodyAnchorBasis basis = mesh.Bases[p0Index];
            if (!basis.Valid) return BodyAnchorMiss(context);
            if (!IsValidBodyAnchorVertex(mesh, p0Index)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor1)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor2))
                return BodyAnchorMiss(context);

            if (!TryTransformBodyAnchorPoint(mesh, basis, p0Index, original, 1f, out target, out rotationDelta))
                return BodyAnchorMiss(context);

            context.ClothHits++;
            return true;
        }

        private static bool TryTransformBodyAnchorPoint(
            BodyAnchorMesh mesh,
            BodyAnchorBasis basis,
            int p0Index,
            Vector3 original,
            float multiplier,
            out Vector3 target,
            out Quaternion rotationDelta)
        {
            target = original;
            rotationDelta = Quaternion.identity;
            if (mesh == null || mesh.Original == null || mesh.Morphed == null)
                return false;
            if (p0Index < 0 || p0Index >= mesh.Original.Length)
                return false;
            if (!IsValidBodyAnchorVertex(mesh, p0Index)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor1)
                || !IsValidBodyAnchorVertex(mesh, basis.Neighbor2))
                return false;

            float mul = multiplier;
            Vector3 p0 = mesh.Original[p0Index];
            Vector3 p0m = ScaleAnchorMove(mesh.Original[p0Index], mesh.Morphed[p0Index], mul);
            Vector3 originalOffset = original - p0;
            float distance = originalOffset.magnitude;

            if (distance < 1e-7f)
            {
                target = p0m;
                TryCreateBodyAnchorRotationDelta(mesh, basis, p0Index, mul, out rotationDelta);
                return IsFinite(target);
            }

            Vector3 local = basis.OriginalInverse.MultiplyVector(originalOffset);
            Vector3 n1m = ScaleAnchorMove(mesh.Original[basis.Neighbor1], mesh.Morphed[basis.Neighbor1], mul);
            Vector3 n2m = ScaleAnchorMove(mesh.Original[basis.Neighbor2], mesh.Morphed[basis.Neighbor2], mul);
            Vector3 e1m = n1m - p0m;
            Vector3 e2m = n2m - p0m;
            Vector3 normalM = Vector3.Cross(e1m, e2m);
            Vector3 transformed = BuildBasisMatrix(e1m, e2m, normalM).MultiplyVector(local);
            if (transformed.sqrMagnitude < 1e-10f || !IsFinite(transformed))
                transformed = originalOffset;
            if (transformed.sqrMagnitude < 1e-10f || !IsFinite(transformed))
                return false;

            target = p0m + transformed.normalized * distance;
            if (!IsFinite(target)) return false;
            TryCreateBodyAnchorRotationDelta(mesh, basis, p0Index, mul, out rotationDelta);
            return true;
        }

        private static bool TryCreateBodyAnchorRotationDelta(
            BodyAnchorMesh mesh,
            BodyAnchorBasis basis,
            int p0Index,
            float multiplier,
            out Quaternion rotationDelta)
        {
            rotationDelta = Quaternion.identity;
            try
            {
                Vector3 p0 = mesh.Original[p0Index];
                Vector3 p0m = ScaleAnchorMove(mesh.Original[p0Index], mesh.Morphed[p0Index], multiplier);
                Vector3 e1 = mesh.Original[basis.Neighbor1] - p0;
                Vector3 e2 = mesh.Original[basis.Neighbor2] - p0;
                Vector3 e1m = ScaleAnchorMove(mesh.Original[basis.Neighbor1], mesh.Morphed[basis.Neighbor1], multiplier) - p0m;
                Vector3 e2m = ScaleAnchorMove(mesh.Original[basis.Neighbor2], mesh.Morphed[basis.Neighbor2], multiplier) - p0m;

                if (!TryBuildAnchorRotation(e1, e2, out Quaternion oldRot)) return false;
                if (!TryBuildAnchorRotation(e1m, e2m, out Quaternion newRot)) return false;
                rotationDelta = newRot * Quaternion.Inverse(oldRot);
                if (!IsFinite(rotationDelta))
                {
                    rotationDelta = Quaternion.identity;
                    return false;
                }
                return true;
            }
            catch
            {
                rotationDelta = Quaternion.identity;
                return false;
            }
        }

        private static bool TryBuildAnchorRotation(Vector3 e1, Vector3 e2, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            Vector3 right = e1;
            if (right.sqrMagnitude < 1e-10f) return false;
            right.Normalize();

            Vector3 normal = Vector3.Cross(e1, e2);
            if (normal.sqrMagnitude < 1e-10f) return false;
            normal.Normalize();

            Vector3 up = Vector3.Cross(normal, right);
            if (up.sqrMagnitude < 1e-10f) return false;
            up.Normalize();

            rotation = Quaternion.LookRotation(normal, up);
            return IsFinite(rotation);
        }

        private static bool TryGetBodyAnchorLinearTarget(
            BodyAnchorContext context,
            Vector3 original,
            bool requireAffected,
            out Vector3 target)
        {
            target = original;
            if (context == null || context.Points.Count == 0 || context.Meshes.Count == 0)
                return false;
            if (!TryFindNearestBodyAnchorPoint(context, original, requireAffected, out BodyAnchorPoint point))
                return false;
            if (point.MeshIndex < 0 || point.MeshIndex >= context.Meshes.Count)
                return false;

            BodyAnchorMesh mesh = context.Meshes[point.MeshIndex];
            int index = point.VertexIndex;
            if (mesh == null || mesh.Original == null || mesh.Morphed == null || index < 0 || index >= mesh.Original.Length)
                return false;
            if (requireAffected && (mesh.Affected == null || !mesh.Affected[index]))
                return false;

            target = original + (mesh.Morphed[index] - mesh.Original[index]);
            return IsFinite(target);
        }

        private static bool AcceptBodyAnchorClothTarget(
            BodyAnchorContext context,
            Vector3 original,
            Vector3 baseTarget,
            Vector3 anchorTarget,
            LocalFrame fr)
        {
            if (!IsFinite(baseTarget) || !IsFinite(anchorTarget))
            {
                if (context != null) context.ClothRejected++;
                return false;
            }

            var p = Shape;
            float boneLen = Mathf.Max(fr.BoneLen, 1e-5f);
            float threshold = Mathf.Max(0.35f, p.ClothDistortThreshold);
            Vector3 baseDisp = baseTarget - original;
            Vector3 anchorDisp = anchorTarget - original;
            float baseMag = baseDisp.magnitude;
            float anchorMag = anchorDisp.magnitude;
            float delta = (anchorTarget - baseTarget).magnitude;

            float maxDelta = Mathf.Max(baseMag * 0.90f, boneLen * threshold * 0.65f);
            if (delta > maxDelta)
            {
                if (context != null) context.ClothRejected++;
                return false;
            }

            float maxMag = Mathf.Max(baseMag * 2.25f, baseMag + boneLen * threshold);
            if (anchorMag > maxMag)
            {
                if (context != null) context.ClothRejected++;
                return false;
            }

            if (baseMag > boneLen * 0.05f && anchorMag > boneLen * 0.05f)
            {
                float dir = Vector3.Dot(baseDisp / baseMag, anchorDisp / anchorMag);
                if (dir < -0.25f)
                {
                    if (context != null) context.ClothRejected++;
                    return false;
                }
            }

            return true;
        }

        private static bool BodyAnchorMiss(BodyAnchorContext context)
        {
            if (context != null) context.ClothMisses++;
            return false;
        }

        private static Vector3 ScaleAnchorMove(Vector3 original, Vector3 morphed, float multiplier)
        {
            return original + (morphed - original) * multiplier;
        }

        private static bool IsValidBodyAnchorVertex(BodyAnchorMesh mesh, int index)
        {
            return mesh != null
                && mesh.Valid != null
                && index >= 0
                && index < mesh.Valid.Length
                && mesh.Valid[index];
        }

        private static bool TryFindNearestBodyAnchorPoint(BodyAnchorContext context, Vector3 point, out BodyAnchorPoint nearest)
        {
            return TryFindNearestBodyAnchorPoint(context, point, false, out nearest);
        }

        private static bool TryFindNearestBodyAnchorPoint(BodyAnchorContext context, Vector3 point, bool requireAffected, out BodyAnchorPoint nearest)
        {
            nearest = new BodyAnchorPoint();
            if (context == null || context.Points.Count == 0)
                return false;

            BodyAnchorBucketCoords(point, context.CellSize, out int bx, out int by, out int bz);
            bool found = false;
            float bestSq = float.MaxValue;

            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        int key = BodyAnchorBucketKey(bx + dx, by + dy, bz + dz);
                        if (!context.Buckets.TryGetValue(key, out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                            TestBodyAnchorPoint(context, point, list[i], requireAffected, ref found, ref bestSq, ref nearest);
                    }

            if (found) return true;

            for (int i = 0; i < context.Points.Count; i++)
                TestBodyAnchorPoint(context, point, i, requireAffected, ref found, ref bestSq, ref nearest);
            return found;
        }

        private static void TestBodyAnchorPoint(
            BodyAnchorContext context,
            Vector3 point,
            int pointIndex,
            bool requireAffected,
            ref bool found,
            ref float bestSq,
            ref BodyAnchorPoint nearest)
        {
            if (context == null || pointIndex < 0 || pointIndex >= context.Points.Count) return;
            BodyAnchorPoint candidate = context.Points[pointIndex];
            if (requireAffected)
            {
                if (candidate.MeshIndex < 0 || candidate.MeshIndex >= context.Meshes.Count) return;
                BodyAnchorMesh mesh = context.Meshes[candidate.MeshIndex];
                if (mesh?.Affected == null || candidate.VertexIndex < 0 || candidate.VertexIndex >= mesh.Affected.Length) return;
                if (!mesh.Affected[candidate.VertexIndex]) return;
            }

            float sq = (candidate.Original - point).sqrMagnitude;
            if (sq >= bestSq) return;

            found = true;
            bestSq = sq;
            nearest = candidate;
        }

        private static Matrix4x4 BuildBasisMatrix(Vector3 e1, Vector3 e2, Vector3 normal)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(e1.x, e1.y, e1.z, 0f));
            matrix.SetColumn(1, new Vector4(e2.x, e2.y, e2.z, 0f));
            matrix.SetColumn(2, new Vector4(normal.x, normal.y, normal.z, 0f));
            matrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
            return matrix;
        }

        private static int StableAnchorHash(int index, int count, int attempt, int salt)
        {
            unchecked
            {
                int hash = 216613626;
                hash = (hash * 16777619) ^ index;
                hash = (hash * 16777619) ^ count;
                hash = (hash * 16777619) ^ attempt;
                hash = (hash * 16777619) ^ salt;
                return hash;
            }
        }

        private static int PositiveMod(int value, int mod)
        {
            if (mod <= 0) return 0;
            int result = value % mod;
            return result < 0 ? result + mod : result;
        }

        private static void BodyAnchorBucketCoords(Vector3 point, float cellSize, out int x, out int y, out int z)
        {
            float cell = Mathf.Max(cellSize, 0.0001f);
            x = Mathf.FloorToInt(point.x / cell);
            y = Mathf.FloorToInt(point.y / cell);
            z = Mathf.FloorToInt(point.z / cell);
        }

        private static int BodyAnchorBucketKey(int x, int y, int z)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(Quaternion q)
        {
            return IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        private static int RepairClothDistortion(
            Vector3[] original,
            Vector3[] morphed,
            bool[] moved,
            LocalFrame fr,
            List<int>[] topologyNeighbors)
        {
            try
            {
                var p = Shape;
                float detectThreshold = p.ClothDistortThreshold;
                float normalNeighborDiff = p.ClothDistortNeighborDiff;
                if (detectThreshold <= 0f || normalNeighborDiff < 0f) return 0;
                if (original == null || morphed == null || moved == null) return 0;
                int count = original.Length;
                if (morphed.Length != count || moved.Length != count) return 0;
                if (topologyNeighbors == null || topologyNeighbors.Length != count) return 0;

                float boneLen = Mathf.Max(fr.BoneLen, 1e-5f);
                float minMoveSq = boneLen * boneLen * 1e-8f;
                var movedIndices = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    if (!moved[i]) continue;
                    Vector3 disp = morphed[i] - original[i];
                    if (disp.sqrMagnitude <= minMoveSq) continue;

                    movedIndices.Add(i);
                }

                if (movedIndices.Count < 3) return 0;
                float radius = Mathf.Max(boneLen * 1.50f, 0.012f);
                float radiusSq = radius * radius;
                float cell = Mathf.Max(radius, 0.001f);
                var buckets = BuildSpatialBuckets(original, movedIndices, cell);
                if (buckets.Count == 0) return 0;

                var replacement = new Vector3[count];
                var shouldReplace = new bool[count];
                var neighbors = new List<int>(48);
                int fixedCount = 0;
                int checkedCount = 0;
                int noNeighborCount = 0;
                int noSampleCount = 0;
                float maxDiffNorm = 0f;
                float maxEdgeDiffNorm = 0f;

                for (int m = 0; m < movedIndices.Count; m++)
                {
                    int i = movedIndices[m];
                    List<int> topo = topologyNeighbors[i];
                    if (topo == null || topo.Count == 0)
                    {
                        noNeighborCount++;
                        continue;
                    }

                    Vector3 dispI = morphed[i] - original[i];
                    Vector3 topoSum = Vector3.zero;
                    int topoSamples = 0;
                    int tornEdges = 0;
                    float localMaxEdgeDiffNorm = 0f;

                    for (int t = 0; t < topo.Count; t++)
                    {
                        int j = topo[t];
                        if (j == i || j < 0 || j >= count || !moved[j]) continue;
                        Vector3 dispJ = morphed[j] - original[j];
                        if (dispJ.sqrMagnitude <= minMoveSq) continue;

                        float edgeDiffNorm = (dispI - dispJ).magnitude / boneLen;
                        if (edgeDiffNorm > localMaxEdgeDiffNorm)
                            localMaxEdgeDiffNorm = edgeDiffNorm;
                        if (edgeDiffNorm >= detectThreshold)
                            tornEdges++;

                        topoSum += dispJ;
                        topoSamples++;
                    }

                    if (localMaxEdgeDiffNorm > maxEdgeDiffNorm)
                        maxEdgeDiffNorm = localMaxEdgeDiffNorm;
                    if (topoSamples < 2 || tornEdges == 0)
                    {
                        if (topoSamples < 2) noNeighborCount++;
                        continue;
                    }

                    neighbors.Clear();
                    CollectSpatialNeighbors(original, buckets, cell, radiusSq, i, neighbors);
                    if (neighbors.Count == 0)
                    {
                        noNeighborCount++;
                        continue;
                    }

                    Vector3 localSum = topoSum;
                    int localSamples = topoSamples;
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int j = neighbors[n];
                        if (j == i || !moved[j]) continue;

                        Vector3 dispJ = morphed[j] - original[j];
                        if (dispJ.sqrMagnitude <= minMoveSq) continue;
                        localSum += dispJ;
                        localSamples++;
                    }

                    if (localSamples < 2)
                    {
                        noNeighborCount++;
                        continue;
                    }

                    Vector3 localAvg = localSum / localSamples;
                    float diffNorm = (dispI - localAvg).magnitude / boneLen;
                    if (diffNorm > maxDiffNorm)
                        maxDiffNorm = diffNorm;
                    checkedCount++;

                    if (diffNorm < detectThreshold) continue;

                    Vector3 sum = Vector3.zero;
                    int samples = 0;
                    for (int t = 0; t < topo.Count; t++)
                    {
                        int k = topo[t];
                        if (k == i || k < 0 || k >= count || !moved[k]) continue;

                        Vector3 kDisp = morphed[k] - original[k];
                        if (kDisp.sqrMagnitude <= minMoveSq) continue;

                        float sampleDiff = (kDisp - localAvg).magnitude / boneLen;
                        if (sampleDiff <= normalNeighborDiff || sampleDiff < diffNorm * 0.50f)
                        {
                            sum += kDisp;
                            samples++;
                        }
                    }

                    if (samples == 0)
                    {
                        for (int n = 0; n < neighbors.Count; n++)
                        {
                            int k = neighbors[n];
                            if (k == i || !moved[k]) continue;

                            Vector3 kDisp = morphed[k] - original[k];
                            if (kDisp.sqrMagnitude <= minMoveSq) continue;

                            float sampleDiff = (kDisp - localAvg).magnitude / boneLen;
                            if (sampleDiff <= normalNeighborDiff || sampleDiff < diffNorm * 0.50f)
                            {
                                sum += kDisp;
                                samples++;
                            }
                        }
                    }

                    Vector3 replacementDisp;
                    if (samples > 0)
                    {
                        replacementDisp = sum / samples;
                    }
                    else
                    {
                        noSampleCount++;
                        if (tornEdges < 2 || localMaxEdgeDiffNorm < detectThreshold * 2f)
                            continue;
                        replacementDisp = topoSum / topoSamples;
                    }

                    replacement[i] = original[i] + replacementDisp;
                    shouldReplace[i] = true;
                    fixedCount++;
                }

                if (checkedCount > 0 && (fixedCount > 0 || maxDiffNorm >= detectThreshold * 0.75f || maxEdgeDiffNorm >= detectThreshold))
                    RuntimeLogInfo($"[VtxMorph] ClothDistortFix: fixed={fixedCount}/{movedIndices.Count} checked={checkedCount} noNbr={noNeighborCount} noSample={noSampleCount} maxDiff={maxDiffNorm:F2} maxEdge={maxEdgeDiffNorm:F2} thr={detectThreshold:F2} near={normalNeighborDiff:F2}");

                if (fixedCount == 0) return 0;
                for (int i = 0; i < count; i++)
                    if (shouldReplace[i])
                        morphed[i] = replacement[i];
                return fixedCount;
            }
            catch (Exception e)
            {
                _log.LogWarning("[VtxMorph] ClothDistortFix: " + e.Message);
                return 0;
            }
        }

        private static Dictionary<int, List<int>> BuildSpatialBuckets(Vector3[] points, List<int> indices, float cellSize)
        {
            var buckets = new Dictionary<int, List<int>>();
            if (points == null || indices == null) return buckets;
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= points.Length) continue;
                SpatialBucketCoords(points[index], cellSize, out int x, out int y, out int z);
                int key = SpatialBucketKey(x, y, z);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>(8);
                    buckets[key] = list;
                }
                list.Add(index);
            }
            return buckets;
        }

        private static void CollectSpatialNeighbors(
            Vector3[] points,
            Dictionary<int, List<int>> buckets,
            float cellSize,
            float radiusSq,
            int centerIndex,
            List<int> result)
        {
            if (points == null || buckets == null || result == null) return;
            if (centerIndex < 0 || centerIndex >= points.Length) return;

            Vector3 center = points[centerIndex];
            SpatialBucketCoords(center, cellSize, out int bx, out int by, out int bz);
            int range = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(radiusSq) / Mathf.Max(cellSize, 1e-5f)));

            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                    for (int dz = -range; dz <= range; dz++)
                    {
                        int key = SpatialBucketKey(bx + dx, by + dy, bz + dz);
                        if (!buckets.TryGetValue(key, out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            int candidate = list[i];
                            if (candidate == centerIndex) continue;
                            if (candidate < 0 || candidate >= points.Length) continue;
                            if ((points[candidate] - center).sqrMagnitude <= radiusSq)
                                result.Add(candidate);
                        }
                    }
        }

        private static void SpatialBucketCoords(Vector3 point, float cellSize, out int x, out int y, out int z)
        {
            float cell = Mathf.Max(cellSize, 0.0001f);
            x = Mathf.FloorToInt(point.x / cell);
            y = Mathf.FloorToInt(point.y / cell);
            z = Mathf.FloorToInt(point.z / cell);
        }

        private static int SpatialBucketKey(int x, int y, int z)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        // ── Belly mask from bone weights (PP vertex-selection mechanism) ──

        private static int[] ComputeNormalWeldGroup(Vector3[] verts)
        {
            int n = verts.Length;
            var map = new Dictionary<WeldKey, int>(n);
            var group = new int[n];
            const float invEps = 1000f; // 1 mm precision
            for (int i = 0; i < n; i++)
            {
                Vector3 v = verts[i];
                var key = new WeldKey(Mathf.RoundToInt(v.x * invEps),
                           Mathf.RoundToInt(v.y * invEps),
                           Mathf.RoundToInt(v.z * invEps));
                if (!map.TryGetValue(key, out int rep))
                {
                    rep = i;
                    map[key] = i;
                }
                group[i] = rep;
            }
            return group;
        }

        private readonly record struct WeldKey(int X, int Y, int Z);

        /// <summary>
        /// Same algorithm as RecalculateNormalsWelded, but returns the normals array
        /// without writing to the mesh.  Used for offline diagnostic logging only.
        /// Returns null on any error.
        /// </summary>

    }
}
