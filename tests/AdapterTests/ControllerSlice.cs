using System.Collections.Generic;
using UnityEngine;
using COM3D2.Pregnancy.Plugin.Growth;
using NVector=COM3D2.Pregnancy.Plugin.Growth.Numerics.Vector3;
using NMatrix=COM3D2.Pregnancy.Plugin.Growth.Numerics.Matrix4x4;
namespace COM3D2.Pregnancy.Plugin
{
    public static partial class BellyMorphController
    {
        public static VtxSettings Shape=new();
        static Dictionary<int,List<MeshRecord>> _records=new();
        static Dictionary<TMorph,MorphBaseBakeState> _morphBaseBakeStates=new();
        static TestLog _log=new();
        class TestLog {public void LogInfo(string s){} public void LogWarning(string s)=>Console.WriteLine(s);}
        static bool IsDebugMeshLoggingEnabled()=>false;
        static bool RuntimeClassification;
        static MeshMorphClass ClassifyMesh(SkinnedMeshRenderer r)=>RuntimeClassification?RuntimeClassifyMesh(r):r.sharedMesh.name.Contains("accheso")?MeshMorphClass.NavelAccessory:r.sharedMesh.name.Contains("body")?MeshMorphClass.Body:r.sharedMesh.name.Contains("bra")?MeshMorphClass.InnerCloth:MeshMorphClass.OuterCloth;
        static MeshRecord FindRecord(Maid m,SkinnedMeshRenderer r)=>_records.TryGetValue(m.GetHashCode(),out var list)?list.Find(x=>x.SMR==r&&x.Mesh==r.sharedMesh):null;
        static bool IsBreastBoneName(string s)=>s.Contains("mune")||s.Contains("breast");
        static void ApplySmoothedNormals(Mesh m,MeshRecord r,Vector3[] v)
        {m.normals=DeformationNormals.Build(r.OrigVerts,r.Skirt==null?v:r.Skirt.VisualVertices,r.OrigNormals,m.triangles);}
        static void LogMorphSkip(Maid m,SkinnedMeshRenderer r,MeshMorphClass c,string s) {throw new Exception(s);}
        static bool ShouldLogMorphDiagnostics(SkinnedMeshRenderer r,MeshMorphClass c)=>false;
        static void LogMorphApply(Maid m,SkinnedMeshRenderer r,MeshMorphClass c,string f,bool refresh,int n,float p,float ep,DeformStats s){}
        class MeshRecord
        {
            public SkinnedMeshRenderer SMR;
            public Mesh Mesh;
            public Vector3[] OrigVerts;
            public Vector3[] OrigNormals;
            public Vector3[] LastDeltaVerts;
            public Vector3[] LastNewV;
            public NMatrix ToReference;
            public float[] BellyInfluence;
            public float[] TorsoOwnership;
            public float[] ThighGuardRestore;
            public bool[] BreastExcluded;
            public List<int>[] Neighbors;
            public ALContext GrowthContext;
            public NavelAttachment Navel;
            public SkirtDrapePlan Skirt;
            public ClothingMotionPlan ClothingMotion;
            public int AppliedSignature;
            public bool RefreshReady,HadBinding;
            public ALContext AppliedContext;
            public int RefreshShapeSignature,RefreshStructureSignature,AppliedExactSignature,AppliedNormalSignature;
            public BodySurfaceHit[] RestHits;
            public byte[] RestHitState;
        }
        class MorphBaseBakeState
        {
            public Maid Maid;
            public TBodySkin Skin;
            public SkinnedMeshRenderer Renderer;
            public Mesh Mesh;
            public Vector3[] OriginalVerts;
            public Vector3[] OriginalNormals;
            public Vector3[] BakedVerts;
            public Vector3[] BakedNormals;
            public int OriginalSignature;
            public int AppliedSignature;
            public int ShapeSignature;
            public float Progress;
            public int MeshInstanceId;
            public MeshMorphClass MeshClass;
        }
        enum MeshMorphClass
        {
            Ignore,
            Body,
            InnerCloth,
            OuterCloth,
            NavelAccessory,
        }
        struct LocalFrame
        {
            public Vector3 Center;
            public Vector3 Up;
            public Vector3 Fwd;
            public Vector3 Right;
            public float ScaleSide;
            public float ScaleUp;
            public float ScaleFwd;
            public float ScaleGeneral;
            public TorsoProfile Profile;
            public float BoneLen;
        }
        struct DeformStats
        {
            public int MaskedVerts;
            public int EllipsoidVerts;
            public int NonZeroVerts;
            public float MaxDelta;
            public float MaxStrength;
        }
        static void ApplySMR(Maid maid, SkinnedMeshRenderer smr, float progress, MeshMorphClass meshClass, bool refreshBase)
        {
            Mesh mesh = smr.sharedMesh;
            int key = maid.GetHashCode();
            if (!_records.ContainsKey(key)) _records[key] = new List<MeshRecord>();
            var records = _records[key];

            records.RemoveAll(r => r.SMR == smr && r.Mesh != mesh);
            MeshRecord rec = records.Find(r => r.SMR == smr && r.Mesh == mesh);
            Vector3[] currentVerts = mesh.vertices;
            Vector3[] currentNormals = mesh.normals;
            int structure=MeshStructureSignature(smr);
            if(rec!=null && !refreshBase && CanReuseAppliedMesh(rec,currentVerts,currentNormals,progress,meshClass,structure))
            {if(_refreshStats!=null)_refreshStats.Reused++;return;}
            if(_refreshStats!=null)_refreshStats.Rebuilt++;
            if(rec!=null && rec.RefreshStructureSignature!=structure)rec.Neighbors=null;
            if (rec == null)
            {
                rec = new MeshRecord
                {
                    SMR = smr,
                    Mesh = mesh,
                    OrigVerts = (Vector3[])currentVerts.Clone(),
                    OrigNormals = (Vector3[])currentNormals.Clone(),
                };
                records.Add(rec);
            }

            // Guard: if current mesh already has our deformation applied, keep the stored
            // clean base rather than overwriting it with the deformed state.
            bool alreadyApplied = !refreshBase
                && rec.LastDeltaVerts != null
                && rec.AppliedSignature != 0
                && currentVerts.Length == rec.LastDeltaVerts.Length
                && ComputeVertexSignature(currentVerts) == rec.AppliedSignature;

            if (!alreadyApplied)
            {
                rec.OrigVerts = (Vector3[])CleanVertices(maid, smr).Clone();
                rec.OrigNormals = (Vector3[])currentNormals.Clone();
            }
            if (refreshBase)
            {
                rec.LastDeltaVerts = null;

                rec.AppliedSignature = 0;
            }

            bool[] mask = BuildVertexMask(smr, rec, meshClass);
            int mc = 0;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) mc++;

            if (mc == 0)
            {
                LogMorphSkip(maid, smr, meshClass, "empty-mask");
                return;
            }

            float effectiveProgress = Mathf.Clamp01(progress);

            Vector3[] newVerts;
            DeformStats stats;
            long deformStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            if (!TryDeformVertsInBindPoseWorld(
                smr,
                rec,
                mask,
                meshClass,
                effectiveProgress,
                out newVerts,
                out stats))
            {
                LogMorphSkip(maid, smr, meshClass, "bindpose-skin-unavailable");
                return;
            }

            if(_refreshStats!=null)_refreshStats.DeformTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-deformStarted;
            Vector3[] deltaVerts = BuildDeltaVerts(rec.OrigVerts, newVerts);
            // CleanVertices retains current game morphs and removes only our known
            // baked delta. Evaluate once from that base, even after TMorph rewrites it.
            Vector3[] appliedVerts = newVerts;

            rec.LastDeltaVerts = deltaVerts;
            rec.AppliedSignature = ComputeVertexSignature(appliedVerts);
            mesh.vertices = appliedVerts;
            long normalStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            ApplySmoothedNormals(mesh, rec, appliedVerts);
            if(_refreshStats!=null)_refreshStats.NormalTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-normalStarted;
            mesh.RecalculateBounds();
            long bindingStarted=System.Diagnostics.Stopwatch.GetTimestamp();
            InstallALBinding(maid, rec, appliedVerts, progress);
            if(_refreshStats!=null)_refreshStats.BindingTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-bindingStarted;
            RememberAppliedMesh(rec,appliedVerts,progress,meshClass,structure);

            if (ShouldLogMorphDiagnostics(smr, meshClass))
            {
                string frameSource = stats.EllipsoidVerts > 0 ? "bindpose-skin" : "bindpose-skin-zero";
                LogMorphApply(
                    maid,
                    smr,
                    meshClass,
                    frameSource,
                    refreshBase,
                    rec.OrigVerts.Length,
                    progress,
                    effectiveProgress,
                    stats);
            }
        }
        static int ComputeVertexSignature(Vector3[] verts)
        { return ExactVectors(verts); }
        static Vector3[] BuildDeltaVerts(Vector3[] baseVerts, Vector3[] newVerts)
        {
            if (baseVerts == null || newVerts == null || baseVerts.Length != newVerts.Length)
                return null;

            Vector3[] delta = new Vector3[baseVerts.Length];
            for (int i = 0; i < delta.Length; i++)
                delta[i] = newVerts[i] - baseVerts[i];
            return delta;
        }
        static List<int>[] BuildMeshNeighbors(Mesh mesh, int count)
        {
            List<int>[] neighbors = new List<int>[count];
            for (int i = 0; i < count; i++)
                neighbors[i] = new List<int>();

            int[] triangles = null;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return null;
            }

            if (triangles == null)
                return null;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) continue;
                AddNeighbor(neighbors, a, b);
                AddNeighbor(neighbors, b, a);
                AddNeighbor(neighbors, a, c);
                AddNeighbor(neighbors, c, a);
                AddNeighbor(neighbors, b, c);
                AddNeighbor(neighbors, c, b);
            }

            return neighbors;
        }
        static void AddNeighbor(List<int>[] neighbors, int index, int neighbor)
        {
            if (!neighbors[index].Contains(neighbor))
                neighbors[index].Add(neighbor);
        }
        static int FindBoneIndex(Transform[] bones, params string[] names)
        {
            if (bones == null || names == null) return -1;
            for (int n = 0; n < names.Length; n++)
            {
                string target = names[n];
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone = bones[i];
                    if (bone != null && bone.name == target)
                        return i;
                }
            }
            return -1;
        }
        static int ComputeMorphBakeSignature(float progress, MeshMorphClass meshClass)
        {
            unchecked
            {
                int hash = 17 * 31 + (int)meshClass;
                AddBakeSignatureFloat(ref hash, progress);
                AddBakeSignatureFloat(ref hash, Shape.SubtleStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.VisibleStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.MidStageProgress);
                AddBakeSignatureFloat(ref hash, Shape.GrowthFullness);
                AddBakeSignatureFloat(ref hash, Shape.GrowthWidth);
                AddBakeSignatureFloat(ref hash, Shape.UpperReach);
                AddBakeSignatureFloat(ref hash, Shape.LateSettle);
                AddBakeSignatureFloat(ref hash, Shape.VerticalRange);
                AddBakeSignatureFloat(ref hash, Shape.WallSmoothing);
                AddBakeSignatureFloat(ref hash, Shape.SagStrength);
                AddBakeSignatureFloat(ref hash, Shape.BellySag);
                AddBakeSignatureFloat(ref hash, Shape.MidVolume);
                AddBakeSignatureFloat(ref hash, Shape.LowerPoleLift);
                AddBakeSignatureFloat(ref hash, Shape.LateForwardShift);
                AddBakeSignatureFloat(ref hash, Shape.GrowthAxisTilt);
                AddBakeSignatureFloat(ref hash, Shape.LateHeightScale);
                AddBakeSignatureFloat(ref hash, Shape.LateDepthScale);
                AddBakeSignatureFloat(ref hash, Shape.LateWidthScale);
                AddBakeSignatureFloat(ref hash, Shape.ThighGuardSpeed);
                AddBakeSignatureFloat(ref hash, Shape.InnerThighGuardStrength);
                AddBakeSignatureFloat(ref hash, Shape.ThighGuardSmoothStrength);
                AddBakeSignatureFloat(ref hash, Shape.SkinClearance);
                AddBakeSignatureFloat(ref hash, Shape.VirtualAxisStrength);
                AddBakeSignatureFloat(ref hash, Shape.AxisBlendStart);
                AddBakeSignatureFloat(ref hash, Shape.AxisBlendFull);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionStart);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionWidth);
                AddBakeSignatureFloat(ref hash, Shape.LowerTransitionBias);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionStart);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionWidth);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionBias);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionJoin);
                AddBakeSignatureFloat(ref hash, Shape.UpperTransitionActivation);
                hash = hash * 31 + (Shape.BreastExclusionEnabled ? 1 : 0);
                hash = hash * 31 + (Shape.UpperBoneFilterEnabled ? 1 : 0);
                hash = hash * 31 + (Shape.UpperFieldFadeEnabled ? 1 : 0);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullLow);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullHigh);
                AddBakeSignatureFloat(ref hash, Shape.AxisPullAngle);
                AddBakeSignatureFloat(ref hash, Shape.AxisAnchorY);
                AddBakeSignatureFloat(ref hash, Shape.AxisAnchorZ);
                hash = hash * 31 + (Shape.NavelPreviewFull ? 1 : 0);
                AddBakeSignatureFloat(ref hash, Shape.NavelEversion);
                AddBakeSignatureFloat(ref hash, Shape.NavelStart);
                AddBakeSignatureFloat(ref hash, Shape.NavelHeight);
                AddBakeSignatureFloat(ref hash, Shape.NavelVerticalOffset);
                AddBakeSignatureFloat(ref hash, Shape.NavelRadius);
                AddBakeSignatureFloat(ref hash, Shape.NavelProportion);
                AddBakeSignatureFloat(ref hash, Shape.ClothOffset);
                AddBakeSignatureFloat(ref hash, Shape.ClothDistortThreshold);
                AddBakeSignatureFloat(ref hash, Shape.ClothDistortNeighborDiff);
                return hash;
            }
        }
        static void AddBakeSignatureFloat(ref int hash, float value)
        {
            hash = hash * 31 + value.GetHashCode();
        }
        static bool[] BuildVertexMask(SkinnedMeshRenderer smr, MeshRecord rec, MeshMorphClass meshClass)
        {
            var mask = new bool[rec.OrigVerts.Length];
            for (int i=0; i<mask.Length; i++) mask[i] = meshClass != MeshMorphClass.Ignore;
            return mask;
        }
        static bool TryDeformVertsInBindPoseWorld(SkinnedMeshRenderer smr, MeshRecord rec, bool[] mask,
            MeshMorphClass meshClass, float progress, out Vector3[] newVerts, out DeformStats stats)
            => TryDeformAL(smr, rec, meshClass, progress, out newVerts, out stats);

    }
}
