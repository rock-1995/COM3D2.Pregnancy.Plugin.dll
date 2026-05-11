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

        const float BaseRadiusSide = 0.22f;
        const float BaseRadiusFront = 0.22f;
        const float BaseRadiusBack = 0.22f;
        const float BaseRadiusUp = 0.21f;
        const float BaseRadiusDown = 0.21f;
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
        public static float InflationMoveY = 0.025f;
        public static float InflationMoveZ = 0.0f;
        public static float InflationStretchX = -0.2f;
        public static float InflationStretchY = 0.0f;
        public static float InflationStretchZ = 0.13f;
        public static float InflationShiftY = 0.04f;
        public static float InflationShiftZ = -0.3f;
        public static float InflationTaperY = -0.03f;
        public static float InflationTaperZ = -0.05f;
        public static float InflationRoundness = 0.03f;
        public static float InflationDrop = 0.1f;
        public static float InflationFatFold = 0.0f;
        public static float InflationFatFoldHeight = 0.0f;
        public static float InflationFatFoldGap = 0.0f;
        public static float RegionRadiusSide = BaseRadiusSide;
        public static float RegionRadiusFront = BaseRadiusFront;
        public static float RegionRadiusBack = 0.13f;
        public static float RegionRadiusUp = 0.26f;
        public static float RegionRadiusDown = 0.18f;
        public static float ThighGuardSpeed = 4.0f;
        public static float InnerThighGuardStrength = 1.0f;
        public static float ThighGuardSmoothStrength = 0.35f;
        public static float TopEdgeTaper = -1.0f;
        public static float BottomEdgeTaper = 0.0f;
        public static float SideSmoothWidth = 0.8f;
        public static float SideSmoothStrength = 1.4f;
        public static float BreastGuardStrength = 1.0f;
        public static float ClothOverdrive = 1.03f;
        public static float OuterClothOverdrive = 1.20f;
        public static float OuterClothPregnancyScale = 1.0f;
        public static bool OuterClothSkirtDrape = false;
        public static float OuterClothLayerGuard = 0.0f;
        public static float InnerClothOffset = 0.0f;
        public static float OuterClothOffset = 0.0f;
        public static float ClothThicknessPreserve = 2.0f;
        public static float ClothOffsetSideRatio = 0.0f;
        public static float ClothBackOffsetBoost = 0.0f;
        public static float ClothDepthStretch = 3.0f;
        public static int SkirtBoundarySmoothPasses = 2;
        public static float SkirtBoundarySmoothStrength = 0.35f;
        public static float SkirtBoundaryUpOffset = 0.0f;
        public static float SkirtFrontPlaneFwdOffset = 0.0f;
        public static float SkirtTopRadiusSideScale = 1.15f;
        public static float SkirtTopRadiusFwdScale = 1.15f;
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
            public float ClothDepthStretchFactor;
            public float ClothDepthDeltaFwd;
            public float ClothLayerDeltaFwd;
            public bool InwardGuardApplied;
            public float InwardGuardDeltaFwd;

            public bool SkirtDrapeEnabled;
            public bool SkirtDrapeReached;
            public float SkirtDrapeAddFwd;
            public string SkirtDrapeReason;

            public bool SkirtLowerEnabled;
            public float SkirtLowerBoundaryUp;
            public float SkirtLowerLowerLimit;
            public float SkirtLowerTopDeltaFwd;
            public float SkirtLowerGrowth;
            public float SkirtLowerDownT;
            public float SkirtLowerAddFwd;
            public string SkirtLowerReason;

            public float CrossLayerGuardDeltaFwd;
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
            public float FwdDepthStretch;
            public float FwdLayerOffset;
            public float FwdInwardGuard;
            public float FwdBreastRestore;
            public float FwdInnerThighRestore;
            public float FwdBottomTaper;
            public float FwdSkirtDrape;
            public float FwdSkirtLower;
            public float FwdCrossLayerGuard;
            public float FwdThighSmooth;
            public float FwdFinal;
        }

        enum TraceFwdStage
        {
            SkirtDrape,
            SkirtLower,
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

        class CrossLayerGuardMesh
        {
            public SkinnedMeshRenderer SMR;
            public MeshRecord Record;
            public Mesh Mesh;
            public Vector3[] CurrentVerts;
            public BoneWeight[] Weights;
            public Matrix4x4[] BoneMatrices;
            public Vector3[] OriginalWorld;
            public Vector3[] MorphedWorld;
            public bool[] Valid;
            public LayerGuardCoord[] Coords;
            public bool Changed;
        }

        struct LayerGuardRef
        {
            public int MeshIndex;
            public int VertexIndex;

            public LayerGuardRef(int meshIndex, int vertexIndex)
            {
                MeshIndex = meshIndex;
                VertexIndex = vertexIndex;
            }
        }

        enum MeshMorphClass
        {
            Ignore,
            Body,
            InnerCloth,
            OuterCloth,
        }

        static Dictionary<int, List<MeshRecord>> _records = new Dictionary<int, List<MeshRecord>>();
        static readonly Dictionary<int, HashSet<TBodySkin>> _morphDirtySkins = new Dictionary<int, HashSet<TBodySkin>>();
        static readonly Dictionary<int, float> _morphSpyLastLogTime = new Dictionary<int, float>();
        static readonly Dictionary<TMorph, MorphBaseBakeState> _morphBaseBakeStates = new Dictionary<TMorph, MorphBaseBakeState>();
        static readonly FieldInfo _blendValuesChkField = typeof(TMorph).GetField(
            "BlendValuesCHK",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static BepInEx.Logging.ManualLogSource _log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
        static bool _suppressMorphBaseBake = false;
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
            InflationMoveY            = 0.025f;
            InflationMoveZ            = 0.0f;
            InflationStretchX         = -0.2f;
            InflationStretchY         = 0.0f;
            InflationStretchZ         = 0.13f;
            InflationShiftY           = 0.04f;
            InflationShiftZ           = -0.3f;
            InflationTaperY           = -0.03f;
            InflationTaperZ           = -0.05f;
            InflationRoundness        = 0.03f;
            InflationDrop             = 0.1f;
            InflationFatFold          = 0.0f;
            InflationFatFoldHeight    = 0.0f;
            InflationFatFoldGap       = 0.0f;
            RegionRadiusSide          = BaseRadiusSide;
            RegionRadiusFront         = BaseRadiusFront;
            RegionRadiusBack          = 0.13f;
            RegionRadiusUp            = 0.26f;
            RegionRadiusDown          = 0.18f;
            ThighGuardSpeed           = 4.0f;
            InnerThighGuardStrength   = 1.0f;
            ThighGuardSmoothStrength  = 0.35f;
            TopEdgeTaper              = -1.0f;
            BottomEdgeTaper           = 0.0f;
            SideSmoothWidth           = 0.8f;
            SideSmoothStrength        = 1.4f;
            BreastGuardStrength       = 1.0f;
            OuterClothPregnancyScale  = 1.0f;
            OuterClothSkirtDrape      = false;
            OuterClothLayerGuard      = 0.0f;
            InnerClothOffset          = 0.0f;
            OuterClothOffset          = 0.0f;
            ClothThicknessPreserve    = 2.0f;
            ClothOffsetSideRatio      = 0.0f;
            ClothBackOffsetBoost      = 0.0f;
            ClothDepthStretch         = 3.0f;
            SkirtBoundarySmoothPasses = 2;
            SkirtBoundarySmoothStrength = 0.35f;
            SkirtBoundaryUpOffset    = 0.0f;
            SkirtFrontPlaneFwdOffset = 0.0f;
            SkirtTopRadiusSideScale  = 1.15f;
            SkirtTopRadiusFwdScale   = 1.15f;
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

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            HashSet<int> appliedMeshIds = new HashSet<int>();
            List<SkinnedMeshRenderer> skirtLayerGuardCandidates = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;

                MeshMorphClass meshClass = ClassifyMesh(smr);
                if (meshClass == MeshMorphClass.Ignore)
                {
                    if (IsDebugMeshLoggingEnabled() && !IsFaceSlot(GetMeshId(smr)))
                        _log.LogInfo($"[BellyDiag] unrecognized maid={GetMaidName(maid)}"
                            + $" id={GetMeshId(smr)}"
                            + $" verts={smr.sharedMesh.vertexCount}");
                    continue;
                }

                AttachNotifier(smr.gameObject, maid);

                bool willApply = includeInactive || smr.gameObject.activeInHierarchy;
                if (IsDebugMeshLoggingEnabled())
                    LogMorphScan(maid, smr, meshClass, includeInactive, willApply);

                if (willApply)
                {
                    int meshId = smr.sharedMesh.GetInstanceID();
                    if (!appliedMeshIds.Add(meshId))
                    {
                        if (IsDebugMeshLoggingEnabled())
                            LogMorphSkip(maid, smr, meshClass, "duplicate-shared-mesh");
                        continue;
                    }

                    ApplySMR(maid, smr, progress, meshClass, false);
                    if (meshClass == MeshMorphClass.OuterCloth && IsSkirtLikeMesh(smr))
                        skirtLayerGuardCandidates.Add(smr);
                }
            }

            if (OuterClothSkirtDrape && OuterClothLayerGuard > 0f && skirtLayerGuardCandidates.Count > 0)
                ApplyCrossOuterClothLayerGuard(maid, skirtLayerGuardCandidates);
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
            if (ContainsAny(id, "body", "base", "karada", "inmou", "nip", "under")) return MeshMorphClass.Body;
            if (ContainsAny(id, "bra", "pants", "psnts", "stkg", "mizugi", "zurashi")) return MeshMorphClass.InnerCloth;
            if (ContainsAny(id, "wear", "onep", "skrt", "zubon", "skirt", "mekure")) return MeshMorphClass.OuterCloth;

            return MeshMorphClass.Ignore;
        }

        static bool IsSkirtLikeMesh(SkinnedMeshRenderer smr)
        {
            string id = GetMeshId(smr);
            if (string.IsNullOrEmpty(id)) return false;
            return ContainsAny(id, "skrt", "skirt", "onep", "mekure");
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

        static bool ShouldLogMorphDiagnostics(SkinnedMeshRenderer smr, MeshMorphClass meshClass)
        {
            if (!IsDebugMeshLoggingEnabled()) return false;
            if (smr == null || meshClass == MeshMorphClass.Ignore) return false;
            string id = GetMeshId(smr);
            return meshClass == MeshMorphClass.OuterCloth
                || id.Contains("218")
                || id.Contains("skrt")
                || id.Contains("skirt");
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
            if (!TryDeformVertsInBindPoseWorld(
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
            trace.FwdDepthStretch = fwd;
            trace.FwdLayerOffset = fwd;
            trace.FwdInwardGuard = fwd;
            trace.FwdBreastRestore = fwd;
            trace.FwdInnerThighRestore = fwd;
            trace.FwdBottomTaper = fwd;
            trace.FwdSkirtDrape = fwd;
            trace.FwdSkirtLower = fwd;
            trace.FwdCrossLayerGuard = fwd;
            trace.FwdThighSmooth = fwd;
            trace.FwdFinal = fwd;
        }

        static void CaptureTraceWorldFwd(
            VertexMorphTrace[] traces,
            Vector3[] worldVerts,
            bool[] valid,
            Vector3 center,
            Vector3 fwd,
            TraceFwdStage stage)
        {
            if (traces == null || worldVerts == null || valid == null) return;

            int count = Mathf.Min(traces.Length, Mathf.Min(worldVerts.Length, valid.Length));
            for (int i = 0; i < count; i++)
            {
                VertexMorphTrace trace = traces[i];
                if (trace == null || !valid[i]) continue;

                float value = TraceFwd(worldVerts[i], center, fwd);
                if (stage == TraceFwdStage.SkirtDrape)
                    trace.FwdSkirtDrape = value;
                else if (stage == TraceFwdStage.SkirtLower)
                    trace.FwdSkirtLower = value;
            }
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
                    trace.ThighSmoothDeltaFwd = afterFwd - beforeFwd;
                    if (Mathf.Abs(trace.CrossLayerGuardDeltaFwd) <= 1e-8f)
                        trace.FwdCrossLayerGuard = trace.FwdSkirtLower;
                    trace.FwdThighSmooth = afterFwd;
                    trace.FwdFinal = afterFwd;
                }
                else
                {
                    float finalFwd = TraceFwd(skin.MultiplyPoint3x4(finalLocalVerts[i]), center, fwd);
                    if (Mathf.Abs(trace.CrossLayerGuardDeltaFwd) <= 1e-8f)
                        trace.FwdCrossLayerGuard = trace.FwdSkirtLower;
                    trace.FwdThighSmooth = finalFwd;
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
                hash = hash * 31 + (OuterClothSkirtDrape ? 1 : 0);
                AddBakeSignatureFloat(ref hash, OuterClothLayerGuard);
                AddBakeSignatureFloat(ref hash, InnerClothOffset);
                AddBakeSignatureFloat(ref hash, OuterClothOffset);
                AddBakeSignatureFloat(ref hash, ClothThicknessPreserve);
                AddBakeSignatureFloat(ref hash, ClothOffsetSideRatio);
                AddBakeSignatureFloat(ref hash, ClothBackOffsetBoost);
                AddBakeSignatureFloat(ref hash, ClothDepthStretch);
                hash = hash * 31 + SkirtBoundarySmoothPasses;
                AddBakeSignatureFloat(ref hash, SkirtBoundarySmoothStrength);
                AddBakeSignatureFloat(ref hash, SkirtBoundaryUpOffset);
                AddBakeSignatureFloat(ref hash, SkirtFrontPlaneFwdOffset);
                AddBakeSignatureFloat(ref hash, SkirtTopRadiusSideScale);
                AddBakeSignatureFloat(ref hash, SkirtTopRadiusFwdScale);
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

            _bpWorldCached = false;
            _bpBoneWorld.Clear();
            PruneRecords(maid);

            foreach (SkinnedMeshRenderer smr in CollectTargetRenderers(maid))
            {
                if (smr?.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.Body) continue;
                if (TryCacheBindPoseWorldRef(smr)) break;
            }

            foreach (TBodySkin skin in toProcess)
                ApplyToBodySkin(maid, skin, progress);
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

            List<SkinnedMeshRenderer> targetSmrs = new List<SkinnedMeshRenderer>();
            HashSet<int> seenRenderers = new HashSet<int>();
            AddRenderers(skin.obj.transform, targetSmrs, seenRenderers);

            HashSet<int> appliedMeshIds = new HashSet<int>();
            foreach (SkinnedMeshRenderer smr in targetSmrs)
            {
                if (smr?.sharedMesh == null) continue;

                MeshMorphClass meshClass = ClassifyMesh(smr);
                if (meshClass == MeshMorphClass.Ignore) continue;
                if (!smr.gameObject.activeInHierarchy) continue;

                int meshId = smr.sharedMesh.GetInstanceID();
                if (!appliedMeshIds.Add(meshId)) continue;

                ApplySMR(maid, smr, progress, meshClass, false);
            }
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
            if (meshClass == MeshMorphClass.InnerCloth)
            {
                effectiveProgress = progress * ClothOverdrive;
            }
            else if (meshClass == MeshMorphClass.OuterCloth)
            {
                effectiveProgress = progress * OuterClothPregnancyScale;
            }

            Vector3[] newVerts;
            DeformStats stats;
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
            bool isSkirtLikeOuter = meshClass == MeshMorphClass.OuterCloth && IsSkirtLikeMesh(smr);
            bool useSkirtDrape = isSkirtLikeOuter && OuterClothSkirtDrape;
            bool useSkirtLowerShape = isSkirtLikeOuter;
            bool trackOuterClothWorld = useSkirtDrape || useSkirtLowerShape;
            VertexMorphTrace[] morphTrace = isSkirtLikeOuter ? new VertexMorphTrace[rec.OrigVerts.Length] : null;
            rec.LastMorphTrace = morphTrace;
            Vector3[] clothOriginalWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            Vector3[] clothMorphedWorld = trackOuterClothWorld ? new Vector3[rec.OrigVerts.Length] : null;
            bool[] clothValid = trackOuterClothWorld ? new bool[rec.OrigVerts.Length] : null;
            float[] thighGuardSmoothMask = ThighGuardSmoothStrength > 0f ? new float[rec.OrigVerts.Length] : null;

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
                        SkirtDrapeEnabled = useSkirtDrape,
                        SkirtLowerEnabled = useSkirtLowerShape,
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

                float beforeDepthFwd = trace != null ? TraceFwd(newWorldVert, regionCenter, worldFwd) : 0f;
                float depthStretchFactor = trace != null
                    ? GetClothDepthStretchFactor(meshClass, dist, directionalRadius, upDot, radiusDown)
                    : 1f;
                newWorldVert = ApplyClothDepthStretchWorld(
                    meshClass,
                    worldVert,
                    newWorldVert,
                    directionalRadius,
                    dist,
                    upDot,
                    radiusDown);
                if (trace != null)
                {
                    float afterDepthFwd = TraceFwd(newWorldVert, regionCenter, worldFwd);
                    trace.ClothDepthStretchFactor = depthStretchFactor;
                    trace.ClothDepthDeltaFwd = afterDepthFwd - beforeDepthFwd;
                    trace.FwdDepthStretch = afterDepthFwd;
                }

                float beforeLayerFwd = trace != null ? TraceFwd(newWorldVert, regionCenter, worldFwd) : 0f;
                newWorldVert = ApplyClothLayerOffsetWorld(
                    meshClass,
                    worldVert,
                    newWorldVert,
                    regionCenter,
                    morphWeight,
                    directionalRadius,
                    dist,
                    edgeRatio,
                    upDot,
                    radiusDown,
                    worldRight,
                    worldUp,
                    worldFwd);
                if (trace != null)
                {
                    float afterLayerFwd = TraceFwd(newWorldVert, regionCenter, worldFwd);
                    trace.ClothLayerDeltaFwd = afterLayerFwd - beforeLayerFwd;
                    trace.FwdLayerOffset = afterLayerFwd;
                }

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
                    clothMorphedWorld[i] = newWorldVert;

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

            if (useSkirtDrape)
            {
                ApplyOuterClothSkirtDrapeToMesh(
                    smr.sharedMesh,
                    rec.OrigVerts,
                    weights,
                    boneMatrices,
                    clothOriginalWorld,
                    clothMorphedWorld,
                    clothValid,
                    newVerts,
                    worldCenter,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    radiusSide,
                    radiusDown,
                    morphTrace);
            }

            if (morphTrace != null && trackOuterClothWorld)
                CaptureTraceWorldFwd(morphTrace, clothMorphedWorld, clothValid, regionCenter, worldFwd, TraceFwdStage.SkirtDrape);

            if (useSkirtLowerShape)
            {
                ApplySkirtLowerShapeToMesh(
                    smr.sharedMesh,
                    rec.OrigVerts,
                    weights,
                    boneMatrices,
                    clothOriginalWorld,
                    clothMorphedWorld,
                    clothValid,
                    newVerts,
                    worldCenter,
                    regionCenter,
                    worldRight,
                    worldUp,
                    worldFwd,
                    radiusSide,
                    radiusFront,
                    radiusBack,
                    radiusUp,
                    radiusDown,
                    morphTrace);
            }

            if (morphTrace != null && trackOuterClothWorld)
                CaptureTraceWorldFwd(morphTrace, clothMorphedWorld, clothValid, regionCenter, worldFwd, TraceFwdStage.SkirtLower);

            Vector3[] beforeThighSmoothVerts = morphTrace != null && thighGuardSmoothMask != null
                ? (Vector3[])newVerts.Clone()
                : null;
            SmoothThighGuardAffectedVerts(smr.sharedMesh, rec.OrigVerts, newVerts, thighGuardSmoothMask);

            if (morphTrace != null)
                CaptureTraceFinalLocalFwd(morphTrace, newVerts, beforeThighSmoothVerts, weights, boneMatrices, regionCenter, worldFwd);

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
            if (!IsValid(maid)) return string.Empty;

            PruneRecords(maid);
            EnsureBindPoseWorldForDump(maid);

            string root = BepInEx.Paths.BepInExRootPath;
            if (string.IsNullOrEmpty(root))
                root = System.Environment.CurrentDirectory;

            string dir = Path.Combine(root, "PregnancySkirtDumps");
            Directory.CreateDirectory(dir);

            string maidName = SafeDumpName(GetMaidName(maid));
            string fileName = "skirt_vertices_"
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
                writer.WriteLine("# COM3D2 Pregnancy skirt vertex morph dump");
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
                    + "\tbone0\tboneW0\tbone1\tboneW1\tbone2\tboneW2\tbone3\tboneW3"
                    + "\teffectiveProgress\tradiusScale\tradiusSide\tradiusFront\tradiusBack\tradiusUp\tradiusDown"
                    + "\tfwdRadius\tupRadius\tellip\tedgeRatio\tedgeFade\ttopEdgeStrength\tshapeWeight\tmorphWeight\toriginalRadius\tdirectionalRadius"
                    + "\tlowerBodyRestore\tinnerThighRestore\tbreastRestore\tbottomTaperInfluence\tbottomTaperLower\tbottomTaperFront\tbottomTaperGuardKeep"
                    + "\tclothDepthFactor\tclothDepthDeltaFwd\tclothLayerDeltaFwd\tinwardGuardApplied\tinwardGuardDeltaFwd"
                    + "\tskirtDrapeEnabled\tskirtDrapeReached\tskirtDrapeAddFwd\tskirtDrapeReason"
                    + "\tskirtLowerEnabled\tskirtLowerBoundaryUp\tskirtLowerLowerLimit\tskirtLowerTopDeltaFwd\tskirtLowerGrowth\tskirtLowerDownT\tskirtLowerAddFwd\tskirtLowerReason"
                    + "\tcrossLayerGuardDeltaFwd\tthighSmoothMask\tthighSmoothDeltaFwd"
                    + "\tfwdBase\tfwdSphere\tfwdSculpt\tfwdShift\tfwdStretch\tfwdRoundness\tfwdTaperY\tfwdTaperZ\tfwdFatFold\tfwdDrop"
                    + "\tfwdSideSmooth\tfwdRibReduce\tfwdLowerRestore\tfwdDepthStretch\tfwdLayerOffset\tfwdInwardGuard\tfwdBreastRestore\tfwdInnerThighRestore"
                    + "\tfwdBottomTaper\tfwdSkirtDrape\tfwdSkirtLower\tfwdCrossLayerGuard\tfwdThighSmooth\tfwdFinal");

                List<SkinnedMeshRenderer> renderers = CollectTargetRenderers(maid);
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    if (ClassifyMesh(smr) != MeshMorphClass.OuterCloth) continue;
                    if (!IsSkirtLikeMesh(smr)) continue;

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

            _log.LogInfo("[Pregnancy] Skirt vertex dump written: " + path);
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
            sb.Append(";OuterClothSkirtDrape=").Append(OuterClothSkirtDrape ? "1" : "0");
            sb.Append(";OuterClothLayerGuard=").Append(F(OuterClothLayerGuard));
            sb.Append(";InnerClothOffset=").Append(F(InnerClothOffset));
            sb.Append(";OuterClothOffset=").Append(F(OuterClothOffset));
            sb.Append(";ClothThicknessPreserve=").Append(F(ClothThicknessPreserve));
            sb.Append(";ClothOffsetSideRatio=").Append(F(ClothOffsetSideRatio));
            sb.Append(";ClothBackOffsetBoost=").Append(F(ClothBackOffsetBoost));
            sb.Append(";ClothDepthStretch=").Append(F(ClothDepthStretch));
            sb.Append(";SkirtBoundarySmoothPasses=").Append(SkirtBoundarySmoothPasses.ToString(CultureInfo.InvariantCulture));
            sb.Append(";SkirtBoundarySmoothStrength=").Append(F(SkirtBoundarySmoothStrength));
            sb.Append(";SkirtBoundaryUpOffset=").Append(F(SkirtBoundaryUpOffset));
            sb.Append(";SkirtFrontPlaneFwdOffset=").Append(F(SkirtFrontPlaneFwdOffset));
            sb.Append(";SkirtTopRadiusSideScale=").Append(F(SkirtTopRadiusSideScale));
            sb.Append(";SkirtTopRadiusFwdScale=").Append(F(SkirtTopRadiusFwdScale));
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
                AppendTraceDelta(sb, "clothDepthStretch factor=" + F(trace.ClothDepthStretchFactor), trace.ClothDepthDeltaFwd);
                AppendTraceDelta(sb, "clothLayerOffset", trace.ClothLayerDeltaFwd);
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
                if (trace.SkirtDrapeEnabled)
                    AppendTraceStep(sb, trace.SkirtDrapeReason);
                AppendTraceDelta(sb, "skirtDrape", trace.FwdSkirtDrape - trace.FwdBottomTaper);
                if (trace.SkirtLowerEnabled)
                    AppendTraceStep(sb, trace.SkirtLowerReason);
                AppendTraceDelta(sb, "skirtLower", trace.FwdSkirtLower - trace.FwdSkirtDrape);
                AppendTraceDelta(sb, "crossLayerGuard", trace.CrossLayerGuardDeltaFwd);
                AppendTraceDelta(sb, "thighSmooth", trace.ThighSmoothDeltaFwd);
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
            const int blankColumnsAfterSteps = 76;
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

            WriteCell(writer, ref first, F(trace.ClothDepthStretchFactor));
            WriteCell(writer, ref first, F(trace.ClothDepthDeltaFwd));
            WriteCell(writer, ref first, F(trace.ClothLayerDeltaFwd));
            WriteCell(writer, ref first, trace.InwardGuardApplied ? "1" : "0");
            WriteCell(writer, ref first, F(trace.InwardGuardDeltaFwd));

            WriteCell(writer, ref first, trace.SkirtDrapeEnabled ? "1" : "0");
            WriteCell(writer, ref first, trace.SkirtDrapeReached ? "1" : "0");
            WriteCell(writer, ref first, F(trace.SkirtDrapeAddFwd));
            WriteCell(writer, ref first, trace.SkirtDrapeReason ?? string.Empty);

            WriteCell(writer, ref first, trace.SkirtLowerEnabled ? "1" : "0");
            WriteCell(writer, ref first, F(trace.SkirtLowerBoundaryUp));
            WriteCell(writer, ref first, F(trace.SkirtLowerLowerLimit));
            WriteCell(writer, ref first, F(trace.SkirtLowerTopDeltaFwd));
            WriteCell(writer, ref first, F(trace.SkirtLowerGrowth));
            WriteCell(writer, ref first, F(trace.SkirtLowerDownT));
            WriteCell(writer, ref first, F(trace.SkirtLowerAddFwd));
            WriteCell(writer, ref first, trace.SkirtLowerReason ?? string.Empty);

            WriteCell(writer, ref first, F(trace.CrossLayerGuardDeltaFwd));
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
            WriteCell(writer, ref first, F(trace.FwdDepthStretch));
            WriteCell(writer, ref first, F(trace.FwdLayerOffset));
            WriteCell(writer, ref first, F(trace.FwdInwardGuard));
            WriteCell(writer, ref first, F(trace.FwdBreastRestore));
            WriteCell(writer, ref first, F(trace.FwdInnerThighRestore));
            WriteCell(writer, ref first, F(trace.FwdBottomTaper));
            WriteCell(writer, ref first, F(trace.FwdSkirtDrape));
            WriteCell(writer, ref first, F(trace.FwdSkirtLower));
            WriteCell(writer, ref first, F(trace.FwdCrossLayerGuard));
            WriteCell(writer, ref first, F(trace.FwdThighSmooth));
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

        const int SkirtDrapePropagatePasses = 48;
        const float SkirtDrapePropagationDecay = 0.995f;
        const float SkirtDrapeDeltaBoost = 1.03f;
        const int SkirtDrapeSmoothPasses = 4;
        const float SkirtDrapeSmoothStrength = 0.35f;

        static void ApplyOuterClothSkirtDrapeToMesh(
            Mesh mesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorldVerts,
            Vector3[] morphedWorldVerts,
            bool[] valid,
            Vector3[] newLocalVerts,
            Vector3 spineCenter,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float radiusSide,
            float radiusDown,
            VertexMorphTrace[] traces)
        {
            if (mesh == null || localOriginalVerts == null || weights == null || boneMatrices == null)
                return;
            if (originalWorldVerts == null || morphedWorldVerts == null || valid == null || newLocalVerts == null)
                return;

            int count = newLocalVerts.Length;
            if (localOriginalVerts.Length != count || weights.Length != count
                || originalWorldVerts.Length != count || morphedWorldVerts.Length != count || valid.Length != count)
                return;

            List<int>[] neighbors = BuildMeshNeighbors(mesh, count);
            if (neighbors == null)
                return;

            Vector2[] propagatedDelta = new Vector2[count];
            bool[] reached = new bool[count];

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;

                Vector3 originalRel = originalWorldVerts[i] - center;
                Vector3 deltaWorld = morphedWorldVerts[i] - originalWorldVerts[i];
                float originalUp = Vector3.Dot(originalRel, up);
                float originalFwd = Vector3.Dot(originalRel, fwd);
                if (originalUp > 0f || originalFwd <= 0f) continue;
                if (originalUp < -radiusDown * 0.70f) continue;

                Vector2 delta = new Vector2(
                    Vector3.Dot(deltaWorld, right),
                    Vector3.Dot(deltaWorld, fwd));
                if (delta.y <= 0f || delta.sqrMagnitude <= 1e-8f) continue;

                propagatedDelta[i] = delta;
                reached[i] = true;
            }

            PropagateSkirtDrapeDelta(neighbors, valid, originalWorldVerts, center, up, fwd, propagatedDelta, reached);
            SmoothSkirtDrapeDelta(neighbors, valid, originalWorldVerts, center, up, fwd, radiusDown, propagatedDelta, reached);

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                if (!reached[i] || propagatedDelta[i].sqrMagnitude <= 1e-8f)
                {
                    if (trace != null) trace.SkirtDrapeReason = "drape:not-reached";
                    continue;
                }
                if (trace != null) trace.SkirtDrapeReached = true;

                Vector3 originalRel = originalWorldVerts[i] - center;
                float originalUp = Vector3.Dot(originalRel, up);
                float originalRight = Vector3.Dot(originalRel, right);
                float originalFwd = Vector3.Dot(originalRel, fwd);
                if (originalFwd <= 0f)
                {
                    if (trace != null) trace.SkirtDrapeReason = "drape:not-front";
                    continue;
                }

                Vector2 delta = propagatedDelta[i] * SkirtDrapeDeltaBoost;
                if (delta.y <= 0f)
                {
                    if (trace != null) trace.SkirtDrapeReason = "drape:no-forward-delta";
                    continue;
                }

                float targetRight = originalRight + delta.x;
                float targetFwd = originalFwd + delta.y;
                float finalRight = Vector3.Dot(morphedWorldVerts[i] - center, right);
                float finalFwd = Vector3.Dot(morphedWorldVerts[i] - center, fwd);
                float addRight = targetRight - finalRight;
                float addFwd = Mathf.Max(0f, targetFwd - finalFwd);
                if (Mathf.Abs(addRight) <= 1e-5f && addFwd <= 1e-5f)
                {
                    if (trace != null) trace.SkirtDrapeReason = "drape:already-forward-enough";
                    continue;
                }

                Vector3 adjustedWorld = morphedWorldVerts[i] + right * addRight + fwd * addFwd;
                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                newLocalVerts[i] = skin.inverse.MultiplyPoint3x4(adjustedWorld);
                morphedWorldVerts[i] = adjustedWorld;
                if (trace != null)
                {
                    trace.SkirtDrapeAddFwd = addFwd;
                    trace.SkirtDrapeReason = "drape:applied";
                }
            }
        }

        static void ApplySkirtLowerShapeToMesh(
            Mesh mesh,
            Vector3[] localOriginalVerts,
            BoneWeight[] weights,
            Matrix4x4[] boneMatrices,
            Vector3[] originalWorldVerts,
            Vector3[] morphedWorldVerts,
            bool[] valid,
            Vector3[] newLocalVerts,
            Vector3 spineCenter,
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 fwd,
            float radiusSide,
            float radiusFront,
            float radiusBack,
            float radiusUp,
            float radiusDown,
            VertexMorphTrace[] traces)
        {
            if (mesh == null || localOriginalVerts == null || weights == null || boneMatrices == null)
                return;
            if (originalWorldVerts == null || morphedWorldVerts == null || valid == null || newLocalVerts == null)
                return;

            int count = newLocalVerts.Length;
            if (localOriginalVerts.Length != count || weights.Length != count
                || originalWorldVerts.Length != count || morphedWorldVerts.Length != count || valid.Length != count)
                return;

            List<int>[] neighbors = BuildMeshNeighbors(mesh, count);
            if (neighbors == null)
            {
                SetSkirtLowerTraceReason(traces, valid, "lower:no-neighbors");
                return;
            }

            SetSkirtLowerTraceReason(traces, valid, "lower:not-evaluated");

            float[] side = new float[count];
            float[] originalUp = new float[count];
            float[] originalFwd = new float[count];
            float[] spineFwd = new float[count];
            float[] strength = new float[count];
            Vector3[] deltaWorld = new Vector3[count];
            float[] morphedFwd = new float[count];
            float maxFront = float.MinValue;
            float minDelta = RelGeneral(SkirtLowerMinDelta);

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;

                Vector3 rel = originalWorldVerts[i] - center;
                side[i] = Vector3.Dot(rel, right);
                originalUp[i] = Vector3.Dot(rel, up);
                originalFwd[i] = Vector3.Dot(rel, fwd);
                spineFwd[i] = Vector3.Dot(originalWorldVerts[i] - spineCenter, fwd);

                Vector3 delta = morphedWorldVerts[i] - originalWorldVerts[i];
                float deltaSide = Vector3.Dot(delta, right);
                float deltaFwd = Vector3.Dot(delta, fwd);
                morphedFwd[i] = Vector3.Dot(morphedWorldVerts[i] - center, fwd);
                if (deltaFwd <= minDelta || originalFwd[i] <= 0f)
                    continue;

                deltaWorld[i] = delta;
                strength[i] = Mathf.Sqrt(deltaSide * deltaSide + deltaFwd * deltaFwd);
                if (morphedFwd[i] > maxFront)
                    maxFront = morphedFwd[i];
            }

            if (maxFront <= 0f)
            {
                SetSkirtLowerTraceReason(traces, valid, "lower:no-front-delta");
                return;
            }

            float frontBand = Mathf.Max(radiusFront * SkirtBoundaryFrontBand, RelFwd(0.01f));
            List<float> boundaryUps = new List<float>();

            for (int i = 0; i < count; i++)
            {
                if (!valid[i] || strength[i] <= minDelta) continue;
                if (originalFwd[i] <= 0f) continue;
                if (morphedFwd[i] < maxFront - frontBand) continue;
                boundaryUps.Add(originalUp[i]);
            }

            if (boundaryUps.Count < 3)
            {
                SetSkirtLowerTraceReason(traces, valid, "lower:not-enough-boundary");
                return;
            }

            float boundaryUp = MedianFloat(boundaryUps) + RelUp(SkirtBoundaryUpOffset);
            float boundaryBand = Mathf.Max(radiusDown * SkirtBoundaryPlaneBand, RelUp(0.012f));
            bool[] boundaryMask = new bool[count];
            Vector3[] boundaryDelta = new Vector3[count];
            float[] boundaryStrength = new float[count];
            if (traces != null)
            {
                int traceCount = Mathf.Min(traces.Length, count);
                for (int i = 0; i < traceCount; i++)
                {
                    if (traces[i] == null || !valid[i]) continue;
                    traces[i].SkirtLowerBoundaryUp = boundaryUp;
                    traces[i].SkirtLowerReason = string.Empty;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (!valid[i] || strength[i] <= minDelta) continue;
                if (originalFwd[i] <= 0f) continue;
                if (Mathf.Abs(originalUp[i] - boundaryUp) > boundaryBand) continue;

                boundaryMask[i] = true;
                boundaryDelta[i] = deltaWorld[i];
                boundaryStrength[i] = strength[i];
            }

            SmoothSkirtBoundaryDeltas(neighbors, boundaryMask, boundaryDelta, boundaryStrength);

            Vector3 topDelta = Vector3.zero;
            float topWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                if (!boundaryMask[i]) continue;

                float w = Mathf.Max(boundaryStrength[i], minDelta);
                topDelta += boundaryDelta[i] * w;
                topWeight += w;
            }

            if (topWeight <= 0f)
            {
                SetSkirtLowerTraceReason(traces, valid, "lower:no-top-weight");
                return;
            }

            topDelta /= topWeight;

            float topDeltaFwd = Mathf.Max(0f, Vector3.Dot(topDelta, fwd));
            float lowerLimit = boundaryUp;
            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                if (originalFwd[i] <= 0f) continue;
                if (originalUp[i] <= boundaryUp)
                    lowerLimit = Mathf.Min(lowerLimit, originalUp[i]);
            }

            float verticalSpan = Mathf.Max(boundaryUp - lowerLimit, RelUp(0.02f));
            Vector3[] inheritedDelta = new Vector3[count];
            bool[] inheritedReached = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (!boundaryMask[i]) continue;
                inheritedDelta[i] = boundaryDelta[i];
                inheritedReached[i] = true;
            }

            PropagateSkirtLowerBoundaryDeltas(
                neighbors,
                valid,
                originalUp,
                originalFwd,
                boundaryUp,
                inheritedDelta,
                inheritedReached);

            float growth = topDeltaFwd / Mathf.Max(radiusFront, minDelta);
            if (traces != null)
            {
                int traceCount = Mathf.Min(traces.Length, count);
                for (int i = 0; i < traceCount; i++)
                {
                    if (traces[i] == null || !valid[i]) continue;
                    traces[i].SkirtLowerLowerLimit = lowerLimit;
                    traces[i].SkirtLowerTopDeltaFwd = topDeltaFwd;
                    traces[i].SkirtLowerGrowth = growth;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (!valid[i]) continue;
                VertexMorphTrace trace = traces != null && i < traces.Length ? traces[i] : null;
                if (originalFwd[i] <= 0f)
                {
                    if (trace != null) trace.SkirtLowerReason = "lower:not-front";
                    continue;
                }
                if (originalUp[i] > boundaryUp)
                {
                    if (trace != null) trace.SkirtLowerReason = "lower:above-boundary";
                    continue;
                }

                float downT = Mathf.Clamp01((boundaryUp - originalUp[i]) / Mathf.Max(verticalSpan, 0.0001f));
                if (trace != null) trace.SkirtLowerDownT = downT;
                if (boundaryMask[i])
                {
                    if (trace != null) trace.SkirtLowerReason = "lower:boundary-kept";
                    continue;
                }
                if (!inheritedReached[i])
                {
                    inheritedDelta[i] = topDelta;
                    inheritedReached[i] = true;
                }

                Vector3 adjustedWorld = originalWorldVerts[i] + inheritedDelta[i];
                Vector3 addWorld = adjustedWorld - morphedWorldVerts[i];
                float addFwd = Vector3.Dot(addWorld, fwd);
                if (addWorld.sqrMagnitude <= minDelta * minDelta)
                {
                    if (trace != null) trace.SkirtLowerReason = "lower:already-inherited";
                    continue;
                }

                Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weights[i]);
                newLocalVerts[i] = skin.inverse.MultiplyPoint3x4(adjustedWorld);
                morphedWorldVerts[i] = adjustedWorld;
                if (trace != null)
                {
                    trace.SkirtLowerAddFwd = addFwd;
                    trace.SkirtLowerReason = "lower:inherited";
                }
            }
        }

        static void PropagateSkirtLowerBoundaryDeltas(
            List<int>[] neighbors,
            bool[] valid,
            float[] originalUp,
            float[] originalFwd,
            float boundaryUp,
            Vector3[] inheritedDelta,
            bool[] inheritedReached)
        {
            if (neighbors == null || valid == null || originalUp == null || originalFwd == null
                || inheritedDelta == null || inheritedReached == null)
                return;

            int count = Mathf.Min(
                inheritedDelta.Length,
                Mathf.Min(inheritedReached.Length, Mathf.Min(valid.Length, Mathf.Min(originalUp.Length, originalFwd.Length))));
            int passes = Mathf.Min(count, 128);

            for (int pass = 0; pass < passes; pass++)
            {
                bool changed = false;
                Vector3[] nextDelta = (Vector3[])inheritedDelta.Clone();
                bool[] nextReached = (bool[])inheritedReached.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!valid[i] || inheritedReached[i]) continue;
                    if (originalFwd[i] <= 0f || originalUp[i] > boundaryUp) continue;

                    Vector3 sum = Vector3.zero;
                    float sumWeight = 0f;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (j < 0 || j >= count || !inheritedReached[j]) continue;
                        if (originalFwd[j] <= 0f) continue;
                        if (originalUp[j] < originalUp[i] - 0.0001f) continue;

                        float verticalStep = Mathf.Max(0f, originalUp[j] - originalUp[i]);
                        float w = 1f / (1f + verticalStep * 40f);
                        sum += inheritedDelta[j] * w;
                        sumWeight += w;
                    }

                    if (sumWeight <= 0f) continue;

                    nextDelta[i] = sum / sumWeight;
                    nextReached[i] = true;
                    changed = true;
                }

                for (int i = 0; i < count; i++)
                {
                    inheritedDelta[i] = nextDelta[i];
                    inheritedReached[i] = nextReached[i];
                }

                if (!changed)
                    break;
            }
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

        static void SmoothSkirtBoundaryDeltas(
            List<int>[] neighbors,
            bool[] boundaryMask,
            Vector3[] boundaryDelta,
            float[] boundaryStrength)
        {
            int passes = Mathf.Clamp(SkirtBoundarySmoothPasses, 0, 16);
            float smooth = Mathf.Clamp01(SkirtBoundarySmoothStrength);
            if (passes <= 0 || smooth <= 0f || neighbors == null || boundaryMask == null)
                return;

            int count = boundaryMask.Length;
            for (int pass = 0; pass < passes; pass++)
            {
                Vector3[] nextDelta = (Vector3[])boundaryDelta.Clone();
                float[] nextStrength = (float[])boundaryStrength.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!boundaryMask[i]) continue;

                    Vector3 sumDelta = boundaryDelta[i] * 2f;
                    float sumStrength = boundaryStrength[i] * 2f;
                    float sumWeight = 2f;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (j < 0 || j >= count || !boundaryMask[j]) continue;
                        sumDelta += boundaryDelta[j];
                        sumStrength += boundaryStrength[j];
                        sumWeight += 1f;
                    }

                    if (sumWeight <= 2f) continue;
                    nextDelta[i] = Vector3.Lerp(boundaryDelta[i], sumDelta / sumWeight, smooth);
                    nextStrength[i] = Mathf.Lerp(boundaryStrength[i], sumStrength / sumWeight, smooth);
                }

                for (int i = 0; i < count; i++)
                {
                    boundaryDelta[i] = nextDelta[i];
                    boundaryStrength[i] = nextStrength[i];
                }
            }
        }

        static void PropagateSkirtDrapeDelta(
            List<int>[] neighbors,
            bool[] valid,
            Vector3[] originalWorldVerts,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            Vector2[] propagatedDelta,
            bool[] reached)
        {
            int count = propagatedDelta.Length;
            for (int pass = 0; pass < SkirtDrapePropagatePasses; pass++)
            {
                bool changed = false;
                Vector2[] nextDelta = (Vector2[])propagatedDelta.Clone();
                bool[] nextReached = (bool[])reached.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!valid[i]) continue;

                    Vector3 rel = originalWorldVerts[i] - center;
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);
                    if (originalFwd <= 0f) continue;

                    Vector2 sumDelta = reached[i] ? propagatedDelta[i] : Vector2.zero;
                    int sumCount = reached[i] ? 1 : 0;
                    Vector2 bestDelta = reached[i] ? propagatedDelta[i] : Vector2.zero;
                    float bestMagnitude = bestDelta.sqrMagnitude;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (!reached[j]) continue;

                        Vector3 nr = originalWorldVerts[j] - center;
                        float neighborUp = Vector3.Dot(nr, up);
                        float neighborFwd = Vector3.Dot(nr, fwd);
                        if (neighborFwd <= 0f) continue;
                        if (neighborUp < originalUp - 0.0001f) continue;

                        Vector2 candidate = propagatedDelta[j] * SkirtDrapePropagationDecay;
                        if (candidate.y <= 0f || candidate.sqrMagnitude <= 1e-8f) continue;
                        sumDelta += candidate;
                        sumCount++;
                        float candidateMagnitude = candidate.sqrMagnitude;
                        if (candidateMagnitude > bestMagnitude)
                        {
                            bestDelta = candidate;
                            bestMagnitude = candidateMagnitude;
                        }
                    }

                    if (sumCount <= 0) continue;

                    Vector2 averageDelta = sumDelta / sumCount;
                    Vector2 targetDelta = reached[i]
                        ? (averageDelta.sqrMagnitude > propagatedDelta[i].sqrMagnitude ? averageDelta : propagatedDelta[i])
                        : Vector2.Lerp(averageDelta, bestDelta, 0.50f);

                    if (targetDelta.y > 0f
                        && targetDelta.sqrMagnitude > 1e-8f
                        && (!nextReached[i] || targetDelta.sqrMagnitude > nextDelta[i].sqrMagnitude + 1e-8f))
                    {
                        nextDelta[i] = targetDelta;
                        nextReached[i] = true;
                        changed = true;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    propagatedDelta[i] = nextDelta[i];
                    reached[i] = nextReached[i];
                }

                if (!changed)
                    break;
            }
        }

        static void SmoothSkirtDrapeDelta(
            List<int>[] neighbors,
            bool[] valid,
            Vector3[] originalWorldVerts,
            Vector3 center,
            Vector3 up,
            Vector3 fwd,
            float radiusDown,
            Vector2[] propagatedDelta,
            bool[] reached)
        {
            int count = propagatedDelta.Length;
            float upRange = Mathf.Max(radiusDown * 0.16f, RelUp(0.01f));

            for (int pass = 0; pass < SkirtDrapeSmoothPasses; pass++)
            {
                Vector2[] nextDelta = (Vector2[])propagatedDelta.Clone();

                for (int i = 0; i < count; i++)
                {
                    if (!valid[i] || !reached[i]) continue;

                    Vector3 rel = originalWorldVerts[i] - center;
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);
                    if (originalFwd <= 0f) continue;

                    Vector2 sum = propagatedDelta[i] * 2f;
                    float weight = 2f;
                    List<int> ns = neighbors[i];
                    for (int n = 0; n < ns.Count; n++)
                    {
                        int j = ns[n];
                        if (!valid[j] || !reached[j]) continue;

                        Vector3 nr = originalWorldVerts[j] - center;
                        float neighborUp = Vector3.Dot(nr, up);
                        float neighborFwd = Vector3.Dot(nr, fwd);
                        if (neighborFwd <= 0f) continue;

                        float upDiff = Mathf.Abs(neighborUp - originalUp);
                        float w = 1f - Mathf.Clamp01(upDiff / upRange);
                        if (w <= 0f) continue;

                        sum += propagatedDelta[j] * w;
                        weight += w;
                    }

                    if (weight <= 2f) continue;

                    Vector2 average = sum / weight;
                    if (average.y <= 0f) continue;

                    nextDelta[i] = Vector2.Lerp(propagatedDelta[i], average, SkirtDrapeSmoothStrength);
                }

                for (int i = 0; i < count; i++)
                    propagatedDelta[i] = nextDelta[i];
            }
        }

        struct LayerGuardCoord
        {
            public float Side;
            public float Up;
            public float Fwd;
        }

        static void ApplyCrossOuterClothLayerGuard(Maid maid, List<SkinnedMeshRenderer> smrs)
        {
            if (!_bpWorldCached || maid == null || smrs == null || smrs.Count == 0)
                return;

            float strength = Mathf.Max(0f, OuterClothLayerGuard);
            if (strength <= 0f)
                return;

            float radiusScale = Mathf.Max(0.05f, 1f + InflationMultiplier);
            float radiusSide = Mathf.Max(RelSide(RegionRadiusSide) * radiusScale, RelGeneral(0.0001f));
            float radiusFront = Mathf.Max(RelFwd(RegionRadiusFront) * radiusScale, RelGeneral(0.0001f));
            float radiusUp = Mathf.Max(RelUp(RegionRadiusUp) * radiusScale, RelGeneral(0.0001f));
            float radiusDown = Mathf.Max(RelUp(RegionRadiusDown) * radiusScale, RelGeneral(0.0001f));
            Vector3 center = _bpWorldFrame.Center + _bpWorldFrame.Up * RelUp(InflationMoveY) + _bpWorldFrame.Fwd * RelFwd(InflationMoveZ);
            Vector3 right = _bpWorldFrame.Right;
            Vector3 up = _bpWorldFrame.Up;
            Vector3 fwd = _bpWorldFrame.Fwd;

            float sideRange = Mathf.Max(radiusSide * 0.18f, RelSide(0.016f));
            float upRange = Mathf.Max((radiusUp + radiusDown) * 0.08f, RelUp(0.014f));
            float minOriginalGap = Mathf.Max(radiusFront * 0.006f, RelFwd(0.001f));
            float maxOriginalGap = Mathf.Max(radiusFront * 0.80f, minOriginalGap * 2f);
            float minFinalGap = Mathf.Max(RelFwd(0.001f), RelFwd(0.008f) * strength);

            List<CrossLayerGuardMesh> meshes = new List<CrossLayerGuardMesh>();
            Dictionary<long, List<LayerGuardRef>> buckets = new Dictionary<long, List<LayerGuardRef>>();
            HashSet<int> seenMeshes = new HashSet<int>();

            for (int m = 0; m < smrs.Count; m++)
            {
                SkinnedMeshRenderer smr = smrs[m];
                if (smr == null || smr.sharedMesh == null) continue;
                if (ClassifyMesh(smr) != MeshMorphClass.OuterCloth) continue;

                Mesh mesh = smr.sharedMesh;
                if (!seenMeshes.Add(mesh.GetInstanceID())) continue;

                MeshRecord rec = FindRecord(maid, smr);
                if (rec == null || rec.OrigVerts == null || rec.Mesh != mesh) continue;

                Vector3[] currentVerts = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                if (currentVerts == null || weights == null) continue;
                if (currentVerts.Length != rec.OrigVerts.Length || weights.Length != currentVerts.Length) continue;

                Matrix4x4[] boneMatrices;
                if (!TryBuildBindPoseSkinMatrices(smr, out boneMatrices)) continue;

                CrossLayerGuardMesh entry = new CrossLayerGuardMesh
                {
                    SMR = smr,
                    Record = rec,
                    Mesh = mesh,
                    CurrentVerts = (Vector3[])currentVerts.Clone(),
                    Weights = weights,
                    BoneMatrices = boneMatrices,
                    OriginalWorld = new Vector3[currentVerts.Length],
                    MorphedWorld = new Vector3[currentVerts.Length],
                    Valid = new bool[currentVerts.Length],
                    Coords = new LayerGuardCoord[currentVerts.Length],
                    Changed = false,
                };

                int meshIndex = meshes.Count;
                for (int i = 0; i < entry.CurrentVerts.Length; i++)
                {
                    BoneWeight weight = weights[i];
                    if (!HasValidBoneWeight(weight, boneMatrices)) continue;

                    Matrix4x4 skin = GetWeightedSkinMatrix(boneMatrices, weight);
                    Vector3 originalWorld = skin.MultiplyPoint3x4(rec.OrigVerts[i]);
                    Vector3 morphedWorld = skin.MultiplyPoint3x4(entry.CurrentVerts[i]);
                    Vector3 rel = originalWorld - center;
                    float side = Vector3.Dot(rel, right);
                    float originalUp = Vector3.Dot(rel, up);
                    float originalFwd = Vector3.Dot(rel, fwd);

                    entry.OriginalWorld[i] = originalWorld;
                    entry.MorphedWorld[i] = morphedWorld;
                    entry.Valid[i] = true;
                    entry.Coords[i].Side = side;
                    entry.Coords[i].Up = originalUp;
                    entry.Coords[i].Fwd = originalFwd;

                    if (originalFwd <= 0f) continue;
                    if (GetOuterLayerGuardVerticalFade(originalUp, radiusUp, radiusDown) <= 0f) continue;

                    int sideBin = Mathf.FloorToInt(side / sideRange);
                    int upBin = Mathf.FloorToInt(originalUp / upRange);
                    long key = LayerBucketKey(sideBin, upBin);
                    if (!buckets.TryGetValue(key, out List<LayerGuardRef> list))
                    {
                        list = new List<LayerGuardRef>();
                        buckets[key] = list;
                    }
                    list.Add(new LayerGuardRef(meshIndex, i));
                }

                meshes.Add(entry);
            }

            if (meshes.Count == 0 || buckets.Count == 0)
                return;

            for (int pass = 0; pass < 2; pass++)
            {
                bool changedThisPass = false;

                for (int mi = 0; mi < meshes.Count; mi++)
                {
                    CrossLayerGuardMesh entry = meshes[mi];
                    for (int i = 0; i < entry.CurrentVerts.Length; i++)
                    {
                        if (!entry.Valid[i]) continue;

                        LayerGuardCoord c = entry.Coords[i];
                        if (c.Fwd <= 0f) continue;
                        float guardFade = GetOuterLayerGuardVerticalFade(c.Up, radiusUp, radiusDown);
                        if (guardFade <= 0f) continue;

                        int sideBin = Mathf.FloorToInt(c.Side / sideRange);
                        int upBin = Mathf.FloorToInt(c.Up / upRange);
                        int coverMesh = -1;
                        int coverVertex = -1;
                        float bestScore = float.MaxValue;

                        for (int sy = -2; sy <= 2; sy++)
                        {
                            for (int uy = -2; uy <= 2; uy++)
                            {
                                long key = LayerBucketKey(sideBin + sy, upBin + uy);
                                if (!buckets.TryGetValue(key, out List<LayerGuardRef> list)) continue;

                                for (int n = 0; n < list.Count; n++)
                                {
                                    LayerGuardRef r = list[n];
                                    if (r.MeshIndex == mi && r.VertexIndex == i) continue;

                                    CrossLayerGuardMesh other = meshes[r.MeshIndex];
                                    if (!other.Valid[r.VertexIndex]) continue;

                                    LayerGuardCoord jc = other.Coords[r.VertexIndex];
                                    float fwdGap = jc.Fwd - c.Fwd;
                                    if (fwdGap < minOriginalGap || fwdGap > maxOriginalGap) continue;

                                    float sideDiff = Mathf.Abs(jc.Side - c.Side);
                                    if (sideDiff > sideRange * 1.6f) continue;
                                    float upDiff = Mathf.Abs(jc.Up - c.Up);
                                    if (upDiff > upRange * 1.6f) continue;

                                    float sideScore = sideDiff / sideRange;
                                    float upScore = upDiff / upRange;
                                    float fwdScore = fwdGap / maxOriginalGap;
                                    float sameMeshPenalty = r.MeshIndex == mi ? 0.15f : 0f;
                                    float score = sideScore * sideScore + upScore * upScore + fwdScore * 0.20f + sameMeshPenalty;
                                    if (score < bestScore)
                                    {
                                        bestScore = score;
                                        coverMesh = r.MeshIndex;
                                        coverVertex = r.VertexIndex;
                                    }
                                }
                            }
                        }

                        if (coverMesh < 0 || coverVertex < 0)
                            continue;

                        CrossLayerGuardMesh cover = meshes[coverMesh];
                        float currentFwd = Vector3.Dot(entry.MorphedWorld[i] - center, fwd);
                        float coverFwd = Vector3.Dot(cover.MorphedWorld[coverVertex] - center, fwd);
                        float targetFwd = coverFwd - minFinalGap * guardFade;
                        float overrun = currentFwd - targetFwd;
                        if (overrun <= 0f)
                            continue;

                        Vector3 adjustedWorld = entry.MorphedWorld[i] - fwd * (overrun * guardFade);
                        Matrix4x4 skin = GetWeightedSkinMatrix(entry.BoneMatrices, entry.Weights[i]);
                        entry.CurrentVerts[i] = skin.inverse.MultiplyPoint3x4(adjustedWorld);
                        VertexMorphTrace trace = entry.Record.LastMorphTrace != null && i < entry.Record.LastMorphTrace.Length
                            ? entry.Record.LastMorphTrace[i]
                            : null;
                        if (trace != null)
                        {
                            float adjustedFwd = Vector3.Dot(adjustedWorld - center, fwd);
                            trace.CrossLayerGuardDeltaFwd += adjustedFwd - currentFwd;
                            trace.FwdCrossLayerGuard = adjustedFwd;
                            trace.FwdFinal = adjustedFwd;
                        }
                        entry.MorphedWorld[i] = adjustedWorld;
                        entry.Changed = true;
                        changedThisPass = true;
                    }
                }

                if (!changedThisPass)
                    break;
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                CrossLayerGuardMesh entry = meshes[i];
                if (!entry.Changed) continue;

                entry.Record.LastDeltaVerts = BuildDeltaVerts(entry.Record.OrigVerts, entry.CurrentVerts);
                entry.Record.AppliedSignature = ComputeVertexSignature(entry.CurrentVerts);
                entry.Mesh.vertices = entry.CurrentVerts;
                ApplySmoothedNormals(entry.Mesh, entry.Record, entry.CurrentVerts);
                entry.Mesh.RecalculateBounds();
            }
        }

        static float GetOuterLayerGuardVerticalFade(float originalUp, float radiusUp, float radiusDown)
        {
            if (originalUp > radiusUp * 0.35f)
                return 0f;

            float fadeStart = -radiusDown * 0.55f;
            float fadeEnd = -radiusDown * 1.15f;
            if (originalUp >= fadeStart)
                return 1f;
            if (originalUp <= fadeEnd)
                return 0f;

            return Smooth01((originalUp - fadeEnd) / Mathf.Max(fadeStart - fadeEnd, 0.0001f));
        }

        static long LayerBucketKey(int sideBin, int upBin)
        {
            return ((long)sideBin << 32) ^ (uint)upBin;
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

        static Vector3 ApplyClothLayerOffsetWorld(
            MeshMorphClass meshClass,
            Vector3 original,
            Vector3 morphed,
            Vector3 center,
            float shapeWeight,
            float directionalRadius,
            float originalRadius,
            float edgeRatio,
            float upDot,
            float radiusDown,
            Vector3 right,
            Vector3 up,
            Vector3 fwd)
        {
            if (meshClass == MeshMorphClass.Body || meshClass == MeshMorphClass.Ignore)
                return morphed;

            bool isOuterCloth = meshClass == MeshMorphClass.OuterCloth;
            float layerOffset = meshClass == MeshMorphClass.OuterCloth
                ? RelGeneral(OuterClothOffset)
                : RelGeneral(InnerClothOffset);
            float preserve = isOuterCloth ? ClothThicknessPreserve : 0f;

            if (layerOffset == 0f && preserve == 0f) return morphed;

            Vector3 rel = original - center;
            if (rel.sqrMagnitude < 1e-8f)
                rel = morphed - center;
            if (rel.sqrMagnitude < 1e-8f)
                return morphed;

            Vector3 displacement = morphed - original;
            Vector3 offsetBase = displacement.sqrMagnitude > 1e-8f
                ? displacement.normalized
                : rel.normalized;

            float side = Vector3.Dot(offsetBase, right);
            float vertical = Vector3.Dot(offsetBase, up);
            float forward = Vector3.Dot(offsetBase, fwd);
            float sideRatio = isOuterCloth ? ClothOffsetSideRatio : 1f;
            Vector3 offsetDir = right * (side * sideRatio)
                + up * vertical
                + fwd * forward;
            float directionScale = offsetDir.magnitude;
            if (offsetDir.sqrMagnitude < 1e-8f)
                return morphed;
            else
                offsetDir.Normalize();

            float shellRatio = originalRadius / Mathf.Max(directionalRadius, 0.0001f);
            float surface = Smooth01((shellRatio - 0.35f) / 0.65f);
            float boundaryFade = Smooth01((1f - edgeRatio) / 0.35f);
            float fade = Smooth01(shapeWeight) * boundaryFade;
            float baseThickness = layerOffset + directionalRadius * 0.10f * preserve * surface;
            float back = Mathf.Clamp01(-forward);
            float backScale = isOuterCloth ? Mathf.Lerp(1f, ClothBackOffsetBoost, back) : 1f;
            float baseAmount = baseThickness * fade;

            return morphed + offsetDir * (baseAmount * directionScale * backScale);
        }

        static Vector3 ApplyClothDepthStretchWorld(
            MeshMorphClass meshClass,
            Vector3 original,
            Vector3 morphed,
            float directionalRadius,
            float originalRadius,
            float upDot,
            float radiusDown)
        {
            if (meshClass != MeshMorphClass.OuterCloth || ClothDepthStretch == 0f)
                return morphed;

            Vector3 displacement = morphed - original;
            if (displacement.sqrMagnitude < 1e-10f)
                return morphed;

            float depthStretch = GetClothDepthStretchFactor(
                meshClass,
                originalRadius,
                directionalRadius,
                upDot,
                radiusDown);

            return original + displacement * depthStretch;
        }

        static float GetClothDepthStretchFactor(
            MeshMorphClass meshClass,
            float originalRadius,
            float directionalRadius,
            float upDot,
            float radiusDown)
        {
            if (meshClass != MeshMorphClass.OuterCloth)
                return 1f;

            float shellRatio = Mathf.Clamp01(originalRadius / Mathf.Max(directionalRadius, 0.0001f));
            float outerSurface = Smooth01((shellRatio - 0.20f) / 0.80f);
            float lower = upDot < 0f
                ? Smooth01((-upDot / Mathf.Max(radiusDown, 0.0001f) - 0.25f) / 0.75f)
                : 0f;
            float minFollow = Mathf.Lerp(0.30f, 0.16f, lower);
            float oldCompressionFollow = Mathf.Lerp(minFollow, 1f, outerSurface);
            float stretchControl = ClothDepthStretch * Mathf.Lerp(1f, 1.35f, lower);
            float inverseCompression = (oldCompressionFollow - minFollow) / Mathf.Max(1f - minFollow, 0.0001f);

            return 1f + inverseCompression * stretchControl;
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
                BellyMorphController.FlushMorphDirty(_maid);
                ProcessFullRefresh();
                ProcessVisibilityApply();
            }
        }
    }
}

