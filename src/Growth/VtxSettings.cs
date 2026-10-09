using System;
namespace COM3D2.Pregnancy.Plugin.Growth
{
    [Serializable]
    public sealed class VtxSettings
    {
        public float SubtleStageProgress = 0.1733491f;
        public float VisibleStageProgress = 0.2804245f;
        public float MidStageProgress = 0.4669811f;
        public float GrowthFullness = 1.15896225f;
        public float GrowthWidth = 1.1927711f;
        public float UpperReach = 0.883726418f;
        public float LateSettle = 0.502008f;
        public float VerticalRange = 0.993976f;
        public float WallSmoothing = 0.35f;
        // Keep the legacy JSON key and value for the renamed skin-slide control.
        public float SagStrength = -0.8f;
        public float BellySag = 2f;
        public float MidVolume = 1.1551205f;
        public float LowerPoleLift = 0f;
        public float LateForwardShift = 0.05f;
        public float GrowthAxisTilt = 0f;
        public float LateHeightScale = 1.15f;
        public float LateDepthScale = 1.10f;
        public float LateWidthScale = 1.10f;
        public float SkinClearance = 0.0386792459f;
        public float ThighGuardSpeed = 0f;
        public float InnerThighGuardStrength = 3f;
        public float ThighGuardSmoothStrength = 0f;
        public float VirtualAxisStrength = 1f;
        public float AxisBlendStart = 0.02f;
        public float AxisBlendFull = 0.25f;
        public float LowerTransitionStart = -0.39976415f;
        public float LowerTransitionWidth = .34f;
        public float LowerTransitionBias = 0f;
        public float UpperTransitionStart = .28f;
        public float UpperTransitionWidth = 0.599433959f;
        public float UpperTransitionBias = -0.886792481f;
        public float UpperTransitionJoin = 0.422169805f;
        public float UpperTransitionActivation = .02f;
        public bool BreastExclusionEnabled = true;
        public bool UpperBoneFilterEnabled = false;
        public bool UpperFieldFadeEnabled = false;
        public float AxisPullLow = .60f;
        public float AxisPullHigh = .90f;
        public float AxisPullAngle = 35f;
        public float AxisAnchorY = 0f;
        public float AxisAnchorZ = 0f;
        public bool NavelPreviewFull = false;
        public float NavelEversion = 0.800000012f;
        public float NavelStart = 0.4f;
        public float NavelHeight = 0.0134433955f;
        public float NavelVerticalOffset = -0.0270000007f;
        public float NavelRadius = 0.046194777f;
        public float NavelProportion = 0.5461847f;
        public float ClothOffset = 1.01f;
        public float ClothDistortThreshold = 1.2f;
        public float ClothDistortNeighborDiff = 0.45f;
        public void CompleteGrowthDefaults(string savedJson)
        {
            // Old Unity JSON saves have none of these newly added controls.
            // Only absent fields receive defaults; an explicitly saved zero
            // remains a user choice, as do all pre-existing shape settings.
            var defaults=new VtxSettings();
            if(!savedJson.Contains("\"BellySag\""))BellySag=defaults.BellySag;
            if(!savedJson.Contains("\"SubtleStageProgress\""))SubtleStageProgress=defaults.SubtleStageProgress;
            if(!savedJson.Contains("\"VisibleStageProgress\""))VisibleStageProgress=defaults.VisibleStageProgress;
            if(!savedJson.Contains("\"MidStageProgress\""))MidStageProgress=defaults.MidStageProgress;
            if(!savedJson.Contains("\"LateForwardShift\""))LateForwardShift=defaults.LateForwardShift;
            if(!savedJson.Contains("\"GrowthAxisTilt\""))GrowthAxisTilt=defaults.GrowthAxisTilt;
            if(!savedJson.Contains("\"LateHeightScale\""))LateHeightScale=defaults.LateHeightScale;
            if(!savedJson.Contains("\"LateDepthScale\""))LateDepthScale=defaults.LateDepthScale;
            if(!savedJson.Contains("\"LateWidthScale\""))LateWidthScale=defaults.LateWidthScale;
            if(!savedJson.Contains("\"NavelVerticalOffset\""))NavelVerticalOffset=defaults.NavelVerticalOffset;
        }
        public VtxSettings Copy() => (VtxSettings)MemberwiseClone();
    }
}
