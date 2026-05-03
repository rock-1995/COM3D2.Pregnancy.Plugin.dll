using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [Serializable]
    public class PregSettings
    {
        public float bellyInflationMultiplier = 0.0f;
        public float bellyInflationMoveY = 0.025f;
        public float bellyInflationMoveZ = 0.0f;
        public float bellyInflationStretchX = -0.2f;
        public float bellyInflationStretchY = 0.0f;
        public float bellyInflationStretchZ = 0.13f;
        public float bellyInflationShiftY = 0.04f;
        public float bellyInflationShiftZ = -0.3f;
        public float bellyInflationTaperY = -0.03f;
        public float bellyInflationTaperZ = -0.05f;
        public float bellyInflationRoundness = 0.03f;
        public float bellyInflationDrop = 0.2f;
        public float bellyInflationFatFold = 0.0f;
        public float bellyInflationFatFoldHeight = 0.0f;
        public float bellyInflationFatFoldGap = 0.0f;
        public float bellyRegionRadiusSide = 0.22f;
        public float bellyRegionRadiusFront = 0.22f;
        public float bellyRegionRadiusBack = 0.13f;
        public float bellyRegionRadiusUp = 0.26f;
        public float bellyRegionRadiusDown = 0.18f;
        public float bellyThighGuardSpeed = 4.0f;
        public float bellyInnerThighGuardStrength = 1.0f;
        public float bellyTopEdgeTaper = -1.0f;
        public float bellyBottomEdgeTaper = 0.0f;
        public float bellySideSmoothWidth = 0.8f;
        public float bellySideSmoothStrength = 1.4f;
        public float bellyBreastGuardStrength = 1.0f;
        public float bellyOuterClothPregnancyScale = 1.0f;
        public bool bellyOuterClothSkirtDrape = false;
        public float bellyOuterClothLayerGuard = 0.0f;
        public float bellyInnerClothOffset = 0.0f;
        public float bellyOuterClothOffset = 0.006f;
        public float bellyClothThicknessPreserve = 3.0f;
        public float bellyClothOffsetSideRatio = 0.0f;
        public float bellyClothBackOffsetBoost = 0.0f;
        public float bellyClothDepthStretch = 4.0f;
        public float bellyOuterClothLowerFrontGuard = 0.0f;
        public int   bellyClothDeformSmoothPasses    = 8;
        public float bellyClothDeformSmoothStrength  = 0.6f;
        public float bellyClothDeformSmoothThreshold = 60f;
        public int   bellyClothDeformSmoothRings     = 2;

    }
}
