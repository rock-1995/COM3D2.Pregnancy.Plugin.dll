using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public static class BellyMorphController
    {
        public const int HaraMax = 100;
        const int AutoMorphStableFrames = 2;

        const float BaseRadiusSide = 0.2f;
        const float BaseRadiusFront = 0.33f;
        const float BasePushOut = 1.0f;
        const float NormalAffectedDelta = 0.0002f;
        const int NormalAffectedExpandPasses = 4;
        const int ThighGuardSmoothPasses = 2;
        const float ReferenceBodyUp = 0.25f;
        const float ReferenceBodySide = 0.25f;
        const float MinBodyRelativeScale = 0.35f;
        const float MaxBodyRelativeScale = 3.0f;

        public static float SpineLerpT = 1.0f;
        public static float OffsetSide = 0.0f;
        public static float InflationMultiplier = 0.0f;
        public static float InflationMoveY = 0.05f;
        public static float InflationMoveZ = 0.0f;
        public static float InflationStretchX = 0.22f;
        public static float InflationStretchY = 0.0f;
        public static float InflationStretchZ = 0.24f;
        public static float InflationShiftY = 0.04f;
        public static float InflationShiftZ = -0.3f;
        public static float InflationTaperY = -0.01f;
        public static float InflationTaperZ = -0.05f;
        public static float InflationRoundness = 0.03f;
        public static float InflationDrop = 0.13f;
        public static float InflationFatFold = 0.0f;
        public static float InflationFatFoldHeight = 0.0f;
        public static float InflationFatFoldGap = 0.0f;
        public static float RegionRadiusSide = BaseRadiusSide;
        public static float RegionRadiusFront = BaseRadiusFront;
        public static float RegionRadiusBack = 0.13f;
        public static float RegionRadiusUp = 0.51f;
        public static float RegionRadiusDown = 0.33f;
        public static float ThighGuardSpeed = 3.0f;
        public static float InnerThighGuardStrength = 1.0f;
        public static float ThighGuardSmoothStrength = 0.0f;
        public static float TopEdgeTaper = -1.0f;
        public static float BottomEdgeTaper = 0.0f;
        public static float SideSmoothWidth = 0.8f;
        public static float SideSmoothStrength = 1.4f;
        public static float BreastGuardStrength = 1.0f;
        public static float ClothOverdrive = 1.03f;
        public static float OuterClothOverdrive = 1.20f;
        public static float OuterClothPregnancyScale = 1.0f;
        public static int SkirtBoundarySmoothPasses = 2;
        public static float SkirtBoundarySmoothStrength = 0.35f;
        public static float SkirtBoundaryUpOffset = 0.08f;
        public static float SkirtFrontPlaneFwdOffset = 0.0f;
        public static float SkirtTopRadiusSideScale = 1.15f;
        public static float SkirtTopRadiusFwdScale = 1.15f;
        public static float SkirtHemFadeRangeScale = 1.0f;
        public static float SkirtLowerTipSide = 0.0f;
        public static float SkirtLowerTipUp = -0.42f;
        public static float SkirtLowerTipFwd = 0.0f;
        public static float SkirtLowerRadiusSide = 1.0f;
        public static float SkirtLowerRadiusUp = 1.0f;
        public static float SkirtLowerRadiusFwd = 1.0f;

        const float BellyEdgeBlend = 0.35f;
        const float ArmDetachWeightThreshold = 0.25f;
        const float SkirtLowerMinDelta = 0.00025f;
        const float SkirtBoundaryFrontBand = 0.08f;
        const float SkirtBoundaryPlaneBand = 0.16f;

        static readonly string[] FaceKeywords =
        {
            "face", "head", "eye", "mayu", "tooth", "teeth",
            "tongue", "lip", "nose", "ear"
        };

        static bool IsFaceSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string lower = name.ToLowerInvariant();
            if (lower.Contains("wear")) return false;
            foreach (string kw in FaceKeywords)
                if (lower.Contains(kw)) return true;

            return false;
        }

        static Dictionary<int, float> _activeProgress = new Dictionary<int, float>();
        static Dictionary<int, int> _aysHaraWriteSpyCountBySession = new Dictionary<int, int>();
        static bool _aysHooksPatched;

        class MeshRecord
        {
            public SkinnedMeshRenderer SMR;
            public Mesh Mesh;
            public Vector3[] OrigVerts;
            public Vector3[] OrigNormals;
            public Vector3[] LastDeltaVerts;
            public VertexMorphTrace[] LastMorphTrace;
            public int AppliedSignature;
            public int LastNavelReferenceTriangle = -1;
            public int LastNavelReferenceA = -1;
            public int LastNavelReferenceB = -1;
            public int LastNavelReferenceC = -1;
            public string LastNavelReferenceReason;
        }

        class MorphReferenceContext
        {
            public int MaidKey;
            public List<BodyReferencePoint> BodyPoints = new List<BodyReferencePoint>();
            public List<BodyReferenceMesh> BodyMeshes = new List<BodyReferenceMesh>();
            public List<BodySurfaceTriangle> SurfaceTriangles = new List<BodySurfaceTriangle>();
            public Dictionary<int, List<int>> SurfaceBuckets = new Dictionary<int, List<int>>();
            public float CellSize = 0.025f;
            public bool BodyChangedUpRangeValid;
            public float BodyChangedMinUp = float.MaxValue;
            public float BodyChangedMaxUp = float.MinValue;
            public int SurfaceQueries;
            public int SurfaceHits;
            public int SurfaceMisses;
            public int VirtualSurfaceTriangles;
            public int SuppressedSurfaceTriangles;
        }

        class BodyReferenceMesh
        {
            public string MeshId;
            public int MeshInstanceId;
            public Vector3[] OriginalWorld;
            public Vector3[] MorphedWorld;
            public bool[] Valid;
            public bool[] Eligible;
            public List<int>[] Neighbors;
            public List<int>[] SurfaceTrianglesByVertex;
        }

        class BoundaryReferenceMesh
        {
            public BodyReferenceMesh Mesh;
            public bool[] Boundary;
            public bool[] FrameEligible;
        }

        struct BodyReferencePoint
        {
            public BodyReferenceMesh Mesh;
            public int Index;
            public Vector3 OriginalWorld;
        }

        struct BodySurfaceTriangle
        {
            public BodyReferenceMesh Mesh;
            public int A;
            public int B;
            public int C;
            public Vector3 Center;
            public float RadiusSq;
        }

        struct BodySurfaceHit
        {
            public int TriangleIndex;
            public Vector3 Closest;
            public Vector3 Barycentric;
            public Vector3 Normal;
            public float DistanceSq;
        }

        struct BodySurfaceTargetDebug
        {
            public string FailReason;
            public int BodyPointCount;
            public int SurfaceTriangleCount;
            public bool SearchHit;
            public bool FullScanHit;
            public int TriangleIndex;
            public string BodyMeshId;
            public int BodyMeshInstanceId;
            public int BodyA;
            public int BodyB;
            public int BodyC;
            public float Distance;
            public float OffsetLen;
            public float SurfaceMoveLen;
            public float NormalDot;
            public Vector3 Barycentric;
            public Vector3 Closest;
            public Vector3 SurfaceMorphed;
            public Vector3 OriginalNormal;
            public Vector3 MorphedNormal;
            public Vector3 Target;
        }

        struct ClothInheritDebug
        {
            public string SourceMesh;
            public int OriginIndex;
            public int PointAIndex;
            public int PointBIndex;
            public int CandidateCount;
            public float FrameScore;
            public float NearestDistance;
            public string FailReason;
            public float OriginalThickness;
            public float RawThickness;
            public float FinalThickness;
            public Vector3 LocalOffset;
            public Vector3 MorphedOrigin;
        }

        struct MorphFrame
        {
            public Vector3 Origin;
            public Vector3 X;
            public Vector3 Y;
            public Vector3 Z;

            public bool TryToLocal(Vector3 worldOffset, out Vector3 localOffset)
            {
                localOffset = Vector3.zero;
                float det = Vector3.Dot(X, Vector3.Cross(Y, Z));
                if (det == 0f)
                    return false;

                localOffset = new Vector3(
                    Vector3.Dot(worldOffset, Vector3.Cross(Y, Z)) / det,
                    Vector3.Dot(X, Vector3.Cross(worldOffset, Z)) / det,
                    Vector3.Dot(X, Vector3.Cross(Y, worldOffset)) / det);
                return true;
            }

            public Vector3 ToWorld(Vector3 localOffset)
            {
                return X * localOffset.x + Y * localOffset.y + Z * localOffset.z;
            }
        }

        class VertexMorphTrace
        {
            public bool Masked;
            public bool SkinOk;
            public bool InEllipsoid;
            public bool MorphApplied;

            public string StopReason;
            public string BoneName0;
            public string BoneName1;
            public string BoneName2;
            public string BoneName3;
            public int BoneIndex0;
            public int BoneIndex1;
            public int BoneIndex2;
            public int BoneIndex3;
            public float BoneWeight0;
            public float BoneWeight1;
            public float BoneWeight2;
            public float BoneWeight3;

            public float EffectiveProgress;
            public float RadiusScale;
            public float RadiusSide;
            public float RadiusFront;
            public float RadiusBack;
            public float RadiusUp;
            public float RadiusDown;
            public float OrigSide;
            public float OrigUp;
            public float OrigFwd;
            public float FwdRadius;
            public float UpRadius;
            public float Ellip;
            public float EdgeRatio;
            public float EdgeFade;
            public float TopEdgeStrength;
            public float ShapeWeight;
            public float MorphWeight;
            public float OriginalRadius;
            public float DirectionalRadius;

            public float LowerBodyRestore;
            public float BreastRestore;
            public float InnerThighRestore;
            public float BottomTaperInfluence;
            public float BottomTaperLower;
            public float BottomTaperFront;
            public float BottomTaperGuardKeep;
            public bool InwardGuardApplied;
            public float InwardGuardDeltaFwd;

            public bool SkirtLowerEnabled;
            public float SkirtLowerBoundaryUp;
            public float SkirtLowerLowerLimit;
            public float SkirtLowerTopDeltaFwd;
            public float SkirtLowerGrowth;
            public float SkirtLowerDownT;
            public float SkirtLowerAddFwd;
            public string SkirtLowerReason;

            public bool OldFinalChanged;
            public float OldFinalDeltaWorldLen;
            public float OldFinalMinDelta;
            public string OldFinalReason;

            public bool ClothInheritApplied;
            public string ClothInheritKind;
            public string ClothInheritReason;
            public string ClothInheritSourceMesh;
            public int ClothInheritSourceVertex = -1;
            public int ClothInheritFrameA = -1;
            public int ClothInheritFrameB = -1;
            public int ClothInheritFrameCandidateCount;
            public float ClothInheritFrameScore;
            public float ClothInheritNearestDistance;
            public float ClothInheritOriginalThickness;
            public float ClothInheritRawThickness;
            public float ClothInheritFinalThickness;
            public Vector3 ClothInheritLocalOffset;
            public Vector3 ClothInheritPreWorld;
            public Vector3 ClothInheritPostWorld;
            public Vector3 ClothInheritPreCoord;
            public Vector3 ClothInheritPostCoord;
            public Vector3 ClothInheritDeltaCoord;
            public float ClothInheritDeltaWorldLen;
            public Vector3 ClothInheritDeltaDirCoord;
            public Vector3 ClothInheritFinalOffsetCoord;
            public string ClothInheritFailKind;
            public string ClothInheritFailReason;
            public string ClothInheritFailSourceMesh;
            public int ClothInheritFailSourceVertex = -1;
            public int ClothInheritFailFrameCandidateCount;
            public float ClothInheritFailFrameScore;
            public float ClothInheritFailNearestDistance;

            public bool SkirtHemBoundaryValid;
            public float SkirtHemBoundaryUp;
            public float SkirtHemFwdThreshold;
            public bool SkirtHemFront;
            public bool SkirtHemLowerRange;
            public bool SkirtHemCandidate;
            public float SkirtHemBlend = -1f;
            public string SkirtHemReason;

            public float ThighSmoothMask;
            public float ThighSmoothDeltaFwd;

            public float FwdBase;
            public float FwdSphere;
            public float FwdSculpt;
            public float FwdShift;
            public float FwdStretch;
            public float FwdRoundness;
            public float FwdTaperY;
            public float FwdTaperZ;
            public float FwdFatFold;
            public float FwdDrop;
            public float FwdSideSmooth;
            public float FwdRibReduce;
            public float FwdLowerRestore;
            public float FwdInwardGuard;
            public float FwdBreastRestore;
            public float FwdInnerThighRestore;
            public float FwdBottomTaper;
            public float FwdSkirtLower;
            public float FwdClothInherit;
            public float FwdThighSmooth;
            public float FwdFinal;
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

        static Dictionary<int, List<MeshRecord>> _records = new Dictionary<int, List<MeshRecord>>();
        static readonly Dictionary<int, HashSet<TBodySkin>> _morphDirtySkins = new Dictionary<int, HashSet<TBodySkin>>();
        static readonly Dictionary<int, float> _morphSpyLastLogTime = new Dictionary<int, float>();
        static readonly Dictionary<int, int> _meshInventorySignatures = new Dictionary<int, int>();
        static readonly Dictionary<TMorph, MorphBaseBakeState> _morphBaseBakeStates = new Dictionary<TMorph, MorphBaseBakeState>();
        static readonly FieldInfo _blendValuesChkField = typeof(TMorph).GetField(
            "BlendValuesCHK",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static BepInEx.Logging.ManualLogSource _log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
        static bool _suppressMorphBaseBake = false;
        static MorphReferenceContext _morphReferenceContext = null;
        const float MorphSpyMinInterval = 0.5f;

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
        }

        struct DeformStats
        {
            public int MaskedVerts;
            public int EllipsoidVerts;
            public int NonZeroVerts;
            public float MaxDelta;
            public float MaxStrength;
        }

        struct NavelAccessoryReference
        {
            public int Triangle;
            public int A;
            public int B;
            public int C;
            public float Score;
            public Vector3 OriginalAWorld;
            public Vector3 OriginalBWorld;
            public Vector3 OriginalCWorld;
            public Vector3 OriginalCenterWorld;
            public Vector3 TargetCenterWorld;
            public Quaternion DeltaRotation;
            public string Reason;
        }

        struct VirtualSeamSurfaceFit
        {
            public float CenterSide;
            public float HalfWidth;
            public float MinUp;
            public float MaxUp;
            public float OriginalPlaneA;
            public float OriginalPlaneSide;
            public float OriginalPlaneUp;
            public float MorphedSidePlaneA;
            public float MorphedSidePlaneSide;
            public float MorphedSidePlaneUp;
            public float MorphedUpPlaneA;
            public float MorphedUpPlaneSide;
            public float MorphedUpPlaneUp;
            public float MorphedPlaneA;
            public float MorphedPlaneSide;
            public float MorphedPlaneUp;
            public int SampleCount;
        }

        static bool _bpWorldCached = false;
        static LocalFrame _bpWorldFrame;
        static Dictionary<string, Matrix4x4> _bpBoneWorld = new Dictionary<string, Matrix4x4>();
        static HashSet<int> _pendingVisibilityApply = new HashSet<int>();
        static MorphTriggerMode CurrentTriggerMode =>
            PregnancyPlugin.CfgMorphTriggerMode != null
                ? PregnancyPlugin.CfgMorphTriggerMode.Value
                : MorphTriggerMode.ManualOnly;

        public static bool IsValid(Maid maid) => maid != null && maid.body0 != null;

        public static bool IsActive(Maid maid) => maid != null && _activeProgress.ContainsKey(maid.GetHashCode());

        public static float GetActiveProgress(Maid maid)
        {
            return maid != null && _activeProgress.TryGetValue(maid.GetHashCode(), out float p) ? p : 0f;
        }

        public static void MarkActive(Maid maid, float progress)
        {
            if (maid == null) return;
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
        }

        public static void ApplyProgress(Maid maid, float progress)
        {
            if (!IsValid(maid)) return;
            _bpWorldCached = false;
            _activeProgress[maid.GetHashCode()] = Mathf.Clamp01(progress);
            EnsureMonitor(maid);
            PruneRecords(maid);

            ApplyToSlots(maid, Mathf.Clamp01(progress), false);
        }

        public static void SetBelly(Maid maid, float sliderValue)
        {
            ApplyProgress(maid, sliderValue / 100f);
        }

        public static void Reset(Maid maid)
        {
            if (maid == null) return;

            ResetInternal(maid);
            _bpWorldCached = false;

            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon != null) Object.DestroyImmediate(mon);
        }

        public static void ResetToDefaults()
        {
            InflationMultiplier       = 0.0f;
            InflationMoveY            = 0.05f;
            InflationMoveZ            = 0.0f;
            InflationStretchX         = 0.22f;
            InflationStretchY         = 0.0f;
            InflationStretchZ         = 0.24f;
            InflationShiftY           = 0.04f;
            InflationShiftZ           = -0.3f;
            InflationTaperY           = -0.01f;
            InflationTaperZ           = -0.05f;
            InflationRoundness        = 0.03f;
            InflationDrop             = 0.13f;
            InflationFatFold          = 0.0f;
            InflationFatFoldHeight    = 0.0f;
            InflationFatFoldGap       = 0.0f;
            RegionRadiusSide          = BaseRadiusSide;
            RegionRadiusFront         = BaseRadiusFront;
            RegionRadiusBack          = 0.13f;
            RegionRadiusUp            = 0.51f;
            RegionRadiusDown          = 0.33f;
            ThighGuardSpeed           = 3.0f;
            InnerThighGuardStrength   = 1.0f;
            ThighGuardSmoothStrength  = 0.0f;
            TopEdgeTaper              = -1.0f;
            BottomEdgeTaper           = 0.0f;
            SideSmoothWidth           = 0.8f;
            SideSmoothStrength        = 1.4f;
            BreastGuardStrength       = 1.0f;
            OuterClothPregnancyScale  = 1.0f;
            SkirtBoundarySmoothPasses = 2;
            SkirtBoundarySmoothStrength = 0.35f;
            SkirtBoundaryUpOffset    = 0.08f;
            SkirtFrontPlaneFwdOffset = 0.0f;
            SkirtTopRadiusSideScale  = 1.15f;
            SkirtTopRadiusFwdScale   = 1.15f;
            SkirtHemFadeRangeScale   = 1.0f;
            SkirtLowerTipSide        = 0.0f;
            SkirtLowerTipUp          = -0.42f;
            SkirtLowerTipFwd         = 0.0f;
            SkirtLowerRadiusSide     = 1.0f;
            SkirtLowerRadiusUp       = 1.0f;
            SkirtLowerRadiusFwd      = 1.0f;
        }

        static void ResetInternal(Maid maid)
        {
            int key = maid.GetHashCode();
            _activeProgress.Remove(key);
            _morphDirtySkins.Remove(key);
            RestoreMorphBases(maid);

            if (!_records.TryGetValue(key, out var records)) return;

            foreach (var r in records)
            {
                if (r.SMR != null && r.Mesh != null && r.OrigVerts != null)
                {
                    Vector3[] currentVerts = r.Mesh.vertices;
                    if (r.LastDeltaVerts != null
                        && currentVerts != null
                        && currentVerts.Length == r.LastDeltaVerts.Length
                        && r.AppliedSignature != 0
                        && ComputeVertexSignature(currentVerts) == r.AppliedSignature)
                    {
                        for (int i = 0; i < currentVerts.Length; i++)
                            currentVerts[i] -= r.LastDeltaVerts[i];
                        r.Mesh.vertices = currentVerts;
                        r.Mesh.RecalculateBounds();
                    }
                }
            }

            records.Clear();
            _records.Remove(key);
        }

        static void ApplyToSlots(Maid maid, float progress, bool includeInactive)
        {
            if (maid == null || maid.body0 == null || maid.body0.goSlot == null) return;
            List<SkinnedMeshRenderer> targetSmrs = CollectTargetRenderers(maid);
            MaybeLogMeshInventory(maid, targetSmrs, "apply");

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            MorphReferenceContext previousContext = _morphReferenceContext;
            _morphReferenceContext = new MorphReferenceContext
            {
                MaidKey = maid.GetHashCode(),
                CellSize = Mathf.Max(RelGeneral(0.025f), 0.005f),
            };
            try
            {
                HashSet<int> appliedMeshIds = new HashSet<int>();
                HashSet<int> activeBodyMeshIds = CollectActiveBodyMeshIds(targetSmrs);
                HashSet<int> hiddenBodyReferenceMeshIds = new HashSet<int>();

                for (int pass = 0; pass < 2; pass++)
                {
                    bool bodyPass = pass == 0;
                    foreach (SkinnedMeshRenderer smr in targetSmrs)
                    {
                        if (smr?.sharedMesh == null) continue;

                        MeshMorphClass meshClass = ClassifyMesh(smr);
                        if (meshClass == MeshMorphClass.Ignore)
                        {
                            if (bodyPass && IsDebugMeshLoggingEnabled() && !IsFaceSlot(GetMeshId(smr)))
                                _log.LogInfo($"[BellyDiag] unrecognized maid={GetMaidName(maid)}"
                                    + $" id={GetMeshId(smr)}"
                                    + $" verts={smr.sharedMesh.vertexCount}");
                            continue;
                        }

                        if ((meshClass == MeshMorphClass.Body) != bodyPass)
                            continue;

                        if (IsDebugMeshLoggingEnabled())
                            LogMorphScan(maid, smr, meshClass, includeInactive, includeInactive || smr.gameObject.activeInHierarchy);

                        AttachNotifier(smr.gameObject, maid);

                        bool willApply = includeInactive || smr.gameObject.activeInHierarchy;
                        int meshId = smr.sharedMesh.GetInstanceID();
                        if (bodyPass && !willApply)
                        {
                            if (!activeBodyMeshIds.Contains(meshId) && hiddenBodyReferenceMeshIds.Add(meshId))
                            {
                                if (!TryRegisterBodyReferenceOnly(maid, smr, progress))
                                    LogMorphSkip(maid, smr, meshClass, "hidden-body-reference-failed");
                            }
                            continue;
                        }

                        if (willApply)
                        {
                            if (!appliedMeshIds.Add(meshId))
                            {
                                if (IsDebugMeshLoggingEnabled())
                                    LogMorphSkip(maid, smr, meshClass, "duplicate-shared-mesh");
                                continue;
                            }

                            ApplySMR(maid, smr, progress, meshClass, false);
                        }
                    }
                }
            }
            finally
            {
                _morphReferenceContext = previousContext;
            }
        }

        static void PruneRecords(Maid maid)
        {
            if (maid == null) return;

            int key = maid.GetHashCode();
            if (!_records.TryGetValue(key, out var records)) return;

            records.RemoveAll(r =>
                r == null
                || r.SMR == null
                || r.Mesh == null
                || r.SMR.sharedMesh == null
                || r.SMR.sharedMesh != r.Mesh);

            if (records.Count == 0)
                _records.Remove(key);
        }

        static void ForgetRecords(Maid maid)
        {
            if (maid == null) return;

            int key = maid.GetHashCode();
            if (_records.TryGetValue(key, out var records))
            {
                records.Clear();
                _records.Remove(key);
            }
        }

        static int GetRendererKey(SkinnedMeshRenderer smr)
        {
            return smr != null ? smr.GetInstanceID() : 0;
        }

        static MeshRecord FindRecord(Maid maid, SkinnedMeshRenderer smr)
        {
            if (maid == null || smr == null || smr.sharedMesh == null) return null;
            int key = maid.GetHashCode();
            if (!_records.TryGetValue(key, out var records)) return null;
            return records.Find(r => r != null && r.SMR == smr && r.Mesh == smr.sharedMesh);
        }

        static List<SkinnedMeshRenderer> CollectTargetRenderers(Maid maid)
        {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            HashSet<int> seen = new HashSet<int>();

            for (int si = 0; si < maid.body0.goSlot.Count; si++)
            {
                TBodySkin slot = maid.body0.goSlot[si];
                if (slot?.obj == null) continue;
                AddRenderers(slot.obj.transform, result, seen);
            }

            AddRenderers(maid.transform, result, seen);
            return result;
        }

        static HashSet<int> CollectActiveBodyMeshIds(List<SkinnedMeshRenderer> renderers)
        {
            HashSet<int> result = new HashSet<int>();
            if (renderers == null) return result;

            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (!smr.gameObject.activeInHierarchy) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                result.Add(smr.sharedMesh.GetInstanceID());
            }

            return result;
        }

        static void AddRenderers(Transform root, List<SkinnedMeshRenderer> result, HashSet<int> seen)
        {
            if (root == null) return;

            SkinnedMeshRenderer[] smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                if (smr == null) continue;
                if (seen.Add(smr.GetInstanceID()))
                    result.Add(smr);
            }
        }

        static string GetMeshId(SkinnedMeshRenderer smr)
        {
            if (smr == null) return string.Empty;
            return (smr.name + "/" + smr.gameObject.name).ToLowerInvariant();
        }

        static bool ContainsAny(string value, params string[] patterns)
        {
            foreach (string pattern in patterns)
                if (value.Contains(pattern)) return true;
            return false;
        }

        static bool[] BuildVertexMask(SkinnedMeshRenderer smr, MeshRecord rec, MeshMorphClass meshClass)
        {
            int count = rec.OrigVerts.Length;
            bool[] mask = new bool[count];

            if (meshClass == MeshMorphClass.Ignore)
                return mask;

            Mesh mesh = smr != null ? smr.sharedMesh : null;
            BoneWeight[] weights = mesh != null ? mesh.boneWeights : null;
            Transform[] bones = smr != null ? smr.bones : null;
            for (int i = 0; i < count; i++)
                mask[i] = !IsArmDetachVertex(weights, bones, i);

            return mask;
        }

        static MeshMorphClass ClassifyMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return MeshMorphClass.Ignore;
            if (IsFaceSlot(id)) return MeshMorphClass.Ignore;
            if (id.Contains("moza")) return MeshMorphClass.Ignore;
            if (ContainsAny(id, "accheso")) return MeshMorphClass.NavelAccessory;
            if (ContainsAny(id, "body", "base", "karada", "inmou", "nip", "under")) return MeshMorphClass.Body;
            if (ContainsAny(id, "bra", "pants", "psnts", "stkg", "mizugi", "zurashi")) return MeshMorphClass.InnerCloth;
            if (ContainsAny(id, "wear", "onep", "skrt", "zubon", "skirt", "mekure")) return MeshMorphClass.OuterCloth;

            return MeshMorphClass.Ignore;
        }

        static bool IsSkirtLikeMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return false;
            if (id.Contains("etoile_skrt")) return false;
            return ContainsAny(id, "skrt", "skirt", "onep", "mekure");
        }

        static bool IsPantsLikeMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return false;
            return ContainsAny(id, "zubon");
        }

        static bool IsArmDetachVertex(BoneWeight[] weights, Transform[] bones, int index)
        {
            if (weights == null || bones == null || index < 0 || index >= weights.Length)
                return false;

            float armWeight = 0f;
            AddArmDetachBoneWeight(ref armWeight, bones, weights[index].boneIndex0, weights[index].weight0);
            AddArmDetachBoneWeight(ref armWeight, bones, weights[index].boneIndex1, weights[index].weight1);
            AddArmDetachBoneWeight(ref armWeight, bones, weights[index].boneIndex2, weights[index].weight2);
            AddArmDetachBoneWeight(ref armWeight, bones, weights[index].boneIndex3, weights[index].weight3);
            return armWeight >= ArmDetachWeightThreshold;
        }

        static void AddArmDetachBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsArmDetachBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsArmDetachBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name == "Bip01 L UpperArm"
                || name == "Bip01 L UpperArm_SCL_"
                || name == "Bip01 L Forearm"
                || name == "Bip01 L Forearm_SCL_"
                || name == "Bip01 L Hand"
                || name == "Bip01 L Hand_SCL_"
                || name == "Uppertwist_L"
                || name == "Uppertwist1_L"
                || name == "Foretwist_L"
                || name == "Foretwist1_L"
                || name == "Bip01 R UpperArm"
                || name == "Bip01 R UpperArm_SCL_"
                || name == "Bip01 R Forearm"
                || name == "Bip01 R Forearm_SCL_"
                || name == "Bip01 R Hand"
                || name == "Bip01 R Hand_SCL_"
                || name == "Uppertwist_R"
                || name == "Uppertwist1_R"
                || name == "Foretwist_R"
                || name == "Foretwist1_R";
        }

        static void MaybeLogMeshInventory(Maid maid, List<SkinnedMeshRenderer> renderers, string reason)
        {
            if (!IsDebugMeshLoggingEnabled())
            {
                if (maid != null)
                    _meshInventorySignatures.Remove(maid.GetHashCode());
                return;
            }
            if (maid == null || renderers == null) return;

            int key = maid.GetHashCode();
            int signature = ComputeMeshInventorySignature(renderers);
            int previous;
            if (_meshInventorySignatures.TryGetValue(key, out previous) && previous == signature)
                return;
            _meshInventorySignatures[key] = signature;

            _log.LogInfo("[BellyMesh] inventory"
                + $" maid={GetMaidName(maid)}"
                + $" reason={reason}"
                + $" count={renderers.Count}"
                + $" signature={signature}");

            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null)
                {
                    _log.LogInfo("[BellyMesh] item"
                        + $" maid={GetMaidName(maid)}"
                        + $" index={i}"
                        + " null=1");
                    continue;
                }

                Mesh mesh = smr.sharedMesh;
                MeshMorphClass meshClass = ClassifyMesh(smr);
                _log.LogInfo("[BellyMesh] item"
                    + $" maid={GetMaidName(maid)}"
                    + $" index={i}"
                    + $" id={GetMeshId(smr)}"
                    + $" class={meshClass}"
                    + $" renderer={smr.name}"
                    + $" go={smr.gameObject.name}"
                    + $" path={GetTransformPath(smr.transform)}"
                    + $" mesh={(mesh != null ? mesh.name : string.Empty)}"
                    + $" meshId={(mesh != null ? mesh.GetInstanceID() : 0)}"
                    + $" verts={(mesh != null ? mesh.vertexCount : 0)}"
                    + $" bones={(smr.bones != null ? smr.bones.Length : 0)}"
                    + $" enabled={smr.enabled}"
                    + $" activeSelf={smr.gameObject.activeSelf}"
                    + $" activeInHierarchy={smr.gameObject.activeInHierarchy}");
            }
        }

        static int ComputeMeshInventorySignature(List<SkinnedMeshRenderer> renderers)
        {
            unchecked
            {
                int hash = 17;
                if (renderers == null) return hash;
                hash = hash * 31 + renderers.Count;
                for (int i = 0; i < renderers.Count; i++)
                {
                    SkinnedMeshRenderer smr = renderers[i];
                    if (smr == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    Mesh mesh = smr.sharedMesh;
                    hash = hash * 31 + smr.GetInstanceID();
                    hash = hash * 31 + (mesh != null ? mesh.GetInstanceID() : 0);
                    hash = hash * 31 + (mesh != null ? mesh.vertexCount : 0);
                    hash = hash * 31 + (smr.enabled ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeSelf ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeInHierarchy ? 1 : 0);
                    hash = hash * 31 + GetMeshId(smr).GetHashCode();
                    hash = hash * 31 + ((mesh != null && mesh.name != null) ? mesh.name.GetHashCode() : 0);
                }
                return hash;
            }
        }

        static string GetTransformPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null && names.Count < 32)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        static bool ShouldLogMorphDiagnostics(SkinnedMeshRenderer smr, MeshMorphClass meshClass)
        {
            if (!IsDebugMeshLoggingEnabled()) return false;
            if (smr == null) return false;
            string id = GetMeshId(smr);
            return !IsFaceSlot(id);
        }

        static bool IsDebugMeshLoggingEnabled()
        {
            return PregnancyPlugin.CfgDebugMeshLogging != null
                && PregnancyPlugin.CfgDebugMeshLogging.Value;
        }

        static void LogMorphScan(Maid maid, SkinnedMeshRenderer smr, MeshMorphClass meshClass, bool includeInactive, bool willApply)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            Mesh mesh = smr.sharedMesh;
            _log.LogInfo("[BellyDiag] scan"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" includeInactive={includeInactive}"
                + $" activeSelf={smr.gameObject.activeSelf}"
                + $" activeInHierarchy={smr.gameObject.activeInHierarchy}"
                + $" enabled={smr.enabled}"
                + $" meshId={(mesh != null ? mesh.GetInstanceID() : 0)}"
                + $" verts={(mesh != null ? mesh.vertexCount : 0)}"
                + $" action={(willApply ? "apply" : "skip-inactive")}");
        }

        static void LogMorphApply(
            Maid maid,
            SkinnedMeshRenderer smr,
            MeshMorphClass meshClass,
            string frameSource,
            bool refreshBase,
            int vertexCount,
            float rawProgress,
            float effectiveProgress,
            DeformStats stats)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            _log.LogInfo("[BellyDiag] apply"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" frame={frameSource}"
                + $" refreshBase={refreshBase}"
                + $" verts={vertexCount}"
                + $" masked={stats.MaskedVerts}"
                + $" ellipsoid={stats.EllipsoidVerts}"
                + $" moved={stats.NonZeroVerts}"
                + $" maxDelta={stats.MaxDelta:F6}"
                + $" maxStrength={stats.MaxStrength:F6}"
                + $" progress={rawProgress:F3}"
                + $" effectiveProgress={effectiveProgress:F3}");
        }

        static void LogMorphSkip(Maid maid, SkinnedMeshRenderer smr, MeshMorphClass meshClass, string reason)
        {
            if (!ShouldLogMorphDiagnostics(smr, meshClass)) return;

            _log.LogInfo("[BellyDiag] skip"
                + $" maid={GetMaidName(maid)}"
                + $" id={GetMeshId(smr)}"
                + $" class={meshClass}"
                + $" reason={reason}");
        }

        static string GetMaidName(Maid maid)
        {
            if (maid == null || maid.status == null) return "?";
            return (maid.status.lastName + maid.status.firstName).Trim();
        }

        static void AttachNotifier(GameObject obj, Maid maid)
        {
            var notifier = obj.GetComponent<VisibilityNotifier>();
            if (notifier == null)
            {
                notifier = obj.AddComponent<VisibilityNotifier>();
            }
            notifier.Configure(maid);
        }

        static BellyMonitor EnsureMonitor(Maid maid)
        {
            var mon = maid.gameObject.GetComponent<BellyMonitor>();
            if (mon == null)
            {
                mon = maid.gameObject.AddComponent<BellyMonitor>();
                mon.SetMaid(maid);
            }
            return mon;
        }

        public static void NotifyFixBlendValuesSpy(TMorph morph)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;
                if (object.ReferenceEquals(morph, null)) return;

                string caller;
                if (!TryGetAysCaller(out caller)) return;

                Maid maid = null;
                if (!object.ReferenceEquals(morph.bodyskin, null))
                    maid = FindMaidForBodySkin(morph.bodyskin);

                float progress = IsValid(maid)
                    ? (PregnancyManager.GetPregnant(maid)
                        ? PregnancyManager.GetProgress(maid)
                        : GetActiveProgress(maid))
                    : 0f;

                int key = morph.GetHashCode();
                float now = Time.unscaledTime;
                float last;
                if (_morphSpyLastLogTime.TryGetValue(key, out last) && now - last < MorphSpyMinInterval)
                    return;
                _morphSpyLastLogTime[key] = now;

                string slot = "";
                if (!object.ReferenceEquals(morph.bodyskin, null)
                    && !object.ReferenceEquals(morph.bodyskin.obj, null))
                    slot = morph.bodyskin.obj.name;

                int oriVertId = !object.ReferenceEquals(morph.m_vOriVert, null) ? morph.m_vOriVert.GetHashCode() : 0;
                int oriNormId = !object.ReferenceEquals(morph.m_vOriNorm, null) ? morph.m_vOriNorm.GetHashCode() : 0;
                int blendDataCount = !object.ReferenceEquals(morph.BlendDatas, null) ? morph.BlendDatas.Count : 0;

                _log.LogInfo("[MorphSpy] AYS FixBlendValues"
                    + " maid=" + GetMaidName(maid)
                    + " slot=" + slot
                    + " morphId=" + morph.GetHashCode()
                    + " oriVertId=" + oriVertId
                    + " oriNormId=" + oriNormId
                    + " blendDatas=" + blendDataCount
                    + " morphCount=" + morph.MorphCount
                    + " vCount=" + morph.VCount
                    + " progress=" + Mathf.Clamp01(progress).ToString("0.###")
                    + " caller=" + caller);
            }
            catch { }
        }

        static bool TryGetAysCaller(out string caller)
        {
            caller = "";
            try
            {
                var frames = new System.Diagnostics.StackTrace(false).GetFrames();
                if (frames == null) return false;

                for (int i = 0; i < frames.Length; i++)
                {
                    var method = frames[i].GetMethod();
                    if (object.ReferenceEquals(method, null)) continue;
                    var type = method.DeclaringType;
                    string fullName = !object.ReferenceEquals(type, null) ? type.FullName : "";
                    if (fullName.IndexOf("AddYotogiSlider", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    caller = fullName + "." + method.Name;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static void NotifyFixBlendValues(TMorph morph)
        {
            if (_suppressMorphBaseBake) return;
            if (object.ReferenceEquals(morph, null) || object.ReferenceEquals(morph.bodyskin, null)) return;

            Maid maid = FindMaidForBodySkin(morph.bodyskin);
            if (!IsValid(maid)) return;

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f)
            {
                RestoreMorphBase(morph, true);
                return;
            }

            TryBakeRuntimeMorphBase(maid, morph, progress);
        }

        static bool TryBakeRuntimeMorphBase(Maid maid, TMorph morph, float progress)
        {
            Vector3[] currentVerts = morph.m_vOriVert;
            if (currentVerts == null || currentVerts.Length == 0) return false;

            SkinnedMeshRenderer smr = FindRendererForMorph(maid, morph);
            if (smr == null || smr.sharedMesh == null) return false;

            MeshMorphClass meshClass = ClassifyMesh(smr);
            if (meshClass == MeshMorphClass.Ignore) return false;

            Mesh mesh = smr.sharedMesh;
            int meshId = mesh.GetInstanceID();
            int shapeSignature = ComputeMorphBakeSignature(progress, meshClass);
            int currentSignature = ComputeVertexSignature(currentVerts);

            MorphBaseBakeState state;
            bool hasState = _morphBaseBakeStates.TryGetValue(morph, out state)
                && state != null
                && state.MeshInstanceId == meshId
                && state.OriginalVerts != null
                && state.OriginalVerts.Length == currentVerts.Length;

            bool currentIsKnownBaked = hasState
                && state.AppliedSignature != 0
                && currentSignature == state.AppliedSignature;

            if (currentIsKnownBaked && state.ShapeSignature == shapeSignature)
                return false;

            Vector3[] baseVerts;
            Vector3[] baseNormals;
            bool reusedStoredBase = false;
            if (currentIsKnownBaked)
            {
                baseVerts = (Vector3[])state.OriginalVerts.Clone();
                baseNormals = CloneVectorArray(state.OriginalNormals);
                reusedStoredBase = true;
            }
            else
            {
                baseVerts = (Vector3[])currentVerts.Clone();
                baseNormals = CloneNormalsForMorph(morph.m_vOriNorm, mesh, currentVerts.Length);
            }

            if (!CacheBindPoseWorldForMaid(maid)) return false;

            MorphReferenceContext previousContext = _morphReferenceContext;
            _morphReferenceContext = new MorphReferenceContext
            {
                MaidKey = maid.GetHashCode(),
                CellSize = Mathf.Max(RelGeneral(0.025f), 0.005f),
            };

            try
            {
                if (meshClass != MeshMorphClass.Body && !BuildRuntimeBodyMorphReferences(maid, progress))
                    return false;

                MeshRecord rec = new MeshRecord
                {
                    SMR = smr,
                    Mesh = mesh,
                    OrigVerts = baseVerts,
                    OrigNormals = baseNormals,
                };

                bool[] mask = BuildVertexMask(smr, rec, meshClass);
                if (!HasAnyMaskedVertex(mask)) return false;

                float effectiveProgress = GetEffectiveMorphProgress(progress, meshClass);
                Vector3[] bakedVerts;
                DeformStats stats;
                if (!TryBuildMorphedVertsInBindPoseWorld(
                    smr,
                    rec,
                    mask,
                    meshClass,
                    effectiveProgress,
                    out bakedVerts,
                    out stats))
                {
                    return false;
                }

                Vector3[] bakedNormals = BuildMorphBaseNormals(mesh, rec, bakedVerts);
                if (bakedNormals == null || bakedNormals.Length != bakedVerts.Length)
                    bakedNormals = CloneVectorArray(baseNormals);

                int originalSignature = ComputeVertexSignature(baseVerts);
                int appliedSignature = ComputeVertexSignature(bakedVerts);
                int oldOriVertId = currentVerts.GetHashCode();

                morph.m_vOriVert = bakedVerts;
                if (bakedNormals != null && bakedNormals.Length == bakedVerts.Length)
                    morph.m_vOriNorm = bakedNormals;

                MorphBaseBakeState newState = new MorphBaseBakeState
                {
                    Maid = maid,
                    Skin = morph.bodyskin,
                    Renderer = smr,
                    Mesh = mesh,
                    OriginalVerts = baseVerts,
                    OriginalNormals = baseNormals,
                    BakedVerts = bakedVerts,
                    BakedNormals = bakedNormals,
                    OriginalSignature = originalSignature,
                    AppliedSignature = appliedSignature,
                    ShapeSignature = shapeSignature,
                    Progress = progress,
                    MeshInstanceId = meshId,
                    MeshClass = meshClass,
                };
                _morphBaseBakeStates[morph] = newState;

                ForceFixBlendValues(morph);
                LogMorphBaseBake(maid, morph, smr, meshClass, progress, effectiveProgress, oldOriVertId, newState, stats, reusedStoredBase);

                return true;
            }
            finally
            {
                _morphReferenceContext = previousContext;
            }
        }

        static bool BuildRuntimeBodyMorphReferences(Maid maid, float progress)
        {
            if (_morphReferenceContext == null || !IsValid(maid) || !_bpWorldCached)
                return false;

            List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
            HashSet<int> seenMeshes = new HashSet<int>();
            float bodyProgress = GetEffectiveMorphProgress(progress, MeshMorphClass.Body);

            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;

                Mesh mesh = smr.sharedMesh;
                if (!seenMeshes.Add(mesh.GetInstanceID())) continue;

                Vector3[] currentVerts = mesh.vertices;
                if (currentVerts == null || currentVerts.Length == 0) continue;

                Vector3[] baseVerts = null;
                Vector3[] baseNormals = null;
                MeshRecord existing = FindRecord(maid, smr);
                bool currentIsApplied = existing != null
                    && existing.OrigVerts != null
                    && existing.OrigVerts.Length == currentVerts.Length
                    && existing.LastDeltaVerts != null
                    && existing.LastDeltaVerts.Length == currentVerts.Length
                    && existing.AppliedSignature != 0
                    && ComputeVertexSignature(currentVerts) == existing.AppliedSignature;

                if (currentIsApplied)
                {
                    baseVerts = (Vector3[])existing.OrigVerts.Clone();
                    baseNormals = CloneVectorArray(existing.OrigNormals);
                }
                else
                {
                    baseVerts = (Vector3[])currentVerts.Clone();
                    baseNormals = CloneVectorArray(mesh.normals);
                }

                MeshRecord rec = new MeshRecord
                {
                    SMR = smr,
                    Mesh = mesh,
                    OrigVerts = baseVerts,
                    OrigNormals = baseNormals,
                };

                bool[] mask = BuildVertexMask(smr, rec, MeshMorphClass.Body);
                if (!HasAnyMaskedVertex(mask)) continue;

                Vector3[] ignoredVerts;
                DeformStats ignoredStats;
                TryDeformVertsInBindPoseWorld(
                    smr,
                    rec,
                    mask,
                    MeshMorphClass.Body,
                    bodyProgress,
                    out ignoredVerts,
                    out ignoredStats);
            }

            return _morphReferenceContext.SurfaceTriangles.Count > 0;
        }

        static SkinnedMeshRenderer FindRendererForMorph(Maid maid, TMorph morph)
        {
            if (object.ReferenceEquals(morph, null) || morph.m_vOriVert == null) return null;

            int vertexCount = morph.m_vOriVert.Length;
            List<SkinnedMeshRenderer> candidates = new List<SkinnedMeshRenderer>();
            HashSet<int> seen = new HashSet<int>();

            if (morph.bodyskin != null && morph.bodyskin.obj != null)
                AddRenderers(morph.bodyskin.obj.transform, candidates, seen);

            SkinnedMeshRenderer ignoredMatch = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                SkinnedMeshRenderer smr = candidates[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.sharedMesh.vertexCount != vertexCount) continue;

                if (ClassifyMesh(smr) != MeshMorphClass.Ignore)
                    return smr;
                if (ignoredMatch == null)
                    ignoredMatch = smr;
            }

            if (IsValid(maid))
            {
                List<SkinnedMeshRenderer> all = CollectTargetRenderers(maid);
                for (int i = 0; i < all.Count; i++)
                {
                    SkinnedMeshRenderer smr = all[i];
                    if (smr == null || smr.sharedMesh == null) continue;
                    if (smr.sharedMesh.vertexCount != vertexCount) continue;

                    if (ClassifyMesh(smr) != MeshMorphClass.Ignore)
                        return smr;
                    if (ignoredMatch == null)
                        ignoredMatch = smr;
                }
            }

            return ignoredMatch;
        }

        static bool CacheBindPoseWorldForMaid(Maid maid)
        {
            if (!IsValid(maid)) return false;

            _bpWorldCached = false;
            _bpBoneWorld.Clear();

            List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) return true;
            }

            return false;
        }

        static bool HasAnyMaskedVertex(bool[] mask)
        {
            if (mask == null) return false;
            for (int i = 0; i < mask.Length; i++)
                if (mask[i]) return true;
            return false;
        }

        static float RelSide(float value)
            => value * (_bpWorldCached ? Mathf.Max(_bpWorldFrame.ScaleSide, MinBodyRelativeScale) : 1f);

        static float RelUp(float value)
            => value * (_bpWorldCached ? Mathf.Max(_bpWorldFrame.ScaleUp, MinBodyRelativeScale) : 1f);

        static float RelFwd(float value)
            => value * (_bpWorldCached ? Mathf.Max(_bpWorldFrame.ScaleFwd, MinBodyRelativeScale) : 1f);

        static float RelGeneral(float value)
            => value * (_bpWorldCached ? Mathf.Max(_bpWorldFrame.ScaleGeneral, MinBodyRelativeScale) : 1f);

        static float TraceFwd(Vector3 worldPoint, Vector3 center, Vector3 fwd)
        {
            return Vector3.Dot(worldPoint - center, fwd);
        }

        static string GetBoneName(Transform[] bones, int index)
        {
            if (bones == null || index < 0 || index >= bones.Length || bones[index] == null)
                return string.Empty;
            return bones[index].name ?? string.Empty;
        }

        static void FillTraceBoneInfo(VertexMorphTrace trace, BoneWeight weight, Transform[] bones)
        {
            if (trace == null) return;

            trace.BoneIndex0 = weight.boneIndex0;
            trace.BoneIndex1 = weight.boneIndex1;
            trace.BoneIndex2 = weight.boneIndex2;
            trace.BoneIndex3 = weight.boneIndex3;
            trace.BoneWeight0 = weight.weight0;
            trace.BoneWeight1 = weight.weight1;
            trace.BoneWeight2 = weight.weight2;
            trace.BoneWeight3 = weight.weight3;
            trace.BoneName0 = GetBoneName(bones, weight.boneIndex0);
            trace.BoneName1 = GetBoneName(bones, weight.boneIndex1);
            trace.BoneName2 = GetBoneName(bones, weight.boneIndex2);
            trace.BoneName3 = GetBoneName(bones, weight.boneIndex3);
        }

        static void InitializeTraceFwd(VertexMorphTrace trace, float fwd)
        {
            if (trace == null) return;

            trace.FwdBase = fwd;
            trace.FwdSphere = fwd;
            trace.FwdSculpt = fwd;
            trace.FwdShift = fwd;
            trace.FwdStretch = fwd;
            trace.FwdRoundness = fwd;
            trace.FwdTaperY = fwd;
            trace.FwdTaperZ = fwd;
            trace.FwdFatFold = fwd;
            trace.FwdDrop = fwd;
            trace.FwdSideSmooth = fwd;
            trace.FwdRibReduce = fwd;
            trace.FwdLowerRestore = fwd;
            trace.FwdInwardGuard = fwd;
            trace.FwdBreastRestore = fwd;
            trace.FwdInnerThighRestore = fwd;
            trace.FwdBottomTaper = fwd;
            trace.FwdSkirtLower = fwd;
            trace.FwdClothInherit = fwd;
            trace.FwdThighSmooth = fwd;
            trace.FwdFinal = fwd;
        }

        static void CaptureTraceFinalLocalFwd(
            VertexMorphTrace[] traces,
            Vector3[] finalLocalVerts,
            Vector3[] beforeSmoothLocalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3 center,
            Vector3 fwd)
        {
            if (traces == null || finalLocalVerts == null || weights == null || boneMatrices == null) return;

            int count = Mathf.Min(traces.Length, Mathf.Min(finalLocalVerts.Length, weights.Length));
            for (int i = 0; i < count; i++)
            {
                VertexMorphTrace trace = traces[i];
                if (trace == null || !trace.SkinOk || !HasValidBoneWeight(weights[i], boneMatrices)) continue;

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                if (beforeSmoothLocalVerts != null && i < beforeSmoothLocalVerts.Length)
                {
                    float beforeFwd = TraceFwd(skin.MultiplyPoint3x4(beforeSmoothLocalVerts[i]), center, fwd);
                    float afterFwd = TraceFwd(skin.MultiplyPoint3x4(finalLocalVerts[i]), center, fwd);
                    trace.ThighSmoothMask = Mathf.Max(trace.LowerBodyRestore, trace.InnerThighRestore);
                    float afterThighSmoothFwd = trace.ClothInheritApplied ? trace.ClothInheritPreCoord.z : afterFwd;
                    trace.ThighSmoothDeltaFwd = afterThighSmoothFwd - beforeFwd;
                    trace.FwdThighSmooth = afterThighSmoothFwd;
                    if (!trace.ClothInheritApplied)
                        trace.FwdClothInherit = afterThighSmoothFwd;
                    trace.FwdFinal = afterFwd;
                }
                else
                {
                    float finalFwd = TraceFwd(skin.MultiplyPoint3x4(finalLocalVerts[i]), center, fwd);
                    trace.FwdThighSmooth = trace.ClothInheritApplied ? trace.ClothInheritPreCoord.z : finalFwd;
                    if (!trace.ClothInheritApplied)
                        trace.FwdClothInherit = trace.FwdThighSmooth;
                    trace.FwdFinal = finalFwd;
                }
            }
        }

        static void SetSkirtLowerTraceReason(VertexMorphTrace[] traces, bool[] valid, string reason)
        {
            if (traces == null) return;

            int count = valid != null ? Mathf.Min(traces.Length, valid.Length) : traces.Length;
            for (int i = 0; i < count; i++)
            {
                if (valid != null && !valid[i]) continue;
                if (traces[i] != null)
                    traces[i].SkirtLowerReason = reason;
            }
        }

        static float GetEffectiveMorphProgress(float progress, MeshMorphClass meshClass)
        {
            if (meshClass == MeshMorphClass.InnerCloth)
                return progress * ClothOverdrive;
            if (meshClass == MeshMorphClass.OuterCloth)
                return progress * OuterClothPregnancyScale;
            return progress;
        }

        static Vector3[] CloneVectorArray(Vector3[] values)
        {
            return values != null ? (Vector3[])values.Clone() : null;
        }

        static Vector3[] CloneNormalsForMorph(Vector3[] morphNormals, Mesh mesh, int vertexCount)
        {
            if (morphNormals != null && morphNormals.Length == vertexCount)
                return (Vector3[])morphNormals.Clone();

            Vector3[] meshNormals = mesh != null ? mesh.normals : null;
            if (meshNormals != null && meshNormals.Length == vertexCount)
                return (Vector3[])meshNormals.Clone();

            return null;
        }

        static Vector3[] BuildMorphBaseNormals(Mesh mesh, MeshRecord rec, Vector3[] bakedVerts)
        {
            if (mesh == null || bakedVerts == null) return null;

            Vector3[] oldVerts = null;
            Vector3[] oldNormals = null;
            try
            {
                oldVerts = mesh.vertices;
                oldNormals = mesh.normals;
                mesh.vertices = bakedVerts;

                if (rec != null
                    && rec.OrigVerts != null
                    && rec.OrigNormals != null
                    && rec.OrigVerts.Length == bakedVerts.Length
                    && rec.OrigNormals.Length == bakedVerts.Length)
                {
                    ApplySmoothedNormals(mesh, rec, bakedVerts);
                }
                else
                {
                    mesh.RecalculateNormals();
                }

                Vector3[] normals = mesh.normals;
                if (normals != null && normals.Length == bakedVerts.Length)
                    return (Vector3[])normals.Clone();
            }
            catch
            {
            }
            finally
            {
                if (oldVerts != null)
                    mesh.vertices = oldVerts;
                if (oldNormals != null)
                    mesh.normals = oldNormals;
            }

            return rec != null ? CloneVectorArray(rec.OrigNormals) : null;
        }

        static void ForceFixBlendValues(TMorph morph)
        {
            if (object.ReferenceEquals(morph, null)) return;

            bool oldSuppress = _suppressMorphBaseBake;
            _suppressMorphBaseBake = true;
            try
            {
                ForceBlendValuesDirty(morph);
                morph.FixBlendValues();
            }
            finally
            {
                _suppressMorphBaseBake = oldSuppress;
            }
        }

        static void ForceBlendValuesDirty(TMorph morph)
        {
            try
            {
                if (object.ReferenceEquals(_blendValuesChkField, null)) return;
                float[] chk = _blendValuesChkField.GetValue(morph) as float[];
                if (chk != null && chk.Length > 0)
                    chk[0] = float.NaN;
            }
            catch
            {
            }
        }

        static void RestoreMorphBases(Maid maid)
        {
            if (maid == null || _morphBaseBakeStates.Count == 0) return;

            List<TMorph> toRestore = new List<TMorph>();
            foreach (KeyValuePair<TMorph, MorphBaseBakeState> entry in _morphBaseBakeStates)
            {
                MorphBaseBakeState state = entry.Value;
                if (state != null && object.ReferenceEquals(state.Maid, maid))
                    toRestore.Add(entry.Key);
            }

            for (int i = 0; i < toRestore.Count; i++)
                RestoreMorphBase(toRestore[i], true);
        }

        static void RestoreMorphBase(TMorph morph, bool forceFix)
        {
            if (object.ReferenceEquals(morph, null)) return;

            MorphBaseBakeState state;
            if (!_morphBaseBakeStates.TryGetValue(morph, out state) || state == null) return;

            try
            {
                Vector3[] currentVerts = morph.m_vOriVert;
                bool currentIsOurs = currentVerts != null
                    && state.AppliedSignature != 0
                    && ComputeVertexSignature(currentVerts) == state.AppliedSignature;

                if (currentIsOurs && state.OriginalVerts != null)
                {
                    morph.m_vOriVert = (Vector3[])state.OriginalVerts.Clone();
                    if (state.OriginalNormals != null && state.OriginalNormals.Length == state.OriginalVerts.Length)
                        morph.m_vOriNorm = (Vector3[])state.OriginalNormals.Clone();
                    if (forceFix)
                        ForceFixBlendValues(morph);
                }
            }
            finally
            {
                _morphBaseBakeStates.Remove(morph);
            }
        }

        static int ComputeMorphBakeSignature(float progress, MeshMorphClass meshClass)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)meshClass;
                AddBakeSignatureFloat(ref hash, progress);
                AddBakeSignatureFloat(ref hash, SpineLerpT);
                AddBakeSignatureFloat(ref hash, OffsetSide);
                AddBakeSignatureFloat(ref hash, InflationMultiplier);
                AddBakeSignatureFloat(ref hash, InflationMoveY);
                AddBakeSignatureFloat(ref hash, InflationMoveZ);
                AddBakeSignatureFloat(ref hash, InflationStretchX);
                AddBakeSignatureFloat(ref hash, InflationStretchY);
                AddBakeSignatureFloat(ref hash, InflationStretchZ);
                AddBakeSignatureFloat(ref hash, InflationShiftY);
                AddBakeSignatureFloat(ref hash, InflationShiftZ);
                AddBakeSignatureFloat(ref hash, InflationTaperY);
                AddBakeSignatureFloat(ref hash, InflationTaperZ);
                AddBakeSignatureFloat(ref hash, InflationRoundness);
                AddBakeSignatureFloat(ref hash, InflationDrop);
                AddBakeSignatureFloat(ref hash, InflationFatFold);
                AddBakeSignatureFloat(ref hash, InflationFatFoldHeight);
                AddBakeSignatureFloat(ref hash, InflationFatFoldGap);
                AddBakeSignatureFloat(ref hash, RegionRadiusSide);
                AddBakeSignatureFloat(ref hash, RegionRadiusFront);
                AddBakeSignatureFloat(ref hash, RegionRadiusBack);
                AddBakeSignatureFloat(ref hash, RegionRadiusUp);
                AddBakeSignatureFloat(ref hash, RegionRadiusDown);
                AddBakeSignatureFloat(ref hash, ThighGuardSpeed);
                AddBakeSignatureFloat(ref hash, InnerThighGuardStrength);
                AddBakeSignatureFloat(ref hash, ThighGuardSmoothStrength);
                AddBakeSignatureFloat(ref hash, TopEdgeTaper);
                AddBakeSignatureFloat(ref hash, BottomEdgeTaper);
                AddBakeSignatureFloat(ref hash, SideSmoothWidth);
                AddBakeSignatureFloat(ref hash, SideSmoothStrength);
                AddBakeSignatureFloat(ref hash, BreastGuardStrength);
                AddBakeSignatureFloat(ref hash, ClothOverdrive);
                AddBakeSignatureFloat(ref hash, OuterClothOverdrive);
                AddBakeSignatureFloat(ref hash, OuterClothPregnancyScale);
                hash = hash * 31 + SkirtBoundarySmoothPasses;
                AddBakeSignatureFloat(ref hash, SkirtBoundarySmoothStrength);
                AddBakeSignatureFloat(ref hash, SkirtBoundaryUpOffset);
                AddBakeSignatureFloat(ref hash, SkirtFrontPlaneFwdOffset);
                AddBakeSignatureFloat(ref hash, SkirtTopRadiusSideScale);
                AddBakeSignatureFloat(ref hash, SkirtTopRadiusFwdScale);
                AddBakeSignatureFloat(ref hash, SkirtHemFadeRangeScale);
                AddBakeSignatureFloat(ref hash, SkirtLowerTipSide);
                AddBakeSignatureFloat(ref hash, SkirtLowerTipUp);
                AddBakeSignatureFloat(ref hash, SkirtLowerTipFwd);
                AddBakeSignatureFloat(ref hash, SkirtLowerRadiusSide);
                AddBakeSignatureFloat(ref hash, SkirtLowerRadiusUp);
                AddBakeSignatureFloat(ref hash, SkirtLowerRadiusFwd);
                return hash;
            }
        }

        static void AddBakeSignatureFloat(ref int hash, float value)
        {
            hash = hash * 31 + Mathf.RoundToInt(value * 100000f);
        }

        static void LogMorphBaseBake(
            Maid maid,
            TMorph morph,
            SkinnedMeshRenderer smr,
            MeshMorphClass meshClass,
            float progress,
            float effectiveProgress,
            int oldOriVertId,
            MorphBaseBakeState state,
            DeformStats stats,
            bool reusedStoredBase)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                string caller = "";
                TryGetAysCaller(out caller);

                string slot = "";
                if (!object.ReferenceEquals(morph, null)
                    && morph.bodyskin != null
                    && morph.bodyskin.obj != null)
                    slot = morph.bodyskin.obj.name;

                int oriVertId = !object.ReferenceEquals(morph, null) && morph.m_vOriVert != null ? morph.m_vOriVert.GetHashCode() : 0;
                int oriNormId = !object.ReferenceEquals(morph, null) && morph.m_vOriNorm != null ? morph.m_vOriNorm.GetHashCode() : 0;
                int morphId = !object.ReferenceEquals(morph, null) ? morph.GetHashCode() : 0;
                int vertexCount = state != null && state.BakedVerts != null ? state.BakedVerts.Length : 0;

                _log.LogInfo("[MorphBake] runtime base baked"
                    + " maid=" + GetMaidName(maid)
                    + " slot=" + slot
                    + " id=" + GetMeshId(smr)
                    + " class=" + meshClass
                    + " morphId=" + morphId
                    + " oldOriVertId=" + oldOriVertId
                    + " oriVertId=" + oriVertId
                    + " oriNormId=" + oriNormId
                    + " originalSig=" + (state != null ? state.OriginalSignature : 0)
                    + " bakedSig=" + (state != null ? state.AppliedSignature : 0)
                    + " shapeSig=" + (state != null ? state.ShapeSignature : 0)
                    + " verts=" + vertexCount
                    + " moved=" + stats.NonZeroVerts
                    + " maxDelta=" + stats.MaxDelta.ToString("0.######")
                    + " progress=" + progress.ToString("0.###")
                    + " effectiveProgress=" + effectiveProgress.ToString("0.###")
                    + " source=" + (reusedStoredBase ? "stored-base" : "new-base")
                    + " caller=" + caller);
            }
            catch
            {
            }
        }

        internal static void FlushMorphDirty(Maid maid)
        {
            if (maid == null) return;
            int key = maid.GetHashCode();
            if (!_morphDirtySkins.TryGetValue(key, out var skins) || skins.Count == 0) return;

            var toProcess = new List<TBodySkin>(skins);
            skins.Clear();

            if (!IsValid(maid)) return;

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f) return;

            ApplyToSlots(maid, progress, false);
        }

        static Maid FindMaidForBodySkin(TBodySkin skin)
        {
            if (skin == null) return null;

            var cm = GameMain.Instance?.CharacterMgr;
            if (object.ReferenceEquals(cm, null)) return null;

            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid maid = cm.GetMaid(i);
                if (maid == null || maid.body0 == null || maid.body0.goSlot == null) continue;

                for (int si = 0; si < maid.body0.goSlot.Count; si++)
                {
                    TBodySkin slot = maid.body0.goSlot[si];
                    if (object.ReferenceEquals(slot, skin)) return maid;
                    if (slot != null && object.ReferenceEquals(slot.morph, skin.morph)) return maid;
                }
            }

            return null;
        }

        static void ApplyToBodySkin(Maid maid, TBodySkin skin, float progress)
        {
            if (maid == null || skin == null || skin.obj == null) return;
            ApplyToSlots(maid, progress, false);
        }

        public static void RequestCurrentMeshRefresh(Maid maid)
        {
            if (maid == null) return;
            EnsureMonitor(maid).TriggerFullRefresh();
        }

        public static void EnsureVisibilityObservers(Maid maid)
        {
            if (!IsValid(maid)) return;
            if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
            if (!PregnancyManager.GetPregnant(maid)) return;
            if (PregnancyManager.GetProgress(maid) <= 0f) return;

            foreach (SkinnedMeshRenderer smr in CollectRelevantRenderers(maid))
                AttachNotifier(smr.gameObject, maid);
        }

        public static void RequestVisibilityApplyBelly(Maid maid)
        {
            if (!IsValid(maid)) return;
            if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
            if (PregnancyPlugin.Instance == null) return;

            int key = maid.GetHashCode();
            if (!_pendingVisibilityApply.Add(key)) return;
            EnsureMonitor(maid).RequestVisibilityApply(key);
        }

        internal static bool PatchAysHooks(Harmony harmony)
        {
            if (_aysHooksPatched) return true;
            if (object.ReferenceEquals(harmony, null)) return false;

            bool patched = false;
            string[] typeNames =
            {
                "COM3D2.AddYotogiSliderSE.Plugin.AddYotogiSliderSE",
                "COM3D2.AddYotogiSlider.Plugin.AddYotogiSlider"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                System.Type type = AccessTools.TypeByName(typeNames[i]);
                if (object.ReferenceEquals(type, null)) continue;

                MethodInfo initOnStartSkill = AccessTools.Method(type, "initOnStartSkill");
                MethodInfo updateMaidHaraValue = AccessTools.Method(type, "updateMaidHaraValue");
                if (object.ReferenceEquals(initOnStartSkill, null)) continue;

                try
                {
                    harmony.Patch(
                        initOnStartSkill,
                        postfix: new HarmonyMethod(typeof(BellyMorphController), nameof(AysInitOnStartSkillPostfix)));

                    if (!object.ReferenceEquals(updateMaidHaraValue, null))
                    {
                        harmony.Patch(
                            updateMaidHaraValue,
                            postfix: new HarmonyMethod(typeof(BellyMorphController), nameof(AysUpdateMaidHaraValuePostfix)));
                    }

                    patched = true;
                }
                catch (System.Exception e)
                {
                    if (!object.ReferenceEquals(PregnancyPlugin.CfgMorphSpyLogging, null)
                        && PregnancyPlugin.CfgMorphSpyLogging.Value)
                        _log.LogInfo("[AYSHaraZero] patch failed type=" + typeNames[i]
                            + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }

            _aysHooksPatched = patched;
            if (patched
                && !object.ReferenceEquals(PregnancyPlugin.CfgMorphSpyLogging, null)
                && PregnancyPlugin.CfgMorphSpyLogging.Value)
                _log.LogInfo("[AYSHaraZero] patched AYS initOnStartSkill");
            return patched;
        }

        public static void AysInitOnStartSkillPostfix(object __instance)
        {
            if (object.ReferenceEquals(__instance, null)) return;
            TryZeroAysHaraAfterRead(__instance, "initOnStartSkill-postfix");
        }

        public static void AysUpdateMaidHaraValuePostfix(object __instance, float value)
        {
            if (object.ReferenceEquals(__instance, null)) return;
            LogAysHaraWriteSpy(__instance, value);
        }

        static bool TryZeroAysHaraAfterRead(object instance, string reason)
        {
            System.Type type = !object.ReferenceEquals(instance, null) ? instance.GetType() : null;
            if (!IsAysPluginType(type)) return false;

            Maid maid = GetAysMaid(instance, type);
            if (!IsValid(maid)) return false;

            float sliderValue;
            if (!TryGetAysHaraSliderValue(instance, type, out sliderValue))
                return false;

            int sessionKey = ComputeAysSessionKey(instance, type, maid);
            int beforeHara = GetMaidHaraValue(maid);
            int defHara = GetAysIntField(instance, type, "iDefHara");
            bool oldSuppress = _suppressMorphBaseBake;

            try
            {
                _suppressMorphBaseBake = true;
                maid.SetProp("Hara", 0, true);
                if (!object.ReferenceEquals(maid.body0, null))
                    maid.body0.VertexMorph_FromProcItem("hara", 0f);

                LogAysHaraZero(maid, beforeHara, defHara, sliderValue, reason, sessionKey);
                return true;
            }
            catch (System.Exception e)
            {
                LogAysHaraZeroError(maid, reason, e);
                return false;
            }
            finally
            {
                _suppressMorphBaseBake = oldSuppress;
            }
        }

        static int ComputeAysSessionKey(object instance, System.Type type, Maid maid)
        {
            object manager = null;
            try
            {
                FieldInfo managerField = type.GetField(
                    "yotogiPlayManager",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!object.ReferenceEquals(managerField, null))
                    manager = managerField.GetValue(instance);
            }
            catch
            {
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 397 + (!object.ReferenceEquals(maid, null) ? maid.GetHashCode() : 0);
                hash = hash * 397 + (!object.ReferenceEquals(instance, null) ? instance.GetHashCode() : 0);
                hash = hash * 397 + (!object.ReferenceEquals(manager, null) ? manager.GetHashCode() : 0);
                return hash;
            }
        }

        static bool IsAysPluginType(System.Type type)
        {
            if (object.ReferenceEquals(type, null)) return false;
            string fullName = type.FullName ?? "";
            return fullName == "COM3D2.AddYotogiSliderSE.Plugin.AddYotogiSliderSE"
                || fullName == "COM3D2.AddYotogiSlider.Plugin.AddYotogiSlider";
        }

        static Maid GetAysMaid(object instance, System.Type type)
        {
            try
            {
                FieldInfo maidField = type.GetField(
                    "maid",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return !object.ReferenceEquals(maidField, null) ? maidField.GetValue(instance) as Maid : null;
            }
            catch
            {
                return null;
            }
        }

        static bool TryGetAysHaraSliderValue(object instance, System.Type type, out float value)
        {
            value = 0f;
            try
            {
                FieldInfo sliderField = type.GetField(
                    "slider",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (object.ReferenceEquals(sliderField, null)) return false;

                object sliders = sliderField.GetValue(instance);
                System.Collections.IDictionary dict = sliders as System.Collections.IDictionary;
                if (object.ReferenceEquals(dict, null) || !dict.Contains("Hara")) return false;

                object haraSlider = dict["Hara"];
                if (object.ReferenceEquals(haraSlider, null)) return false;

                PropertyInfo valueProp = haraSlider.GetType().GetProperty(
                    "Value",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (object.ReferenceEquals(valueProp, null)) return false;

                value = System.Convert.ToSingle(valueProp.GetValue(haraSlider, null));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static int GetAysIntField(object instance, System.Type type, string name)
        {
            try
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return !object.ReferenceEquals(field, null) ? System.Convert.ToInt32(field.GetValue(instance)) : 0;
            }
            catch
            {
                return 0;
            }
        }

        static int GetMaidHaraValue(Maid maid)
        {
            try
            {
                MaidProp prop = !object.ReferenceEquals(maid, null) ? maid.GetProp("Hara") : null;
                return !object.ReferenceEquals(prop, null) ? prop.value : 0;
            }
            catch
            {
                return 0;
            }
        }

        static void LogAysHaraWriteSpy(object instance, float value)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                System.Type type = !object.ReferenceEquals(instance, null) ? instance.GetType() : null;
                if (!IsAysPluginType(type)) return;

                Maid maid = GetAysMaid(instance, type);
                int sessionKey = ComputeAysSessionKey(instance, type, maid);
                int count;
                _aysHaraWriteSpyCountBySession.TryGetValue(sessionKey, out count);
                if (count >= 12) return;

                count++;
                _aysHaraWriteSpyCountBySession[sessionKey] = count;

                int currentHara = GetMaidHaraValue(maid);
                int defHara = GetAysIntField(instance, type, "iDefHara");
                int currentAysHara = GetAysIntField(instance, type, "iCurrentHara");

                _log.LogInfo("[AYSHaraWriteSpy]"
                    + " maid=" + GetMaidName(maid)
                    + " value=" + value.ToString("0.###")
                    + " maidHara=" + currentHara
                    + " defHara=" + defHara
                    + " currentAysHara=" + currentAysHara
                    + " session=" + sessionKey
                    + " count=" + count);
            }
            catch
            {
            }
        }

        static void LogAysHaraZero(Maid maid, int beforeHara, int defHara, float sliderValue, string reason, int sessionKey)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                _log.LogInfo("[AYSHaraZero]"
                    + " maid=" + GetMaidName(maid)
                    + " reason=" + reason
                    + " beforeHara=" + beforeHara
                    + " defHara=" + defHara
                    + " sliderHara=" + sliderValue.ToString("0.###")
                    + " session=" + sessionKey);
            }
            catch
            {
            }
        }

        static void LogAysHaraZeroError(Maid maid, string reason, System.Exception e)
        {
            try
            {
                if (PregnancyPlugin.CfgMorphSpyLogging == null || !PregnancyPlugin.CfgMorphSpyLogging.Value)
                    return;

                _log.LogInfo("[AYSHaraZero]"
                    + " maid=" + GetMaidName(maid)
                    + " reason=" + reason
                    + " error=" + e.GetType().Name + ": " + e.Message);
            }
            catch
            {
            }
        }

        static bool TryCacheBindPoseWorldRef(SkinnedMeshRenderer smr)
        {
            if (!TryBuildBindPoseWorldRef(smr, out LocalFrame frame, out Dictionary<string, Matrix4x4> boneWorld))
                return false;

            _bpWorldFrame = frame;
            _bpBoneWorld = boneWorld;
            _bpWorldCached = true;
            if (IsDebugMeshLoggingEnabled())
                _log.LogInfo($"[Belly] BindPose world ref cached from {smr.name}: center={_bpWorldFrame.Center}");
            return true;
        }

        static bool TryBuildBindPoseWorldRef(
            SkinnedMeshRenderer smr,
            out LocalFrame frame,
            out Dictionary<string, Matrix4x4> boneWorld)
        {
            frame = default(LocalFrame);
            boneWorld = new Dictionary<string, Matrix4x4>();
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
            if (bones == null || bindposes == null) return false;

            int pelvisIdx = -1;
            int spineIdx = -1;
            int spineaIdx = -1;
            int leftRefIdx = -1;
            int rightRefIdx = -1;

            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            for (int j = 0; j < bones.Length && j < bindposes.Length; j++)
            {
                if (bones[j] == null) continue;
                string n = bones[j].name;
                if (!boneWorld.ContainsKey(n))
                    boneWorld[n] = l2w * bindposes[j].inverse;

                if (n == "Bip01 Pelvis_SCL_" || n == "Bip01 Pelvis") pelvisIdx = j;
                else if (n == "Bip01 Spine_SCL_" || n == "Bip01 Spine") spineIdx = j;
                else if (n == "Bip01 Spinea_SCL_" || n == "Bip01 Spine0a_SCL_" || n == "Bip01 Spinea") spineaIdx = j;

            }

            leftRefIdx = FindBoneIndex(bones,
                "Bip01 L Thigh_SCL_",
                "Bip01 L Thigh",
                "Hip_L",
                "Hip_L_nub",
                "momotwist_L",
                "momotwist2_L",
                "momoniku_L");
            rightRefIdx = FindBoneIndex(bones,
                "Bip01 R Thigh_SCL_",
                "Bip01 R Thigh",
                "Hip_R",
                "Hip_R_nub",
                "momotwist_R",
                "momotwist2_R",
                "momoniku_R");

            if (pelvisIdx < 0 || spineIdx < 0 || leftRefIdx < 0 || rightRefIdx < 0) return false;

            Vector3 pelvisW = GetMatrixPosition(l2w * bindposes[pelvisIdx].inverse);
            Vector3 spineW = GetMatrixPosition(l2w * bindposes[spineIdx].inverse);
            Vector3 leftRefW = GetMatrixPosition(l2w * bindposes[leftRefIdx].inverse);
            Vector3 rightRefW = GetMatrixPosition(l2w * bindposes[rightRefIdx].inverse);

            Vector3 up = (spineW - pelvisW).normalized;
            Vector3 tv = rightRefW - leftRefW;
            Vector3 rawRight = (tv - up * Vector3.Dot(tv, up)).normalized;
            Vector3 rawFwd = Vector3.Cross(rawRight, up).normalized;
            Vector3 fwd = smr.transform.forward;
            fwd = fwd - up * Vector3.Dot(fwd, up);
            if (fwd.sqrMagnitude < 1e-6f)
                fwd = rawFwd;
            else
                fwd = fwd.normalized;

            if (Vector3.Dot(fwd, rawFwd) < 0f)
                fwd = -fwd;

            Vector3 right = Vector3.Cross(up, fwd).normalized;
            if (right.sqrMagnitude < 1e-6f)
                right = rawRight;
            if (Vector3.Dot(right, rawRight) < 0f)
                right = -right;
            fwd = Vector3.Cross(right, up).normalized;

            float t = SpineLerpT;
            Vector3 worldBase;
            if (t <= 1f)
            {
                worldBase = Vector3.Lerp(pelvisW, spineW, t);
            }
            else
            {
                float step = Vector3.Distance(pelvisW, spineW);
                if (spineaIdx >= 0)
                {
                    Vector3 spineaW = GetMatrixPosition(l2w * bindposes[spineaIdx].inverse);
                    worldBase = Vector3.Lerp(spineW, spineaW, t - 1f);
                }
                else
                {
                    worldBase = spineW + up * ((t - 1f) * step);
                }
            }

            Vector3 lateralMid = (leftRefW + rightRefW) * 0.5f;
            worldBase += right * Vector3.Dot(lateralMid - worldBase, right);

            frame.Up = up;
            frame.Fwd = fwd;
            frame.Right = right;
            float upLen = Vector3.Distance(pelvisW, spineW);
            float sideLen = Vector3.Distance(leftRefW, rightRefW);
            frame.ScaleUp = Mathf.Clamp(upLen / ReferenceBodyUp, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleSide = Mathf.Clamp(sideLen / ReferenceBodySide, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleFwd = Mathf.Clamp((frame.ScaleUp + frame.ScaleSide) * 0.5f, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.ScaleGeneral = Mathf.Clamp((frame.ScaleUp + frame.ScaleSide + frame.ScaleFwd) / 3f, MinBodyRelativeScale, MaxBodyRelativeScale);
            frame.Center = worldBase + right * (OffsetSide * frame.ScaleSide);
            return true;
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

        static Vector3 GetMatrixPosition(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        static bool IsLeftLateralRefBone(string name)
        {
            return name == "Bip01 L Thigh_SCL_"
                || name == "Bip01 L Thigh"
                || name == "Hip_L"
                || name == "Hip_L_nub"
                || name == "momotwist_L"
                || name == "momotwist2_L"
                || name == "momoniku_L";
        }

        static bool IsRightLateralRefBone(string name)
        {
            return name == "Bip01 R Thigh_SCL_"
                || name == "Bip01 R Thigh"
                || name == "Hip_R"
                || name == "Hip_R_nub"
                || name == "momotwist_R"
                || name == "momotwist2_R"
                || name == "momoniku_R";
        }

        static bool TryBuildBindPoseSkinMatrices(SkinnedMeshRenderer smr, out Matrix4x4[] boneMatrices)
        {
            boneMatrices = null;
            if (!_bpWorldCached) return false;
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
            if (bones == null || bindposes == null || bones.Length == 0 || bindposes.Length == 0) return false;

            int count = Mathf.Min(bones.Length, bindposes.Length);
            boneMatrices = new Matrix4x4[count];
            Matrix4x4 meshToBodyOffset = Matrix4x4.identity;
            bool hasOffset = TryGetMeshToBodyBindPoseOffset(smr, out meshToBodyOffset);
            bool hasAnyUsableBone = false;

            Matrix4x4 smrL2W = smr.transform.localToWorldMatrix;
            for (int i = 0; i < count; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    boneMatrices[i] = smrL2W;
                    continue;
                }

                Matrix4x4 unifiedBoneWorld;
                if (_bpBoneWorld.TryGetValue(bone.name, out unifiedBoneWorld))
                {
                    hasAnyUsableBone = true;
                }
                else
                {
                    Matrix4x4 meshBoneWorld = smrL2W * bindposes[i].inverse;
                    unifiedBoneWorld = hasOffset ? meshToBodyOffset * meshBoneWorld : meshBoneWorld;
                }

                boneMatrices[i] = unifiedBoneWorld * bindposes[i];
            }

            return hasAnyUsableBone || hasOffset;
        }

        static bool TryBuildNavelAccessorySkinMatrices(SkinnedMeshRenderer smr, out Matrix4x4[] boneMatrices)
        {
            if (TryBuildBindPoseSkinMatrices(smr, out boneMatrices))
                return true;

            boneMatrices = null;
            if (smr == null || smr.sharedMesh == null)
                return false;

            int matrixCount = GetRequiredBoneMatrixCount(smr.sharedMesh.boneWeights);
            if (matrixCount <= 0)
            {
                Transform[] bones = smr.bones;
                matrixCount = bones != null && bones.Length > 0 ? bones.Length : 1;
            }

            Matrix4x4 fallback = smr.transform != null ? smr.transform.localToWorldMatrix : Matrix4x4.identity;
            boneMatrices = new Matrix4x4[matrixCount];
            for (int i = 0; i < boneMatrices.Length; i++)
                boneMatrices[i] = fallback;
            return true;
        }

        static int GetRequiredBoneMatrixCount(BoneWeight[] weights)
        {
            if (weights == null || weights.Length == 0)
                return 0;

            int maxIndex = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                AddRequiredBoneIndex(ref maxIndex, weights[i].boneIndex0, weights[i].weight0);
                AddRequiredBoneIndex(ref maxIndex, weights[i].boneIndex1, weights[i].weight1);
                AddRequiredBoneIndex(ref maxIndex, weights[i].boneIndex2, weights[i].weight2);
                AddRequiredBoneIndex(ref maxIndex, weights[i].boneIndex3, weights[i].weight3);
            }

            return maxIndex + 1;
        }

        static void AddRequiredBoneIndex(ref int maxIndex, int index, float weight)
        {
            if (weight > 0f && index > maxIndex)
                maxIndex = index;
        }

        static bool TryGetMeshToBodyBindPoseOffset(SkinnedMeshRenderer smr, out Matrix4x4 offset)
        {
            offset = Matrix4x4.identity;
            if (smr == null || smr.sharedMesh == null) return false;

            Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
            Transform[] bones = smr.bones;
            if (bones == null || bindposes == null) return false;

            Matrix4x4 smrL2W = smr.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                Matrix4x4 bodyBoneWorld;
                if (!_bpBoneWorld.TryGetValue(bone.name, out bodyBoneWorld)) continue;

                Matrix4x4 meshBoneWorld = smrL2W * bindposes[i].inverse;
                offset = bodyBoneWorld * meshBoneWorld.inverse;
                return true;
            }

            return false;
        }

        static Matrix4x4 GetWeightedSkinMatrix(Matrix4x4[] boneMatrices, BoneWeight weight)
        {
            Matrix4x4 result = new Matrix4x4();
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex0, weight.weight0);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex1, weight.weight1);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex2, weight.weight2);
            AddWeightedMatrix(ref result, boneMatrices, weight.boneIndex3, weight.weight3);
            return result;
        }

        static void AddWeightedMatrix(ref Matrix4x4 result, Matrix4x4[] matrices, int index, float weight)
        {
            if (weight <= 0f || matrices == null || index < 0 || index >= matrices.Length) return;

            Matrix4x4 m = matrices[index];
            result.m00 += m.m00 * weight; result.m01 += m.m01 * weight; result.m02 += m.m02 * weight; result.m03 += m.m03 * weight;
            result.m10 += m.m10 * weight; result.m11 += m.m11 * weight; result.m12 += m.m12 * weight; result.m13 += m.m13 * weight;
            result.m20 += m.m20 * weight; result.m21 += m.m21 * weight; result.m22 += m.m22 * weight; result.m23 += m.m23 * weight;
            result.m30 += m.m30 * weight; result.m31 += m.m31 * weight; result.m32 += m.m32 * weight; result.m33 += m.m33 * weight;
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
                rec.OrigVerts = (Vector3[])currentVerts.Clone();
                rec.OrigNormals = (Vector3[])currentNormals.Clone();
            }
            if (refreshBase)
            {
                rec.LastDeltaVerts = null;
                rec.LastMorphTrace = null;
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

            float effectiveProgress = progress;

            Vector3[] newVerts;
            DeformStats stats;
            if (!TryBuildMorphedVertsInBindPoseWorld(
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

            Vector3[] deltaVerts = BuildDeltaVerts(rec.OrigVerts, newVerts);
            // When our delta is already in the mesh (alreadyApplied), newVerts is the correct
            // target directly (computed from the clean OrigVerts base).  Otherwise accumulate
            // onto whatever the game left in currentVerts (e.g. after FixBlendValues body-shape
            // update) so shape-slider changes are not discarded.
            Vector3[] appliedVerts = alreadyApplied
                ? newVerts
                : (AddDeltaVerts(currentVerts, deltaVerts) ?? newVerts);

            rec.LastDeltaVerts = deltaVerts;
            rec.AppliedSignature = ComputeVertexSignature(appliedVerts);
            mesh.vertices = appliedVerts;
            ApplySmoothedNormals(mesh, rec, appliedVerts);
            mesh.RecalculateBounds();

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

        static bool TryRegisterBodyReferenceOnly(Maid maid, SkinnedMeshRenderer smr, float progress)
        {
            if (maid == null || smr == null || smr.sharedMesh == null)
                return false;
            if (ClassifyMesh(smr) != MeshMorphClass.Body)
                return false;

            Mesh mesh = smr.sharedMesh;
            Vector3[] currentVerts = mesh.vertices;
            if (currentVerts == null || currentVerts.Length == 0)
                return false;

            MeshRecord existing = FindRecord(maid, smr);
            bool currentIsApplied = existing != null
                && existing.OrigVerts != null
                && existing.OrigVerts.Length == currentVerts.Length
                && existing.LastDeltaVerts != null
                && existing.LastDeltaVerts.Length == currentVerts.Length
                && existing.AppliedSignature != 0
                && ComputeVertexSignature(currentVerts) == existing.AppliedSignature;

            MeshRecord rec = new MeshRecord
            {
                SMR = smr,
                Mesh = mesh,
                OrigVerts = currentIsApplied ? (Vector3[])existing.OrigVerts.Clone() : (Vector3[])currentVerts.Clone(),
                OrigNormals = currentIsApplied ? CloneVectorArray(existing.OrigNormals) : CloneVectorArray(mesh.normals),
            };

            bool[] mask = BuildVertexMask(smr, rec, MeshMorphClass.Body);
            if (!HasAnyMaskedVertex(mask))
                return false;

            Vector3[] ignoredVerts;
            DeformStats stats;
            bool ok = TryDeformVertsInBindPoseWorld(
                smr,
                rec,
                mask,
                MeshMorphClass.Body,
                GetEffectiveMorphProgress(progress, MeshMorphClass.Body),
                out ignoredVerts,
                out stats);

            if (ok && IsDebugMeshLoggingEnabled())
            {
                _log.LogInfo("[BellyDiag] hidden-body-reference"
                    + $" maid={GetMaidName(maid)}"
                    + $" id={GetMeshId(smr)}"
                    + $" activeSelf={smr.gameObject.activeSelf}"
                    + $" activeInHierarchy={smr.gameObject.activeInHierarchy}"
                    + $" verts={rec.OrigVerts.Length}"
                    + $" ellipsoid={stats.EllipsoidVerts}"
                    + $" moved={stats.NonZeroVerts}"
                    + $" bodyRefs={(_morphReferenceContext != null ? _morphReferenceContext.BodyPoints.Count : 0)}"
                    + $" surfaceTris={(_morphReferenceContext != null ? _morphReferenceContext.SurfaceTriangles.Count : 0)}");
            }

            return ok;
        }

        static bool TryBuildMorphedVertsInBindPoseWorld(
            SkinnedMeshRenderer smr,
            MeshRecord rec,
            bool[] mask,
            MeshMorphClass meshClass,
            float progress,
            out Vector3[] newVerts,
            out DeformStats stats)
        {
            if (meshClass == MeshMorphClass.NavelAccessory)
                return TryDeformNavelAccessoryInBindPoseWorld(smr, rec, mask, progress, out newVerts, out stats);

            return TryDeformVertsInBindPoseWorld(smr, rec, mask, meshClass, progress, out newVerts, out stats);
        }

        static bool TryDeformNavelAccessoryInBindPoseWorld(
            SkinnedMeshRenderer smr,
            MeshRecord rec,
            bool[] mask,
            float progress,
            out Vector3[] newVerts,
            out DeformStats stats)
        {
            newVerts = null;
            stats = new DeformStats();
            if (rec != null)
                rec.LastMorphTrace = null;
            if (!_bpWorldCached) return false;
            if (_morphReferenceContext == null || _morphReferenceContext.SurfaceTriangles.Count == 0) return false;
            if (smr == null || smr.sharedMesh == null || rec == null || rec.OrigVerts == null) return false;

            Mesh mesh = smr.sharedMesh;
            BoneWeight[] weights = mesh.boneWeights;
            if (weights == null || weights.Length != rec.OrigVerts.Length) return false;

            Matrix4x4[] boneMatrices;
            if (!TryBuildNavelAccessorySkinMatrices(smr, out boneMatrices)) return false;

            NavelAccessoryReference reference;
            if (!TryBuildNavelAccessoryReference(mesh, rec.OrigVerts, weights, boneMatrices, out reference))
                return false;

            if (progress <= 0.00001f)
            {
                newVerts = (Vector3[])rec.OrigVerts.Clone();
                rec.LastNavelReferenceTriangle = reference.Triangle;
                rec.LastNavelReferenceA = reference.A;
                rec.LastNavelReferenceB = reference.B;
                rec.LastNavelReferenceC = reference.C;
                rec.LastNavelReferenceReason = "zero-progress";
                return true;
            }

            Vector3 targetA;
            Vector3 targetB;
            Vector3 targetC;
            if (!TryGetBodySurfaceClothTarget(_morphReferenceContext, GetNavelReferenceWorld(reference, 0), 1f, out targetA)
                || !TryGetBodySurfaceClothTarget(_morphReferenceContext, GetNavelReferenceWorld(reference, 1), 1f, out targetB)
                || !TryGetBodySurfaceClothTarget(_morphReferenceContext, GetNavelReferenceWorld(reference, 2), 1f, out targetC))
            {
                return false;
            }

            Vector3 referenceDelta =
                ((targetA - reference.OriginalAWorld)
                + (targetB - reference.OriginalBWorld)
                + (targetC - reference.OriginalCWorld)) / 3f;
            if (!IsFinite(referenceDelta))
                return false;

            reference.OriginalCenterWorld = (reference.OriginalAWorld + reference.OriginalBWorld + reference.OriginalCWorld) / 3f;
            reference.TargetCenterWorld = reference.OriginalCenterWorld + referenceDelta;
            reference.DeltaRotation = Quaternion.identity;
            rec.LastNavelReferenceTriangle = reference.Triangle;
            rec.LastNavelReferenceA = reference.A;
            rec.LastNavelReferenceB = reference.B;
            rec.LastNavelReferenceC = reference.C;
            rec.LastNavelReferenceReason = (reference.Reason ?? string.Empty) + ":translation";

            newVerts = (Vector3[])rec.OrigVerts.Clone();
            bool trackStats = IsDebugMeshLoggingEnabled();
            int count = rec.OrigVerts.Length;
            for (int i = 0; i < count; i++)
            {
                if (mask != null && (i >= mask.Length || !mask[i])) continue;
                if (trackStats) stats.MaskedVerts++;
                if (!HasValidBoneWeight(weights[i], boneMatrices)) continue;

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                Vector3 originalWorld = skin.MultiplyPoint3x4(rec.OrigVerts[i]);
                Vector3 targetWorld = originalWorld + referenceDelta;
                Vector3 local = skin.inverse.MultiplyPoint3x4(targetWorld);
                if (!IsFinite(local)) continue;

                newVerts[i] = local;
                float moved = (local - rec.OrigVerts[i]).magnitude;
                if (moved > 0.00001f)
                {
                    stats.NonZeroVerts++;
                    if (moved > stats.MaxDelta) stats.MaxDelta = moved;
                }
            }

            stats.EllipsoidVerts = 3;
            stats.MaxStrength = 1f;
            return true;
        }

        static Vector3 GetNavelReferenceWorld(NavelAccessoryReference reference, int index)
        {
            if (index == 0) return reference.OriginalAWorld;
            if (index == 1) return reference.OriginalBWorld;
            return reference.OriginalCWorld;
        }

        static bool TryBuildNavelAccessoryReference(
            Mesh mesh,
            Vector3[] localVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            out NavelAccessoryReference reference)
        {
            reference = new NavelAccessoryReference
            {
                Triangle = -1,
                A = -1,
                B = -1,
                C = -1,
            };
            if (!TrySelectNavelReferenceTriangle(mesh, localVerts, out reference))
                return false;

            Vector3 aWorld;
            Vector3 bWorld;
            Vector3 cWorld;
            if (!TryGetBindPoseSkinnedWorld(localVerts, weights, boneMatrices, reference.A, out aWorld)
                || !TryGetBindPoseSkinnedWorld(localVerts, weights, boneMatrices, reference.B, out bWorld)
                || !TryGetBindPoseSkinnedWorld(localVerts, weights, boneMatrices, reference.C, out cWorld))
            {
                return false;
            }

            reference.OriginalAWorld = aWorld;
            reference.OriginalBWorld = bWorld;
            reference.OriginalCWorld = cWorld;
            return true;
        }

        static bool TrySelectNavelReferenceTriangle(
            Mesh mesh,
            Vector3[] localVerts,
            out NavelAccessoryReference reference)
        {
            reference = new NavelAccessoryReference
            {
                Triangle = -1,
                A = -1,
                B = -1,
                C = -1,
                Score = float.MaxValue,
                Reason = "not-found",
            };
            if (mesh == null || localVerts == null || localVerts.Length == 0)
                return false;

            int[] triangles;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return false;
            }
            if (triangles == null || triangles.Length < 3)
                return false;

            bool found = false;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t];
                int b = triangles[t + 1];
                int c = triangles[t + 2];
                if (!IsValidVertexIndex(a, localVerts.Length)
                    || !IsValidVertexIndex(b, localVerts.Length)
                    || !IsValidVertexIndex(c, localVerts.Length))
                {
                    continue;
                }

                Vector3 va = localVerts[a];
                Vector3 vb = localVerts[b];
                Vector3 vc = localVerts[c];
                if (!IsFinite(va) || !IsFinite(vb) || !IsFinite(vc))
                    continue;

                Vector3 normal = Vector3.Cross(vb - va, vc - va);
                if (normal.sqrMagnitude <= 1e-14f)
                    continue;

                Vector3 center = (va + vb + vc) / 3f;
                float minX = Mathf.Min(va.x, Mathf.Min(vb.x, vc.x));
                float maxX = Mathf.Max(va.x, Mathf.Max(vb.x, vc.x));
                bool spansOriginX = minX <= 0f && maxX >= 0f;
                float minAbsX = Mathf.Min(Mathf.Abs(va.x), Mathf.Min(Mathf.Abs(vb.x), Mathf.Abs(vc.x)));
                float score = center.sqrMagnitude;
                if (!spansOriginX)
                    score += 0.0001f + minAbsX * minAbsX;

                if (found && score >= reference.Score)
                    continue;

                found = true;
                reference.Triangle = t / 3;
                reference.A = a;
                reference.B = b;
                reference.C = c;
                reference.Score = score;
                reference.Reason = spansOriginX ? "origin-x-span" : "origin-nearest";
            }

            return found;
        }

        static bool IsValidVertexIndex(int index, int count)
        {
            return index >= 0 && index < count;
        }

        static bool TryGetBindPoseSkinnedWorld(
            Vector3[] localVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            int index,
            out Vector3 world)
        {
            world = Vector3.zero;
            if (localVerts == null || weights == null || boneMatrices == null)
                return false;
            if (index < 0 || index >= localVerts.Length || index >= weights.Length)
                return false;
            if (!HasValidBoneWeight(weights[index], boneMatrices))
                return false;

            Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[index]);
            world = skin.MultiplyPoint3x4(localVerts[index]);
            return IsFinite(world);
        }

        static bool TryBuildRigidTrianglePose(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out Vector3 center,
            out Quaternion rotation,
            out string reason)
        {
            center = (a + b + c) / 3f;
            rotation = Quaternion.identity;
            reason = string.Empty;

            Vector3 x = b - a;
            if (x.sqrMagnitude <= 1e-14f)
                x = c - a;
            if (x.sqrMagnitude <= 1e-14f)
            {
                reason = "navel-frame:x-zero";
                return false;
            }

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= 1e-14f)
            {
                reason = "navel-frame:normal-zero";
                return false;
            }

            Vector3 forward = normal.normalized;
            x.Normalize();
            Vector3 up = Vector3.Cross(forward, x);
            if (up.sqrMagnitude <= 1e-14f)
            {
                up = Vector3.Cross(forward, Vector3.up);
                if (up.sqrMagnitude <= 1e-14f)
                    up = Vector3.Cross(forward, Vector3.right);
            }
            if (up.sqrMagnitude <= 1e-14f)
            {
                reason = "navel-frame:up-zero";
                return false;
            }

            rotation = Quaternion.LookRotation(forward, up.normalized);
            if (!IsFinite(rotation))
            {
                reason = "navel-frame:rotation-nonfinite";
                return false;
            }

            return true;
        }

        static bool TryDeformVertsInBindPoseWorld(
            SkinnedMeshRenderer smr,
            MeshRecord rec,
            bool[] mask,
            MeshMorphClass meshClass,
            float progress,
            out Vector3[] newVerts,
            out DeformStats stats)
        {
            newVerts = null;
            stats = new DeformStats();
            if (rec != null)
                rec.LastMorphTrace = null;
            if (!_bpWorldCached) return false;
            if (smr == null || smr.sharedMesh == null || rec == null || rec.OrigVerts == null) return false;

            bool trackStats = IsDebugMeshLoggingEnabled();

            BoneWeight[] weights = smr.sharedMesh.boneWeights;
            if (weights == null || weights.Length != rec.OrigVerts.Length) return false;
            Transform[] bones = smr.bones;

            Matrix4x4[] boneMatrices;
            if (!TryBuildBindPoseSkinMatrices(smr, out boneMatrices)) return false;

            float radiusScale = Mathf.Max(0.05f, 1f + InflationMultiplier);
            float radiusSide = Mathf.Max(RelSide(RegionRadiusSide) * radiusScale, RelGeneral(0.0001f));
            float radiusFront = Mathf.Max(RelFwd(RegionRadiusFront) * radiusScale, RelGeneral(0.0001f));
            float radiusBack = Mathf.Max(RelFwd(RegionRadiusBack) * radiusScale, RelGeneral(0.0001f));
            float radiusUp = Mathf.Max(RelUp(RegionRadiusUp) * radiusScale, RelGeneral(0.0001f));
            float radiusDown = Mathf.Max(RelUp(RegionRadiusDown) * radiusScale, RelGeneral(0.0001f));
            Vector3 worldCenter = _bpWorldFrame.Center;
            Vector3 worldFwd = _bpWorldFrame.Fwd;
            Vector3 worldUp = _bpWorldFrame.Up;
            Vector3 worldRight = _bpWorldFrame.Right;
            if (worldRight.sqrMagnitude < 1e-8f)
                worldRight = Vector3.Cross(worldUp, worldFwd).normalized;
            if (worldRight.sqrMagnitude < 1e-8f) worldRight = Vector3.right;
            Vector3 regionCenter = worldCenter + worldUp * RelUp(InflationMoveY) + worldFwd * RelFwd(InflationMoveZ);
            bool trackOuterClothWorld = true;
            VertexMorphTrace[] morphTrace = meshClass == MeshMorphClass.OuterCloth ? new VertexMorphTrace[rec.OrigVerts.Length] : null;
            rec.LastMorphTrace = morphTrace;
            Vector3[] clothOriginalWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            Vector3[] clothMorphedWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            bool[] clothValid = trackOuterClothWorld ? new bool[rec.OrigVerts.Length] : null;
            bool[] surfaceTransformEligible = trackOuterClothWorld ? new bool[rec.OrigVerts.Length] : null;
            float[] thighGuardSmoothMask = ThighGuardSmoothStrength > 0f ? new float[rec.OrigVerts.Length] : null;
            bool[] oldDirectAffected = new bool[rec.OrigVerts.Length];

            newVerts = new Vector3[rec.OrigVerts.Length];
            for (int i = 0; i < rec.OrigVerts.Length; i++)
            {
                Vector3 vert = rec.OrigVerts[i];
                newVerts[i] = vert;

                VertexMorphTrace trace = null;
                if (morphTrace != null)
                {
                    trace = new VertexMorphTrace
                    {
                        EffectiveProgress = progress,
                        RadiusScale = radiusScale,
                        RadiusSide = radiusSide,
                        RadiusFront = radiusFront,
                        RadiusBack = radiusBack,
                        RadiusUp = radiusUp,
                        RadiusDown = radiusDown,
                        StopReason = "not-processed",
                    };
                    morphTrace[i] = trace;
                }

                if (mask != null && (i >= mask.Length || !mask[i]))
                {
                    if (trace != null) trace.StopReason = "masked-out";
                    continue;
                }
                if (trace != null) trace.Masked = true;
                if (trackStats) stats.MaskedVerts++;

                BoneWeight weight = weights[i];
                if (trace != null) FillTraceBoneInfo(trace, weight, bones);
                if (!HasValidBoneWeight(weight, boneMatrices))
                {
                    if (trace != null) trace.StopReason = "invalid-skin";
                    continue;
                }
                if (trace != null) trace.SkinOk = true;

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weight);
                Vector3 worldVert = skin.MultiplyPoint3x4(vert);
                if (trackOuterClothWorld)
                {
                    clothOriginalWorld[i] = worldVert;
                    clothMorphedWorld[i] = worldVert;
                    clothValid[i] = true;
                }

                Vector3 regionDelta = worldVert - regionCenter;
                float upDot = Vector3.Dot(regionDelta, worldUp);
                float sideDot = Vector3.Dot(regionDelta, worldRight);
                float fwdDot = Vector3.Dot(regionDelta, worldFwd);
                float fwdRadius = fwdDot >= 0f ? radiusFront : radiusBack;
                float upRadius = upDot >= 0f ? radiusUp : radiusDown;
                float ellip = (sideDot / radiusSide) * (sideDot / radiusSide)
                    + (fwdDot / fwdRadius) * (fwdDot / fwdRadius)
                    + (upDot / upRadius) * (upDot / upRadius);
                if (trace != null)
                {
                    trace.OrigSide = sideDot;
                    trace.OrigUp = upDot;
                    trace.OrigFwd = fwdDot;
                    trace.FwdRadius = fwdRadius;
                    trace.UpRadius = upRadius;
                    trace.Ellip = ellip;
                    InitializeTraceFwd(trace, fwdDot);
                }
                if (ellip >= 1f)
                {
                    if (trace != null) trace.StopReason = "outside-ellipsoid";
                    continue;
                }
                if (trace != null) trace.InEllipsoid = true;

                if (trackStats) stats.EllipsoidVerts++;

                float edgeRatio = Mathf.Sqrt(ellip);
                float edgeFade = 1f - BellyEdgeCurve(edgeRatio);
                float topEdgeStrength = GetTopEdgeStrength(
                    upDot,
                    radiusUp);
                float shapeWeight = Mathf.Clamp01(progress * edgeFade * topEdgeStrength * BasePushOut);
                if (trace != null)
                {
                    trace.EdgeRatio = edgeRatio;
                    trace.EdgeFade = edgeFade;
                    trace.TopEdgeStrength = topEdgeStrength;
                    trace.ShapeWeight = shapeWeight;
                }

                float dist = regionDelta.magnitude;
                if (dist < 1e-6f)
                {
                    regionDelta = worldFwd;
                    dist = 1f;
                }

                float directionalRadius = dist / Mathf.Max(Mathf.Sqrt(ellip), 0.0001f);
                float morphWeight = shapeWeight;
                if (trace != null)
                {
                    trace.OriginalRadius = dist;
                    trace.DirectionalRadius = directionalRadius;
                    trace.MorphWeight = morphWeight;
                }
                if (morphWeight <= 0f)
                {
                    if (trace != null) trace.StopReason = "zero-morph-weight";
                    continue;
                }
                if (trace != null)
                {
                    trace.MorphApplied = true;
                    trace.StopReason = "ok";
                }
                oldDirectAffected[i] = true;

                if (trackStats && morphWeight > stats.MaxStrength) stats.MaxStrength = morphWeight;

                Vector3 sphereTarget = regionCenter + regionDelta.normalized * directionalRadius;
                Vector3 newWorldVert = Vector3.Lerp(worldVert, sphereTarget, morphWeight);
                if (trace != null) trace.FwdSphere = TraceFwd(newWorldVert, regionCenter, worldFwd);
                newWorldVert = SculptBaseShapeWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    radiusSide,
                    radiusUp,
                    radiusDown);
                if (trace != null) trace.FwdSculpt = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationShiftY != 0f)
                {
                    float upLimit = upDot >= 0f ? radiusUp : radiusDown;
                    float centerYFade = Mathf.Clamp01(1f - Mathf.Abs(upDot) / Mathf.Max(upLimit * 1.8f, 0.0001f));
                    float sideLimit = Mathf.Clamp01(1f - Mathf.Abs(sideDot) / Mathf.Max(radiusSide * 3f, 0.0001f));
                    newWorldVert += worldUp * (RelUp(InflationShiftY) * centerYFade * sideLimit * morphWeight);
                }

                if (InflationShiftZ != 0f)
                {
                    float frontFade = Mathf.Clamp01(fwdDot / Mathf.Max(radiusFront * 2f, 0.0001f));
                    newWorldVert += worldFwd * (RelFwd(InflationShiftZ) * frontFade * morphWeight);
                }
                if (trace != null) trace.FwdShift = TraceFwd(newWorldVert, regionCenter, worldFwd);

                float sx = Mathf.Max(0.05f, 1f + InflationStretchX * morphWeight);
                float sy = Mathf.Max(0.05f, 1f + InflationStretchY * morphWeight);
                float sz = Mathf.Max(0.05f, 1f + InflationStretchZ * morphWeight);
                if (sx != 1f || sy != 1f || sz != 1f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relSide = Vector3.Dot(rel, worldRight) * sx;
                    float relUp = Vector3.Dot(rel, worldUp) * sy;
                    float relFwd = Vector3.Dot(rel, worldFwd) * sz;
                    newWorldVert = regionCenter + worldRight * relSide + worldUp * relUp + worldFwd * relFwd;
                }
                if (trace != null) trace.FwdStretch = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationRoundness != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relFwd = Vector3.Dot(rel, worldFwd);
                    float roundFade = Mathf.Clamp01(relFwd / Mathf.Max(radiusFront, 0.0001f));
                    Vector3 roundCenter = regionCenter + worldFwd * (radiusFront / 3f);
                    Vector3 roundDir = newWorldVert - roundCenter;
                    if (roundDir.sqrMagnitude < 1e-6f) roundDir = worldFwd;
                    newWorldVert += roundDir.normalized * (RelGeneral(InflationRoundness) * roundFade * edgeFade * progress);
                }
                if (trace != null) trace.FwdRoundness = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationTaperY != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relSide = Vector3.Dot(rel, worldRight);
                    float relUpRadius = relUp >= 0f ? radiusUp : radiusDown;
                    float taper = RelSide(InflationTaperY)
                        * Mathf.Clamp01(Mathf.Abs(relUp) / Mathf.Max(relUpRadius, 0.0001f))
                        * Mathf.Clamp01(Mathf.Abs(relSide) / Mathf.Max(radiusSide, 0.0001f))
                        * morphWeight;
                    if (relSide < 0f) taper = -taper;
                    if (relUp < 0f) taper = -taper;
                    newWorldVert += worldRight * taper;
                }
                if (trace != null) trace.FwdTaperY = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationTaperZ != 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relFwd = Vector3.Dot(rel, worldFwd);
                    float relUpRadius = relUp >= 0f ? radiusUp : radiusDown;
                    float taper = RelFwd(InflationTaperZ)
                        * Mathf.Clamp01(Mathf.Abs(relUp) / Mathf.Max(relUpRadius, 0.0001f))
                        * Mathf.Clamp01((relFwd + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f))
                        * morphWeight;
                    if (relUp < 0f) taper = -taper;
                    newWorldVert += worldFwd * taper;
                }
                if (trace != null) trace.FwdTaperZ = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationFatFold > 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float relUp = Vector3.Dot(rel, worldUp);
                    float relSide = Vector3.Dot(rel, worldRight);
                    float foldCenter = InflationFatFoldHeight * radiusUp;
                    float foldDist = Mathf.Abs(relUp - foldCenter);
                    float foldFade = 1f - BellyGapCurve(foldDist / Mathf.Max(radiusUp, 0.0001f));
                    if (foldFade > 0f)
                    {
                        float foldPull = Mathf.Clamp01(InflationFatFold * foldFade);
                        newWorldVert = Vector3.Lerp(newWorldVert, worldVert, foldPull);

                        if (InflationFatFoldGap != 0f)
                        {
                            float sideLimit = Mathf.Clamp01(1f - Mathf.Abs(relSide) / Mathf.Max(radiusSide, 0.0001f));
                            float gapDir = relUp >= foldCenter ? 1f : -1f;
                            newWorldVert += worldUp * (InflationFatFoldGap * gapDir * radiusUp * 0.35f * foldFade * sideLimit * progress);
                        }
                    }
                }
                if (trace != null) trace.FwdFatFold = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (InflationDrop > 0f)
                {
                    Vector3 rel = newWorldVert - regionCenter;
                    float frontFade = Mathf.Clamp01(Vector3.Dot(rel, worldFwd) / Mathf.Max(radiusFront * 1.5f, 0.0001f));
                    newWorldVert -= worldUp * (radiusFront * InflationDrop * frontFade * morphWeight);
                }
                if (trace != null) trace.FwdDrop = TraceFwd(newWorldVert, regionCenter, worldFwd);

                newWorldVert = RoundToSidesWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldFwd,
                    radiusBack,
                    radiusFront);
                if (trace != null) trace.FwdSideSmooth = TraceFwd(newWorldVert, regionCenter, worldFwd);

                newWorldVert = ReduceRibStretchingWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldUp,
                    worldFwd,
                    radiusUp);
                if (trace != null) trace.FwdRibReduce = TraceFwd(newWorldVert, regionCenter, worldFwd);

                float thighRestore = LowerBodyRestoreMask(
                    upDot,
                    sideDot,
                    fwdDot,
                    radiusDown,
                    radiusSide,
                    radiusBack,
                    radiusFront);
                if (trace != null) trace.LowerBodyRestore = thighRestore;
                if (thighRestore > 0f)
                {
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, thighRestore);
                    if (thighGuardSmoothMask != null)
                        thighGuardSmoothMask[i] = Mathf.Max(thighGuardSmoothMask[i], thighRestore);
                }
                if (trace != null) trace.FwdLowerRestore = TraceFwd(newWorldVert, regionCenter, worldFwd);

                Vector3 originalRel = worldVert - regionCenter;
                Vector3 finalRel = newWorldVert - regionCenter;
                float originalCoreSide = Vector3.Dot(originalRel, worldRight);
                float originalCoreFwd = Vector3.Dot(originalRel, worldFwd);
                float finalCoreSide = Vector3.Dot(finalRel, worldRight);
                float finalCoreFwd = Vector3.Dot(finalRel, worldFwd);
                float originalCoreDist = Mathf.Sqrt(originalCoreSide * originalCoreSide + originalCoreFwd * originalCoreFwd);
                float finalCoreDist = Mathf.Sqrt(finalCoreSide * finalCoreSide + finalCoreFwd * finalCoreFwd);
                float beforeInwardGuardFwd = trace != null ? finalCoreFwd : 0f;
                if (finalCoreDist < originalCoreDist)
                {
                    float finalUp = Vector3.Dot(finalRel, worldUp);
                    newWorldVert = regionCenter + worldRight * originalCoreSide + worldUp * finalUp + worldFwd * originalCoreFwd;
                    finalRel = newWorldVert - regionCenter;
                    finalCoreFwd = originalCoreFwd;
                    if (trace != null) trace.InwardGuardApplied = true;
                }
                if (trace != null)
                {
                    trace.InwardGuardDeltaFwd = finalCoreFwd - beforeInwardGuardFwd;
                    trace.FwdInwardGuard = finalCoreFwd;
                }

                float breastRestore = BreastRestoreMask(weight, bones);
                if (breastRestore <= 0f)
                    breastRestore = OuterClothBreastRestoreMask(
                        meshClass,
                        upDot,
                        fwdDot,
                        radiusUp,
                        radiusBack,
                        radiusFront);
                if (trace != null) trace.BreastRestore = breastRestore;
                if (breastRestore > 0f)
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, breastRestore);
                if (trace != null) trace.FwdBreastRestore = TraceFwd(newWorldVert, regionCenter, worldFwd);

                float innerThighRestore = InnerThighRestoreMask(weight, bones);
                if (trace != null) trace.InnerThighRestore = innerThighRestore;
                if (innerThighRestore > 0f)
                {
                    newWorldVert = Vector3.Lerp(newWorldVert, worldVert, innerThighRestore);
                    if (thighGuardSmoothMask != null)
                        thighGuardSmoothMask[i] = Mathf.Max(thighGuardSmoothMask[i], innerThighRestore);
                }
                if (trace != null) trace.FwdInnerThighRestore = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (trace != null)
                {
                    trace.BottomTaperInfluence = GetBottomEdgeTaperInfluence(
                        upDot,
                        fwdDot,
                        radiusDown,
                        radiusBack,
                        radiusFront,
                        morphWeight,
                        Mathf.Max(thighRestore, innerThighRestore),
                        out trace.BottomTaperLower,
                        out trace.BottomTaperFront,
                        out trace.BottomTaperGuardKeep);
                }
                newWorldVert = ApplyBottomEdgeTaperWorld(
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    worldUp,
                    worldFwd,
                    upDot,
                    fwdDot,
                    radiusDown,
                    radiusBack,
                    radiusFront,
                    morphWeight,
                    Mathf.Max(thighRestore, innerThighRestore));
                if (trace != null) trace.FwdBottomTaper = TraceFwd(newWorldVert, regionCenter, worldFwd);

                if (trackOuterClothWorld)
                {
                    clothMorphedWorld[i] = newWorldVert;
                    surfaceTransformEligible[i] = (newWorldVert - worldVert).sqrMagnitude > 1e-10f;
                }

                Vector3 newLocalVert = skin.inverse.MultiplyPoint3x4(newWorldVert);
                if (trackStats)
                {
                    float moved = (newLocalVert - vert).magnitude;
                    if (moved > 0.00001f)
                    {
                        stats.NonZeroVerts++;
                        if (moved > stats.MaxDelta) stats.MaxDelta = moved;
                    }
                }

                newVerts[i] = newLocalVert;
            }

            Vector3[] beforeThighSmoothVerts = morphTrace != null && thighGuardSmoothMask != null
                ? (Vector3[])newVerts.Clone()
                : null;
            SmoothThighGuardAffectedVerts(smr.sharedMesh, rec.OrigVerts, newVerts, thighGuardSmoothMask);
            RefreshMorphedWorldFromLocal(newVerts, weights, boneMatrices, clothMorphedWorld, clothValid);
            ExpandTriangleEligibilityFromAnyVertex(smr.sharedMesh, surfaceTransformEligible, clothValid);
            bool[] oldFinalChanged = BuildWorldChangedMask(clothOriginalWorld, clothMorphedWorld, clothValid);

            if (meshClass == MeshMorphClass.Body)
            {
                RegisterBodyMorphReferences(
                    smr.sharedMesh,
                    clothOriginalWorld,
                    clothMorphedWorld,
                    clothValid,
                    oldDirectAffected,
                    worldCenter,
                    worldUp);
            }
            else
            {
                ApplyBodySurfaceTransformToCloth(
                    smr.sharedMesh,
                    rec.OrigVerts,
                    weights,
                    boneMatrices,
                    clothOriginalWorld,
                    clothMorphedWorld,
                    clothValid,
                    surfaceTransformEligible,
                    newVerts,
                    meshClass,
                    morphTrace,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    ref stats);

                if (meshClass == MeshMorphClass.OuterCloth && IsSkirtLikeMesh(smr))
                {
                    ApplySkirtLowerEdgeFadeToMesh(
                        smr.sharedMesh,
                        rec.OrigVerts,
                        weights,
                        boneMatrices,
                        clothOriginalWorld,
                        clothMorphedWorld,
                        clothValid,
                        surfaceTransformEligible,
                        newVerts,
                        regionCenter,
                        worldRight,
                        worldUp,
                        worldFwd,
                        morphTrace,
                        ref stats);
                }
            }
            if (morphTrace != null)
                CaptureTraceFinalLocalFwd(morphTrace, newVerts, beforeThighSmoothVerts, weights, boneMatrices, regionCenter, worldFwd);

            return true;
        }

        static void ApplyNearestBodyDisplacementToCloth(
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorld,
            bool[] valid,
            Vector3[] newLocalVerts,
            ref DeformStats stats)
        {
            if (_morphReferenceContext == null || _morphReferenceContext.BodyPoints.Count == 0)
                return;
            if (localOriginalVerts == null || weights == null || boneMatrices == null
                || originalWorld == null || valid == null || newLocalVerts == null)
                return;

            bool trackStats = IsDebugMeshLoggingEnabled();
            var bodyPoints = _morphReferenceContext.BodyPoints;
            int count = Mathf.Min(newLocalVerts.Length,
                Mathf.Min(localOriginalVerts.Length,
                Mathf.Min(weights.Length,
                Mathf.Min(originalWorld.Length, valid.Length))));

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;

                Vector3 clothOrig = originalWorld[i];

                int nearest = -1;
                float bestSqrDist = float.MaxValue;
                for (int j = 0; j < bodyPoints.Count; j++)
                {
                    float sqd = (bodyPoints[j].OriginalWorld - clothOrig).sqrMagnitude;
                    if (sqd >= bestSqrDist) continue;
                    bestSqrDist = sqd;
                    nearest = j;
                }
                if (nearest < 0) continue;

                BodyReferencePoint refPoint = bodyPoints[nearest];
                float dist = Mathf.Sqrt(bestSqrDist);
                Vector3 bodyOrigWorld = refPoint.OriginalWorld;
                Vector3 bodyMorphedWorld = refPoint.Mesh.MorphedWorld[refPoint.Index];
                Vector3 bodyDelta = bodyMorphedWorld - bodyOrigWorld;
                Vector3 bodyDir = bodyDelta.sqrMagnitude > 1e-12f ? bodyDelta.normalized : Vector3.zero;

                Vector3 newClothWorld = clothOrig + bodyDelta + bodyDir * dist;
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                Vector3 newLocal = skin.inverse.MultiplyPoint3x4(newClothWorld);
                newLocalVerts[i] = newLocal;

                if (trackStats)
                {
                    float moved = (newLocal - localOriginalVerts[i]).magnitude;
                    if (moved > stats.MaxDelta) stats.MaxDelta = moved;
                }
            }
        }

        // Selects one body surface triangle per cloth triangle (via centroid) and records
        // the per-vertex closest-point hit for that triangle. Adjacent vertices in the same
        // cloth triangle always share the same body reference, eliminating intra-triangle
        // tears caused by per-vertex independent nearest-triangle selection.
        static BodySurfaceHit[] BuildClothTriangleBodyHits(
            Mesh clothMesh,
            Vector3[] originalWorld,
            bool[] valid,
            bool[] surfaceEligible,
            int count)
        {
            BodySurfaceHit[] result = new BodySurfaceHit[count];
            for (int i = 0; i < count; i++)
                result[i] = new BodySurfaceHit { TriangleIndex = -1 };

            if (_morphReferenceContext == null || clothMesh == null) return result;

            int[] clothTris = null;
            try { clothTris = clothMesh.triangles; } catch { }
            if (clothTris == null || clothTris.Length < 3) return result;

            float[] bestDistSq = new float[count];
            for (int i = 0; i < count; i++)
                bestDistSq[i] = float.MaxValue;

            for (int t = 0; t + 2 < clothTris.Length; t += 3)
            {
                int a = clothTris[t], b = clothTris[t + 1], c = clothTris[t + 2];
                if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) continue;

                bool anyEligible = (valid[a] && surfaceEligible[a])
                    || (valid[b] && surfaceEligible[b])
                    || (valid[c] && surfaceEligible[c]);
                if (!anyEligible) continue;

                // Centroid from valid vertices
                Vector3 centroid = Vector3.zero;
                int validCount = 0;
                if (valid[a]) { centroid += originalWorld[a]; validCount++; }
                if (valid[b]) { centroid += originalWorld[b]; validCount++; }
                if (valid[c]) { centroid += originalWorld[c]; validCount++; }
                if (validCount == 0) continue;
                centroid *= (1f / validCount);

                BodySurfaceHit centHit;
                if (!TryFindNearestBodySurfaceTriangle(_morphReferenceContext, centroid, out centHit)) continue;
                if (centHit.TriangleIndex < 0) continue;

                // Assign this body triangle to each vertex, re-evaluating the closest point
                // per vertex so barycentric coords are correct for each individual position.
                TryAssignVertexBodyHit(a, originalWorld, valid, surfaceEligible, centHit.TriangleIndex, count, result, bestDistSq);
                TryAssignVertexBodyHit(b, originalWorld, valid, surfaceEligible, centHit.TriangleIndex, count, result, bestDistSq);
                TryAssignVertexBodyHit(c, originalWorld, valid, surfaceEligible, centHit.TriangleIndex, count, result, bestDistSq);
            }

            return result;
        }

        static void TryAssignVertexBodyHit(
            int vertIdx,
            Vector3[] originalWorld,
            bool[] valid,
            bool[] surfaceEligible,
            int bodyTriIdx,
            int count,
            BodySurfaceHit[] result,
            float[] bestDistSq)
        {
            if (vertIdx < 0 || vertIdx >= count) return;
            if (!valid[vertIdx] || !surfaceEligible[vertIdx]) return;
            if (_morphReferenceContext == null) return;
            if (bodyTriIdx < 0 || bodyTriIdx >= _morphReferenceContext.SurfaceTriangles.Count) return;

            BodySurfaceTriangle bodyTri = _morphReferenceContext.SurfaceTriangles[bodyTriIdx];
            BodyReferenceMesh mesh = bodyTri.Mesh;
            if (mesh == null || mesh.OriginalWorld == null) return;
            if (!IsValidBodySurfaceVertex(mesh, bodyTri.A)
                || !IsValidBodySurfaceVertex(mesh, bodyTri.B)
                || !IsValidBodySurfaceVertex(mesh, bodyTri.C)) return;

            Vector3 pa = mesh.OriginalWorld[bodyTri.A];
            Vector3 pb = mesh.OriginalWorld[bodyTri.B];
            Vector3 pc = mesh.OriginalWorld[bodyTri.C];

            Vector3 point = originalWorld[vertIdx];
            Vector3 closest, barycentric;
            if (!TryClosestPointOnTriangle(point, pa, pb, pc, out closest, out barycentric)) return;

            float distSq = (point - closest).sqrMagnitude;
            if (distSq >= bestDistSq[vertIdx]) return;

            Vector3 normal = Vector3.Cross(pb - pa, pc - pa);
            float normalLen = normal.magnitude;
            if (normalLen < 1e-9f || !IsFinite(normal)) return;
            normal /= normalLen;

            bestDistSq[vertIdx] = distSq;
            result[vertIdx] = new BodySurfaceHit
            {
                TriangleIndex = bodyTriIdx,
                Closest = closest,
                Barycentric = barycentric,
                Normal = normal,
                DistanceSq = distSq,
            };
        }

        static void ApplyBodySurfaceTransformToCloth(
            Mesh clothMesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorld,
            Vector3[] morphedWorld,
            bool[] valid,
            bool[] surfaceEligible,
            Vector3[] newLocalVerts,
            MeshMorphClass meshClass,
            VertexMorphTrace[] traces,
            Vector3 traceCenter,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            ref DeformStats stats)
        {
            if (_morphReferenceContext == null || _morphReferenceContext.SurfaceTriangles.Count == 0)
                return;
            if (localOriginalVerts == null || weights == null || boneMatrices == null
                || originalWorld == null || morphedWorld == null || valid == null || surfaceEligible == null || newLocalVerts == null)
                return;

            int count = Mathf.Min(newLocalVerts.Length,
                Mathf.Min(localOriginalVerts.Length,
                Mathf.Min(weights.Length,
                Mathf.Min(originalWorld.Length, Mathf.Min(morphedWorld.Length, Mathf.Min(valid.Length, surfaceEligible.Length))))));
            if (count <= 0) return;

            // Pre-assign body surface references per cloth triangle to ensure all 3 vertices
            // of each cloth triangle use the same body reference → no intra-triangle tears.
            BodySurfaceHit[] triBodyHits = BuildClothTriangleBodyHits(
                clothMesh, originalWorld, valid, surfaceEligible, count);

            float multiplier = GetBodySurfaceClothMultiplier(meshClass);
            int applied = 0;
            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                if (!surfaceEligible[i]) continue;
                if (!HasValidBoneWeight(weights[i], boneMatrices)) continue;

                Vector3 targetWorld = Vector3.zero;
                bool transformed = false;

                // Try the triangle-coherent hit first
                if (triBodyHits != null && i < triBodyHits.Length && triBodyHits[i].TriangleIndex >= 0)
                {
                    if (TryBuildBodySurfaceTargetFromHit(
                        _morphReferenceContext, originalWorld[i], multiplier, triBodyHits[i], out targetWorld))
                    {
                        _morphReferenceContext.SurfaceQueries++;
                        _morphReferenceContext.SurfaceHits++;
                        transformed = true;
                    }
                }

                // Fall back to per-vertex nearest if the triangle-coherent hit failed
                if (!transformed)
                {
                    if (!TryGetBodySurfaceClothTarget(_morphReferenceContext, originalWorld[i], multiplier, out targetWorld))
                        continue;
                    transformed = true;
                }

                Vector3 preWorld = morphedWorld[i];
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                Vector3 local = skin.inverse.MultiplyPoint3x4(targetWorld);
                if (!IsFinite(local)) continue;

                newLocalVerts[i] = local;
                morphedWorld[i] = targetWorld;
                applied++;

                float moved = (local - localOriginalVerts[i]).magnitude;
                if (moved > 0.00001f && moved > stats.MaxDelta)
                    stats.MaxDelta = moved;

                VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                if (trace != null)
                {
                    Vector3 preCoord = ProjectPointCoord(preWorld, traceCenter, right, up, fwd);
                    Vector3 postCoord = ProjectPointCoord(targetWorld, traceCenter, right, up, fwd);
                    Vector3 delta = targetWorld - preWorld;
                    trace.ClothInheritApplied = true;
                    trace.ClothInheritKind = "surface";
                    trace.ClothInheritReason = "surface:applied";
                    trace.ClothInheritPreWorld = preWorld;
                    trace.ClothInheritPostWorld = targetWorld;
                    trace.ClothInheritPreCoord = preCoord;
                    trace.ClothInheritPostCoord = postCoord;
                    trace.ClothInheritDeltaCoord = ProjectVectorCoord(delta, right, up, fwd);
                    trace.ClothInheritDeltaWorldLen = delta.magnitude;
                    trace.FwdClothInherit = postCoord.z;
                    trace.FwdFinal = postCoord.z;
                }
            }

            if (IsDebugMeshLoggingEnabled() && applied > 0)
            {
                _log.LogInfo("[BellyDiag] surface-cloth"
                    + $" class={meshClass}"
                    + $" applied={applied}/{count}"
                    + $" hits={_morphReferenceContext.SurfaceHits}"
                    + $" queries={_morphReferenceContext.SurfaceQueries}"
                    + $" misses={_morphReferenceContext.SurfaceMisses}");
            }
        }

        static void ApplySkirtLowerEdgeFadeToMesh(
            Mesh mesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorld,
            Vector3[] morphedWorld,
            bool[] valid,
            bool[] surfaceEligible,
            Vector3[] newLocalVerts,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            VertexMorphTrace[] traces,
            ref DeformStats stats)
        {
            if (mesh == null || localOriginalVerts == null || weights == null || boneMatrices == null)
                return;
            if (originalWorld == null || morphedWorld == null || valid == null || surfaceEligible == null || newLocalVerts == null)
                return;

            int count = Mathf.Min(newLocalVerts.Length,
                Mathf.Min(localOriginalVerts.Length,
                Mathf.Min(weights.Length,
                Mathf.Min(originalWorld.Length,
                Mathf.Min(morphedWorld.Length,
                Mathf.Min(valid.Length, surfaceEligible.Length))))));
            if (count <= 0) return;

            int[] triangles = null;
            try { triangles = mesh.triangles; }
            catch { return; }
            if (triangles == null || triangles.Length < 3)
                return;

            int triCount = triangles.Length / 3;
            List<int>[] triNeighbors = BuildTriangleNeighborsByEdge(triangles, triCount, count);
            if (triNeighbors == null)
                return;

            int[] triA = new int[triCount];
            int[] triB = new int[triCount];
            int[] triC = new int[triCount];
            bool[] triValid = new bool[triCount];
            bool[] triFront = new bool[triCount];
            bool[] inSkirtRegion = new bool[triCount];
            bool[] assigned = new bool[triCount];
            float[] triUp = new float[triCount];
            float[] triSide = new float[triCount];
            float[] triFwd = new float[triCount];
            float[] triDeltaUp = new float[triCount];
            Vector3[] triDelta = new Vector3[triCount];
            Vector3[] assignedDelta = new Vector3[triCount];

            float minDelta = RelGeneral(SkirtLowerMinDelta);
            float minDeltaSq = minDelta * minDelta;
            float fwdThreshold = RelFwd(SkirtFrontPlaneFwdOffset);
            float lowerHalfLimit = RelUp(0.06f);
            float upEpsilon = RelUp(0.0005f);
            float sideStepTolerance = RelUp(0.012f);

            for (int tri = 0; tri < triCount; tri++)
            {
                int baseIndex = tri * 3;
                int a = triangles[baseIndex];
                int b = triangles[baseIndex + 1];
                int c = triangles[baseIndex + 2];
                triA[tri] = a;
                triB[tri] = b;
                triC[tri] = c;
                if (!IsValidVertexIndex(a, count) || !IsValidVertexIndex(b, count) || !IsValidVertexIndex(c, count))
                    continue;
                if (!valid[a] || !valid[b] || !valid[c])
                    continue;

                Vector3 centerWorld = (originalWorld[a] + originalWorld[b] + originalWorld[c]) / 3f;
                Vector3 rel = centerWorld - center;
                triUp[tri] = Vector3.Dot(rel, up);
                triSide[tri] = Vector3.Dot(rel, right);
                triFwd[tri] = Vector3.Dot(rel, fwd);
                triFront[tri] = triFwd[tri] > fwdThreshold;
                triValid[tri] = triFront[tri] && triUp[tri] <= lowerHalfLimit;

                Vector3 da = morphedWorld[a] - originalWorld[a];
                Vector3 db = morphedWorld[b] - originalWorld[b];
                Vector3 dc = morphedWorld[c] - originalWorld[c];
                triDelta[tri] = (da + db + dc) / 3f;
                triDeltaUp[tri] = Vector3.Dot(triDelta[tri], up);
            }

            Queue<int> queue = new Queue<int>();
            float maxSeedUp = float.MinValue;
            for (int tri = 0; tri < triCount; tri++)
            {
                if (!triValid[tri] || triDelta[tri].sqrMagnitude <= minDeltaSq || triDeltaUp[tri] >= -minDelta)
                    continue;

                if (!HasUpperNonDownSkirtNeighbor(triNeighbors, triValid, triFront, triUp, triDeltaUp, tri, upEpsilon, minDelta))
                    continue;

                inSkirtRegion[tri] = true;
                queue.Enqueue(tri);
                if (triUp[tri] > maxSeedUp)
                    maxSeedUp = triUp[tri];
            }

            if (queue.Count <= 0)
                return;

            while (queue.Count > 0)
            {
                int tri = queue.Dequeue();
                List<int> ns = triNeighbors[tri];
                for (int i = 0; i < ns.Count; i++)
                {
                    int n = ns[i];
                    if (n < 0 || n >= triCount || inSkirtRegion[n]) continue;
                    if (!triValid[n] || !triFront[n]) continue;
                    if (triUp[n] > maxSeedUp + sideStepTolerance) continue;
                    if (triUp[n] > triUp[tri] + sideStepTolerance) continue;

                    inSkirtRegion[n] = true;
                    queue.Enqueue(n);
                }
            }

            List<int> ordered = BuildSkirtFlowOrder(inSkirtRegion, triUp);
            AssignSkirtFlowDeltas(ordered, triNeighbors, triValid, triFront, inSkirtRegion, assigned, triUp, triDelta, assignedDelta, upEpsilon, minDeltaSq);

            if (SeedDisconnectedSkirtFlowRegions(
                triNeighbors,
                triValid,
                triFront,
                inSkirtRegion,
                assigned,
                triUp,
                triSide,
                triFwd,
                triDeltaUp,
                triDelta,
                assignedDelta,
                maxSeedUp,
                minDelta,
                minDeltaSq,
                upEpsilon,
                sideStepTolerance) > 0)
            {
                ordered = BuildSkirtFlowOrder(inSkirtRegion, triUp);
                AssignSkirtFlowDeltas(ordered, triNeighbors, triValid, triFront, inSkirtRegion, assigned, triUp, triDelta, assignedDelta, upEpsilon, minDeltaSq);
            }

            Vector3[] accumDelta = new Vector3[count];
            float[] accumWeight = new float[count];
            int assignedCount = 0;
            for (int tri = 0; tri < triCount; tri++)
            {
                if (!assigned[tri])
                    continue;
                AddSkirtTriangleFadeTarget(triA, triB, triC, valid, tri, assignedDelta[tri], 1f, accumDelta, accumWeight);
                assignedCount++;
            }

            if (assignedCount <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                if (accumWeight[i] <= 0f || !valid[i]) continue;
                if (!HasValidBoneWeight(weights[i], boneMatrices)) continue;

                Vector3 delta = accumDelta[i] / accumWeight[i];
                Vector3 targetWorld = originalWorld[i] + delta;
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                Vector3 local = skin.inverse.MultiplyPoint3x4(targetWorld);
                if (!IsFinite(local)) continue;

                Vector3 before = morphedWorld[i];
                newLocalVerts[i] = local;
                morphedWorld[i] = targetWorld;

                float moved = (local - localOriginalVerts[i]).magnitude;
                if (moved > 0.00001f && moved > stats.MaxDelta)
                    stats.MaxDelta = moved;

                VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                if (trace != null)
                {
                    Vector3 add = targetWorld - before;
                    trace.SkirtLowerEnabled = true;
                    trace.SkirtLowerAddFwd += Vector3.Dot(add, fwd);
                    trace.SkirtLowerReason = "lowerFlow100:applied";
                    trace.FwdSkirtLower = TraceFwd(targetWorld, center, fwd);
                    trace.FwdFinal = trace.FwdSkirtLower;
                }
            }
        }

        static List<int> BuildSkirtFlowOrder(bool[] inSkirtRegion, float[] triUp)
        {
            List<int> ordered = new List<int>();
            if (inSkirtRegion == null || triUp == null)
                return ordered;

            int count = Mathf.Min(inSkirtRegion.Length, triUp.Length);
            for (int tri = 0; tri < count; tri++)
                if (inSkirtRegion[tri])
                    ordered.Add(tri);
            ordered.Sort((a, b) => triUp[b].CompareTo(triUp[a]));
            return ordered;
        }

        static int AssignSkirtFlowDeltas(
            List<int> ordered,
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triFront,
            bool[] inSkirtRegion,
            bool[] assigned,
            float[] triUp,
            Vector3[] triDelta,
            Vector3[] assignedDelta,
            float upEpsilon,
            float minDeltaSq)
        {
            if (ordered == null || assigned == null || assignedDelta == null)
                return 0;

            int assignedNow = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                int tri = ordered[i];
                if (tri < 0 || tri >= assigned.Length || assigned[tri])
                    continue;

                int source = FindUpperSkirtFlowSource(triNeighbors, triValid, triFront, inSkirtRegion, assigned, triUp, tri, upEpsilon);
                Vector3 sourceDelta;
                if (!TryGetSkirtFlowSourceDelta(source, assigned, triDelta, assignedDelta, minDeltaSq, out sourceDelta))
                    continue;

                assignedDelta[tri] = sourceDelta;
                assigned[tri] = true;
                assignedNow++;
            }

            return assignedNow;
        }

        static int SeedDisconnectedSkirtFlowRegions(
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triFront,
            bool[] inSkirtRegion,
            bool[] assigned,
            float[] triUp,
            float[] triSide,
            float[] triFwd,
            float[] triDeltaUp,
            Vector3[] triDelta,
            Vector3[] assignedDelta,
            float maxSeedUp,
            float minDelta,
            float minDeltaSq,
            float upEpsilon,
            float sideStepTolerance)
        {
            if (triNeighbors == null || triValid == null || triFront == null || inSkirtRegion == null
                || assigned == null || triUp == null || triSide == null || triFwd == null || triDeltaUp == null
                || triDelta == null || assignedDelta == null)
                return 0;
            if (maxSeedUp == float.MinValue)
                return 0;

            int triCount = Mathf.Min(triValid.Length,
                Mathf.Min(triFront.Length,
                Mathf.Min(inSkirtRegion.Length,
                Mathf.Min(assigned.Length,
                Mathf.Min(triUp.Length,
                Mathf.Min(triSide.Length,
                Mathf.Min(triFwd.Length,
                Mathf.Min(triDeltaUp.Length,
                Mathf.Min(triDelta.Length, assignedDelta.Length)))))))));
            if (triCount <= 0)
                return 0;

            bool[] candidate = new bool[triCount];
            float topLimit = maxSeedUp + Mathf.Max(sideStepTolerance, RelUp(0.01f));
            for (int tri = 0; tri < triCount; tri++)
            {
                if (!triValid[tri] || !triFront[tri] || inSkirtRegion[tri])
                    continue;
                if (triUp[tri] > topLimit)
                    continue;
                candidate[tri] = true;
            }

            bool[] visited = new bool[triCount];
            bool[] componentMask = new bool[triCount];
            Queue<int> queue = new Queue<int>();
            List<int> component = new List<int>();
            float topBand = Mathf.Max(RelUp(0.018f), sideStepTolerance * 1.5f);
            int added = 0;

            for (int start = 0; start < triCount; start++)
            {
                if (!candidate[start] || visited[start])
                    continue;

                component.Clear();
                queue.Enqueue(start);
                visited[start] = true;
                while (queue.Count > 0)
                {
                    int tri = queue.Dequeue();
                    component.Add(tri);
                    List<int> ns = tri < triNeighbors.Length ? triNeighbors[tri] : null;
                    if (ns == null) continue;
                    for (int i = 0; i < ns.Count; i++)
                    {
                        int n = ns[i];
                        if (n < 0 || n >= triCount || visited[n] || !candidate[n])
                            continue;
                        visited[n] = true;
                        queue.Enqueue(n);
                    }
                }

                if (component.Count <= 0)
                    continue;

                float componentMaxUp = float.MinValue;
                int topTri = -1;
                for (int i = 0; i < component.Count; i++)
                {
                    int tri = component[i];
                    componentMask[tri] = true;
                    if (triUp[tri] > componentMaxUp)
                    {
                        componentMaxUp = triUp[tri];
                        topTri = tri;
                    }
                }

                int seeded = 0;
                for (int i = 0; i < component.Count; i++)
                {
                    int tri = component[i];
                    if (componentMaxUp - triUp[tri] > topBand)
                        continue;

                    int source = FindSpatialSkirtFlowSource(
                        triValid,
                        triFront,
                        componentMask,
                        assigned,
                        triUp,
                        triSide,
                        triFwd,
                        triDeltaUp,
                        triDelta,
                        assignedDelta,
                        tri,
                        minDelta,
                        minDeltaSq,
                        upEpsilon);
                    Vector3 sourceDelta;
                    if (!TryGetSkirtFlowSourceDelta(source, assigned, triDelta, assignedDelta, minDeltaSq, out sourceDelta))
                        continue;

                    assignedDelta[tri] = sourceDelta;
                    assigned[tri] = true;
                    seeded++;
                }

                if (seeded <= 0 && topTri >= 0)
                {
                    int source = FindSpatialSkirtFlowSource(
                        triValid,
                        triFront,
                        componentMask,
                        assigned,
                        triUp,
                        triSide,
                        triFwd,
                        triDeltaUp,
                        triDelta,
                        assignedDelta,
                        topTri,
                        minDelta,
                        minDeltaSq,
                        upEpsilon);
                    Vector3 sourceDelta;
                    if (TryGetSkirtFlowSourceDelta(source, assigned, triDelta, assignedDelta, minDeltaSq, out sourceDelta))
                    {
                        assignedDelta[topTri] = sourceDelta;
                        assigned[topTri] = true;
                        seeded = 1;
                    }
                }

                if (seeded > 0)
                {
                    for (int i = 0; i < component.Count; i++)
                        inSkirtRegion[component[i]] = true;
                    added += component.Count;
                }

                for (int i = 0; i < component.Count; i++)
                    componentMask[component[i]] = false;
            }

            return added;
        }

        static List<int>[] BuildTriangleNeighborsByEdge(int[] triangles, int triCount, int vertexCount)
        {
            if (triangles == null || triCount <= 0 || vertexCount <= 0)
                return null;

            List<int>[] neighbors = new List<int>[triCount];
            for (int i = 0; i < triCount; i++)
                neighbors[i] = new List<int>(3);

            Dictionary<long, int> edgeOwner = new Dictionary<long, int>();
            for (int tri = 0; tri < triCount; tri++)
            {
                int baseIndex = tri * 3;
                AddTriangleEdgeNeighbor(edgeOwner, neighbors, tri, triangles[baseIndex], triangles[baseIndex + 1], vertexCount);
                AddTriangleEdgeNeighbor(edgeOwner, neighbors, tri, triangles[baseIndex + 1], triangles[baseIndex + 2], vertexCount);
                AddTriangleEdgeNeighbor(edgeOwner, neighbors, tri, triangles[baseIndex + 2], triangles[baseIndex], vertexCount);
            }

            return neighbors;
        }

        static void AddTriangleEdgeNeighbor(
            Dictionary<long, int> edgeOwner,
            List<int>[] neighbors,
            int tri,
            int a,
            int b,
            int vertexCount)
        {
            if (edgeOwner == null || neighbors == null) return;
            if (!IsValidVertexIndex(a, vertexCount) || !IsValidVertexIndex(b, vertexCount) || a == b)
                return;

            int lo = Mathf.Min(a, b);
            int hi = Mathf.Max(a, b);
            long key = ((long)lo << 32) ^ (uint)hi;
            int other;
            if (edgeOwner.TryGetValue(key, out other))
            {
                AddTriangleNeighbor(neighbors, tri, other);
                AddTriangleNeighbor(neighbors, other, tri);
            }
            else
            {
                edgeOwner[key] = tri;
            }
        }

        static void AddTriangleNeighbor(List<int>[] neighbors, int tri, int other)
        {
            if (neighbors == null || tri < 0 || tri >= neighbors.Length || other < 0 || other >= neighbors.Length || tri == other)
                return;
            if (!neighbors[tri].Contains(other))
                neighbors[tri].Add(other);
        }

        static bool HasUpperNonDownSkirtNeighbor(
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triFront,
            float[] triUp,
            float[] triDeltaUp,
            int tri,
            float upEpsilon,
            float minDelta)
        {
            if (triNeighbors == null || tri < 0 || tri >= triNeighbors.Length)
                return false;

            List<int> ns = triNeighbors[tri];
            for (int i = 0; i < ns.Count; i++)
            {
                int n = ns[i];
                if (n < 0 || n >= triValid.Length || !triValid[n] || !triFront[n]) continue;
                if (triUp[n] <= triUp[tri] + upEpsilon) continue;
                if (triDeltaUp[n] >= -minDelta * 0.5f || triDeltaUp[n] > triDeltaUp[tri] + minDelta)
                    return true;
            }

            return false;
        }

        static int FindUpperSkirtFlowSource(
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triFront,
            bool[] inSkirtRegion,
            bool[] assigned,
            float[] triUp,
            int tri,
            float upEpsilon)
        {
            if (triNeighbors == null || tri < 0 || tri >= triNeighbors.Length)
                return -1;

            int best = -1;
            float bestStep = float.MaxValue;
            List<int> ns = triNeighbors[tri];
            for (int i = 0; i < ns.Count; i++)
            {
                int n = ns[i];
                if (n < 0 || n >= triValid.Length || !triValid[n] || !triFront[n]) continue;

                float step = triUp[n] - triUp[tri];
                if (step <= upEpsilon || step >= bestStep) continue;
                if (inSkirtRegion != null && n < inSkirtRegion.Length && inSkirtRegion[n]
                    && (assigned == null || n >= assigned.Length || !assigned[n]))
                    continue;

                bestStep = step;
                best = n;
            }

            return best;
        }

        static int FindSpatialSkirtFlowSource(
            bool[] triValid,
            bool[] triFront,
            bool[] componentMask,
            bool[] assigned,
            float[] triUp,
            float[] triSide,
            float[] triFwd,
            float[] triDeltaUp,
            Vector3[] triDelta,
            Vector3[] assignedDelta,
            int target,
            float minDelta,
            float minDeltaSq,
            float upEpsilon)
        {
            if (triValid == null || triFront == null || triUp == null || triSide == null || triFwd == null
                || triDeltaUp == null || triDelta == null || assignedDelta == null)
                return -1;
            if (target < 0 || target >= triValid.Length || target >= triUp.Length || target >= triSide.Length || target >= triFwd.Length)
                return -1;

            int triCount = Mathf.Min(triValid.Length,
                Mathf.Min(triFront.Length,
                Mathf.Min(triUp.Length,
                Mathf.Min(triSide.Length,
                Mathf.Min(triFwd.Length,
                Mathf.Min(triDeltaUp.Length,
                Mathf.Min(triDelta.Length, assignedDelta.Length)))))));
            if (triCount <= 0)
                return -1;

            float targetUp = triUp[target];
            float targetSide = triSide[target];
            float targetFwd = triFwd[target];
            float nearBelowTolerance = Mathf.Max(upEpsilon, RelUp(0.006f));
            int best = -1;
            float bestScore = float.MaxValue;

            for (int source = 0; source < triCount; source++)
            {
                if (source == target)
                    continue;
                if (componentMask != null && source < componentMask.Length && componentMask[source])
                    continue;
                if (!triValid[source] || !triFront[source])
                    continue;
                if (triUp[source] < targetUp - nearBelowTolerance)
                    continue;

                bool sourceAssigned = assigned != null && source < assigned.Length && assigned[source];
                if (!sourceAssigned && triDeltaUp[source] < -minDelta)
                    continue;

                Vector3 sourceDelta;
                if (!TryGetSkirtFlowSourceDelta(source, assigned, triDelta, assignedDelta, minDeltaSq, out sourceDelta))
                    continue;

                float sideGap = Mathf.Abs(triSide[source] - targetSide);
                float fwdGap = Mathf.Abs(triFwd[source] - targetFwd);
                float upGap = Mathf.Max(0f, triUp[source] - targetUp);
                float score = sideGap + fwdGap * 0.45f + upGap * 0.65f;
                if (triUp[source] <= targetUp + upEpsilon)
                    score += RelUp(0.06f);
                if (!sourceAssigned)
                    score += RelUp(0.01f);

                if (score >= bestScore)
                    continue;
                bestScore = score;
                best = source;
            }

            return best;
        }

        static bool TryGetSkirtFlowSourceDelta(
            int source,
            bool[] assigned,
            Vector3[] triDelta,
            Vector3[] assignedDelta,
            float minDeltaSq,
            out Vector3 delta)
        {
            delta = Vector3.zero;
            if (source < 0)
                return false;

            bool useAssigned = assigned != null && source < assigned.Length && assigned[source]
                && assignedDelta != null && source < assignedDelta.Length;
            if (useAssigned)
            {
                delta = assignedDelta[source];
            }
            else
            {
                if (triDelta == null || source >= triDelta.Length)
                    return false;
                delta = triDelta[source];
            }

            return IsFinite(delta) && delta.sqrMagnitude > minDeltaSq;
        }

        static bool HasLowerUnchangedTriangle(
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triAffected,
            float[] triUp,
            int tri,
            float upEpsilon)
        {
            if (triNeighbors == null || tri < 0 || tri >= triNeighbors.Length)
                return false;

            List<int> ns = triNeighbors[tri];
            for (int i = 0; i < ns.Count; i++)
            {
                int n = ns[i];
                if (n < 0 || n >= triValid.Length || !triValid[n]) continue;
                if (triUp[n] >= triUp[tri] - upEpsilon) continue;
                if (!triAffected[n])
                    return true;
            }
            return false;
        }

        static int FindAdjacentSkirtTriangle(
            List<int>[] triNeighbors,
            bool[] triValid,
            bool[] triAffected,
            float[] triUp,
            int tri,
            bool upward,
            bool requireAffected,
            float upEpsilon)
        {
            if (triNeighbors == null || tri < 0 || tri >= triNeighbors.Length)
                return -1;

            int best = -1;
            float bestStep = float.MaxValue;
            List<int> ns = triNeighbors[tri];
            for (int i = 0; i < ns.Count; i++)
            {
                int n = ns[i];
                if (n < 0 || n >= triValid.Length || !triValid[n]) continue;
                if (requireAffected && !triAffected[n]) continue;

                float step = upward ? triUp[n] - triUp[tri] : triUp[tri] - triUp[n];
                if (step <= upEpsilon || step >= bestStep) continue;
                bestStep = step;
                best = n;
            }
            return best;
        }

        static void AddSkirtTriangleFadeTarget(
            int[] triA,
            int[] triB,
            int[] triC,
            bool[] valid,
            int tri,
            Vector3 sourceDelta,
            float fade,
            Vector3[] accumDelta,
            float[] accumWeight)
        {
            if (triA == null || triB == null || triC == null || valid == null || accumDelta == null || accumWeight == null)
                return;
            if (tri < 0 || tri >= triA.Length)
                return;

            Vector3 delta = sourceDelta * Mathf.Clamp01(fade);
            AddSkirtVertexFadeTarget(triA[tri], valid, delta, accumDelta, accumWeight);
            AddSkirtVertexFadeTarget(triB[tri], valid, delta, accumDelta, accumWeight);
            AddSkirtVertexFadeTarget(triC[tri], valid, delta, accumDelta, accumWeight);
        }

        static void AddSkirtVertexFadeTarget(
            int vertex,
            bool[] valid,
            Vector3 delta,
            Vector3[] accumDelta,
            float[] accumWeight)
        {
            if (vertex < 0 || vertex >= valid.Length || vertex >= accumDelta.Length || vertex >= accumWeight.Length)
                return;
            if (!valid[vertex])
                return;

            accumDelta[vertex] += delta;
            accumWeight[vertex] += 1f;
        }

        static float GetBodySurfaceClothMultiplier(MeshMorphClass meshClass)
        {
            if (meshClass == MeshMorphClass.OuterCloth)
                return Mathf.Max(0f, OuterClothOverdrive * OuterClothPregnancyScale);
            if (meshClass == MeshMorphClass.InnerCloth)
                return Mathf.Max(0f, ClothOverdrive);
            return 1f;
        }

        static bool TryGetBodySurfaceClothTarget(
            MorphReferenceContext context,
            Vector3 original,
            float multiplier,
            out Vector3 target)
        {
            target = original;
            if (context == null || context.SurfaceTriangles.Count == 0)
                return false;

            context.SurfaceQueries++;
            BodySurfaceHit hit;
            if (!TryFindNearestBodySurfaceTriangle(context, original, out hit))
                return BodySurfaceMiss(context);

            if (hit.TriangleIndex < 0 || hit.TriangleIndex >= context.SurfaceTriangles.Count)
                return BodySurfaceMiss(context);

            if (TryBuildBodySurfaceTargetFromHit(context, original, multiplier, hit, out target))
            {
                context.SurfaceHits++;
                return true;
            }

            BodySurfaceHit fallbackHit;
            if (TryFindNearestValidBodySurfaceTargetFullScan(context, original, multiplier, out fallbackHit, out target))
            {
                context.SurfaceHits++;
                return true;
            }

            return BodySurfaceMiss(context);
        }

        static bool TryBuildBodySurfaceTargetFromHit(
            MorphReferenceContext context,
            Vector3 original,
            float multiplier,
            BodySurfaceHit hit,
            out Vector3 target)
        {
            target = original;
            if (context == null || context.SurfaceTriangles == null)
                return false;
            if (hit.TriangleIndex < 0 || hit.TriangleIndex >= context.SurfaceTriangles.Count)
                return false;

            BodySurfaceTriangle tri = context.SurfaceTriangles[hit.TriangleIndex];
            BodyReferenceMesh mesh = tri.Mesh;
            if (mesh == null || mesh.OriginalWorld == null || mesh.MorphedWorld == null)
                return false;
            if (!IsValidBodySurfaceVertex(mesh, tri.A)
                || !IsValidBodySurfaceVertex(mesh, tri.B)
                || !IsValidBodySurfaceVertex(mesh, tri.C))
                return false;

            float mul = Mathf.Max(0f, multiplier);
            Vector3 a = mesh.OriginalWorld[tri.A];
            Vector3 b = mesh.OriginalWorld[tri.B];
            Vector3 c = mesh.OriginalWorld[tri.C];
            Vector3 am = ScaleBodySurfaceMove(a, mesh.MorphedWorld[tri.A], mul);
            Vector3 bm = ScaleBodySurfaceMove(b, mesh.MorphedWorld[tri.B], mul);
            Vector3 cm = ScaleBodySurfaceMove(c, mesh.MorphedWorld[tri.C], mul);

            Vector3 normalM = Vector3.Cross(bm - am, cm - am);
            float normalMLen = normalM.magnitude;
            if (normalMLen < 1e-9f || !IsFinite(normalM))
                return false;
            normalM /= normalMLen;

            if (Vector3.Dot(hit.Normal, normalM) < -0.25f)
                return false;

            Vector3 surfaceM =
                am * hit.Barycentric.x +
                bm * hit.Barycentric.y +
                cm * hit.Barycentric.z;

            Vector3 offset = original - hit.Closest;
            Quaternion normalRotation = Quaternion.FromToRotation(hit.Normal, normalM);
            target = surfaceM + normalRotation * offset;
            if (!IsFinite(target))
                return false;

            return true;
        }

        static bool BodySurfaceMiss(MorphReferenceContext context)
        {
            if (context != null) context.SurfaceMisses++;
            return false;
        }

        static bool TryGetBodySurfaceClothTargetDebug(
            MorphReferenceContext context,
            Vector3 original,
            float multiplier,
            out Vector3 target,
            out BodySurfaceTargetDebug debug)
        {
            target = original;
            debug = new BodySurfaceTargetDebug
            {
                FailReason = "init",
                TriangleIndex = -1,
                BodyMeshInstanceId = 0,
                BodyA = -1,
                BodyB = -1,
                BodyC = -1,
                Target = original,
            };

            if (context == null)
            {
                debug.FailReason = "no-context";
                return false;
            }

            debug.BodyPointCount = context.BodyPoints != null ? context.BodyPoints.Count : 0;
            debug.SurfaceTriangleCount = context.SurfaceTriangles != null ? context.SurfaceTriangles.Count : 0;
            if (context.SurfaceTriangles == null || context.SurfaceTriangles.Count == 0)
            {
                debug.FailReason = "no-surface-triangles";
                return false;
            }

            BodySurfaceHit hit;
            if (!TryFindNearestBodySurfaceTriangle(context, original, out hit))
            {
                BodySurfaceHit fullHit;
                if (TryFindNearestBodySurfaceTriangleFullScan(context, original, out fullHit))
                {
                    debug.FullScanHit = true;
                    bool fullScanWouldPass = FillBodySurfaceTargetDebugFromHit(context, original, fullHit, multiplier, ref debug, out target);
                    string fullScanReason = debug.FailReason ?? string.Empty;
                    debug.FailReason = fullScanWouldPass
                        ? "nearest-not-found-in-buckets/full-scan-ok"
                        : "nearest-not-found-in-buckets/full-scan-" + fullScanReason;
                }
                else
                {
                    debug.FailReason = "nearest-not-found";
                }
                return false;
            }

            debug.SearchHit = true;
            if (FillBodySurfaceTargetDebugFromHit(context, original, hit, multiplier, ref debug, out target))
                return true;

            string nearestReason = debug.FailReason ?? string.Empty;
            BodySurfaceHit fallbackHit;
            if (TryFindNearestValidBodySurfaceTargetFullScan(context, original, multiplier, out fallbackHit, out target))
            {
                debug.FullScanHit = true;
                if (FillBodySurfaceTargetDebugFromHit(context, original, fallbackHit, multiplier, ref debug, out target))
                {
                    debug.FailReason = "fallback-after-" + nearestReason;
                    return true;
                }
            }

            debug.FailReason = nearestReason;
            return false;
        }

        static bool FillBodySurfaceTargetDebugFromHit(
            MorphReferenceContext context,
            Vector3 original,
            BodySurfaceHit hit,
            float multiplier,
            ref BodySurfaceTargetDebug debug,
            out Vector3 target)
        {
            target = original;
            debug.TriangleIndex = hit.TriangleIndex;
            debug.Distance = Mathf.Sqrt(Mathf.Max(hit.DistanceSq, 0f));
            debug.OffsetLen = (original - hit.Closest).magnitude;
            debug.Barycentric = hit.Barycentric;
            debug.Closest = hit.Closest;
            debug.OriginalNormal = hit.Normal;

            if (hit.TriangleIndex < 0 || context == null || context.SurfaceTriangles == null || hit.TriangleIndex >= context.SurfaceTriangles.Count)
            {
                debug.FailReason = "nearest-index-invalid";
                return false;
            }

            BodySurfaceTriangle tri = context.SurfaceTriangles[hit.TriangleIndex];
            BodyReferenceMesh mesh = tri.Mesh;
            debug.BodyMeshId = MeshDebugId(mesh);
            debug.BodyMeshInstanceId = mesh != null ? mesh.MeshInstanceId : 0;
            debug.BodyA = tri.A;
            debug.BodyB = tri.B;
            debug.BodyC = tri.C;

            if (mesh == null || mesh.OriginalWorld == null || mesh.MorphedWorld == null)
            {
                debug.FailReason = "body-mesh-missing";
                return false;
            }
            if (!IsValidBodySurfaceVertex(mesh, tri.A))
            {
                debug.FailReason = "body-vertex-a-invalid";
                return false;
            }
            if (!IsValidBodySurfaceVertex(mesh, tri.B))
            {
                debug.FailReason = "body-vertex-b-invalid";
                return false;
            }
            if (!IsValidBodySurfaceVertex(mesh, tri.C))
            {
                debug.FailReason = "body-vertex-c-invalid";
                return false;
            }

            float mul = Mathf.Max(0f, multiplier);
            Vector3 a = mesh.OriginalWorld[tri.A];
            Vector3 b = mesh.OriginalWorld[tri.B];
            Vector3 c = mesh.OriginalWorld[tri.C];
            Vector3 am = ScaleBodySurfaceMove(a, mesh.MorphedWorld[tri.A], mul);
            Vector3 bm = ScaleBodySurfaceMove(b, mesh.MorphedWorld[tri.B], mul);
            Vector3 cm = ScaleBodySurfaceMove(c, mesh.MorphedWorld[tri.C], mul);

            Vector3 normalM = Vector3.Cross(bm - am, cm - am);
            float normalMLen = normalM.magnitude;
            if (normalMLen < 1e-9f || !IsFinite(normalM))
            {
                debug.FailReason = "morphed-normal-invalid";
                return false;
            }
            normalM /= normalMLen;
            debug.MorphedNormal = normalM;
            debug.NormalDot = Vector3.Dot(hit.Normal, normalM);

            if (debug.NormalDot < -0.25f)
            {
                debug.FailReason = "normal-flipped";
                return false;
            }

            Vector3 surfaceM =
                am * hit.Barycentric.x +
                bm * hit.Barycentric.y +
                cm * hit.Barycentric.z;
            debug.SurfaceMorphed = surfaceM;
            debug.SurfaceMoveLen = (surfaceM - hit.Closest).magnitude;

            Vector3 offset = original - hit.Closest;
            Quaternion normalRotation = Quaternion.FromToRotation(hit.Normal, normalM);
            target = surfaceM + normalRotation * offset;
            debug.Target = target;
            if (!IsFinite(target))
            {
                debug.FailReason = "target-nonfinite";
                return false;
            }

            debug.FailReason = "ok";
            return true;
        }

        static bool TryFindNearestBodySurfaceTriangle(
            MorphReferenceContext context,
            Vector3 point,
            out BodySurfaceHit nearest)
        {
            nearest = new BodySurfaceHit { TriangleIndex = -1 };
            if (context == null || context.SurfaceTriangles.Count == 0)
                return false;

            bool found = false;
            float bestSq = float.MaxValue;
            HashSet<int> seen = new HashSet<int>();

            if (context.SurfaceBuckets != null && context.SurfaceBuckets.Count > 0)
            {
                int bx, by, bz;
                BodySurfaceBucketCoords(point, context.CellSize, out bx, out by, out bz);
                for (int dx = -4; dx <= 4; dx++)
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dz = -4; dz <= 4; dz++)
                        {
                            int key = BodySurfaceBucketKey(bx + dx, by + dy, bz + dz);
                            List<int> list;
                            if (!context.SurfaceBuckets.TryGetValue(key, out list)) continue;
                            for (int i = 0; i < list.Count; i++)
                            {
                                int triIndex = list[i];
                                if (!seen.Add(triIndex)) continue;
                                TestBodySurfaceTriangle(context, point, triIndex, ref found, ref bestSq, ref nearest);
                            }
                        }
                if (found) return true;
                return false;
            }

            for (int i = 0; i < context.SurfaceTriangles.Count; i++)
                TestBodySurfaceTriangle(context, point, i, ref found, ref bestSq, ref nearest);
            return found;
        }

        static bool TryFindNearestBodySurfaceTriangleFullScan(
            MorphReferenceContext context,
            Vector3 point,
            out BodySurfaceHit nearest)
        {
            nearest = new BodySurfaceHit { TriangleIndex = -1 };
            if (context == null || context.SurfaceTriangles == null || context.SurfaceTriangles.Count == 0)
                return false;

            bool found = false;
            float bestSq = float.MaxValue;
            for (int i = 0; i < context.SurfaceTriangles.Count; i++)
                TestBodySurfaceTriangle(context, point, i, ref found, ref bestSq, ref nearest);
            return found;
        }

        static bool TryFindNearestValidBodySurfaceTargetFullScan(
            MorphReferenceContext context,
            Vector3 point,
            float multiplier,
            out BodySurfaceHit nearest,
            out Vector3 target)
        {
            nearest = new BodySurfaceHit { TriangleIndex = -1 };
            target = point;
            if (context == null || context.SurfaceTriangles == null || context.SurfaceTriangles.Count == 0)
                return false;

            bool found = false;
            float bestSq = float.MaxValue;
            for (int i = 0; i < context.SurfaceTriangles.Count; i++)
            {
                bool candidateFound = false;
                float candidateBestSq = float.MaxValue;
                BodySurfaceHit candidate = new BodySurfaceHit { TriangleIndex = -1 };
                TestBodySurfaceTriangle(context, point, i, ref candidateFound, ref candidateBestSq, ref candidate);
                if (!candidateFound || candidate.DistanceSq >= bestSq)
                    continue;

                Vector3 candidateTarget;
                if (!TryBuildBodySurfaceTargetFromHit(context, point, multiplier, candidate, out candidateTarget))
                    continue;

                found = true;
                bestSq = candidate.DistanceSq;
                nearest = candidate;
                target = candidateTarget;
            }

            return found;
        }

        static void TestBodySurfaceTriangle(
            MorphReferenceContext context,
            Vector3 point,
            int triangleIndex,
            ref bool found,
            ref float bestSq,
            ref BodySurfaceHit nearest)
        {
            if (context == null || triangleIndex < 0 || triangleIndex >= context.SurfaceTriangles.Count)
                return;

            BodySurfaceTriangle tri = context.SurfaceTriangles[triangleIndex];
            BodyReferenceMesh mesh = tri.Mesh;
            if (mesh == null || mesh.OriginalWorld == null)
                return;
            if (!IsValidBodySurfaceVertex(mesh, tri.A)
                || !IsValidBodySurfaceVertex(mesh, tri.B)
                || !IsValidBodySurfaceVertex(mesh, tri.C))
                return;
            if (ShouldSuppressBodySurfaceTriangleCandidate(context, mesh, tri))
                return;

            Vector3 a = mesh.OriginalWorld[tri.A];
            Vector3 b = mesh.OriginalWorld[tri.B];
            Vector3 c = mesh.OriginalWorld[tri.C];
            Vector3 closest;
            Vector3 barycentric;
            if (!TryClosestPointOnTriangle(point, a, b, c, out closest, out barycentric))
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

        static bool ShouldSuppressBodySurfaceTriangleCandidate(
            MorphReferenceContext context,
            BodyReferenceMesh mesh,
            BodySurfaceTriangle tri)
        {
            if (context == null || context.VirtualSurfaceTriangles <= 0)
                return false;
            if (mesh == null || mesh.OriginalWorld == null || mesh.MorphedWorld == null)
                return false;
            if (IsVirtualBodySurfaceMesh(mesh))
                return false;
            if (!IsValidBodySurfaceVertex(mesh, tri.A)
                || !IsValidBodySurfaceVertex(mesh, tri.B)
                || !IsValidBodySurfaceVertex(mesh, tri.C))
                return false;

            Vector3 p0 = mesh.OriginalWorld[tri.A];
            Vector3 p1 = mesh.OriginalWorld[tri.B];
            Vector3 p2 = mesh.OriginalWorld[tri.C];
            Vector3 m0 = mesh.MorphedWorld[tri.A];
            Vector3 m1 = mesh.MorphedWorld[tri.B];
            Vector3 m2 = mesh.MorphedWorld[tri.C];
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            float minEdge = Mathf.Max(context.CellSize * 0.015f, 0.00001f);
            float minArea = minEdge * minEdge;
            float minAreaSq = minArea * minArea;
            if (!IsFinite(normal) || normal.sqrMagnitude < minAreaSq)
                return false;

            return IsVirtualFrontSeamSurfaceCandidate(context, p0, p1, p2, m0, m1, m2, normal, minAreaSq);
        }

        static bool IsVirtualBodySurfaceMesh(BodyReferenceMesh mesh)
        {
            return mesh != null
                && mesh.MeshId != null
                && mesh.MeshId.IndexOf(":virtual-front-seam", System.StringComparison.Ordinal) >= 0;
        }

        static bool TryClosestPointOnTriangle(
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

        static bool IsValidBodySurfaceVertex(BodyReferenceMesh mesh, int index)
        {
            return mesh != null
                && mesh.Valid != null
                && mesh.OriginalWorld != null
                && mesh.MorphedWorld != null
                && index >= 0
                && index < mesh.Valid.Length
                && index < mesh.OriginalWorld.Length
                && index < mesh.MorphedWorld.Length
                && mesh.Valid[index];
        }

        static bool BodySurfaceMoveSignificant(Vector3 original, Vector3 morphed)
        {
            return IsFinite(original)
                && IsFinite(morphed)
                && (morphed - original).sqrMagnitude > 1e-10f;
        }

        static Vector3 ScaleBodySurfaceMove(Vector3 original, Vector3 morphed, float multiplier)
        {
            return original + (morphed - original) * multiplier;
        }

        static void BodySurfaceBucketCoords(Vector3 point, float cellSize, out int x, out int y, out int z)
        {
            float cell = Mathf.Max(cellSize, 0.0001f);
            x = Mathf.FloorToInt(point.x / cell);
            y = Mathf.FloorToInt(point.y / cell);
            z = Mathf.FloorToInt(point.z / cell);
        }

        static int BodySurfaceBucketKey(int x, int y, int z)
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

        static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        static bool IsFinite(Quaternion q)
        {
            return IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        }

        static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        static void RefreshMorphedWorldFromLocal(
            Vector3[] localVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] morphedWorld,
            bool[] valid)
        {
            if (localVerts == null || weights == null || boneMatrices == null || morphedWorld == null || valid == null)
                return;

            int count = Mathf.Min(localVerts.Length, Mathf.Min(weights.Length, Mathf.Min(morphedWorld.Length, valid.Length)));
            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                morphedWorld[i] = skin.MultiplyPoint3x4(localVerts[i]);
            }
        }

        static bool[] BuildWorldChangedMask(Vector3[] originalWorld, Vector3[] morphedWorld, bool[] valid)
        {
            if (originalWorld == null || morphedWorld == null || valid == null)
                return null;

            int count = Mathf.Min(originalWorld.Length, Mathf.Min(morphedWorld.Length, valid.Length));
            bool[] changed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (!valid[i])
                    continue;

                Vector3 original = originalWorld[i];
                Vector3 morphed = morphedWorld[i];
                changed[i] = WorldPositionChanged(original, morphed);
            }

            return changed;
        }

        static bool WorldPositionChanged(Vector3 original, Vector3 morphed)
        {
            return original.x != morphed.x
                || original.y != morphed.y
                || original.z != morphed.z;
        }

        static void RecordBodyChangedUp(float value)
        {
            if (_morphReferenceContext == null)
                return;

            if (!_morphReferenceContext.BodyChangedUpRangeValid)
            {
                _morphReferenceContext.BodyChangedUpRangeValid = true;
                _morphReferenceContext.BodyChangedMinUp = value;
                _morphReferenceContext.BodyChangedMaxUp = value;
                return;
            }

            if (value < _morphReferenceContext.BodyChangedMinUp)
                _morphReferenceContext.BodyChangedMinUp = value;
            if (value > _morphReferenceContext.BodyChangedMaxUp)
                _morphReferenceContext.BodyChangedMaxUp = value;
        }

        static void RegisterBodyMorphReferences(
            Mesh mesh,
            Vector3[] originalWorld,
            Vector3[] morphedWorld,
            bool[] valid,
            bool[] referenceEligible,
            Vector3 center,
            Vector3 up)
        {
            if (_morphReferenceContext == null || mesh == null || originalWorld == null
                || morphedWorld == null || valid == null || referenceEligible == null)
                return;

            int count = Mathf.Min(originalWorld.Length,
                Mathf.Min(morphedWorld.Length, Mathf.Min(valid.Length, referenceEligible.Length)));
            List<int>[] neighbors = BuildMeshNeighbors(mesh, count);
            if (neighbors == null) return;

            bool[] eligible = new bool[count];
            BodyReferenceMesh refMesh = new BodyReferenceMesh
            {
                MeshId = mesh.name ?? string.Empty,
                MeshInstanceId = mesh.GetInstanceID(),
                OriginalWorld = originalWorld,
                MorphedWorld = morphedWorld,
                Valid = valid,
                Eligible = eligible,
                Neighbors = neighbors,
                SurfaceTrianglesByVertex = new List<int>[count],
            };
            _morphReferenceContext.BodyMeshes.Add(refMesh);

            for (int i = 0; i < count; i++)
            {
                bool finalMoved = valid[i] && BodySurfaceMoveSignificant(originalWorld[i], morphedWorld[i]);
                bool finalEligible = finalMoved && referenceEligible[i];

                if (finalMoved)
                    RecordBodyChangedUp(Vector3.Dot(morphedWorld[i] - center, up));

                if (!finalEligible) continue;

                eligible[i] = true;
                _morphReferenceContext.BodyPoints.Add(new BodyReferencePoint
                {
                    Mesh = refMesh,
                    Index = i,
                    OriginalWorld = originalWorld[i],
                });
            }

            // Use referenceEligible (all in-ellipsoid vertices) not eligible (only moved ones).
            // Breast-area body vertices are in-ellipsoid but the breast guard pulled them back
            // to near-original, so they show no movement → their triangles were missing from the
            // reference. Cloth near the breast then fell back to belly triangles and got pushed
            // forward. With these triangles present, surfaceM ≈ hit.Closest and R ≈ identity →
            // target ≈ cloth_original, which is the correct result for that region.
            AddBodySurfaceTriangles(_morphReferenceContext, mesh, refMesh, referenceEligible, count);
        }

        static int AddBodySurfaceTriangles(
            MorphReferenceContext context,
            Mesh mesh,
            BodyReferenceMesh refMesh,
            bool[] referenceEligible,
            int count)
        {
            if (context == null || mesh == null || refMesh == null) return 0;
            if (refMesh.OriginalWorld == null || refMesh.MorphedWorld == null || refMesh.Valid == null || referenceEligible == null)
                return 0;

            int[] triangles = null;
            try { triangles = mesh.triangles; }
            catch { return 0; }
            if (triangles == null || triangles.Length < 3)
                return 0;

            float minEdge = Mathf.Max(context.CellSize * 0.015f, 0.00001f);
            float minArea = minEdge * minEdge;
            float minAreaSq = minArea * minArea;
            int added = 0;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (!IsValidBodySurfaceTriangleVertex(refMesh, a, count)
                    || !IsValidBodySurfaceTriangleVertex(refMesh, b, count)
                    || !IsValidBodySurfaceTriangleVertex(refMesh, c, count))
                    continue;

                if (!IsEligibleBodySurfaceVertex(referenceEligible, a, count)
                    && !IsEligibleBodySurfaceVertex(referenceEligible, b, count)
                    && !IsEligibleBodySurfaceVertex(referenceEligible, c, count))
                    continue;

                Vector3 p0 = refMesh.OriginalWorld[a];
                Vector3 p1 = refMesh.OriginalWorld[b];
                Vector3 p2 = refMesh.OriginalWorld[c];
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                if (!IsFinite(normal) || normal.sqrMagnitude < minAreaSq)
                    continue;

                Vector3 m0 = refMesh.MorphedWorld[a];
                Vector3 m1 = refMesh.MorphedWorld[b];
                Vector3 m2 = refMesh.MorphedWorld[c];
                if (IsVirtualFrontSeamSurfaceCandidate(context, p0, p1, p2, m0, m1, m2, normal, minAreaSq))
                {
                    if (TryAddVirtualFrontSeamSurface(
                        context,
                        refMesh,
                        triangles,
                        referenceEligible,
                        count,
                        a,
                        b,
                        c,
                        minAreaSq))
                    {
                        context.SuppressedSurfaceTriangles++;
                        continue;
                    }
                }

                Vector3 center = (p0 + p1 + p2) / 3f;
                float radiusSq = (p0 - center).sqrMagnitude;
                radiusSq = Mathf.Max(radiusSq, (p1 - center).sqrMagnitude);
                radiusSq = Mathf.Max(radiusSq, (p2 - center).sqrMagnitude);

                int triIndex = context.SurfaceTriangles.Count;
                context.SurfaceTriangles.Add(new BodySurfaceTriangle
                {
                    Mesh = refMesh,
                    A = a,
                    B = b,
                    C = c,
                    Center = center,
                    RadiusSq = radiusSq,
                });
                AddBodySurfaceTriangleForVertex(refMesh, a, triIndex);
                AddBodySurfaceTriangleForVertex(refMesh, b, triIndex);
                AddBodySurfaceTriangleForVertex(refMesh, c, triIndex);
                AddBodySurfaceTriangleToBuckets(context, triIndex, p0, p1, p2);
                added++;
            }

            return added;
        }

        static bool IsVirtualFrontSeamSurfaceCandidate(
            MorphReferenceContext context,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 m0,
            Vector3 m1,
            Vector3 m2,
            Vector3 originalNormal,
            float minAreaSq)
        {
            if (context == null || !_bpWorldCached)
                return false;
            if (!IsFinite(p0) || !IsFinite(p1) || !IsFinite(p2)
                || !IsFinite(m0) || !IsFinite(m1) || !IsFinite(m2)
                || !IsFinite(originalNormal))
                return false;

            Vector3 morphedNormal = Vector3.Cross(m1 - m0, m2 - m0);
            if (!IsFinite(morphedNormal) || morphedNormal.sqrMagnitude < minAreaSq)
                return false;

            Vector3 right = _bpWorldFrame.Right;
            Vector3 up = _bpWorldFrame.Up;
            Vector3 fwd = _bpWorldFrame.Fwd;
            if (right.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f || fwd.sqrMagnitude < 1e-8f)
                return false;
            right.Normalize();
            up.Normalize();
            fwd.Normalize();

            Vector3 n0 = originalNormal.normalized;
            Vector3 center = (p0 + p1 + p2) / 3f;
            Vector3 rel = center - _bpWorldFrame.Center;

            float side = Mathf.Abs(Vector3.Dot(rel, right));
            float upCoord = Vector3.Dot(rel, up);
            float front = Vector3.Dot(rel, fwd);
            float sideFacing = Mathf.Abs(Vector3.Dot(n0, right));
            float frontFacing = Mathf.Abs(Vector3.Dot(n0, fwd));

            float centerlineLimit = Mathf.Max(RelSide(0.03f), context.CellSize * 0.6f);
            float minUp = -RelUp(0.30f);
            float maxUp = RelUp(0.05f);
            float minFront = RelFwd(0.04f);
            float maxFront = RelFwd(0.38f);
            return side <= centerlineLimit
                && upCoord >= minUp
                && upCoord <= maxUp
                && front >= minFront
                && front <= maxFront
                && sideFacing >= 0.70f
                && frontFacing <= 0.35f;
        }

        static bool TryAddVirtualFrontSeamSurface(
            MorphReferenceContext context,
            BodyReferenceMesh sourceMesh,
            int[] triangles,
            bool[] referenceEligible,
            int count,
            int candidateA,
            int candidateB,
            int candidateC,
            float minAreaSq)
        {
            if (context == null || sourceMesh == null || !_bpWorldCached)
                return false;

            Vector3 origin = _bpWorldFrame.Center;
            Vector3 right = _bpWorldFrame.Right;
            Vector3 up = _bpWorldFrame.Up;
            Vector3 fwd = _bpWorldFrame.Fwd;
            if (right.sqrMagnitude < 1e-8f || up.sqrMagnitude < 1e-8f || fwd.sqrMagnitude < 1e-8f)
                return false;
            right.Normalize();
            up.Normalize();
            fwd.Normalize();

            if (!IsValidBodySurfaceTriangleVertex(sourceMesh, candidateA, count)
                || !IsValidBodySurfaceTriangleVertex(sourceMesh, candidateB, count)
                || !IsValidBodySurfaceTriangleVertex(sourceMesh, candidateC, count))
                return false;

            Vector3 p0 = sourceMesh.OriginalWorld[candidateA];
            Vector3 p1 = sourceMesh.OriginalWorld[candidateB];
            Vector3 p2 = sourceMesh.OriginalWorld[candidateC];
            Vector3 m0 = sourceMesh.MorphedWorld[candidateA];
            Vector3 m1 = sourceMesh.MorphedWorld[candidateB];
            Vector3 m2 = sourceMesh.MorphedWorld[candidateC];
            if (!IsFinite(p0) || !IsFinite(p1) || !IsFinite(p2)
                || !IsFinite(m0) || !IsFinite(m1) || !IsFinite(m2))
                return false;

            float originalSide;
            float originalMinUp;
            float originalMaxUp;
            float originalFwd;
            float morphedSide;
            float morphedMinUp;
            float morphedMaxUp;
            float morphedFwd;
            ComputeVirtualFrontSeamCoords(origin, right, up, fwd, p0, p1, p2, out originalSide, out originalMinUp, out originalMaxUp, out originalFwd);
            ComputeVirtualFrontSeamCoords(origin, right, up, fwd, m0, m1, m2, out morphedSide, out morphedMinUp, out morphedMaxUp, out morphedFwd);

            float forwardBias = Mathf.Max(RelFwd(0.012f), context.CellSize * 0.35f);
            morphedFwd += forwardBias;

            float halfWidth = Mathf.Max(RelSide(0.024f), context.CellSize * 0.75f);
            float minHeight = Mathf.Max(RelUp(0.018f), context.CellSize * 0.5f);
            float originalCenterUp = (originalMinUp + originalMaxUp) * 0.5f;
            float morphedCenterUp = (morphedMinUp + morphedMaxUp) * 0.5f;
            float halfHeight = Mathf.Max((originalMaxUp - originalMinUp) * 0.5f + Mathf.Max(RelUp(0.006f), context.CellSize * 0.2f), minHeight);

            originalMinUp = originalCenterUp - halfHeight;
            originalMaxUp = originalCenterUp + halfHeight;
            morphedMinUp = morphedCenterUp - halfHeight;
            morphedMaxUp = morphedCenterUp + halfHeight;

            Vector3[] original = new Vector3[4];
            Vector3[] morphed = new Vector3[4];
            original[0] = ComposeVirtualSurfacePoint(origin, right, up, fwd, originalSide - halfWidth, originalMinUp, originalFwd);
            original[1] = ComposeVirtualSurfacePoint(origin, right, up, fwd, originalSide + halfWidth, originalMinUp, originalFwd);
            original[2] = ComposeVirtualSurfacePoint(origin, right, up, fwd, originalSide - halfWidth, originalMaxUp, originalFwd);
            original[3] = ComposeVirtualSurfacePoint(origin, right, up, fwd, originalSide + halfWidth, originalMaxUp, originalFwd);
            morphed[0] = ComposeVirtualSurfacePoint(origin, right, up, fwd, morphedSide - halfWidth, morphedMinUp, morphedFwd);
            morphed[1] = ComposeVirtualSurfacePoint(origin, right, up, fwd, morphedSide + halfWidth, morphedMinUp, morphedFwd);
            morphed[2] = ComposeVirtualSurfacePoint(origin, right, up, fwd, morphedSide - halfWidth, morphedMaxUp, morphedFwd);
            morphed[3] = ComposeVirtualSurfacePoint(origin, right, up, fwd, morphedSide + halfWidth, morphedMaxUp, morphedFwd);

            if (!BodySurfaceMoveSignificant(original[0], morphed[0])
                && !BodySurfaceMoveSignificant(original[1], morphed[1])
                && !BodySurfaceMoveSignificant(original[2], morphed[2])
                && !BodySurfaceMoveSignificant(original[3], morphed[3]))
            {
                return false;
            }

            BodyReferenceMesh virtualMesh = new BodyReferenceMesh
            {
                MeshId = (sourceMesh.MeshId ?? string.Empty) + ":virtual-front-seam-fwd",
                MeshInstanceId = sourceMesh.MeshInstanceId,
                OriginalWorld = original,
                MorphedWorld = morphed,
                Valid = new[] { true, true, true, true },
                Eligible = new[] { true, true, true, true },
                Neighbors = new List<int>[4],
                SurfaceTrianglesByVertex = new List<int>[4],
            };
            virtualMesh.Neighbors[0] = new List<int> { 1, 2 };
            virtualMesh.Neighbors[1] = new List<int> { 0, 2, 3 };
            virtualMesh.Neighbors[2] = new List<int> { 0, 1, 3 };
            virtualMesh.Neighbors[3] = new List<int> { 1, 2 };

            context.BodyMeshes.Add(virtualMesh);
            AddVirtualBodySurfaceTriangle(context, virtualMesh, 0, 1, 2);
            AddVirtualBodySurfaceTriangle(context, virtualMesh, 1, 3, 2);
            return true;
        }

        static bool TryFitVirtualFrontSeamSurface(
            MorphReferenceContext context,
            BodyReferenceMesh sourceMesh,
            int[] triangles,
            bool[] referenceEligible,
            int count,
            int candidateA,
            int candidateB,
            int candidateC,
            Vector3 origin,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float minAreaSq,
            out VirtualSeamSurfaceFit fit)
        {
            fit = new VirtualSeamSurfaceFit();
            if (context == null || sourceMesh == null || sourceMesh.OriginalWorld == null || sourceMesh.MorphedWorld == null
                || sourceMesh.Valid == null || sourceMesh.Neighbors == null || triangles == null)
                return false;

            Vector3 p0 = sourceMesh.OriginalWorld[candidateA];
            Vector3 p1 = sourceMesh.OriginalWorld[candidateB];
            Vector3 p2 = sourceMesh.OriginalWorld[candidateC];
            float candidateSide;
            float candidateMinUp;
            float candidateMaxUp;
            float candidateFwd;
            ComputeVirtualFrontSeamCoords(origin, right, up, fwd, p0, p1, p2, out candidateSide, out candidateMinUp, out candidateMaxUp, out candidateFwd);
            float candidateCenterUp = (candidateMinUp + candidateMaxUp) * 0.5f;

            HashSet<int> topological = CollectTopologicalVertexSet(sourceMesh.Neighbors, candidateA, candidateB, candidateC, 5);
            if (topological == null || topological.Count == 0)
                return false;

            float sideLimit = Mathf.Max(RelSide(0.09f), context.CellSize * 3.0f);
            float upLimit = Mathf.Max(RelUp(0.07f), context.CellSize * 2.5f);
            float backLimit = Mathf.Max(RelFwd(0.04f), context.CellSize * 1.5f);
            List<Vector3> originalSamples = new List<Vector3>();
            List<Vector3> morphedSamples = new List<Vector3>();
            HashSet<int> sampleVertices = new HashSet<int>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (!IsValidBodySurfaceTriangleVertex(sourceMesh, a, count)
                    || !IsValidBodySurfaceTriangleVertex(sourceMesh, b, count)
                    || !IsValidBodySurfaceTriangleVertex(sourceMesh, c, count))
                    continue;
                if (!topological.Contains(a) && !topological.Contains(b) && !topological.Contains(c))
                    continue;
                if (!IsEligibleBodySurfaceVertex(referenceEligible, a, count)
                    && !IsEligibleBodySurfaceVertex(referenceEligible, b, count)
                    && !IsEligibleBodySurfaceVertex(referenceEligible, c, count))
                    continue;

                Vector3 q0 = sourceMesh.OriginalWorld[a];
                Vector3 q1 = sourceMesh.OriginalWorld[b];
                Vector3 q2 = sourceMesh.OriginalWorld[c];
                Vector3 mq0 = sourceMesh.MorphedWorld[a];
                Vector3 mq1 = sourceMesh.MorphedWorld[b];
                Vector3 mq2 = sourceMesh.MorphedWorld[c];
                Vector3 normal = Vector3.Cross(q1 - q0, q2 - q0);
                Vector3 morphedNormal = Vector3.Cross(mq1 - mq0, mq2 - mq0);
                if (!IsFinite(normal) || !IsFinite(morphedNormal) || normal.sqrMagnitude < minAreaSq || morphedNormal.sqrMagnitude < minAreaSq)
                    continue;

                Vector3 n = normal.normalized;
                Vector3 mn = morphedNormal.normalized;
                if (Vector3.Dot(n, fwd) < 0.25f)
                    continue;
                if (Vector3.Dot(n, mn) < -0.25f)
                    continue;

                Vector3 triCenter = (q0 + q1 + q2) / 3f;
                Vector3 rel = triCenter - origin;
                float triSide = Vector3.Dot(rel, right);
                float triUp = Vector3.Dot(rel, up);
                float triFwd = Vector3.Dot(rel, fwd);
                if (Mathf.Abs(triSide - candidateSide) > sideLimit)
                    continue;
                if (Mathf.Abs(triUp - candidateCenterUp) > upLimit)
                    continue;
                if (triFwd < candidateFwd - backLimit)
                    continue;

                AddVirtualFitSample(sourceMesh, a, sampleVertices, originalSamples, morphedSamples);
                AddVirtualFitSample(sourceMesh, b, sampleVertices, originalSamples, morphedSamples);
                AddVirtualFitSample(sourceMesh, c, sampleVertices, originalSamples, morphedSamples);
            }

            if (originalSamples.Count < 3)
                return false;

            if (!TryFitVirtualPlaneComponent(originalSamples, originalSamples, origin, right, up, fwd, 2, out fit.OriginalPlaneA, out fit.OriginalPlaneSide, out fit.OriginalPlaneUp))
                return false;
            if (!TryFitVirtualPlaneComponent(originalSamples, morphedSamples, origin, right, up, fwd, 0, out fit.MorphedSidePlaneA, out fit.MorphedSidePlaneSide, out fit.MorphedSidePlaneUp))
                return false;
            if (!TryFitVirtualPlaneComponent(originalSamples, morphedSamples, origin, right, up, fwd, 1, out fit.MorphedUpPlaneA, out fit.MorphedUpPlaneSide, out fit.MorphedUpPlaneUp))
                return false;
            if (!TryFitVirtualPlaneComponent(originalSamples, morphedSamples, origin, right, up, fwd, 2, out fit.MorphedPlaneA, out fit.MorphedPlaneSide, out fit.MorphedPlaneUp))
                return false;

            float maxSideDelta = 0f;
            float maxUpDelta = 0f;
            for (int i = 0; i < originalSamples.Count; i++)
            {
                Vector3 rel = originalSamples[i] - origin;
                maxSideDelta = Mathf.Max(maxSideDelta, Mathf.Abs(Vector3.Dot(rel, right) - candidateSide));
                maxUpDelta = Mathf.Max(maxUpDelta, Mathf.Abs(Vector3.Dot(rel, up) - candidateCenterUp));
            }

            float minHalfWidth = Mathf.Max(RelSide(0.024f), context.CellSize * 0.75f);
            float maxHalfWidth = Mathf.Max(minHalfWidth, RelSide(0.085f));
            float minHalfHeight = Mathf.Max(RelUp(0.012f), context.CellSize * 0.35f);
            float maxHalfHeight = Mathf.Max(minHalfHeight, RelUp(0.055f));

            fit.CenterSide = candidateSide;
            fit.HalfWidth = Mathf.Clamp(maxSideDelta, minHalfWidth, maxHalfWidth);
            float candidateHalfHeight = Mathf.Max((candidateMaxUp - candidateMinUp) * 0.5f + Mathf.Max(RelUp(0.006f), context.CellSize * 0.2f), maxUpDelta * 0.35f);
            fit.MinUp = candidateCenterUp - Mathf.Clamp(candidateHalfHeight, minHalfHeight, maxHalfHeight);
            fit.MaxUp = candidateCenterUp + Mathf.Clamp(candidateHalfHeight, minHalfHeight, maxHalfHeight);
            fit.SampleCount = originalSamples.Count;
            return true;
        }

        static HashSet<int> CollectTopologicalVertexSet(List<int>[] neighbors, int a, int b, int c, int depth)
        {
            if (neighbors == null)
                return null;

            HashSet<int> result = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            Queue<int> depths = new Queue<int>();
            AddTopologicalSeed(neighbors, result, queue, depths, a);
            AddTopologicalSeed(neighbors, result, queue, depths, b);
            AddTopologicalSeed(neighbors, result, queue, depths, c);

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                int d = depths.Dequeue();
                if (d >= depth) continue;
                if (v < 0 || v >= neighbors.Length || neighbors[v] == null) continue;

                List<int> ns = neighbors[v];
                for (int i = 0; i < ns.Count; i++)
                {
                    int n = ns[i];
                    if (n < 0 || n >= neighbors.Length || !result.Add(n)) continue;
                    queue.Enqueue(n);
                    depths.Enqueue(d + 1);
                }
            }

            return result;
        }

        static void AddTopologicalSeed(List<int>[] neighbors, HashSet<int> result, Queue<int> queue, Queue<int> depths, int vertex)
        {
            if (neighbors == null || result == null || queue == null || depths == null) return;
            if (vertex < 0 || vertex >= neighbors.Length || !result.Add(vertex)) return;
            queue.Enqueue(vertex);
            depths.Enqueue(0);
        }

        static void AddVirtualFitSample(
            BodyReferenceMesh sourceMesh,
            int vertex,
            HashSet<int> sampleVertices,
            List<Vector3> originalSamples,
            List<Vector3> morphedSamples)
        {
            if (sourceMesh == null || sampleVertices == null || originalSamples == null || morphedSamples == null)
                return;
            if (!sampleVertices.Add(vertex))
                return;
            if (sourceMesh.OriginalWorld == null || sourceMesh.MorphedWorld == null || sourceMesh.Valid == null)
                return;
            if (vertex < 0 || vertex >= sourceMesh.Valid.Length || !sourceMesh.Valid[vertex])
                return;
            if (vertex >= sourceMesh.OriginalWorld.Length || vertex >= sourceMesh.MorphedWorld.Length)
                return;
            Vector3 original = sourceMesh.OriginalWorld[vertex];
            Vector3 morphed = sourceMesh.MorphedWorld[vertex];
            if (!IsFinite(original) || !IsFinite(morphed))
                return;
            originalSamples.Add(original);
            morphedSamples.Add(morphed);
        }

        static bool TryFitVirtualPlaneComponent(
            List<Vector3> parameterSamples,
            List<Vector3> targetSamples,
            Vector3 origin,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            int component,
            out float a,
            out float sideCoeff,
            out float upCoeff)
        {
            a = 0f;
            sideCoeff = 0f;
            upCoeff = 0f;
            if (parameterSamples == null || targetSamples == null || parameterSamples.Count == 0 || targetSamples.Count != parameterSamples.Count)
                return false;

            float n = 0f;
            float sumS = 0f;
            float sumU = 0f;
            float sumSS = 0f;
            float sumSU = 0f;
            float sumUU = 0f;
            float sumT = 0f;
            float sumST = 0f;
            float sumUT = 0f;

            for (int i = 0; i < parameterSamples.Count; i++)
            {
                Vector3 rel = parameterSamples[i] - origin;
                float s = Vector3.Dot(rel, right);
                float u = Vector3.Dot(rel, up);
                Vector3 targetRel = targetSamples[i] - origin;
                float t = component == 0
                    ? Vector3.Dot(targetRel, right)
                    : (component == 1 ? Vector3.Dot(targetRel, up) : Vector3.Dot(targetRel, fwd));

                n += 1f;
                sumS += s;
                sumU += u;
                sumSS += s * s;
                sumSU += s * u;
                sumUU += u * u;
                sumT += t;
                sumST += s * t;
                sumUT += u * t;
            }

            float det = Determinant3(
                n, sumS, sumU,
                sumS, sumSS, sumSU,
                sumU, sumSU, sumUU);
            if (Mathf.Abs(det) < 1e-12f)
            {
                a = sumT / Mathf.Max(n, 1f);
                sideCoeff = 0f;
                upCoeff = 0f;
                return IsFinite(a);
            }

            a = Determinant3(
                sumT, sumS, sumU,
                sumST, sumSS, sumSU,
                sumUT, sumSU, sumUU) / det;
            sideCoeff = Determinant3(
                n, sumT, sumU,
                sumS, sumST, sumSU,
                sumU, sumUT, sumUU) / det;
            upCoeff = Determinant3(
                n, sumS, sumT,
                sumS, sumSS, sumST,
                sumU, sumSU, sumUT) / det;
            return IsFinite(a) && IsFinite(sideCoeff) && IsFinite(upCoeff);
        }

        static float Determinant3(
            float a00, float a01, float a02,
            float a10, float a11, float a12,
            float a20, float a21, float a22)
        {
            return a00 * (a11 * a22 - a12 * a21)
                - a01 * (a10 * a22 - a12 * a20)
                + a02 * (a10 * a21 - a11 * a20);
        }

        static float EvaluateVirtualFwdPlane(float a, float sideCoeff, float upCoeff, float side, float upCoord)
        {
            return a + sideCoeff * side + upCoeff * upCoord;
        }

        static Vector3 ComposeVirtualFittedMorphedPoint(
            Vector3 origin,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            VirtualSeamSurfaceFit fit,
            float side,
            float upCoord)
        {
            float morphedSide = EvaluateVirtualFwdPlane(fit.MorphedSidePlaneA, fit.MorphedSidePlaneSide, fit.MorphedSidePlaneUp, side, upCoord);
            float morphedUp = EvaluateVirtualFwdPlane(fit.MorphedUpPlaneA, fit.MorphedUpPlaneSide, fit.MorphedUpPlaneUp, side, upCoord);
            float morphedFwd = EvaluateVirtualFwdPlane(fit.MorphedPlaneA, fit.MorphedPlaneSide, fit.MorphedPlaneUp, side, upCoord);
            return ComposeVirtualSurfacePoint(origin, right, up, fwd, morphedSide, morphedUp, morphedFwd);
        }

        static void ComputeVirtualFrontSeamCoords(
            Vector3 origin,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            out float side,
            out float minUp,
            out float maxUp,
            out float maxFwd)
        {
            Vector3 r0 = p0 - origin;
            Vector3 r1 = p1 - origin;
            Vector3 r2 = p2 - origin;
            float s0 = Vector3.Dot(r0, right);
            float s1 = Vector3.Dot(r1, right);
            float s2 = Vector3.Dot(r2, right);
            float u0 = Vector3.Dot(r0, up);
            float u1 = Vector3.Dot(r1, up);
            float u2 = Vector3.Dot(r2, up);
            float f0 = Vector3.Dot(r0, fwd);
            float f1 = Vector3.Dot(r1, fwd);
            float f2 = Vector3.Dot(r2, fwd);

            side = (s0 + s1 + s2) / 3f;
            minUp = Mathf.Min(u0, Mathf.Min(u1, u2));
            maxUp = Mathf.Max(u0, Mathf.Max(u1, u2));
            maxFwd = Mathf.Max(f0, Mathf.Max(f1, f2));
        }

        static Vector3 ComposeVirtualSurfacePoint(
            Vector3 origin,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float side,
            float upCoord,
            float fwdCoord)
        {
            return origin + right * side + up * upCoord + fwd * fwdCoord;
        }

        static void AddVirtualBodySurfaceTriangle(
            MorphReferenceContext context,
            BodyReferenceMesh virtualMesh,
            int a,
            int b,
            int c)
        {
            if (context == null || virtualMesh == null || virtualMesh.OriginalWorld == null)
                return;

            Vector3 p0 = virtualMesh.OriginalWorld[a];
            Vector3 p1 = virtualMesh.OriginalWorld[b];
            Vector3 p2 = virtualMesh.OriginalWorld[c];
            Vector3 center = (p0 + p1 + p2) / 3f;
            float radiusSq = (p0 - center).sqrMagnitude;
            radiusSq = Mathf.Max(radiusSq, (p1 - center).sqrMagnitude);
            radiusSq = Mathf.Max(radiusSq, (p2 - center).sqrMagnitude);

            int triIndex = context.SurfaceTriangles.Count;
            context.SurfaceTriangles.Add(new BodySurfaceTriangle
            {
                Mesh = virtualMesh,
                A = a,
                B = b,
                C = c,
                Center = center,
                RadiusSq = radiusSq,
            });
            AddBodySurfaceTriangleForVertex(virtualMesh, a, triIndex);
            AddBodySurfaceTriangleForVertex(virtualMesh, b, triIndex);
            AddBodySurfaceTriangleForVertex(virtualMesh, c, triIndex);
            AddBodySurfaceTriangleToBuckets(context, triIndex, p0, p1, p2);
            context.VirtualSurfaceTriangles++;
        }

        static bool IsEligibleBodySurfaceVertex(bool[] eligible, int index, int count)
        {
            return eligible != null && index >= 0 && index < count && index < eligible.Length && eligible[index];
        }

        static bool IsValidBodySurfaceTriangleVertex(BodyReferenceMesh mesh, int index, int count)
        {
            return index >= 0
                && index < count
                && mesh.Valid != null
                && index < mesh.Valid.Length
                && mesh.Valid[index]
                && mesh.OriginalWorld != null
                && mesh.MorphedWorld != null
                && index < mesh.OriginalWorld.Length
                && index < mesh.MorphedWorld.Length
                && IsFinite(mesh.OriginalWorld[index])
                && IsFinite(mesh.MorphedWorld[index]);
        }

        static void AddBodySurfaceTriangleForVertex(BodyReferenceMesh mesh, int vertexIndex, int triIndex)
        {
            if (mesh == null || mesh.SurfaceTrianglesByVertex == null) return;
            if (vertexIndex < 0 || vertexIndex >= mesh.SurfaceTrianglesByVertex.Length) return;
            List<int> list = mesh.SurfaceTrianglesByVertex[vertexIndex];
            if (list == null)
            {
                list = new List<int>(6);
                mesh.SurfaceTrianglesByVertex[vertexIndex] = list;
            }
            list.Add(triIndex);
        }

        static void AddBodySurfaceTriangleToBuckets(
            MorphReferenceContext context,
            int triIndex,
            Vector3 p0,
            Vector3 p1,
            Vector3 p2)
        {
            if (context == null) return;

            float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x));
            float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y));
            float minZ = Mathf.Min(p0.z, Mathf.Min(p1.z, p2.z));
            float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x));
            float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y));
            float maxZ = Mathf.Max(p0.z, Mathf.Max(p1.z, p2.z));

            int minBx, minBy, minBz;
            int maxBx, maxBy, maxBz;
            BodySurfaceBucketCoords(new Vector3(minX, minY, minZ), context.CellSize, out minBx, out minBy, out minBz);
            BodySurfaceBucketCoords(new Vector3(maxX, maxY, maxZ), context.CellSize, out maxBx, out maxBy, out maxBz);

            for (int x = minBx; x <= maxBx; x++)
                for (int y = minBy; y <= maxBy; y++)
                    for (int z = minBz; z <= maxBz; z++)
                    {
                        int key = BodySurfaceBucketKey(x, y, z);
                        List<int> list;
                        if (!context.SurfaceBuckets.TryGetValue(key, out list))
                        {
                            list = new List<int>(4);
                            context.SurfaceBuckets[key] = list;
                        }
                        list.Add(triIndex);
                    }
        }

        static void ApplyClothThicknessInheritancePostprocess(
            Mesh mesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorld,
            Vector3[] oldMorphedWorld,
            bool[] valid,
            bool[] oldFinalChanged,
            Vector3[] newLocalVerts,
            MeshMorphClass meshClass,
            bool isSkirtLikeOuter,
            bool skirtLowerBoundaryValid,
            float skirtLowerBoundaryUp,
            Vector3 center,
            Vector3 traceCenter,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            VertexMorphTrace[] traces,
            ref DeformStats stats)
        {
            if (_morphReferenceContext == null || _morphReferenceContext.BodyPoints.Count == 0)
                return;
            if (mesh == null || localOriginalVerts == null || weights == null || boneMatrices == null
                || originalWorld == null || oldMorphedWorld == null || valid == null
                || oldFinalChanged == null || newLocalVerts == null)
                return;

            int count = Mathf.Min(newLocalVerts.Length,
                Mathf.Min(localOriginalVerts.Length,
                Mathf.Min(weights.Length,
                Mathf.Min(originalWorld.Length,
                Mathf.Min(oldMorphedWorld.Length,
                Mathf.Min(valid.Length, oldFinalChanged.Length))))));
            if (count <= 0) return;

            Vector3[] postWorld = (Vector3[])oldMorphedWorld.Clone();
            bool[] postChanged = new bool[count];
            if (meshClass == MeshMorphClass.OuterCloth)
            {
                float diagnosticBoundaryUp = skirtLowerBoundaryUp;
                bool diagnosticBoundaryValid = skirtLowerBoundaryValid
                    || TryComputeSkirtBoundaryUpFromOldChanged(
                        originalWorld,
                        oldMorphedWorld,
                        valid,
                        oldFinalChanged,
                        center,
                        up,
                        fwd,
                        out diagnosticBoundaryUp);
                RecordSkirtHemEligibilityTrace(
                    originalWorld,
                    valid,
                    oldFinalChanged,
                    diagnosticBoundaryValid,
                    diagnosticBoundaryUp,
                    center,
                    up,
                    fwd,
                    traces);
            }

            float oldFinalMinDelta = 0f;
            for (int i = 0; i < count; i++)
            {
                VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                RecordOldFinalChangedTrace(trace, valid[i], oldFinalChanged[i], originalWorld[i], oldMorphedWorld[i], oldFinalMinDelta);
                if (!valid[i])
                    continue;
                if (!oldFinalChanged[i])
                {
                    SetClothInheritSkipTrace(trace, "body:skip:not-oldFinalChanged");
                    continue;
                }

                Vector3 inherited;
                ClothInheritDebug inheritDebug;
                if (!TryInheritFromNearestBodyFrame(originalWorld[i], _morphReferenceContext.BodyPoints, out inherited, out inheritDebug))
                {
                    RecordClothInheritFailTrace(trace, "body", "body:no-frame", inheritDebug);
                    SetClothInheritSkipTrace(trace, "body:no-frame");
                    continue;
                }

                Vector3 preWorld = postWorld[i];
                postWorld[i] = inherited;
                postChanged[i] = true;
                RecordClothInheritTrace(
                    trace,
                    "body",
                    "body:applied",
                    inheritDebug,
                    preWorld,
                    inherited,
                    traceCenter,
                    right,
                    up,
                    fwd);
            }

            if (isSkirtLikeOuter)
                ApplySkirtHemThicknessInheritance(
                    mesh,
                    originalWorld,
                    oldMorphedWorld,
                    valid,
                    oldFinalChanged,
                    postWorld,
                    postChanged,
                    skirtLowerBoundaryValid,
                    skirtLowerBoundaryUp,
                    center,
                    traceCenter,
                    right,
                    up,
                    fwd,
                    traces);

            for (int i = 0; i < count; i++)
            {
                if (!postChanged[i]) continue;

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                Vector3 local = skin.inverse.MultiplyPoint3x4(postWorld[i]);
                newLocalVerts[i] = local;
                oldMorphedWorld[i] = postWorld[i];

                float moved = (local - localOriginalVerts[i]).magnitude;
                if (moved > 0.00001f)
                {
                    if (moved > stats.MaxDelta) stats.MaxDelta = moved;
                }
            }
        }

        static void RecordOldFinalChangedTrace(
            VertexMorphTrace trace,
            bool valid,
            bool changed,
            Vector3 originalWorld,
            Vector3 oldMorphedWorld,
            float minDelta)
        {
            if (trace == null) return;

            float delta = valid ? (oldMorphedWorld - originalWorld).magnitude : 0f;
            trace.OldFinalChanged = valid && changed;
            trace.OldFinalDeltaWorldLen = delta;
            trace.OldFinalMinDelta = minDelta;
            if (!valid)
                trace.OldFinalReason = "invalid";
            else
                trace.OldFinalReason = changed ? "changed:position-different" : "unchanged:position-same";
        }

        static void RecordSkirtHemEligibilityTrace(
            Vector3[] originalWorld,
            bool[] valid,
            bool[] oldFinalChanged,
            bool boundaryValid,
            float boundaryUp,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            VertexMorphTrace[] traces)
        {
            if (traces == null || originalWorld == null || valid == null || oldFinalChanged == null)
                return;

            int count = Mathf.Min(traces.Length,
                Mathf.Min(originalWorld.Length, Mathf.Min(valid.Length, oldFinalChanged.Length)));
            float fwdThreshold = RelFwd(SkirtFrontPlaneFwdOffset);
            for (int i = 0; i < count; i++)
            {
                VertexMorphTrace trace = traces[i];
                if (trace == null) continue;

                trace.SkirtHemBoundaryValid = boundaryValid;
                trace.SkirtHemBoundaryUp = boundaryUp;
                trace.SkirtHemFwdThreshold = fwdThreshold;

                if (!valid[i])
                {
                    trace.SkirtHemReason = "skirtHem:invalid";
                    continue;
                }

                Vector3 rel = originalWorld[i] - center;
                float localUp = Vector3.Dot(rel, up);
                float localFwd = Vector3.Dot(rel, fwd);
                bool front = localFwd > fwdThreshold;
                bool lower = boundaryValid && localUp < boundaryUp;
                bool candidate = oldFinalChanged[i] && front && lower;
                trace.SkirtHemFront = front;
                trace.SkirtHemLowerRange = lower;
                trace.SkirtHemCandidate = candidate;

                if (!boundaryValid)
                    trace.SkirtHemReason = "skirtHem:no-boundary";
                else if (!oldFinalChanged[i])
                    trace.SkirtHemReason = "skirtHem:not-oldFinalChanged";
                else if (!front)
                    trace.SkirtHemReason = "skirtHem:not-front";
                else if (!lower)
                    trace.SkirtHemReason = "skirtHem:not-lower-range";
                else
                    trace.SkirtHemReason = "skirtHem:candidate";
            }
        }

        static void SetClothInheritSkipTrace(VertexMorphTrace trace, string reason)
        {
            if (trace == null || trace.ClothInheritApplied) return;
            trace.ClothInheritReason = reason ?? string.Empty;
        }

        static void RecordClothInheritFailTrace(
            VertexMorphTrace trace,
            string kind,
            string reason,
            ClothInheritDebug debug)
        {
            if (trace == null) return;

            trace.ClothInheritFailKind = kind ?? string.Empty;
            trace.ClothInheritFailReason = !string.IsNullOrEmpty(debug.FailReason)
                ? debug.FailReason
                : (reason ?? string.Empty);
            trace.ClothInheritFailSourceMesh = debug.SourceMesh ?? string.Empty;
            trace.ClothInheritFailSourceVertex = debug.OriginIndex;
            trace.ClothInheritFailFrameCandidateCount = debug.CandidateCount;
            trace.ClothInheritFailFrameScore = debug.FrameScore;
            trace.ClothInheritFailNearestDistance = debug.NearestDistance;
        }

        static void RecordClothInheritTrace(
            VertexMorphTrace trace,
            string kind,
            string reason,
            ClothInheritDebug debug,
            Vector3 preWorld,
            Vector3 postWorld,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd)
        {
            if (trace == null) return;

            Vector3 preCoord = ProjectPointCoord(preWorld, center, right, up, fwd);
            Vector3 postCoord = ProjectPointCoord(postWorld, center, right, up, fwd);
            Vector3 delta = postWorld - preWorld;
            Vector3 deltaCoord = ProjectVectorCoord(delta, right, up, fwd);
            Vector3 deltaDir = delta.sqrMagnitude > 1e-12f
                ? ProjectVectorCoord(delta.normalized, right, up, fwd)
                : Vector3.zero;
            Vector3 finalOffset = postWorld - debug.MorphedOrigin;

            trace.ClothInheritApplied = true;
            trace.ClothInheritKind = kind ?? string.Empty;
            trace.ClothInheritReason = reason ?? string.Empty;
            trace.ClothInheritSourceMesh = debug.SourceMesh ?? string.Empty;
            trace.ClothInheritSourceVertex = debug.OriginIndex;
            trace.ClothInheritFrameA = debug.PointAIndex;
            trace.ClothInheritFrameB = debug.PointBIndex;
            trace.ClothInheritFrameCandidateCount = debug.CandidateCount;
            trace.ClothInheritFrameScore = debug.FrameScore;
            trace.ClothInheritNearestDistance = debug.NearestDistance;
            trace.ClothInheritOriginalThickness = debug.OriginalThickness;
            trace.ClothInheritRawThickness = debug.RawThickness;
            trace.ClothInheritFinalThickness = debug.FinalThickness;
            trace.ClothInheritLocalOffset = debug.LocalOffset;
            trace.ClothInheritPreWorld = preWorld;
            trace.ClothInheritPostWorld = postWorld;
            trace.ClothInheritPreCoord = preCoord;
            trace.ClothInheritPostCoord = postCoord;
            trace.ClothInheritDeltaCoord = deltaCoord;
            trace.ClothInheritDeltaWorldLen = delta.magnitude;
            trace.ClothInheritDeltaDirCoord = deltaDir;
            trace.ClothInheritFinalOffsetCoord = ProjectVectorCoord(finalOffset, right, up, fwd);
            trace.FwdClothInherit = postCoord.z;
            trace.FwdFinal = postCoord.z;
        }

        static Vector3 ProjectPointCoord(Vector3 point, Vector3 center, Vector3 right, Vector3 up, Vector3 fwd)
        {
            return ProjectVectorCoord(point - center, right, up, fwd);
        }

        static Vector3 ProjectVectorCoord(Vector3 value, Vector3 right, Vector3 up, Vector3 fwd)
        {
            return new Vector3(
                Vector3.Dot(value, right),
                Vector3.Dot(value, up),
                Vector3.Dot(value, fwd));
        }

        static string MeshDebugId(Mesh mesh)
        {
            if (mesh == null)
                return string.Empty;
            return (mesh.name ?? string.Empty) + "#" + mesh.GetInstanceID().ToString(CultureInfo.InvariantCulture);
        }

        static string MeshDebugId(BodyReferenceMesh mesh)
        {
            if (mesh == null)
                return string.Empty;
            return (mesh.MeshId ?? string.Empty) + "#" + mesh.MeshInstanceId.ToString(CultureInfo.InvariantCulture);
        }

        static bool TryInheritFromNearestBodyFrame(
            Vector3 originalPoint,
            List<BodyReferencePoint> bodyRefs,
            out Vector3 inherited,
            out ClothInheritDebug debug)
        {
            inherited = originalPoint;
            debug = new ClothInheritDebug();
            if (bodyRefs == null || bodyRefs.Count == 0)
            {
                debug.FailReason = "body:no-reference-points";
                return false;
            }

            int nearest = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < bodyRefs.Count; i++)
            {
                float d = (bodyRefs[i].OriginalWorld - originalPoint).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                nearest = i;
            }

            if (nearest >= 0 && TryInheritFromBodyReference(originalPoint, bodyRefs[nearest], Mathf.Sqrt(bestDist), out inherited, out debug))
                return true;

            if (nearest < 0)
                debug.FailReason = "body:no-nearest-point";
            else if (string.IsNullOrEmpty(debug.FailReason))
                debug.FailReason = "body:inherit-failed";
            return false;
        }

        static bool TryInheritFromBodyReference(
            Vector3 originalPoint,
            BodyReferencePoint refPoint,
            float nearestDistance,
            out Vector3 inherited,
            out ClothInheritDebug debug)
        {
            inherited = originalPoint;
            debug = new ClothInheritDebug();
            if (refPoint.Mesh == null)
            {
                debug.FailReason = "body:null-source-mesh";
                return false;
            }

            debug.SourceMesh = (refPoint.Mesh.MeshId ?? string.Empty)
                + "#" + refPoint.Mesh.MeshInstanceId.ToString(CultureInfo.InvariantCulture);
            debug.OriginIndex = refPoint.Index;
            debug.NearestDistance = nearestDistance;

            MorphFrame originalFrame;
            MorphFrame morphedFrame;
            int pointA;
            int pointB;
            int candidateCount;
            float frameScore;
            string failReason;
            if (!TryBuildFrameFromMeshVertices(
                refPoint.Mesh.OriginalWorld,
                refPoint.Mesh.MorphedWorld,
                refPoint.Mesh.Neighbors,
                refPoint.Mesh.Eligible,
                refPoint.Index,
                out originalFrame,
                out morphedFrame,
                out pointA,
                out pointB,
                out candidateCount,
                out frameScore,
                out failReason))
            {
                debug.CandidateCount = candidateCount;
                debug.FrameScore = frameScore;
                debug.FailReason = failReason;
                return false;
            }

            if (!TryInheritPointByFrames(originalPoint, originalFrame, morphedFrame, out inherited, ref debug))
                return false;

            debug.PointAIndex = pointA;
            debug.PointBIndex = pointB;
            debug.CandidateCount = candidateCount;
            debug.FrameScore = frameScore;
            return true;
        }

        static void ApplySkirtHemThicknessInheritance(
            Mesh mesh,
            Vector3[] originalWorld,
            Vector3[] oldMorphedWorld,
            bool[] valid,
            bool[] oldFinalChanged,
            Vector3[] postWorld,
            bool[] postChanged,
            bool skirtLowerBoundaryValid,
            float skirtLowerBoundaryUp,
            Vector3 center,
            Vector3 traceCenter,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            VertexMorphTrace[] traces)
        {
            if (mesh == null || originalWorld == null || oldMorphedWorld == null || valid == null
                || oldFinalChanged == null || postWorld == null || postChanged == null)
                return;

            int count = Mathf.Min(originalWorld.Length,
                Mathf.Min(oldMorphedWorld.Length,
                Mathf.Min(valid.Length,
                Mathf.Min(oldFinalChanged.Length,
                Mathf.Min(postWorld.Length, postChanged.Length)))));

            float boundaryUp = skirtLowerBoundaryUp;
            if (!skirtLowerBoundaryValid
                && !TryComputeSkirtBoundaryUpFromOldChanged(originalWorld, oldMorphedWorld, valid, oldFinalChanged, center, up, fwd, out boundaryUp))
                return;

            float fwdThreshold = RelFwd(SkirtFrontPlaneFwdOffset);
            bool[] lowerSkirtRange = new bool[count];
            bool[] hem = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;

                Vector3 rel = originalWorld[i] - center;
                float localUp = Vector3.Dot(rel, up);
                float localFwd = Vector3.Dot(rel, fwd);
                bool front = localFwd > fwdThreshold;
                lowerSkirtRange[i] = front && localUp < boundaryUp;
                hem[i] = oldFinalChanged[i] && lowerSkirtRange[i];
            }

            List<BoundaryReferenceMesh> bodyBoundaryMeshes = BuildBodyBoundaryReferenceMeshes(boundaryUp, center, up, fwd);

            for (int i = 0; i < count; i++)
            {
                if (!hem[i]) continue;

                Vector3 inherited;
                ClothInheritDebug inheritDebug;
                if (!TryInheritFromNearestBodyBoundaryFrame(
                    originalWorld[i],
                    bodyBoundaryMeshes,
                    out inherited,
                    out inheritDebug))
                {
                    VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                    RecordClothInheritFailTrace(trace, "skirtHem", "skirtHem:no-frame", inheritDebug);
                    SetClothInheritSkipTrace(trace, "skirtHem:no-frame");
                    if (trace != null)
                        trace.SkirtHemReason = "skirtHem:no-frame";
                    continue;
                }

                float blend = GetSkirtHemBodyRangeBlend(inherited, center, up);
                inherited = Vector3.Lerp(originalWorld[i], inherited, blend);

                Vector3 preWorld = postWorld[i];
                postWorld[i] = inherited;
                postChanged[i] = true;
                RecordClothInheritTrace(
                    traces != null && i < traces.Length ? traces[i] : null,
                    "skirtHem",
                    "skirtHem:applied",
                    inheritDebug,
                    preWorld,
                    inherited,
                    traceCenter,
                    right,
                    up,
                    fwd);
                VertexMorphTrace appliedTrace = traces != null && i < traces.Length ? traces[i] : null;
                if (appliedTrace != null)
                {
                    appliedTrace.SkirtHemBlend = blend;
                    appliedTrace.SkirtHemReason = "skirtHem:applied";
                }
            }
        }

        static float GetSkirtHemBodyRangeBlend(Vector3 originalPoint, Vector3 center, Vector3 up)
        {
            if (_morphReferenceContext == null || !_morphReferenceContext.BodyChangedUpRangeValid)
                return 1f;

            float a = _morphReferenceContext.BodyChangedMinUp;
            float b = _morphReferenceContext.BodyChangedMaxUp;
            float span = b - a;
            if (span <= 1e-6f)
                return 1f;

            float pointUp = Vector3.Dot(originalPoint - center, up);
            float fadeScale = Mathf.Max(0f, SkirtHemFadeRangeScale);
            float c = a - fadeScale * span;
            if (c >= a)
                return pointUp >= a ? 1f : 0f;
            if (pointUp >= a)
                return 1f;
            if (pointUp <= c)
                return 0f;
            return Mathf.Clamp01((pointUp - c) / (a - c));
        }

        static bool TryComputeSkirtBoundaryUpFromOldChanged(
            Vector3[] originalWorld,
            Vector3[] oldMorphedWorld,
            bool[] valid,
            bool[] oldFinalChanged,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            out float boundaryUp)
        {
            boundaryUp = 0f;
            if (originalWorld == null || oldMorphedWorld == null || valid == null || oldFinalChanged == null)
                return false;

            int count = Mathf.Min(originalWorld.Length,
                Mathf.Min(oldMorphedWorld.Length, Mathf.Min(valid.Length, oldFinalChanged.Length)));
            float minMove = RelGeneral(SkirtLowerMinDelta);
            float fwdThreshold = RelFwd(SkirtFrontPlaneFwdOffset);
            float maxMove = 0f;
            for (int i = 0; i < count; i++)
            {
                if (!valid[i] || !oldFinalChanged[i]) continue;
                if (Vector3.Dot(originalWorld[i] - center, fwd) <= fwdThreshold) continue;
                float move = (oldMorphedWorld[i] - originalWorld[i]).magnitude;
                if (move > maxMove)
                    maxMove = move;
            }

            if (maxMove <= minMove)
                return false;

            List<float> ups = new List<float>();
            float strongMove = Mathf.Max(minMove, maxMove * 0.75f);
            for (int i = 0; i < count; i++)
            {
                if (!valid[i] || !oldFinalChanged[i]) continue;
                if (Vector3.Dot(originalWorld[i] - center, fwd) <= fwdThreshold) continue;
                float move = (oldMorphedWorld[i] - originalWorld[i]).magnitude;
                if (move < strongMove) continue;
                ups.Add(Vector3.Dot(originalWorld[i] - center, up));
            }

            if (ups.Count < 3)
            {
                ups.Clear();
                for (int i = 0; i < count; i++)
                {
                    if (!valid[i] || !oldFinalChanged[i]) continue;
                    if (Vector3.Dot(originalWorld[i] - center, fwd) <= fwdThreshold) continue;
                    float move = (oldMorphedWorld[i] - originalWorld[i]).magnitude;
                    if (move <= minMove) continue;
                    ups.Add(Vector3.Dot(originalWorld[i] - center, up));
                }
            }

            if (ups.Count < 3)
                return false;

            boundaryUp = MedianFloat(ups) + RelUp(SkirtBoundaryUpOffset);
            return true;
        }

        static List<BoundaryReferenceMesh> BuildBodyBoundaryReferenceMeshes(
            float boundaryUp,
            Vector3 center,
            Vector3 up,
            Vector3 fwd)
        {
            List<BoundaryReferenceMesh> result = new List<BoundaryReferenceMesh>();
            if (_morphReferenceContext == null || _morphReferenceContext.BodyMeshes == null)
                return result;

            float fwdThreshold = RelFwd(SkirtFrontPlaneFwdOffset);
            for (int m = 0; m < _morphReferenceContext.BodyMeshes.Count; m++)
            {
                BodyReferenceMesh refMesh = _morphReferenceContext.BodyMeshes[m];
                if (refMesh == null || refMesh.OriginalWorld == null || refMesh.MorphedWorld == null
                    || refMesh.Valid == null || refMesh.Neighbors == null)
                    continue;

                int count = Mathf.Min(refMesh.OriginalWorld.Length,
                    Mathf.Min(refMesh.MorphedWorld.Length, Mathf.Min(refMesh.Valid.Length, refMesh.Neighbors.Length)));
                if (count <= 0) continue;

                bool[] upperEligible = new bool[count];
                bool[] lowerRange = new bool[count];
                bool[] boundary = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    if (!refMesh.Valid[i]) continue;

                    float originalFwd = Vector3.Dot(refMesh.OriginalWorld[i] - center, fwd);
                    if (originalFwd <= fwdThreshold) continue;

                    float morphedUp = Vector3.Dot(refMesh.MorphedWorld[i] - center, up);
                    if (morphedUp >= boundaryUp)
                        upperEligible[i] = true;
                    else
                        lowerRange[i] = true;
                }

                bool hasBoundary = false;
                for (int i = 0; i < count; i++)
                {
                    if (!upperEligible[i]) continue;

                    List<int> ns = refMesh.Neighbors[i];
                    if (ns == null) continue;
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (j < 0 || j >= count || !lowerRange[j]) continue;
                        boundary[i] = true;
                        hasBoundary = true;
                        break;
                    }
                }

                if (!hasBoundary) continue;
                result.Add(new BoundaryReferenceMesh
                {
                    Mesh = refMesh,
                    Boundary = boundary,
                    FrameEligible = upperEligible,
                });
            }

            return result;
        }

        static bool TryInheritFromNearestBodyBoundaryFrame(
            Vector3 originalPoint,
            List<BoundaryReferenceMesh> boundaryMeshes,
            out Vector3 inherited,
            out ClothInheritDebug debug)
        {
            inherited = originalPoint;
            debug = new ClothInheritDebug();
            if (boundaryMeshes == null || boundaryMeshes.Count == 0)
            {
                debug.FailReason = "skirtHem:no-boundary-meshes";
                return false;
            }

            BoundaryReferenceMesh nearestMesh = null;
            int nearestIndex = -1;
            float bestDist = float.MaxValue;
            for (int m = 0; m < boundaryMeshes.Count; m++)
            {
                BoundaryReferenceMesh boundaryMesh = boundaryMeshes[m];
                if (boundaryMesh == null || boundaryMesh.Mesh == null || boundaryMesh.Mesh.OriginalWorld == null
                    || boundaryMesh.Boundary == null)
                    continue;

                int count = Mathf.Min(boundaryMesh.Mesh.OriginalWorld.Length, boundaryMesh.Boundary.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!boundaryMesh.Boundary[i]) continue;
                    float d = (boundaryMesh.Mesh.OriginalWorld[i] - originalPoint).sqrMagnitude;
                    if (d >= bestDist) continue;
                    bestDist = d;
                    nearestMesh = boundaryMesh;
                    nearestIndex = i;
                }
            }

            if (nearestMesh != null && nearestIndex >= 0 && TryInheritFromBoundaryReference(
                originalPoint,
                nearestMesh.Mesh.OriginalWorld,
                nearestMesh.Mesh.MorphedWorld,
                nearestMesh.Mesh.Neighbors,
                nearestMesh.FrameEligible,
                nearestIndex,
                MeshDebugId(nearestMesh.Mesh),
                Mathf.Sqrt(bestDist),
                out inherited,
                out debug))
                return true;

            if (nearestMesh == null || nearestIndex < 0)
                debug.FailReason = "skirtHem:no-boundary-point";
            else if (string.IsNullOrEmpty(debug.FailReason))
                debug.FailReason = "skirtHem:inherit-failed";
            return false;
        }

        static bool TryInheritFromBoundaryReference(
            Vector3 originalPoint,
            Vector3[] originalWorld,
            Vector3[] morphedWorld,
            List<int>[] neighbors,
            bool[] frameEligible,
            int originIndex,
            string sourceMesh,
            float nearestDistance,
            out Vector3 inherited,
            out ClothInheritDebug debug)
        {
            inherited = originalPoint;
            debug = new ClothInheritDebug();
            debug.SourceMesh = sourceMesh ?? string.Empty;
            debug.OriginIndex = originIndex;
            debug.NearestDistance = nearestDistance;
            MorphFrame originalFrame;
            MorphFrame morphedFrame;
            int pointA;
            int pointB;
            int candidateCount;
            float frameScore;
            string failReason;
            if (!TryBuildFrameFromMeshVertices(
                originalWorld,
                morphedWorld,
                neighbors,
                frameEligible,
                originIndex,
                out originalFrame,
                out morphedFrame,
                out pointA,
                out pointB,
                out candidateCount,
                out frameScore,
                out failReason))
            {
                debug.CandidateCount = candidateCount;
                debug.FrameScore = frameScore;
                debug.FailReason = failReason;
                return false;
            }

            if (!TryInheritPointByFrames(originalPoint, originalFrame, morphedFrame, out inherited, ref debug))
                return false;

            debug.PointAIndex = pointA;
            debug.PointBIndex = pointB;
            debug.CandidateCount = candidateCount;
            debug.FrameScore = frameScore;
            return true;
        }

        static Vector3 InheritPointByFrames(Vector3 originalPoint, MorphFrame originalFrame, MorphFrame morphedFrame)
        {
            Vector3 inherited;
            ClothInheritDebug debug = new ClothInheritDebug();
            return TryInheritPointByFrames(originalPoint, originalFrame, morphedFrame, out inherited, ref debug)
                ? inherited
                : originalPoint;
        }

        static bool TryInheritPointByFrames(
            Vector3 originalPoint,
            MorphFrame originalFrame,
            MorphFrame morphedFrame,
            out Vector3 inherited,
            ref ClothInheritDebug debug)
        {
            inherited = originalPoint;
            Vector3 originalOffset = originalPoint - originalFrame.Origin;
            float thicknessDistance = originalOffset.magnitude;
            if (thicknessDistance <= 1e-8f)
            {
                inherited = morphedFrame.Origin;
                debug.OriginalThickness = 0f;
                debug.RawThickness = 0f;
                debug.FinalThickness = 0f;
                debug.MorphedOrigin = morphedFrame.Origin;
                return true;
            }

            Vector3 localOffset;
            if (!originalFrame.TryToLocal(originalOffset, out localOffset))
            {
                debug.FailReason = "inherit:original-frame-inverse-failed";
                return false;
            }

            Vector3 morphedOffset = morphedFrame.ToWorld(localOffset);
            float morphedDistance = morphedOffset.magnitude;
            if (morphedDistance <= 1e-8f)
            {
                inherited = morphedFrame.Origin;
                debug.OriginalThickness = thicknessDistance;
                debug.RawThickness = 0f;
                debug.FinalThickness = 0f;
                debug.LocalOffset = localOffset;
                debug.MorphedOrigin = morphedFrame.Origin;
                return true;
            }

            inherited = morphedFrame.Origin + morphedOffset * (thicknessDistance / morphedDistance);
            debug.OriginalThickness = thicknessDistance;
            debug.RawThickness = morphedDistance;
            debug.FinalThickness = (inherited - morphedFrame.Origin).magnitude;
            debug.LocalOffset = localOffset;
            debug.MorphedOrigin = morphedFrame.Origin;
            return true;
        }

        static bool TryBuildFrameFromMeshVertices(
            Vector3[] originalWorld,
            Vector3[] morphedWorld,
            List<int>[] neighbors,
            bool[] eligible,
            int originIndex,
            out MorphFrame originalFrame,
            out MorphFrame morphedFrame,
            out int pointAIndex,
            out int pointBIndex,
            out int candidateCount,
            out float frameScore,
            out string failReason)
        {
            originalFrame = new MorphFrame();
            morphedFrame = new MorphFrame();
            pointAIndex = -1;
            pointBIndex = -1;
            candidateCount = 0;
            frameScore = 0f;
            failReason = string.Empty;
            if (originalWorld == null || morphedWorld == null || neighbors == null || eligible == null)
            {
                failReason = "frame:null-input";
                return false;
            }
            int count = Mathf.Min(originalWorld.Length, Mathf.Min(morphedWorld.Length, eligible.Length));
            if (originIndex < 0 || originIndex >= count || !eligible[originIndex])
            {
                failReason = "frame:origin-not-eligible";
                return false;
            }

            List<int> candidates = CollectFrameNeighborCandidates(neighbors, eligible, originIndex, count);
            candidateCount = candidates.Count;
            if (candidates.Count < 2)
            {
                failReason = "frame:not-enough-candidates";
                return false;
            }

            bool found = false;
            float bestScore = -1f;
            MorphFrame bestOriginal = new MorphFrame();
            MorphFrame bestMorphed = new MorphFrame();
            int pairCount = 0;
            int originalRejectCount = 0;
            int morphedRejectCount = 0;
            string lastOriginalFail = string.Empty;
            string lastMorphedFail = string.Empty;
            Vector3 originMotion = morphedWorld[originIndex] - originalWorld[originIndex];
            for (int a = 0; a < candidates.Count; a++)
            {
                for (int b = a + 1; b < candidates.Count; b++)
                {
                    pairCount++;
                    int ia = candidates[a];
                    int ib = candidates[b];
                    MorphFrame candidateOriginal;
                    MorphFrame candidateMorphed;
                    float score;
                    string frameFail;
                    bool originalCollinear = IsStrictCollinear(
                        originalWorld[originIndex],
                        originalWorld[ia],
                        originalWorld[ib]);
                    bool morphedCollinear = IsStrictCollinear(
                        morphedWorld[originIndex],
                        morphedWorld[ia],
                        morphedWorld[ib]);
                    if (originalCollinear || morphedCollinear)
                    {
                        if (!TryBuildMotionFallbackFrame(originalWorld[originIndex], originalWorld[ia], originMotion, out candidateOriginal, out score, out frameFail))
                        {
                            originalRejectCount++;
                            lastOriginalFail = frameFail;
                            continue;
                        }
                        float morphedScore;
                        if (!TryBuildMotionFallbackFrame(morphedWorld[originIndex], morphedWorld[ia], originMotion, out candidateMorphed, out morphedScore, out frameFail))
                        {
                            morphedRejectCount++;
                            lastMorphedFail = frameFail;
                            continue;
                        }
                    }
                    else if (!TryBuildFrame(originalWorld[originIndex], originalWorld[ia], originalWorld[ib], out candidateOriginal, out score, out frameFail))
                    {
                        originalRejectCount++;
                        lastOriginalFail = frameFail;
                        continue;
                    }
                    else if (!TryBuildFrame(morphedWorld[originIndex], morphedWorld[ia], morphedWorld[ib], out candidateMorphed, out frameFail))
                    {
                        morphedRejectCount++;
                        lastMorphedFail = frameFail;
                        continue;
                    }

                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bestOriginal = candidateOriginal;
                        bestMorphed = candidateMorphed;
                        pointAIndex = ia;
                        pointBIndex = ib;
                    }
                }
            }

            if (!found)
            {
                failReason = "frame:no-valid-pair pairs=" + pairCount.ToString(CultureInfo.InvariantCulture)
                    + " originalReject=" + originalRejectCount.ToString(CultureInfo.InvariantCulture)
                    + " morphedReject=" + morphedRejectCount.ToString(CultureInfo.InvariantCulture)
                    + " lastOriginal=" + lastOriginalFail
                    + " lastMorphed=" + lastMorphedFail;
                return false;
            }

            originalFrame = bestOriginal;
            morphedFrame = bestMorphed;
            frameScore = bestScore;
            return true;
        }

        static List<int> CollectFrameNeighborCandidates(
            List<int>[] neighbors,
            bool[] eligible,
            int originIndex,
            int count)
        {
            List<int> candidates = new List<int>();
            if (neighbors == null || originIndex < 0 || originIndex >= neighbors.Length)
                return candidates;

            List<int> direct = neighbors[originIndex];
            for (int n = 0; n < direct.Count; n++)
                AddFrameCandidate(candidates, eligible, direct[n], originIndex, count);

            if (candidates.Count >= 2)
                return candidates;

            for (int n = 0; n < direct.Count; n++)
            {
                int j = direct[n];
                if (j < 0 || j >= neighbors.Length) continue;

                List<int> second = neighbors[j];
                for (int s = 0; s < second.Count; s++)
                    AddFrameCandidate(candidates, eligible, second[s], originIndex, count);

                if (candidates.Count >= 2)
                    break;
            }

            return candidates;
        }

        static void AddFrameCandidate(List<int> candidates, bool[] eligible, int index, int originIndex, int count)
        {
            if (index < 0 || index >= count || index == originIndex) return;
            if (eligible == null || index >= eligible.Length || !eligible[index]) return;
            if (!candidates.Contains(index))
                candidates.Add(index);
        }

        static bool TryBuildFrame(Vector3 origin, Vector3 pointA, Vector3 pointB, out MorphFrame frame)
        {
            float score;
            return TryBuildFrame(origin, pointA, pointB, out frame, out score);
        }

        static bool TryBuildFrame(Vector3 origin, Vector3 pointA, Vector3 pointB, out MorphFrame frame, out float score)
        {
            string failReason;
            return TryBuildFrame(origin, pointA, pointB, out frame, out score, out failReason);
        }

        static bool TryBuildFrame(Vector3 origin, Vector3 pointA, Vector3 pointB, out MorphFrame frame, out string failReason)
        {
            float score;
            return TryBuildFrame(origin, pointA, pointB, out frame, out score, out failReason);
        }

        static bool IsStrictCollinear(Vector3 origin, Vector3 pointA, Vector3 pointB)
        {
            Vector3 x = pointA - origin;
            Vector3 y = pointB - origin;
            return x.sqrMagnitude > 0f
                && y.sqrMagnitude > 0f
                && Vector3.Cross(x, y).sqrMagnitude == 0f;
        }

        static bool TryBuildMotionFallbackFrame(
            Vector3 origin,
            Vector3 pointA,
            Vector3 originMotion,
            out MorphFrame frame,
            out float score,
            out string failReason)
        {
            frame = new MorphFrame();
            score = 0f;
            failReason = string.Empty;

            Vector3 x = pointA - origin;
            if (x.sqrMagnitude == 0f)
            {
                failReason = "frame:fallback-x-zero";
                return false;
            }

            Vector3 y = originMotion;
            if (y.sqrMagnitude == 0f)
            {
                failReason = "frame:fallback-motion-zero";
                return false;
            }

            Vector3 normal = Vector3.Cross(x, y);
            float normalSq = normal.sqrMagnitude;
            if (normalSq == 0f)
            {
                failReason = "frame:fallback-motion-collinear";
                return false;
            }

            float tangentScale = Mathf.Sqrt(Mathf.Sqrt(x.sqrMagnitude * y.sqrMagnitude));
            Vector3 z = normal.normalized * tangentScale;

            frame.Origin = origin;
            frame.X = x;
            frame.Y = y;
            frame.Z = z;
            score = normalSq;
            return true;
        }

        static bool TryBuildFrame(Vector3 origin, Vector3 pointA, Vector3 pointB, out MorphFrame frame, out float score, out string failReason)
        {
            frame = new MorphFrame();
            score = 0f;
            failReason = string.Empty;

            Vector3 x = pointA - origin;
            if (x.sqrMagnitude == 0f)
            {
                failReason = "frame:x-zero";
                return false;
            }

            Vector3 y = pointB - origin;
            if (y.sqrMagnitude == 0f)
            {
                failReason = "frame:y-zero";
                return false;
            }

            Vector3 normal = Vector3.Cross(x, y);
            float normalSq = normal.sqrMagnitude;
            if (normalSq == 0f)
            {
                failReason = "frame:normal-zero";
                return false;
            }

            float tangentScale = Mathf.Sqrt(Mathf.Sqrt(x.sqrMagnitude * y.sqrMagnitude));
            Vector3 z = normal.normalized * tangentScale;

            frame.Origin = origin;
            frame.X = x;
            frame.Y = y;
            frame.Z = z;
            score = normalSq;
            return true;
        }

        static Vector3[] BuildDeltaVerts(Vector3[] baseVerts, Vector3[] newVerts)
        {
            if (baseVerts == null || newVerts == null || baseVerts.Length != newVerts.Length)
                return null;

            Vector3[] delta = new Vector3[baseVerts.Length];
            for (int i = 0; i < delta.Length; i++)
                delta[i] = newVerts[i] - baseVerts[i];
            return delta;
        }

        public static string ExportSkirtVertexMorphDump(Maid maid)
        {
            return ExportOuterClothVertexMorphDump(maid, true);
        }

        public static string ExportUpperClothVertexMorphDump(Maid maid)
        {
            return ExportOuterClothVertexMorphDump(maid, false);
        }

        public static string ExportNavelAccessoryTriangleDump(Maid maid)
        {
            if (!IsValid(maid)) return string.Empty;

            PruneRecords(maid);
            EnsureBindPoseWorldForDump(maid);

            string root = BepInEx.Paths.BepInExRootPath;
            if (string.IsNullOrEmpty(root))
                root = System.Environment.CurrentDirectory;

            string dir = Path.Combine(root, "PregnancyNavelAccessoryDumps");
            Directory.CreateDirectory(dir);

            string maidName = SafeDumpName(GetMaidName(maid));
            string fileName = "navel_accessory_"
                + maidName
                + "_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + ".tsv";
            string path = Path.Combine(dir, fileName);

            float progress = PregnancyManager.GetPregnant(maid)
                ? PregnancyManager.GetProgress(maid)
                : GetActiveProgress(maid);
            progress = Mathf.Clamp01(progress);

            MorphReferenceContext previousContext = _morphReferenceContext;
            _morphReferenceContext = new MorphReferenceContext
            {
                MaidKey = maid.GetHashCode(),
                CellSize = Mathf.Max(RelGeneral(0.025f), 0.005f),
            };

            try
            {
                bool bodyRefsOk = _bpWorldCached && BuildRuntimeBodyMorphReferences(maid, progress);
                using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    writer.WriteLine("# COM3D2 Pregnancy navel accessory triangle dump");
                    writer.WriteLine("# maid=" + EscapeDumpCell(GetMaidName(maid)));
                    writer.WriteLine("# activeProgress=" + F(progress));
                    writer.WriteLine("# frameCached=" + (_bpWorldCached ? "1" : "0"));
                    writer.WriteLine("# bodyRefsOk=" + (bodyRefsOk ? "1" : "0"));
                    writer.WriteLine("# rows are triangle vertices; ring 0 is the selected reference triangle, rings 1-2 are adjacent by shared vertex");
                    writer.WriteLine(
                        "meshId\trenderer\tmeshInstance\tactiveSelf\tactiveInHierarchy"
                        + "\trefTriangle\trefReason\trefA\trefB\trefC\trefScore"
                        + "\tring\ttriIndex\tcorner\tvertex\tvertexRole\trecordOk\tdeltaOk\tskinOk"
                        + "\torigLocalX\torigLocalY\torigLocalZ"
                        + "\tcurrentLocalX\tcurrentLocalY\tcurrentLocalZ"
                        + "\tappliedTargetLocalX\tappliedTargetLocalY\tappliedTargetLocalZ"
                        + "\tdeltaLocalX\tdeltaLocalY\tdeltaLocalZ\tdeltaLocalLen"
                        + "\torigWorldX\torigWorldY\torigWorldZ"
                        + "\tcurrentWorldX\tcurrentWorldY\tcurrentWorldZ"
                        + "\tappliedTargetWorldX\tappliedTargetWorldY\tappliedTargetWorldZ"
                        + "\tappliedDeltaWorldX\tappliedDeltaWorldY\tappliedDeltaWorldZ\tappliedDeltaWorldLen"
                        + "\tsurfaceTargetOk\tsurfaceTargetWorldX\tsurfaceTargetWorldY\tsurfaceTargetWorldZ\tsurfaceDeltaWorldLen"
                        + "\tsurfaceFailReason\tsurfaceSearchHit\tsurfaceFullScanHit\tsurfaceTriangleIndex\tsurfaceBodyMesh\tsurfaceBodyMeshInstance"
                        + "\tsurfaceBodyA\tsurfaceBodyB\tsurfaceBodyC\tsurfaceDistance\tsurfaceOffsetLen\tsurfaceMoveLen\tsurfaceNormalDot"
                        + "\tsurfaceBaryX\tsurfaceBaryY\tsurfaceBaryZ"
                        + "\tsurfaceClosestX\tsurfaceClosestY\tsurfaceClosestZ"
                        + "\tsurfaceMorphedX\tsurfaceMorphedY\tsurfaceMorphedZ"
                        + "\tsurfaceOriginalNormalX\tsurfaceOriginalNormalY\tsurfaceOriginalNormalZ"
                        + "\tsurfaceMorphedNormalX\tsurfaceMorphedNormalY\tsurfaceMorphedNormalZ"
                        + "\tsurfaceBodyPointCount\tsurfaceTriangleCount"
                        + "\trigidTargetOk\trigidTargetWorldX\trigidTargetWorldY\trigidTargetWorldZ\trigidDeltaWorldLen"
                        + "\trigidTargetLocalX\trigidTargetLocalY\trigidTargetLocalZ"
                        + "\tbone0\tboneW0\tbone1\tboneW1\tbone2\tboneW2\tbone3\tboneW3");

                    int meshCount = 0;
                    List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
                    foreach (SkinnedMeshRenderer smr in renderers)
                    {
                        if (smr == null || smr.sharedMesh == null) continue;
                        if (ClassifyMesh(smr) != MeshMorphClass.NavelAccessory) continue;

                        Mesh mesh = smr.sharedMesh;
                        Vector3[] currentVerts = mesh.vertices;
                        BoneWeight[] weights = mesh.boneWeights;
                        Transform[] bones = smr.bones;
                        int count = mesh.vertexCount;
                        if (count <= 0) continue;

                        MeshRecord rec = FindRecord(maid, smr);
                        bool recordOk = rec != null
                            && rec.Mesh == mesh
                            && rec.OrigVerts != null
                            && rec.OrigVerts.Length == count;
                        bool deltaOk = recordOk
                            && rec.LastDeltaVerts != null
                            && rec.LastDeltaVerts.Length == count;
                        bool currentOk = currentVerts != null && currentVerts.Length == count;
                        bool weightsOk = weights != null && weights.Length == count;
                        Vector3[] baseVerts = recordOk ? rec.OrigVerts : currentVerts;
                        if (baseVerts == null || baseVerts.Length != count) continue;

                        Matrix4x4[] boneMatrices = null;
                        bool skinOk = weightsOk && TryBuildNavelAccessorySkinMatrices(smr, out boneMatrices);

                        NavelAccessoryReference reference;
                        if (!skinOk || !TryBuildNavelAccessoryReference(mesh, baseVerts, weights, boneMatrices, out reference))
                        {
                            writer.WriteLine("# navel reference failed meshId=" + EscapeDumpCell(GetMeshId(smr)));
                            continue;
                        }

                        int[] triangles = null;
                        try
                        {
                            triangles = mesh.triangles;
                        }
                        catch
                        {
                            writer.WriteLine("# triangle read failed meshId=" + EscapeDumpCell(GetMeshId(smr)));
                            continue;
                        }
                        int[] rings = BuildTriangleRings(triangles, count, reference.Triangle, 2);
                        if (rings == null) continue;

                        bool rigidOk = false;
                        Vector3 rigidOriginalCenter = Vector3.zero;
                        Vector3 rigidTargetCenter = Vector3.zero;
                        Quaternion rigidDeltaRotation = Quaternion.identity;
                        if (bodyRefsOk)
                            rigidOk = TryBuildNavelAccessoryRigidReference(reference, out rigidOriginalCenter, out rigidTargetCenter, out rigidDeltaRotation);

                        meshCount++;
                        for (int tri = 0; tri < rings.Length; tri++)
                        {
                            int ring = rings[tri];
                            if (ring < 0) continue;

                            int triBase = tri * 3;
                            for (int corner = 0; corner < 3; corner++)
                            {
                                int vertex = triangles[triBase + corner];
                                if (!IsValidVertexIndex(vertex, count)) continue;

                                Vector3 origLocal = baseVerts[vertex];
                                Vector3 currentLocal = currentOk ? currentVerts[vertex] : origLocal;
                                Vector3 deltaLocal = deltaOk ? rec.LastDeltaVerts[vertex] : Vector3.zero;
                                Vector3 appliedTargetLocal = origLocal + deltaLocal;
                                bool vertexSkinOk = skinOk && HasValidBoneWeight(weights[vertex], boneMatrices);

                                Vector3 origWorld;
                                Vector3 currentWorld;
                                Vector3 appliedTargetWorld;
                                if (vertexSkinOk)
                                {
                                    Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[vertex]);
                                    origWorld = skin.MultiplyPoint3x4(origLocal);
                                    currentWorld = skin.MultiplyPoint3x4(currentLocal);
                                    appliedTargetWorld = skin.MultiplyPoint3x4(appliedTargetLocal);
                                }
                                else
                                {
                                    origWorld = smr.transform.TransformPoint(origLocal);
                                    currentWorld = smr.transform.TransformPoint(currentLocal);
                                    appliedTargetWorld = smr.transform.TransformPoint(appliedTargetLocal);
                                }

                                Vector3 surfaceTargetWorld = origWorld;
                                BodySurfaceTargetDebug surfaceDebug = CreateBodySurfaceTargetDebug(
                                    bodyRefsOk ? "not-run" : "body-refs-not-ok",
                                    _morphReferenceContext,
                                    origWorld);
                                bool surfaceTargetOk = bodyRefsOk
                                    && TryGetBodySurfaceClothTargetDebug(_morphReferenceContext, origWorld, 1f, out surfaceTargetWorld, out surfaceDebug);

                                Vector3 rigidTargetWorld = origWorld;
                                Vector3 rigidTargetLocal = origLocal;
                                if (rigidOk)
                                {
                                    rigidTargetWorld = rigidTargetCenter + rigidDeltaRotation * (origWorld - rigidOriginalCenter);
                                    if (vertexSkinOk)
                                    {
                                        Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[vertex]);
                                        rigidTargetLocal = skin.inverse.MultiplyPoint3x4(rigidTargetWorld);
                                    }
                                }

                                Vector3 appliedDeltaWorld = appliedTargetWorld - origWorld;
                                string role = GetNavelReferenceVertexRole(reference, vertex);

                                bool first = true;
                                WriteCell(writer, ref first, GetMeshId(smr));
                                WriteCell(writer, ref first, smr.name);
                                WriteCell(writer, ref first, mesh.GetInstanceID().ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, smr.gameObject.activeSelf ? "1" : "0");
                                WriteCell(writer, ref first, smr.gameObject.activeInHierarchy ? "1" : "0");
                                WriteCell(writer, ref first, reference.Triangle.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, reference.Reason ?? string.Empty);
                                WriteCell(writer, ref first, reference.A.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, reference.B.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, reference.C.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, F(reference.Score));
                                WriteCell(writer, ref first, ring.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, tri.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, corner.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, vertex.ToString(CultureInfo.InvariantCulture));
                                WriteCell(writer, ref first, role);
                                WriteCell(writer, ref first, recordOk ? "1" : "0");
                                WriteCell(writer, ref first, deltaOk ? "1" : "0");
                                WriteCell(writer, ref first, vertexSkinOk ? "1" : "0");
                                WriteVectorCells(writer, ref first, origLocal);
                                WriteVectorCells(writer, ref first, currentLocal);
                                WriteVectorCells(writer, ref first, appliedTargetLocal);
                                WriteVectorCells(writer, ref first, deltaLocal);
                                WriteCell(writer, ref first, F(deltaLocal.magnitude));
                                WriteVectorCells(writer, ref first, origWorld);
                                WriteVectorCells(writer, ref first, currentWorld);
                                WriteVectorCells(writer, ref first, appliedTargetWorld);
                                WriteVectorCells(writer, ref first, appliedDeltaWorld);
                                WriteCell(writer, ref first, F(appliedDeltaWorld.magnitude));
                                WriteCell(writer, ref first, surfaceTargetOk ? "1" : "0");
                                WriteVectorCells(writer, ref first, surfaceTargetWorld);
                                WriteCell(writer, ref first, F((surfaceTargetWorld - origWorld).magnitude));
                                WriteBodySurfaceTargetDebugCells(writer, ref first, surfaceDebug);
                                WriteCell(writer, ref first, rigidOk ? "1" : "0");
                                WriteVectorCells(writer, ref first, rigidTargetWorld);
                                WriteCell(writer, ref first, F((rigidTargetWorld - origWorld).magnitude));
                                WriteVectorCells(writer, ref first, rigidTargetLocal);
                                if (weightsOk)
                                    WriteBoneWeightCells(writer, ref first, weights[vertex], bones);
                                else
                                    WriteEmptyBoneWeightCells(writer, ref first);
                                writer.WriteLine();
                            }
                        }
                    }

                    writer.WriteLine("# navelAccessoryMeshes=" + meshCount.ToString(CultureInfo.InvariantCulture));
                }
            }
            finally
            {
                _morphReferenceContext = previousContext;
            }

            return path;
        }

        static string ExportOuterClothVertexMorphDump(Maid maid, bool skirtLike)
        {
            if (!IsValid(maid)) return string.Empty;

            PruneRecords(maid);
            EnsureBindPoseWorldForDump(maid);

            string root = BepInEx.Paths.BepInExRootPath;
            if (string.IsNullOrEmpty(root))
                root = System.Environment.CurrentDirectory;

            string dir = Path.Combine(root, skirtLike ? "PregnancySkirtDumps" : "PregnancyUpperClothDumps");
            Directory.CreateDirectory(dir);

            string maidName = SafeDumpName(GetMaidName(maid));
            string fileName = (skirtLike ? "skirt_vertices_" : "upper_vertices_")
                + maidName
                + "_"
                + System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + ".tsv";
            string path = Path.Combine(dir, fileName);

            int meshCount = 0;
            int vertexCount = 0;
            Vector3 frameCenter = _bpWorldCached ? _bpWorldFrame.Center : Vector3.zero;
            Vector3 frameUp = _bpWorldCached ? _bpWorldFrame.Up : Vector3.up;
            Vector3 frameFwd = _bpWorldCached ? _bpWorldFrame.Fwd : Vector3.forward;
            Vector3 frameRight = _bpWorldCached ? _bpWorldFrame.Right : Vector3.right;
            Vector3 regionCenter = frameCenter + frameUp * RelUp(InflationMoveY) + frameFwd * RelFwd(InflationMoveZ);

            using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine(skirtLike
                    ? "# COM3D2 Pregnancy skirt vertex morph dump"
                    : "# COM3D2 Pregnancy upper cloth vertex morph dump");
                writer.WriteLine("# maid=" + EscapeDumpCell(GetMaidName(maid)));
                writer.WriteLine("# activeProgress=" + F(GetActiveProgress(maid)));
                writer.WriteLine("# frameCached=" + (_bpWorldCached ? "1" : "0"));
                writer.WriteLine("# columns are tab-separated; delta columns are the plugin morph amount from stored base to plugin target");
                writer.WriteLine("# traceParams=" + EscapeDumpCell(BuildTraceParamString()));
                writer.WriteLine(
                    "meshId\trenderer\tmeshInstance\tvertex\trecordOk\tdeltaOk\tskinOk"
                    + "\torigLocalX\torigLocalY\torigLocalZ"
                    + "\tcurrentLocalX\tcurrentLocalY\tcurrentLocalZ"
                    + "\ttargetLocalX\ttargetLocalY\ttargetLocalZ"
                    + "\tdeltaLocalX\tdeltaLocalY\tdeltaLocalZ\tdeltaLocalLen"
                    + "\torigWorldX\torigWorldY\torigWorldZ"
                    + "\tcurrentWorldX\tcurrentWorldY\tcurrentWorldZ"
                    + "\ttargetWorldX\ttargetWorldY\ttargetWorldZ"
                    + "\tdeltaWorldX\tdeltaWorldY\tdeltaWorldZ\tdeltaWorldLen"
                    + "\torigSide\torigUp\torigFwd"
                    + "\tdeltaSide\tdeltaUp\tdeltaFwd"
                    + "\ttraceOk\ttraceReason\ttraceSteps"
                    + "\toldFinalChanged\toldFinalDeltaWorldLen\toldFinalMinDelta\toldFinalReason"
                    + "\tclothInheritApplied\tclothInheritKind\tclothInheritReason\tclothInheritSourceMesh\tclothInheritSourceVertex"
                    + "\tclothInheritFrameA\tclothInheritFrameB\tclothInheritFrameCandidateCount\tclothInheritFrameScore\tclothInheritNearestDistance"
                    + "\tclothInheritOriginalThickness\tclothInheritRawThickness\tclothInheritFinalThickness"
                    + "\tclothInheritLocalX\tclothInheritLocalY\tclothInheritLocalZ"
                    + "\tclothInheritPreWorldX\tclothInheritPreWorldY\tclothInheritPreWorldZ"
                    + "\tclothInheritPostWorldX\tclothInheritPostWorldY\tclothInheritPostWorldZ"
                    + "\tclothInheritPreSide\tclothInheritPreUp\tclothInheritPreFwd"
                    + "\tclothInheritPostSide\tclothInheritPostUp\tclothInheritPostFwd"
                    + "\tclothInheritDeltaSide\tclothInheritDeltaUp\tclothInheritDeltaFwd\tclothInheritDeltaWorldLen"
                    + "\tclothInheritDeltaDirSide\tclothInheritDeltaDirUp\tclothInheritDeltaDirFwd"
                    + "\tclothInheritFinalOffsetSide\tclothInheritFinalOffsetUp\tclothInheritFinalOffsetFwd"
                    + "\tclothInheritFailKind\tclothInheritFailReason\tclothInheritFailSourceMesh\tclothInheritFailSourceVertex"
                    + "\tclothInheritFailFrameCandidateCount\tclothInheritFailFrameScore\tclothInheritFailNearestDistance"
                    + "\tskirtHemBoundaryValid\tskirtHemBoundaryUp\tskirtHemFwdThreshold\tskirtHemFront\tskirtHemLowerRange\tskirtHemCandidate\tskirtHemBlend\tskirtHemReason"
                    + "\tbone0\tboneW0\tbone1\tboneW1\tbone2\tboneW2\tbone3\tboneW3"
                    + "\teffectiveProgress\tradiusScale\tradiusSide\tradiusFront\tradiusBack\tradiusUp\tradiusDown"
                    + "\tfwdRadius\tupRadius\tellip\tedgeRatio\tedgeFade\ttopEdgeStrength\tshapeWeight\tmorphWeight\toriginalRadius\tdirectionalRadius"
                    + "\tlowerBodyRestore\tinnerThighRestore\tbreastRestore\tbottomTaperInfluence\tbottomTaperLower\tbottomTaperFront\tbottomTaperGuardKeep"
                    + "\tinwardGuardApplied\tinwardGuardDeltaFwd"
                    + "\tskirtLowerEnabled\tskirtLowerBoundaryUp\tskirtLowerLowerLimit\tskirtLowerTopDeltaFwd\tskirtLowerGrowth\tskirtLowerDownT\tskirtLowerAddFwd\tskirtLowerReason"
                    + "\tthighSmoothMask\tthighSmoothDeltaFwd"
                    + "\tfwdBase\tfwdSphere\tfwdSculpt\tfwdShift\tfwdStretch\tfwdRoundness\tfwdTaperY\tfwdTaperZ\tfwdFatFold\tfwdDrop"
                    + "\tfwdSideSmooth\tfwdRibReduce\tfwdLowerRestore\tfwdInwardGuard\tfwdBreastRestore\tfwdInnerThighRestore"
                    + "\tfwdBottomTaper\tfwdSkirtLower\tfwdThighSmooth\tfwdClothInherit\tfwdFinal");

                List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    if (ClassifyMesh(smr) != MeshMorphClass.OuterCloth) continue;
                    if (skirtLike)
                    {
                        if (!IsSkirtLikeMesh(smr)) continue;
                    }
                    else
                    {
                        if (IsSkirtLikeMesh(smr)) continue;
                        if (IsPantsLikeMesh(smr)) continue;
                    }

                    Mesh mesh = smr.sharedMesh;
                    Vector3[] currentVerts = mesh.vertices;
                    BoneWeight[] weights = mesh.boneWeights;
                    int count = mesh.vertexCount;
                    if (count <= 0) continue;

                    MeshRecord rec = FindRecord(maid, smr);
                    bool recordOk = rec != null
                        && rec.Mesh == mesh
                        && rec.OrigVerts != null
                        && rec.OrigVerts.Length == count;
                    bool deltaOk = recordOk
                        && rec.LastDeltaVerts != null
                        && rec.LastDeltaVerts.Length == count;
                    VertexMorphTrace[] traces = recordOk && rec.LastMorphTrace != null && rec.LastMorphTrace.Length == count
                        ? rec.LastMorphTrace
                        : null;
                    bool currentOk = currentVerts != null && currentVerts.Length == count;
                    bool weightsOk = weights != null && weights.Length == count;

                    Matrix4x4[] boneMatrices = null;
                    bool skinOk = weightsOk && TryBuildBindPoseSkinMatrices(smr, out boneMatrices);

                    meshCount++;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 origLocal = recordOk ? rec.OrigVerts[i] : (currentOk ? currentVerts[i] : Vector3.zero);
                        Vector3 deltaLocal = deltaOk ? rec.LastDeltaVerts[i] : Vector3.zero;
                        Vector3 targetLocal = origLocal + deltaLocal;
                        Vector3 currentLocal = currentOk ? currentVerts[i] : targetLocal;

                        bool vertexSkinOk = skinOk && HasValidBoneWeight(weights[i], boneMatrices);
                        Vector3 origWorld;
                        Vector3 targetWorld;
                        Vector3 currentWorld;
                        if (vertexSkinOk)
                        {
                            Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                            origWorld = skin.MultiplyPoint3x4(origLocal);
                            targetWorld = skin.MultiplyPoint3x4(targetLocal);
                            currentWorld = skin.MultiplyPoint3x4(currentLocal);
                        }
                        else
                        {
                            origWorld = smr.transform.TransformPoint(origLocal);
                            targetWorld = smr.transform.TransformPoint(targetLocal);
                            currentWorld = smr.transform.TransformPoint(currentLocal);
                        }

                        Vector3 deltaWorld = targetWorld - origWorld;
                        Vector3 rel = origWorld - regionCenter;
                        float origSide = Vector3.Dot(rel, frameRight);
                        float origUp = Vector3.Dot(rel, frameUp);
                        float origFwd = Vector3.Dot(rel, frameFwd);
                        float deltaSide = Vector3.Dot(deltaWorld, frameRight);
                        float deltaUp = Vector3.Dot(deltaWorld, frameUp);
                        float deltaFwd = Vector3.Dot(deltaWorld, frameFwd);

                        bool first = true;
                        WriteCell(writer, ref first, GetMeshId(smr));
                        WriteCell(writer, ref first, smr.name);
                        WriteCell(writer, ref first, mesh.GetInstanceID().ToString(CultureInfo.InvariantCulture));
                        WriteCell(writer, ref first, i.ToString(CultureInfo.InvariantCulture));
                        WriteCell(writer, ref first, recordOk ? "1" : "0");
                        WriteCell(writer, ref first, deltaOk ? "1" : "0");
                        WriteCell(writer, ref first, vertexSkinOk ? "1" : "0");
                        WriteVectorCells(writer, ref first, origLocal);
                        WriteVectorCells(writer, ref first, currentLocal);
                        WriteVectorCells(writer, ref first, targetLocal);
                        WriteVectorCells(writer, ref first, deltaLocal);
                        WriteCell(writer, ref first, F(deltaLocal.magnitude));
                        WriteVectorCells(writer, ref first, origWorld);
                        WriteVectorCells(writer, ref first, currentWorld);
                        WriteVectorCells(writer, ref first, targetWorld);
                        WriteVectorCells(writer, ref first, deltaWorld);
                        WriteCell(writer, ref first, F(deltaWorld.magnitude));
                        WriteCell(writer, ref first, F(origSide));
                        WriteCell(writer, ref first, F(origUp));
                        WriteCell(writer, ref first, F(origFwd));
                        WriteCell(writer, ref first, F(deltaSide));
                        WriteCell(writer, ref first, F(deltaUp));
                        WriteCell(writer, ref first, F(deltaFwd));
                        WriteTraceCells(writer, ref first, traces != null ? traces[i] : null);
                        writer.WriteLine();

                        vertexCount++;
                    }
                }

                writer.WriteLine("# meshCount=" + meshCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("# vertexCount=" + vertexCount.ToString(CultureInfo.InvariantCulture));
            }

            _log.LogInfo(skirtLike
                ? "[Pregnancy] Skirt vertex dump written: " + path
                : "[Pregnancy] Upper cloth vertex dump written: " + path);
            return path;
        }

        static void EnsureBindPoseWorldForDump(Maid maid)
        {
            if (_bpWorldCached || maid == null) return;

            _bpBoneWorld.Clear();
            List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
            foreach (SkinnedMeshRenderer smr in renderers)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) return;
            }
        }

        static bool TryBuildNavelAccessoryRigidReference(
            NavelAccessoryReference reference,
            out Vector3 originalCenter,
            out Vector3 targetCenter,
            out Quaternion deltaRotation)
        {
            originalCenter = Vector3.zero;
            targetCenter = Vector3.zero;
            deltaRotation = Quaternion.identity;
            if (_morphReferenceContext == null || _morphReferenceContext.SurfaceTriangles.Count == 0)
                return false;

            Vector3 targetA;
            Vector3 targetB;
            Vector3 targetC;
            if (!TryGetBodySurfaceClothTarget(_morphReferenceContext, reference.OriginalAWorld, 1f, out targetA)
                || !TryGetBodySurfaceClothTarget(_morphReferenceContext, reference.OriginalBWorld, 1f, out targetB)
                || !TryGetBodySurfaceClothTarget(_morphReferenceContext, reference.OriginalCWorld, 1f, out targetC))
            {
                return false;
            }

            originalCenter = (reference.OriginalAWorld + reference.OriginalBWorld + reference.OriginalCWorld) / 3f;
            Vector3 averageDelta =
                ((targetA - reference.OriginalAWorld)
                + (targetB - reference.OriginalBWorld)
                + (targetC - reference.OriginalCWorld)) / 3f;
            targetCenter = originalCenter + averageDelta;
            deltaRotation = Quaternion.identity;
            return IsFinite(originalCenter) && IsFinite(targetCenter) && IsFinite(averageDelta);
        }

        static BodySurfaceTargetDebug CreateBodySurfaceTargetDebug(
            string reason,
            MorphReferenceContext context,
            Vector3 target)
        {
            return new BodySurfaceTargetDebug
            {
                FailReason = reason ?? string.Empty,
                BodyPointCount = context != null && context.BodyPoints != null ? context.BodyPoints.Count : 0,
                SurfaceTriangleCount = context != null && context.SurfaceTriangles != null ? context.SurfaceTriangles.Count : 0,
                TriangleIndex = -1,
                BodyMeshInstanceId = 0,
                BodyA = -1,
                BodyB = -1,
                BodyC = -1,
                Target = target,
            };
        }

        static void WriteBodySurfaceTargetDebugCells(TextWriter writer, ref bool first, BodySurfaceTargetDebug debug)
        {
            WriteCell(writer, ref first, debug.FailReason ?? string.Empty);
            WriteCell(writer, ref first, debug.SearchHit ? "1" : "0");
            WriteCell(writer, ref first, debug.FullScanHit ? "1" : "0");
            WriteCell(writer, ref first, debug.TriangleIndex.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, debug.BodyMeshId ?? string.Empty);
            WriteCell(writer, ref first, debug.BodyMeshInstanceId.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, debug.BodyA.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, debug.BodyB.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, debug.BodyC.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, F(debug.Distance));
            WriteCell(writer, ref first, F(debug.OffsetLen));
            WriteCell(writer, ref first, F(debug.SurfaceMoveLen));
            WriteCell(writer, ref first, F(debug.NormalDot));
            WriteVectorCells(writer, ref first, debug.Barycentric);
            WriteVectorCells(writer, ref first, debug.Closest);
            WriteVectorCells(writer, ref first, debug.SurfaceMorphed);
            WriteVectorCells(writer, ref first, debug.OriginalNormal);
            WriteVectorCells(writer, ref first, debug.MorphedNormal);
            WriteCell(writer, ref first, debug.BodyPointCount.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, debug.SurfaceTriangleCount.ToString(CultureInfo.InvariantCulture));
        }

        static int[] BuildTriangleRings(int[] triangles, int vertexCount, int referenceTriangle, int maxRing)
        {
            if (triangles == null || triangles.Length < 3 || vertexCount <= 0)
                return null;

            int triCount = triangles.Length / 3;
            if (referenceTriangle < 0 || referenceTriangle >= triCount)
                return null;

            List<int>[] trisByVertex = new List<int>[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                trisByVertex[i] = new List<int>();

            for (int tri = 0; tri < triCount; tri++)
            {
                int triBase = tri * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    int v = triangles[triBase + corner];
                    if (IsValidVertexIndex(v, vertexCount))
                        trisByVertex[v].Add(tri);
                }
            }

            int[] rings = new int[triCount];
            for (int i = 0; i < rings.Length; i++)
                rings[i] = -1;

            Queue<int> queue = new Queue<int>();
            rings[referenceTriangle] = 0;
            queue.Enqueue(referenceTriangle);
            while (queue.Count > 0)
            {
                int tri = queue.Dequeue();
                int ring = rings[tri];
                if (ring >= maxRing) continue;

                int triBase = tri * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    int v = triangles[triBase + corner];
                    if (!IsValidVertexIndex(v, vertexCount)) continue;

                    List<int> neighbors = trisByVertex[v];
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int next = neighbors[n];
                        if (next < 0 || next >= rings.Length || rings[next] >= 0) continue;
                        rings[next] = ring + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return rings;
        }

        static string GetNavelReferenceVertexRole(NavelAccessoryReference reference, int vertex)
        {
            if (vertex == reference.A) return "refA";
            if (vertex == reference.B) return "refB";
            if (vertex == reference.C) return "refC";
            return string.Empty;
        }

        static void WriteBoneWeightCells(TextWriter writer, ref bool first, BoneWeight weight, Transform[] bones)
        {
            WriteCell(writer, ref first, FormatBoneCell(GetBoneName(bones, weight.boneIndex0), weight.boneIndex0));
            WriteCell(writer, ref first, F(weight.weight0));
            WriteCell(writer, ref first, FormatBoneCell(GetBoneName(bones, weight.boneIndex1), weight.boneIndex1));
            WriteCell(writer, ref first, F(weight.weight1));
            WriteCell(writer, ref first, FormatBoneCell(GetBoneName(bones, weight.boneIndex2), weight.boneIndex2));
            WriteCell(writer, ref first, F(weight.weight2));
            WriteCell(writer, ref first, FormatBoneCell(GetBoneName(bones, weight.boneIndex3), weight.boneIndex3));
            WriteCell(writer, ref first, F(weight.weight3));
        }

        static void WriteEmptyBoneWeightCells(TextWriter writer, ref bool first)
        {
            for (int i = 0; i < 8; i++)
                WriteCell(writer, ref first, string.Empty);
        }

        static string SafeDumpName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "maid";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }
                sb.Append(bad ? '_' : c);
            }
            return sb.Length > 0 ? sb.ToString() : "maid";
        }

        static string F(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        static void WriteVectorCells(TextWriter writer, ref bool first, Vector3 v)
        {
            WriteCell(writer, ref first, F(v.x));
            WriteCell(writer, ref first, F(v.y));
            WriteCell(writer, ref first, F(v.z));
        }

        static string BuildTraceParamString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("InflationMultiplier=").Append(F(InflationMultiplier));
            sb.Append(";InflationMoveY=").Append(F(InflationMoveY));
            sb.Append(";InflationMoveZ=").Append(F(InflationMoveZ));
            sb.Append(";InflationStretchX=").Append(F(InflationStretchX));
            sb.Append(";InflationStretchY=").Append(F(InflationStretchY));
            sb.Append(";InflationStretchZ=").Append(F(InflationStretchZ));
            sb.Append(";InflationShiftY=").Append(F(InflationShiftY));
            sb.Append(";InflationShiftZ=").Append(F(InflationShiftZ));
            sb.Append(";InflationTaperY=").Append(F(InflationTaperY));
            sb.Append(";InflationTaperZ=").Append(F(InflationTaperZ));
            sb.Append(";InflationRoundness=").Append(F(InflationRoundness));
            sb.Append(";InflationDrop=").Append(F(InflationDrop));
            sb.Append(";InflationFatFold=").Append(F(InflationFatFold));
            sb.Append(";InflationFatFoldHeight=").Append(F(InflationFatFoldHeight));
            sb.Append(";InflationFatFoldGap=").Append(F(InflationFatFoldGap));
            sb.Append(";RegionRadiusSide=").Append(F(RegionRadiusSide));
            sb.Append(";RegionRadiusFront=").Append(F(RegionRadiusFront));
            sb.Append(";RegionRadiusBack=").Append(F(RegionRadiusBack));
            sb.Append(";RegionRadiusUp=").Append(F(RegionRadiusUp));
            sb.Append(";RegionRadiusDown=").Append(F(RegionRadiusDown));
            sb.Append(";ThighGuardSpeed=").Append(F(ThighGuardSpeed));
            sb.Append(";InnerThighGuardStrength=").Append(F(InnerThighGuardStrength));
            sb.Append(";ThighGuardSmoothStrength=").Append(F(ThighGuardSmoothStrength));
            sb.Append(";TopEdgeTaper=").Append(F(TopEdgeTaper));
            sb.Append(";BottomEdgeTaper=").Append(F(BottomEdgeTaper));
            sb.Append(";SideSmoothWidth=").Append(F(SideSmoothWidth));
            sb.Append(";SideSmoothStrength=").Append(F(SideSmoothStrength));
            sb.Append(";BreastGuardStrength=").Append(F(BreastGuardStrength));
            sb.Append(";OuterClothPregnancyScale=").Append(F(OuterClothPregnancyScale));
            sb.Append(";SkirtBoundarySmoothPasses=").Append(SkirtBoundarySmoothPasses.ToString(CultureInfo.InvariantCulture));
            sb.Append(";SkirtBoundarySmoothStrength=").Append(F(SkirtBoundarySmoothStrength));
            sb.Append(";SkirtBoundaryUpOffset=").Append(F(SkirtBoundaryUpOffset));
            sb.Append(";SkirtFrontPlaneFwdOffset=").Append(F(SkirtFrontPlaneFwdOffset));
            sb.Append(";SkirtTopRadiusSideScale=").Append(F(SkirtTopRadiusSideScale));
            sb.Append(";SkirtTopRadiusFwdScale=").Append(F(SkirtTopRadiusFwdScale));
            sb.Append(";SkirtHemFadeRangeScale=").Append(F(SkirtHemFadeRangeScale));
            sb.Append(";SkirtLowerTipSide=").Append(F(SkirtLowerTipSide));
            sb.Append(";SkirtLowerTipUp=").Append(F(SkirtLowerTipUp));
            sb.Append(";SkirtLowerTipFwd=").Append(F(SkirtLowerTipFwd));
            sb.Append(";SkirtLowerRadiusSide=").Append(F(SkirtLowerRadiusSide));
            sb.Append(";SkirtLowerRadiusUp=").Append(F(SkirtLowerRadiusUp));
            sb.Append(";SkirtLowerRadiusFwd=").Append(F(SkirtLowerRadiusFwd));
            return sb.ToString();
        }

        static string BuildTraceSteps(VertexMorphTrace trace)
        {
            if (trace == null) return string.Empty;

            StringBuilder sb = new StringBuilder();
            if (!trace.Masked)
                AppendTraceStep(sb, trace.StopReason);
            else if (!trace.SkinOk)
                AppendTraceStep(sb, trace.StopReason);
            else if (!trace.InEllipsoid)
                AppendTraceStep(sb, "outside ellipsoid=" + F(trace.Ellip));
            else
            {
                AppendTraceStep(sb, "ellipsoid=" + F(trace.Ellip)
                    + " edgeFade=" + F(trace.EdgeFade)
                    + " morphWeight=" + F(trace.MorphWeight));
                AppendTraceDelta(sb, "sphere", trace.FwdSphere - trace.FwdBase);
                AppendTraceDelta(sb, "sculpt", trace.FwdSculpt - trace.FwdSphere);
                AppendTraceDelta(sb, "shiftY/Z", trace.FwdShift - trace.FwdSculpt);
                AppendTraceDelta(sb, "stretch X/Y/Z=" + F(InflationStretchX) + "/" + F(InflationStretchY) + "/" + F(InflationStretchZ), trace.FwdStretch - trace.FwdShift);
                AppendTraceDelta(sb, "roundness=" + F(InflationRoundness), trace.FwdRoundness - trace.FwdStretch);
                AppendTraceDelta(sb, "taperY=" + F(InflationTaperY), trace.FwdTaperY - trace.FwdRoundness);
                AppendTraceDelta(sb, "taperZ=" + F(InflationTaperZ), trace.FwdTaperZ - trace.FwdTaperY);
                AppendTraceDelta(sb, "fatFold=" + F(InflationFatFold), trace.FwdFatFold - trace.FwdTaperZ);
                AppendTraceDelta(sb, "drop=" + F(InflationDrop), trace.FwdDrop - trace.FwdFatFold);
                AppendTraceDelta(sb, "sideSmooth width/strength=" + F(SideSmoothWidth) + "/" + F(SideSmoothStrength), trace.FwdSideSmooth - trace.FwdDrop);
                AppendTraceDelta(sb, "ribReduce", trace.FwdRibReduce - trace.FwdSideSmooth);
                if (trace.LowerBodyRestore > 0f)
                    AppendTraceStep(sb, "lowerBodyRestore=" + F(trace.LowerBodyRestore) + " speed=" + F(ThighGuardSpeed));
                AppendTraceDelta(sb, "lowerBodyRestore", trace.FwdLowerRestore - trace.FwdRibReduce);
                if (trace.InwardGuardApplied)
                    AppendTraceStep(sb, "inwardGuard dFwd=" + F(trace.InwardGuardDeltaFwd));
                if (trace.BreastRestore > 0f)
                    AppendTraceStep(sb, "breastRestore=" + F(trace.BreastRestore));
                AppendTraceDelta(sb, "breastRestore", trace.FwdBreastRestore - trace.FwdInwardGuard);
                if (trace.InnerThighRestore > 0f)
                    AppendTraceStep(sb, "innerThighRestore=" + F(trace.InnerThighRestore));
                AppendTraceDelta(sb, "innerThighRestore", trace.FwdInnerThighRestore - trace.FwdBreastRestore);
                if (trace.BottomTaperInfluence > 0f)
                    AppendTraceStep(sb, "bottomTaper influence=" + F(trace.BottomTaperInfluence)
                        + " lower=" + F(trace.BottomTaperLower)
                        + " front=" + F(trace.BottomTaperFront));
                AppendTraceDelta(sb, "bottomTaper", trace.FwdBottomTaper - trace.FwdInnerThighRestore);
                if (trace.SkirtLowerEnabled)
                    AppendTraceStep(sb, trace.SkirtLowerReason);
                AppendTraceDelta(sb, "skirtLower", trace.FwdSkirtLower - trace.FwdBottomTaper);
                AppendTraceDelta(sb, "thighSmooth", trace.ThighSmoothDeltaFwd);
                if (trace.ClothInheritApplied)
                {
                    AppendTraceStep(sb, "clothInherit=" + (trace.ClothInheritKind ?? string.Empty)
                        + " src=" + (trace.ClothInheritSourceMesh ?? string.Empty)
                        + ":" + trace.ClothInheritSourceVertex.ToString(CultureInfo.InvariantCulture)
                        + " frame=" + trace.ClothInheritFrameA.ToString(CultureInfo.InvariantCulture)
                        + "/" + trace.ClothInheritFrameB.ToString(CultureInfo.InvariantCulture)
                        + " thickness=" + F(trace.ClothInheritOriginalThickness));
                    AppendTraceDelta(sb, "clothInherit", trace.ClothInheritDeltaCoord.z);
                }
                else if (!string.IsNullOrEmpty(trace.ClothInheritReason))
                {
                    AppendTraceStep(sb, trace.ClothInheritReason);
                }
            }

            return sb.ToString();
        }

        static void AppendTraceDelta(StringBuilder sb, string label, float delta)
        {
            if (Mathf.Abs(delta) <= 0.000001f) return;
            AppendTraceStep(sb, label + " dFwd=" + F(delta));
        }

        static void AppendTraceStep(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(text);
        }

        static void WriteTraceCells(TextWriter writer, ref bool first, VertexMorphTrace trace)
        {
            const int blankColumnsAfterSteps = 134;
            if (trace == null)
            {
                WriteCell(writer, ref first, "0");
                WriteCell(writer, ref first, "no-trace");
                WriteCell(writer, ref first, string.Empty);
                for (int i = 0; i < blankColumnsAfterSteps; i++)
                    WriteCell(writer, ref first, string.Empty);
                return;
            }

            WriteCell(writer, ref first, "1");
            WriteCell(writer, ref first, trace.StopReason ?? string.Empty);
            WriteCell(writer, ref first, BuildTraceSteps(trace));

            WriteCell(writer, ref first, trace.OldFinalChanged ? "1" : "0");
            WriteCell(writer, ref first, F(trace.OldFinalDeltaWorldLen));
            WriteCell(writer, ref first, F(trace.OldFinalMinDelta));
            WriteCell(writer, ref first, trace.OldFinalReason ?? string.Empty);

            WriteCell(writer, ref first, trace.ClothInheritApplied ? "1" : "0");
            WriteCell(writer, ref first, trace.ClothInheritKind ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritReason ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritSourceMesh ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritSourceVertex.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, trace.ClothInheritFrameA.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, trace.ClothInheritFrameB.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, trace.ClothInheritFrameCandidateCount.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, F(trace.ClothInheritFrameScore));
            WriteCell(writer, ref first, F(trace.ClothInheritNearestDistance));
            WriteCell(writer, ref first, F(trace.ClothInheritOriginalThickness));
            WriteCell(writer, ref first, F(trace.ClothInheritRawThickness));
            WriteCell(writer, ref first, F(trace.ClothInheritFinalThickness));
            WriteVectorCells(writer, ref first, trace.ClothInheritLocalOffset);
            WriteVectorCells(writer, ref first, trace.ClothInheritPreWorld);
            WriteVectorCells(writer, ref first, trace.ClothInheritPostWorld);
            WriteVectorCells(writer, ref first, trace.ClothInheritPreCoord);
            WriteVectorCells(writer, ref first, trace.ClothInheritPostCoord);
            WriteVectorCells(writer, ref first, trace.ClothInheritDeltaCoord);
            WriteCell(writer, ref first, F(trace.ClothInheritDeltaWorldLen));
            WriteVectorCells(writer, ref first, trace.ClothInheritDeltaDirCoord);
            WriteVectorCells(writer, ref first, trace.ClothInheritFinalOffsetCoord);

            WriteCell(writer, ref first, trace.ClothInheritFailKind ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritFailReason ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritFailSourceMesh ?? string.Empty);
            WriteCell(writer, ref first, trace.ClothInheritFailSourceVertex.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, trace.ClothInheritFailFrameCandidateCount.ToString(CultureInfo.InvariantCulture));
            WriteCell(writer, ref first, F(trace.ClothInheritFailFrameScore));
            WriteCell(writer, ref first, F(trace.ClothInheritFailNearestDistance));

            WriteCell(writer, ref first, trace.SkirtHemBoundaryValid ? "1" : "0");
            WriteCell(writer, ref first, F(trace.SkirtHemBoundaryUp));
            WriteCell(writer, ref first, F(trace.SkirtHemFwdThreshold));
            WriteCell(writer, ref first, trace.SkirtHemFront ? "1" : "0");
            WriteCell(writer, ref first, trace.SkirtHemLowerRange ? "1" : "0");
            WriteCell(writer, ref first, trace.SkirtHemCandidate ? "1" : "0");
            WriteCell(writer, ref first, F(trace.SkirtHemBlend));
            WriteCell(writer, ref first, trace.SkirtHemReason ?? string.Empty);

            WriteCell(writer, ref first, FormatBoneCell(trace.BoneName0, trace.BoneIndex0));
            WriteCell(writer, ref first, F(trace.BoneWeight0));
            WriteCell(writer, ref first, FormatBoneCell(trace.BoneName1, trace.BoneIndex1));
            WriteCell(writer, ref first, F(trace.BoneWeight1));
            WriteCell(writer, ref first, FormatBoneCell(trace.BoneName2, trace.BoneIndex2));
            WriteCell(writer, ref first, F(trace.BoneWeight2));
            WriteCell(writer, ref first, FormatBoneCell(trace.BoneName3, trace.BoneIndex3));
            WriteCell(writer, ref first, F(trace.BoneWeight3));

            WriteCell(writer, ref first, F(trace.EffectiveProgress));
            WriteCell(writer, ref first, F(trace.RadiusScale));
            WriteCell(writer, ref first, F(trace.RadiusSide));
            WriteCell(writer, ref first, F(trace.RadiusFront));
            WriteCell(writer, ref first, F(trace.RadiusBack));
            WriteCell(writer, ref first, F(trace.RadiusUp));
            WriteCell(writer, ref first, F(trace.RadiusDown));

            WriteCell(writer, ref first, F(trace.FwdRadius));
            WriteCell(writer, ref first, F(trace.UpRadius));
            WriteCell(writer, ref first, F(trace.Ellip));
            WriteCell(writer, ref first, F(trace.EdgeRatio));
            WriteCell(writer, ref first, F(trace.EdgeFade));
            WriteCell(writer, ref first, F(trace.TopEdgeStrength));
            WriteCell(writer, ref first, F(trace.ShapeWeight));
            WriteCell(writer, ref first, F(trace.MorphWeight));
            WriteCell(writer, ref first, F(trace.OriginalRadius));
            WriteCell(writer, ref first, F(trace.DirectionalRadius));

            WriteCell(writer, ref first, F(trace.LowerBodyRestore));
            WriteCell(writer, ref first, F(trace.InnerThighRestore));
            WriteCell(writer, ref first, F(trace.BreastRestore));
            WriteCell(writer, ref first, F(trace.BottomTaperInfluence));
            WriteCell(writer, ref first, F(trace.BottomTaperLower));
            WriteCell(writer, ref first, F(trace.BottomTaperFront));
            WriteCell(writer, ref first, F(trace.BottomTaperGuardKeep));

            WriteCell(writer, ref first, trace.InwardGuardApplied ? "1" : "0");
            WriteCell(writer, ref first, F(trace.InwardGuardDeltaFwd));

            WriteCell(writer, ref first, trace.SkirtLowerEnabled ? "1" : "0");
            WriteCell(writer, ref first, F(trace.SkirtLowerBoundaryUp));
            WriteCell(writer, ref first, F(trace.SkirtLowerLowerLimit));
            WriteCell(writer, ref first, F(trace.SkirtLowerTopDeltaFwd));
            WriteCell(writer, ref first, F(trace.SkirtLowerGrowth));
            WriteCell(writer, ref first, F(trace.SkirtLowerDownT));
            WriteCell(writer, ref first, F(trace.SkirtLowerAddFwd));
            WriteCell(writer, ref first, trace.SkirtLowerReason ?? string.Empty);

            WriteCell(writer, ref first, F(trace.ThighSmoothMask));
            WriteCell(writer, ref first, F(trace.ThighSmoothDeltaFwd));

            WriteCell(writer, ref first, F(trace.FwdBase));
            WriteCell(writer, ref first, F(trace.FwdSphere));
            WriteCell(writer, ref first, F(trace.FwdSculpt));
            WriteCell(writer, ref first, F(trace.FwdShift));
            WriteCell(writer, ref first, F(trace.FwdStretch));
            WriteCell(writer, ref first, F(trace.FwdRoundness));
            WriteCell(writer, ref first, F(trace.FwdTaperY));
            WriteCell(writer, ref first, F(trace.FwdTaperZ));
            WriteCell(writer, ref first, F(trace.FwdFatFold));
            WriteCell(writer, ref first, F(trace.FwdDrop));
            WriteCell(writer, ref first, F(trace.FwdSideSmooth));
            WriteCell(writer, ref first, F(trace.FwdRibReduce));
            WriteCell(writer, ref first, F(trace.FwdLowerRestore));
            WriteCell(writer, ref first, F(trace.FwdInwardGuard));
            WriteCell(writer, ref first, F(trace.FwdBreastRestore));
            WriteCell(writer, ref first, F(trace.FwdInnerThighRestore));
            WriteCell(writer, ref first, F(trace.FwdBottomTaper));
            WriteCell(writer, ref first, F(trace.FwdSkirtLower));
            WriteCell(writer, ref first, F(trace.FwdThighSmooth));
            WriteCell(writer, ref first, F(trace.FwdClothInherit));
            WriteCell(writer, ref first, F(trace.FwdFinal));
        }

        static string FormatBoneCell(string boneName, int boneIndex)
        {
            if (string.IsNullOrEmpty(boneName))
                return boneIndex.ToString(CultureInfo.InvariantCulture);
            return boneName + "#" + boneIndex.ToString(CultureInfo.InvariantCulture);
        }

        static void WriteCell(TextWriter writer, ref bool first, string value)
        {
            if (!first) writer.Write('\t');
            writer.Write(EscapeDumpCell(value));
            first = false;
        }

        static string EscapeDumpCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        static void SmoothThighGuardAffectedVerts(
            Mesh mesh,
            Vector3[] originalVerts,
            Vector3[] verts,
            float[] guardMask)
        {
            float smooth = Mathf.Clamp01(ThighGuardSmoothStrength);
            if (smooth <= 0f || mesh == null || originalVerts == null || verts == null || guardMask == null)
                return;

            int count = verts.Length;
            if (originalVerts.Length != count || guardMask.Length != count)
                return;

            List<int>[] neighbors = BuildMeshNeighbors(mesh, count);
            if (neighbors == null)
                return;

            for (int pass = 0; pass < ThighGuardSmoothPasses; pass++)
            {
                Vector3[] next = (Vector3[])verts.Clone();

                for (int i = 0; i < count; i++)
                {
                    float influence = Mathf.Clamp01(guardMask[i]);
                    if (influence <= 0f) continue;

                    Vector3 sumDelta = (verts[i] - originalVerts[i]) * 2f;
                    float sumWeight = 2f;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (j < 0 || j >= count) continue;

                        float neighborGuard = Mathf.Clamp01(guardMask[j]);
                        float neighborWeight = Mathf.Lerp(0.35f, 1f, neighborGuard);
                        sumDelta += (verts[j] - originalVerts[j]) * neighborWeight;
                        sumWeight += neighborWeight;
                    }

                    if (sumWeight <= 0f) continue;
                    Vector3 smoothed = originalVerts[i] + sumDelta / sumWeight;
                    next[i] = Vector3.Lerp(verts[i], smoothed, smooth * influence);
                }

                for (int i = 0; i < count; i++)
                    verts[i] = next[i];
            }
        }

        static Vector3[] AddDeltaVerts(Vector3[] currentVerts, Vector3[] deltaVerts)
        {
            if (currentVerts == null || deltaVerts == null || currentVerts.Length != deltaVerts.Length)
                return null;

            Vector3[] result = new Vector3[currentVerts.Length];
            for (int i = 0; i < currentVerts.Length; i++)
                result[i] = currentVerts[i] + deltaVerts[i];
            return result;
        }

        static Vector3 SculptBaseShapeWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float radiusSide,
            float radiusUp,
            float radiusDown)
        {
            Vector3 originalRel = original - center;
            Vector3 smoothedRel = smoothed - center;

            float originalSide = Vector3.Dot(originalRel, right);
            float originalUp = Vector3.Dot(originalRel, up);
            float smoothedSide = Vector3.Dot(smoothedRel, right);
            float smoothedUp = Vector3.Dot(smoothedRel, up);
            float smoothedFwd = Vector3.Dot(smoothedRel, fwd);

            float averageVerticalRadius = (radiusUp + radiusDown) * 0.5f;
            float smoothRadius = Mathf.Max((radiusSide + averageVerticalRadius) * 0.5f, 0.0001f);
            float sideUpDist = Mathf.Sqrt(smoothedSide * smoothedSide + smoothedUp * smoothedUp);
            float restore = Mathf.Clamp01(sideUpDist / (smoothRadius * 10f));

            float limitedSide = Mathf.Lerp(smoothedSide, originalSide, restore);
            float limitedUp = Mathf.Lerp(smoothedUp, originalUp, restore);
            return center + right * limitedSide + up * limitedUp + fwd * smoothedFwd;
        }

        static float GetTopEdgeStrength(
            float upDot,
            float radiusUp)
        {
            if (upDot > 0f && TopEdgeTaper != 0f)
                return EdgeTaperStrength(TopEdgeTaper, upDot / Mathf.Max(radiusUp, 0.0001f));

            return 1f;
        }

        static float GetBottomEdgeTaperInfluence(
            float upDot,
            float fwdDot,
            float radiusDown,
            float radiusBack,
            float radiusFront,
            float morphWeight,
            float lowerGuardRestore,
            out float lower,
            out float front,
            out float guardKeep)
        {
            lower = 0f;
            front = 0f;
            guardKeep = 1f - Mathf.Clamp01(lowerGuardRestore);

            float strength = Mathf.Clamp01(Mathf.Abs(BottomEdgeTaper)) * Smooth01(morphWeight);
            if (strength <= 0f || upDot >= 0f)
                return 0f;

            float lowerRatio = -upDot / Mathf.Max(radiusDown, 0.0001f);
            lower = Smooth01((lowerRatio - 0.12f) / 0.88f);
            if (lower <= 0f)
                return 0f;

            float frontRatio = (fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f);
            front = Smooth01((frontRatio - 0.05f) / 0.55f);
            if (front <= 0f)
                return 0f;

            return Mathf.Clamp01(strength * lower * front * guardKeep);
        }

        static Vector3 ApplyBottomEdgeTaperWorld(
            Vector3 original,
            Vector3 morphed,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            float upDot,
            float fwdDot,
            float radiusDown,
            float radiusBack,
            float radiusFront,
            float morphWeight,
            float lowerGuardRestore)
        {
            float strength = Mathf.Clamp01(Mathf.Abs(BottomEdgeTaper)) * Smooth01(morphWeight);
            if (strength <= 0f || upDot >= 0f)
                return morphed;

            float lowerRatio = -upDot / Mathf.Max(radiusDown, 0.0001f);
            float lower = Smooth01((lowerRatio - 0.12f) / 0.88f);
            if (lower <= 0f)
                return morphed;

            float frontRatio = (fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f);
            float front = Smooth01((frontRatio - 0.05f) / 0.55f);
            if (front <= 0f)
                return morphed;

            float guardKeep = 1f - Mathf.Clamp01(lowerGuardRestore);
            float influence = Mathf.Clamp01(strength * lower * front * guardKeep);
            if (influence <= 0f)
                return morphed;

            Vector3 originalRel = original - center;
            Vector3 morphedRel = morphed - center;
            float originalUp = Vector3.Dot(originalRel, up);
            float morphedUp = Vector3.Dot(morphedRel, up);
            float originalFwd = Vector3.Dot(originalRel, fwd);
            float morphedFwd = Vector3.Dot(morphedRel, fwd);

            float endBlend = Smooth01((lowerRatio - 0.55f) / 0.45f);
            float upTarget = Mathf.Lerp(morphedUp, originalUp, Mathf.Lerp(0.35f, 0.95f, endBlend));
            float fwdTarget = Mathf.Lerp(morphedFwd, originalFwd, Mathf.Lerp(0.20f, 0.75f, endBlend));
            Vector3 target = morphed
                + up * (upTarget - morphedUp)
                + fwd * (fwdTarget - morphedFwd);

            return Vector3.Lerp(morphed, target, influence);
        }

        static float EdgeTaperStrength(float taper, float edgeRatio)
        {
            float t = Mathf.Clamp01(edgeRatio);
            if (taper < 0f)
            {
                float oldLinearFade = 1f - t;
                return Mathf.Lerp(1f, oldLinearFade, Mathf.Clamp01(-taper));
            }

            return 1f + taper * t;
        }

        static float LowerBodyRestoreMask(
            float upDot,
            float sideDot,
            float fwdDot,
            float radiusDown,
            float radiusSide,
            float radiusBack,
            float radiusFront)
        {
            if (upDot >= 0f) return 0f;

            float lowerRatio = -upDot / Mathf.Max(radiusDown, 0.0001f);
            float speed = Mathf.Max(ThighGuardSpeed, 0.05f);
            float lower = AccelerateGuard(Smooth01(lowerRatio), speed);
            float sideRatio = Mathf.Abs(sideDot) / Mathf.Max(radiusSide, 0.0001f);
            float side = AccelerateGuard(Smooth01((sideRatio - 0.18f) / 0.62f), speed);
            float front = Smooth01((fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f));
            float frontKeep = Mathf.Lerp(1f, 0.35f, front);
            return Mathf.Clamp01(lower * side * frontKeep);
        }

        static float AccelerateGuard(float value, float speed)
        {
            float t = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - t, speed);
        }

        static float BreastRestoreMask(BoneWeight weight, Transform[] bones)
        {
            float strength = Mathf.Max(BreastGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float breastWeight = BreastBoneWeight(weight, bones);
            if (breastWeight <= 0f) return 0f;

            return Mathf.Clamp01(breastWeight * 4f * strength);
        }

        static float OuterClothBreastRestoreMask(
            MeshMorphClass meshClass,
            float upDot,
            float fwdDot,
            float radiusUp,
            float radiusBack,
            float radiusFront)
        {
            if (meshClass == MeshMorphClass.Body || meshClass == MeshMorphClass.Ignore) return 0f;

            float strength = Mathf.Max(BreastGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float upper = Smooth01((upDot / Mathf.Max(radiusUp, 0.0001f) - 0.48f) / 0.40f);
            if (upper <= 0f) return 0f;

            float frontRatio = (fwdDot + radiusBack) / Mathf.Max(radiusFront + radiusBack, 0.0001f);
            float front = Smooth01((frontRatio - 0.35f) / 0.65f);
            return Mathf.Clamp01(upper * front * strength);
        }

        static float BreastBoneWeight(BoneWeight weight, Transform[] bones)
        {
            float total = 0f;
            AddBreastBoneWeight(ref total, bones, weight.boneIndex0, weight.weight0);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex1, weight.weight1);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex2, weight.weight2);
            AddBreastBoneWeight(ref total, bones, weight.boneIndex3, weight.weight3);
            return Mathf.Clamp01(total);
        }

        static void AddBreastBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsBreastBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsBreastBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("mune")
                || lower.Contains("breast")
                || lower.Contains("bust")
                || lower.Contains("chichi")
                || lower.Contains("chikubi")
                || lower.Contains("nipple");
        }

        static float InnerThighRestoreMask(BoneWeight weight, Transform[] bones)
        {
            float strength = Mathf.Max(InnerThighGuardStrength, 0f);
            if (strength <= 0f) return 0f;

            float thighWeight = 0f;
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex0, weight.weight0);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex1, weight.weight1);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex2, weight.weight2);
            AddInnerThighBoneWeight(ref thighWeight, bones, weight.boneIndex3, weight.weight3);
            thighWeight = Mathf.Clamp01(thighWeight);
            if (thighWeight <= 0f) return 0f;

            return Mathf.Clamp01(thighWeight * 4f * strength);
        }

        static void AddInnerThighBoneWeight(ref float total, Transform[] bones, int index, float weight)
        {
            if (weight <= 0f || bones == null || index < 0 || index >= bones.Length) return;
            Transform bone = bones[index];
            if (bone == null || !IsInnerThighBoneName(bone.name)) return;
            total += weight;
        }

        static bool IsInnerThighBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("momoniku")
                || lower.Contains("momotwist");
        }

        static float MedianFloat(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            values.Sort();
            int mid = values.Count / 2;
            if ((values.Count & 1) == 1)
                return values[mid];
            return (values[mid - 1] + values[mid]) * 0.5f;
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

        static void ExpandTriangleEligibilityFromAnyVertex(Mesh mesh, bool[] eligible, bool[] valid)
        {
            if (mesh == null || eligible == null || valid == null)
                return;

            int count = Mathf.Min(eligible.Length, valid.Length);
            if (count <= 0)
                return;

            int[] triangles = null;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return;
            }

            if (triangles == null || triangles.Length < 3)
                return;

            bool[] source = (bool[])eligible.Clone();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count)
                    continue;

                if (!source[a] && !source[b] && !source[c])
                    continue;

                if (valid[a]) eligible[a] = true;
                if (valid[b]) eligible[b] = true;
                if (valid[c]) eligible[c] = true;
            }
        }

        static void AddNeighbor(List<int>[] neighbors, int index, int neighbor)
        {
            if (!neighbors[index].Contains(neighbor))
                neighbors[index].Add(neighbor);
        }



        static void ApplySmoothedNormals(Mesh mesh, MeshRecord rec, Vector3[] newVerts)
        {
            if (mesh == null || rec == null || rec.OrigVerts == null || rec.OrigNormals == null || newVerts == null)
            {
                if (mesh != null) mesh.RecalculateNormals();
                return;
            }

            if (rec.OrigVerts.Length != newVerts.Length || rec.OrigNormals.Length != newVerts.Length)
            {
                mesh.RecalculateNormals();
                return;
            }

            bool[] affected = BuildNormalAffectedMask(mesh, rec.OrigVerts, newVerts);
            mesh.RecalculateNormals();

            Vector3[] recalculated = mesh.normals;
            if (recalculated == null || recalculated.Length != newVerts.Length) return;

            Vector3[] finalNormals = new Vector3[newVerts.Length];
            for (int i = 0; i < finalNormals.Length; i++)
                finalNormals[i] = affected[i] ? recalculated[i] : rec.OrigNormals[i];

            mesh.normals = finalNormals;
        }

        static bool[] BuildNormalAffectedMask(Mesh mesh, Vector3[] originalVerts, Vector3[] newVerts)
        {
            int count = newVerts.Length;
            bool[] affected = new bool[count];
            float normalDelta = RelGeneral(NormalAffectedDelta);

            for (int i = 0; i < count; i++)
                affected[i] = (newVerts[i] - originalVerts[i]).sqrMagnitude >= normalDelta * normalDelta;

            int[] triangles = null;
            try
            {
                triangles = mesh.triangles;
            }
            catch
            {
                return affected;
            }

            if (triangles == null || triangles.Length < 3) return affected;

            for (int pass = 0; pass < NormalAffectedExpandPasses; pass++)
            {
                bool[] expanded = (bool[])affected.Clone();

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i];
                    int b = triangles[i + 1];
                    int c = triangles[i + 2];
                    if (a < 0 || b < 0 || c < 0) continue;
                    if (a >= count || b >= count || c >= count) continue;
                    if (!affected[a] && !affected[b] && !affected[c]) continue;

                    expanded[a] = true;
                    expanded[b] = true;
                    expanded[c] = true;
                }

                affected = expanded;
            }

            return affected;
        }

        static Vector3 RoundToSidesWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 fwd,
            float radiusBack,
            float radiusFront)
        {
            float strength = Mathf.Max(SideSmoothStrength, 0f);
            float width = Mathf.Max(SideSmoothWidth, 0f);
            if (strength <= 0f || width <= 0f) return smoothed;

            float originalFwd = Vector3.Dot(original - center, fwd);
            float forwardFromBack = originalFwd + radiusBack;
            float smoothDistance = Mathf.Max((radiusBack + radiusFront * 0.5f) * width, 0.0001f);
            if (forwardFromBack >= smoothDistance) return smoothed;

            float curve = BellySidesCurve(forwardFromBack / smoothDistance);
            float t = Mathf.Clamp01(1f - (1f - curve) * strength);
            return Vector3.Lerp(original, smoothed, t);
        }

        static Vector3 ReduceRibStretchingWorld(
            Vector3 original,
            Vector3 smoothed,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            float radiusUp)
        {
            float originalUp = Vector3.Dot(original - center, up);
            float topExtent = radiusUp;
            float topOffset = Mathf.Max(radiusUp * 0.5f, 0.0001f);

            if (originalUp > topExtent)
                return original;

            if (originalUp < topExtent - topOffset)
                return smoothed;

            float t = BellyTopCurve((topExtent - originalUp) / topOffset);
            return Vector3.Lerp(original, smoothed, t);
        }

        static float BellyEdgeCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.001f, t / 0.25f);
            if (t < 0.5f) return 0.001f;
            if (t < 0.75f) return Mathf.Lerp(0.001f, 0.2f, (t - 0.5f) / 0.25f);
            if (t < 0.9f) return Mathf.Lerp(0.2f, 0.7f, (t - 0.75f) / 0.15f);
            return Mathf.Lerp(0.7f, 1f, (t - 0.9f) / 0.1f);
        }

        static float BellySidesCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.15f, t / 0.25f);
            if (t < 0.5f) return Mathf.Lerp(0.15f, 0.35f, (t - 0.25f) / 0.25f);
            if (t < 0.7f) return Mathf.Lerp(0.35f, 0.7f, (t - 0.5f) / 0.2f);
            if (t < 0.9f) return Mathf.Lerp(0.7f, 0.9f, (t - 0.7f) / 0.2f);
            return Mathf.Lerp(0.9f, 1f, (t - 0.9f) / 0.1f);
        }

        static float BellyTopCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.25f) return Mathf.Lerp(0f, 0.1f, t / 0.25f);
            if (t < 0.5f) return Mathf.Lerp(0.1f, 0.35f, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Mathf.Lerp(0.35f, 0.9f, (t - 0.5f) / 0.25f);
            return Mathf.Lerp(0.9f, 1f, (t - 0.75f) / 0.25f);
        }

        static float BellyGapCurve(float value)
        {
            float t = Mathf.Clamp01(value);
            if (t < 0.1f) return Mathf.Lerp(0f, 0.15f, t / 0.1f);
            if (t < 0.25f) return Mathf.Lerp(0.15f, 0.25f, (t - 0.1f) / 0.15f);
            if (t < 0.5f) return Mathf.Lerp(0.25f, 0.7f, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Mathf.Lerp(0.7f, 0.95f, (t - 0.5f) / 0.25f);
            return Mathf.Lerp(0.95f, 1f, (t - 0.75f) / 0.25f);
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static bool HasValidBoneWeight(BoneWeight weight, Matrix4x4[] matrices)
        {
            return IsValidBoneWeightIndex(weight.boneIndex0, weight.weight0, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex1, weight.weight1, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex2, weight.weight2, matrices)
                || IsValidBoneWeightIndex(weight.boneIndex3, weight.weight3, matrices);
        }

        static bool IsValidBoneWeightIndex(int index, float weight, Matrix4x4[] matrices)
        {
            return weight > 0f && matrices != null && index >= 0 && index < matrices.Length;
        }

        static int ComputeMeshSignature(SkinnedMeshRenderer smr)
        {
            Mesh mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) return 0;

            Vector3[] verts = mesh.vertices;
            return ComputeVertexSignature(verts);
        }

        static int ComputeVertexSignature(Vector3[] verts)
        {
            if (verts == null) return 0;

            unchecked
            {
                int count = verts.Length;
                int hash = 17;
                hash = hash * 31 + count;

                if (count == 0) return hash;

                int samples = Mathf.Min(12, count);
                for (int i = 0; i < samples; i++)
                {
                    int idx = samples == 1 ? 0 : (int)((long)i * (count - 1) / (samples - 1));
                    Vector3 v = verts[idx];
                    hash = hash * 31 + Mathf.RoundToInt(v.x * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(v.y * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(v.z * 1000f);
                }

                return hash;
            }
        }

        public static int GetCurrentMeshSignature(Maid maid)
        {
            if (maid == null) return 0;

            unchecked
            {
                int hash = 17;
                foreach (SkinnedMeshRenderer smr in CollectRelevantRenderers(maid))
                {
                    hash = hash * 31 + GetRendererKey(smr);
                    hash = hash * 31 + ComputeMeshSignature(smr);
                }
                return hash;
            }
        }

        static int ComputeVisibilitySignature(List<SkinnedMeshRenderer> renderers)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < renderers.Count; i++)
                {
                    SkinnedMeshRenderer smr = renderers[i];
                    if (smr == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + smr.GetInstanceID();
                    hash = hash * 31 + (smr.sharedMesh != null ? smr.sharedMesh.GetInstanceID() : 0);
                    hash = hash * 31 + (smr.enabled ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeSelf ? 1 : 0);
                    hash = hash * 31 + (smr.gameObject.activeInHierarchy ? 1 : 0);
                }
                return hash;
            }
        }

        static List<SkinnedMeshRenderer> CollectRelevantRenderers(Maid maid)
        {
            List<SkinnedMeshRenderer> result = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in CollectTargetRenderers(maid))
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) == MeshMorphClass.Ignore) continue;
                result.Add(smr);
            }
            return result;
        }

        public class VisibilityNotifier : MonoBehaviour
        {
            Maid _maid;
            SkinnedMeshRenderer _smr;
            bool _initialized;
            bool _lastRendererEnabled;
            bool _lastActiveSelf;
            bool _lastActiveHierarchy;
            int _lastMeshInstanceId;

            public void Configure(Maid maid)
            {
                _maid = maid;
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                CaptureState();
                _initialized = true;
            }

            void OnEnable()
            {
                if (!_initialized) return;
                CaptureState();
                RequestVisibilityApplyBelly(_maid);
            }

            void OnDisable()
            {
                if (!_initialized) return;
                RequestVisibilityApplyBelly(_maid);
            }

            void LateUpdate()
            {
                if (!_initialized) return;
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                if (_smr == null) return;

                bool rendererEnabled = _smr.enabled;
                bool activeSelf = _smr.gameObject.activeSelf;
                bool activeHierarchy = _smr.gameObject.activeInHierarchy;
                int meshInstanceId = _smr.sharedMesh != null ? _smr.sharedMesh.GetInstanceID() : 0;

                bool changed =
                    rendererEnabled != _lastRendererEnabled
                    || activeSelf != _lastActiveSelf
                    || activeHierarchy != _lastActiveHierarchy
                    || meshInstanceId != _lastMeshInstanceId;

                _lastRendererEnabled = rendererEnabled;
                _lastActiveSelf = activeSelf;
                _lastActiveHierarchy = activeHierarchy;
                _lastMeshInstanceId = meshInstanceId;

                if (changed)
                    RequestVisibilityApplyBelly(_maid);
            }

            void CaptureState()
            {
                if (_smr == null) _smr = GetComponent<SkinnedMeshRenderer>();
                if (_smr == null) return;

                _lastRendererEnabled = _smr.enabled;
                _lastActiveSelf = _smr.gameObject.activeSelf;
                _lastActiveHierarchy = _smr.gameObject.activeInHierarchy;
                _lastMeshInstanceId = _smr.sharedMesh != null ? _smr.sharedMesh.GetInstanceID() : 0;
            }
        }

        public class BellyMonitor : MonoBehaviour
        {
            Maid _maid;
            bool _needsFullRefresh = false;
            int _refreshStableFrames = 0;
            readonly Dictionary<int, int> _refreshPreviousSignatures = new Dictionary<int, int>();
            bool _visibilityPending = false;
            int _visibilityKey = 0;
            int _visibilityStableFrames = 0;
            int _visibilityPreviousSignature = 0;
            bool _visibilityHasPrevious = false;

            public void SetMaid(Maid m) { _maid = m; }

            public void TriggerFullRefresh()
            {
                if (_maid == null) return;
                _needsFullRefresh = true;
                _refreshStableFrames = 0;
                _refreshPreviousSignatures.Clear();
            }

            public void RequestVisibilityApply(int key)
            {
                if (_maid == null)
                {
                    _pendingVisibilityApply.Remove(key);
                    return;
                }

                _visibilityPending = true;
                _visibilityKey = key;
                _visibilityStableFrames = 0;
                _visibilityPreviousSignature = 0;
                _visibilityHasPrevious = false;
            }

            void ClearVisibilityApply()
            {
                _pendingVisibilityApply.Remove(_visibilityKey);
                _visibilityPending = false;
                _visibilityKey = 0;
                _visibilityStableFrames = 0;
                _visibilityPreviousSignature = 0;
                _visibilityHasPrevious = false;
            }

            bool AreTrackedRenderersStable(List<SkinnedMeshRenderer> renderers, Dictionary<int, int> previous)
            {
                bool stable = true;
                HashSet<int> live = new HashSet<int>();

                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    int key = GetRendererKey(smr);
                    int sig = ComputeMeshSignature(smr);
                    live.Add(key);

                    if (!previous.TryGetValue(key, out int prevSig) || prevSig != sig)
                        stable = false;

                    previous[key] = sig;
                }

                List<int> staleKeys = new List<int>();
                foreach (KeyValuePair<int, int> entry in previous)
                {
                    if (!live.Contains(entry.Key))
                        staleKeys.Add(entry.Key);
                }
                foreach (int stale in staleKeys)
                    previous.Remove(stale);

                return stable;
            }

            void ProcessFullRefresh()
            {
                if (!_needsFullRefresh)
                    return;

                if (_maid == null)
                {
                    _needsFullRefresh = false;
                    _refreshPreviousSignatures.Clear();
                    return;
                }

                if (_maid.body0 == null || !_maid.body0.isLoadedBody)
                    return;

                List<SkinnedMeshRenderer> tracked = CollectRelevantRenderers(_maid);
                if (AreTrackedRenderersStable(tracked, _refreshPreviousSignatures))
                    _refreshStableFrames++;
                else
                    _refreshStableFrames = 0;

                if (_refreshStableFrames < AutoMorphStableFrames)
                    return;

                _needsFullRefresh = false;
                _refreshStableFrames = 0;
                _refreshPreviousSignatures.Clear();
                ForgetRecords(_maid);
                PruneRecords(_maid);

                float pFull = GetActiveProgress(_maid);
                if (pFull > 0f)
                    ApplyToSlots(_maid, pFull, false);
            }

            void ProcessVisibilityApply()
            {
                if (!_visibilityPending)
                    return;

                if (_maid == null)
                {
                    ClearVisibilityApply();
                    return;
                }

                if (_maid.body0 == null || !_maid.body0.isLoadedBody)
                    return;

                int currentSignature = ComputeVisibilitySignature(CollectRelevantRenderers(_maid));
                if (_visibilityHasPrevious && currentSignature == _visibilityPreviousSignature)
                    _visibilityStableFrames++;
                else
                    _visibilityStableFrames = 0;

                _visibilityPreviousSignature = currentSignature;
                _visibilityHasPrevious = true;

                if (_visibilityStableFrames < AutoMorphStableFrames)
                    return;

                ClearVisibilityApply();
                if (CurrentTriggerMode != MorphTriggerMode.VisibilityChange) return;
                if (!PregnancyManager.GetPregnant(_maid)) return;

                float progress = PregnancyManager.GetProgress(_maid);
                if (progress <= 0f) return;

                PregnancyUI.TriggerApplyBelly(_maid, progress);
            }

            void LateUpdate()
            {
                if (_maid == null) return;
                if (IsDebugMeshLoggingEnabled())
                    MaybeLogMeshInventory(_maid, CollectTargetRenderers(_maid), "monitor");
                else
                    _meshInventorySignatures.Remove(_maid.GetHashCode());
                BellyMorphController.FlushMorphDirty(_maid);
                ProcessFullRefresh();
                ProcessVisibilityApply();
            }
        }
    }
}
