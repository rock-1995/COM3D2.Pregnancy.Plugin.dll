using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [Serializable]
    public class PregSettings
    {
        public float bellyInflationMultiplier = 0.0f;
        public float bellyInflationMoveY = 0.05f;
        public float bellyInflationMoveZ = 0.0f;
        public float bellyInflationStretchX = 0.3f;
        public float bellyInflationStretchY = 0.0f;
        public float bellyInflationStretchZ = 0.5f;
        public float bellyInflationShiftY = 0.04f;
        public float bellyInflationShiftZ = -0.3f;
        public float bellyInflationTaperY = -0.01f;
        public float bellyInflationTaperZ = -0.05f;
        public float bellyInflationRoundness = 0.033f;
        public float bellyInflationDrop = 0.15f;
        public float bellyInflationFatFold = 0.0f;
        public float bellyInflationFatFoldHeight = 0.0f;
        public float bellyInflationFatFoldGap = 0.0f;
        public float bellyRegionRadiusSide = 0.23f;
        public float bellyRegionRadiusFront = 0.33f;
        public float bellyRegionRadiusBack = 0.13f;
        public float bellyRegionRadiusUp = 0.58f;
        public float bellyRegionRadiusDown = 0.35f;
        public float bellyThighGuardSpeed = 3.0f;
        public float bellyInnerThighGuardStrength = 1.0f;
        public float bellyThighGuardSmoothStrength = 0.0f;
        public float bellyTopEdgeTaper = -1.0f;
        public float bellyBottomEdgeTaper = 0.0f;
        public float bellySideSmoothWidth = 0.8f;
        public float bellySideSmoothStrength = 1.4f;
        public float bellyBreastGuardStrength = 1.0f;
        public float bellyOuterClothPregnancyScale = 1.0f;
        public int bellySkirtBoundarySmoothPasses = 2;
        public float bellySkirtBoundarySmoothStrength = 0.35f;
        public float bellySkirtBoundaryUpOffset = 0.08f;
        public float bellySkirtFrontPlaneFwdOffset = 0.0f;
        public float bellySkirtTopRadiusSideScale = 1.15f;
        public float bellySkirtTopRadiusFwdScale = 1.15f;
        public float bellySkirtHemFadeRangeScale = 1.0f;
        public float bellySkirtLowerTipSide = 0.0f;
        public float bellySkirtLowerTipUp = -0.42f;
        public float bellySkirtLowerTipFwd = 0.0f;
        public float bellySkirtLowerRadiusSide = 1.0f;
        public float bellySkirtLowerRadiusUp = 1.0f;
        public float bellySkirtLowerRadiusFwd = 1.0f;

    }
}
