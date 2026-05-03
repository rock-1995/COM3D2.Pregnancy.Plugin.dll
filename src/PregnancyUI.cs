using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public class PregnancyUI : MonoBehaviour
    {
        private bool _visible = false;
        private Rect _win = new Rect(120, 120, 440, 800);
        private int _winId;

        private readonly List<Maid> _maids = new List<Maid>();
        private readonly List<string> _names = new List<string>();
        private int _sel = -1;
        private Maid _curMaid = null;
        private bool _curPreg = false;
        private float _curProg = 0f;
        private float _curCycle = 0f;

        private bool _dropOpen = false;
        private Rect _dropRect;
        private Vector2 _scrollPos = Vector2.zero;

        private string _sInflationMultiplier = "0";
        private string _sInflationMoveY = "0.025";
        private string _sInflationMoveZ = "0";
        private string _sInflationStretchX = "-0.2";
        private string _sInflationStretchY = "0";
        private string _sInflationStretchZ = "0.13";
        private string _sInflationShiftY = "0.04";
        private string _sInflationShiftZ = "-0.3";
        private string _sInflationTaperY = "-0.03";
        private string _sInflationTaperZ = "-0.05";
        private string _sInflationRoundness = "0.03";
        private string _sInflationDrop = "0.1";
        private string _sInflationFatFold = "0";
        private string _sInflationFatFoldHeight = "0";
        private string _sInflationFatFoldGap = "0";
        private string _sRegionRadiusSide = "0.22";
        private string _sRegionRadiusFront = "0.22";
        private string _sRegionRadiusBack = "0.13";
        private string _sRegionRadiusUp = "0.26";
        private string _sRegionRadiusDown = "0.18";
        private string _sThighGuardSpeed = "4";
        private string _sInnerThighGuardStrength = "1";
        private string _sTopEdgeTaper = "-1";
        private string _sBottomEdgeTaper = "0";
        private string _sSideSmoothWidth = "0.8";
        private string _sSideSmoothStrength = "1.4";
        private string _sBreastGuardStrength = "1";
        private string _sOuterClothPregnancyScale = "1";
        private bool _outerClothSkirtDrape = false;
        private string _sOuterClothLayerGuard = "0";
        private string _sInnerClothOffset = "0";
        private string _sOuterClothOffset = "0";
        private string _sClothThicknessPreserve = "2";
        private string _sClothOffsetSideRatio = "0";
        private string _sClothBackOffsetBoost = "0";
        private string _sClothDepthStretch = "3";

        void Awake()
        {
            _winId = GetHashCode();
            SyncShapeFieldsFromController();
        }

        void Update()
        {
            if (Input.GetKeyDown(PregnancyPlugin.CfgToggleKey.Value))
            {
                _visible = !_visible;
                if (_visible)
                {
                    SyncShapeFieldsFromController();
                    ScanMaids();
                }
            }
        }

        void OnGUI()
        {
            if (!_visible) return;

            if (_dropOpen && Event.current.type == EventType.MouseDown
                && !_dropRect.Contains(Event.current.mousePosition))
            {
                _dropOpen = false;
                Event.current.Use();
            }

            _win = GUI.Window(_winId, _win, DrawWindow, "COM3D2 Pregnancy");
            if (_dropOpen) DrawDrop();
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _win.width, 20f));

            float scrollBarW = 18f;
            float contentW = _win.width - scrollBarW;
            float contentH = 1078f;

            _scrollPos = GUI.BeginScrollView(
                new Rect(0, 20f, _win.width, _win.height - 20f),
                _scrollPos,
                new Rect(0, 0, contentW, contentH)
            );

            float x = 8f, y = 4f, w = contentW - 16f;
            float lw = 185f;
            float fx = x + lw + 2f;
            float fw = w - lw - 4f;

            GUI.Label(new Rect(x, y, 40f, 18f), "Maid:");
            string lbl = (_sel >= 0 && _sel < _names.Count) ? _names[_sel] : "(no maid in scene)";
            if (GUI.Button(new Rect(x + 44f, y, w - 76f, 24f), lbl + "  v"))
            {
                _dropOpen = !_dropOpen;
                if (_dropOpen)
                    _dropRect = new Rect(
                        _win.x + x + 44f, _win.y + y + 26f,
                        w - 76f, Mathf.Min(_names.Count * 26f + 6f, 180f));
            }
            if (GUI.Button(new Rect(x + w - 28f, y, 26f, 24f), "R"))
            {
                SyncShapeFieldsFromController();
                ScanMaids();
                _dropOpen = false;
            }
            y += 30f;

            if (_curMaid == null)
            {
                GUI.Label(new Rect(x, y, w, 18f), "No maid selected.");
                GUI.EndScrollView();
                return;
            }

            bool newPreg = GUI.Toggle(new Rect(x, y, 160f, 22f), _curPreg, " Pregnant");
            if (newPreg != _curPreg)
            {
                _curPreg = newPreg;
                PregnancyManager.SetPregnant(_curMaid, _curPreg);
                _curCycle = PregnancyManager.EnsureCycleProgress(_curMaid);
            }
            y += 28f;

            int totalDays = PregnancyPlugin.CfgPregnancyWeeks.Value * 7;
            int curDay = Mathf.RoundToInt(_curProg * totalDays);
            GUI.Label(new Rect(x, y, w, 18f),
                string.Format("Progress / P+ Size: {0:F3}  (day {1}/{2})", _curProg, curDay, totalDays));
            y += 20f;

            float newProg = GUI.HorizontalSlider(new Rect(x, y, w, 18f), _curProg, 0f, 1f);
            if (Mathf.Abs(newProg - _curProg) > 0.0005f)
            {
                _curProg = (float)System.Math.Round(newProg, 3);
                PregnancyManager.SetProgress(_curMaid, _curProg);
            }
            y += 26f;

            FertilityCycleMode mode = PregnancyManager.GetCycleMode();
            int cycleLength = PregnancyManager.GetCycleLength(mode);
            if (cycleLength > 0)
            {
                int cycleDay = PregnancyManager.GetCycleDay(_curCycle, cycleLength);
                GUI.Label(new Rect(x, y, w, 18f),
                    string.Format("Cycle Coefficient: {0:F3}  (day {1}/{2})", _curCycle, cycleDay, cycleLength));
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 18f),
                    string.Format("Cycle Coefficient: {0:F3}", _curCycle));
            }
            y += 20f;

            float newCycle = GUI.HorizontalSlider(new Rect(x, y, w, 18f), _curCycle, 0f, 1f);
            if (Mathf.Abs(newCycle - _curCycle) > 0.0005f)
            {
                _curCycle = (float)System.Math.Round(newCycle, 3);
                PregnancyManager.SetCycleProgress(_curMaid, _curCycle);
            }
            y += 26f;

            if (GUI.Button(new Rect(x, y, 110f, 24f), "Apply Belly"))
            {
                ApplyShapeFieldsToController();
                TriggerApplyBelly(_curMaid, _curProg);
                SyncShapeFieldsFromController();
            }
            if (GUI.Button(new Rect(x + 118f, y, 80f, 24f), "Reset Belly"))
                BellyMorphController.Reset(_curMaid);
            if (GUI.Button(new Rect(x + 206f, y, 130f, 24f), "Reset Defaults"))
            {
                BellyMorphController.ResetToDefaults();
                SyncShapeFieldsFromController();
            }
            y += 32f;

            DrawField(ref y, x, lw, fx, fw, "Multiplier (-2..2)", ref _sInflationMultiplier);
            DrawField(ref y, x, lw, fx, fw, "Move Y", ref _sInflationMoveY);
            DrawField(ref y, x, lw, fx, fw, "Move Z", ref _sInflationMoveZ);
            DrawField(ref y, x, lw, fx, fw, "Stretch X", ref _sInflationStretchX);
            DrawField(ref y, x, lw, fx, fw, "Stretch Y", ref _sInflationStretchY);
            DrawField(ref y, x, lw, fx, fw, "Stretch Z", ref _sInflationStretchZ);
            DrawField(ref y, x, lw, fx, fw, "Shift Y", ref _sInflationShiftY);
            DrawField(ref y, x, lw, fx, fw, "Shift Z", ref _sInflationShiftZ);
            DrawField(ref y, x, lw, fx, fw, "Taper Y", ref _sInflationTaperY);
            DrawField(ref y, x, lw, fx, fw, "Taper Z", ref _sInflationTaperZ);
            DrawField(ref y, x, lw, fx, fw, "Roundness", ref _sInflationRoundness);
            DrawField(ref y, x, lw, fx, fw, "Drop", ref _sInflationDrop);
            DrawField(ref y, x, lw, fx, fw, "Fat Fold", ref _sInflationFatFold);
            DrawField(ref y, x, lw, fx, fw, "Fat Fold Height", ref _sInflationFatFoldHeight);
            DrawField(ref y, x, lw, fx, fw, "Fat Fold Gap", ref _sInflationFatFoldGap);

            DrawField(ref y, x, lw, fx, fw, "Region Side", ref _sRegionRadiusSide);
            DrawField(ref y, x, lw, fx, fw, "Region Front", ref _sRegionRadiusFront);
            DrawField(ref y, x, lw, fx, fw, "Region Back", ref _sRegionRadiusBack);
            DrawField(ref y, x, lw, fx, fw, "Region Up", ref _sRegionRadiusUp);
            DrawField(ref y, x, lw, fx, fw, "Region Down", ref _sRegionRadiusDown);
            DrawField(ref y, x, lw, fx, fw, "Thigh Guard Speed", ref _sThighGuardSpeed);
            DrawField(ref y, x, lw, fx, fw, "Inner Thigh Guard", ref _sInnerThighGuardStrength);
            DrawField(ref y, x, lw, fx, fw, "Top Edge Taper", ref _sTopEdgeTaper);
            DrawField(ref y, x, lw, fx, fw, "Bottom Edge Taper", ref _sBottomEdgeTaper);
            DrawField(ref y, x, lw, fx, fw, "Side Smooth Width", ref _sSideSmoothWidth);
            DrawField(ref y, x, lw, fx, fw, "Side Smooth Strength", ref _sSideSmoothStrength);
            DrawField(ref y, x, lw, fx, fw, "Breast Guard Strength", ref _sBreastGuardStrength);
            DrawField(ref y, x, lw, fx, fw, "Outer Cloth Pregnancy Scale", ref _sOuterClothPregnancyScale);
            _outerClothSkirtDrape = GUI.Toggle(new Rect(x, y, w, 22f), _outerClothSkirtDrape, " Outer Cloth Has Skirt Drape");
            y += 24f;
            DrawField(ref y, x, lw, fx, fw, "Skirt Layer Guard", ref _sOuterClothLayerGuard);
            DrawField(ref y, x, lw, fx, fw, "Inner Cloth Offset", ref _sInnerClothOffset);
            DrawField(ref y, x, lw, fx, fw, "Outer Cloth Offset", ref _sOuterClothOffset);
            DrawField(ref y, x, lw, fx, fw, "Cloth Thickness Preserve", ref _sClothThicknessPreserve);
            DrawField(ref y, x, lw, fx, fw, "Cloth Side Offset Ratio", ref _sClothOffsetSideRatio);
            DrawField(ref y, x, lw, fx, fw, "Cloth Back Offset Boost", ref _sClothBackOffsetBoost);
            DrawField(ref y, x, lw, fx, fw, "Cloth Depth Stretch", ref _sClothDepthStretch);

            if (GUI.Button(new Rect(x, y, 120f, 22f), "Log to BepInEx"))
            {
                var log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
                log.LogInfo("[Pregnancy] ===== PregnancyPlus Shape Params =====");
                log.LogInfo("  InflationMultiplier = " + BellyMorphController.InflationMultiplier);
                log.LogInfo("  InflationMoveY      = " + BellyMorphController.InflationMoveY);
                log.LogInfo("  InflationMoveZ      = " + BellyMorphController.InflationMoveZ);
                log.LogInfo("  InflationStretchX   = " + BellyMorphController.InflationStretchX);
                log.LogInfo("  InflationStretchY   = " + BellyMorphController.InflationStretchY);
                log.LogInfo("  InflationStretchZ   = " + BellyMorphController.InflationStretchZ);
                log.LogInfo("  InflationShiftY     = " + BellyMorphController.InflationShiftY);
                log.LogInfo("  InflationShiftZ     = " + BellyMorphController.InflationShiftZ);
                log.LogInfo("  InflationTaperY     = " + BellyMorphController.InflationTaperY);
                log.LogInfo("  InflationTaperZ     = " + BellyMorphController.InflationTaperZ);
                log.LogInfo("  InflationRoundness  = " + BellyMorphController.InflationRoundness);
                log.LogInfo("  InflationDrop       = " + BellyMorphController.InflationDrop);
                log.LogInfo("  InflationFatFold    = " + BellyMorphController.InflationFatFold);
                log.LogInfo("  InflationFatFoldHeight = " + BellyMorphController.InflationFatFoldHeight);
                log.LogInfo("  InflationFatFoldGap = " + BellyMorphController.InflationFatFoldGap);
                log.LogInfo("  RegionRadiusSide    = " + BellyMorphController.RegionRadiusSide);
                log.LogInfo("  RegionRadiusFront   = " + BellyMorphController.RegionRadiusFront);
                log.LogInfo("  RegionRadiusBack    = " + BellyMorphController.RegionRadiusBack);
                log.LogInfo("  RegionRadiusUp      = " + BellyMorphController.RegionRadiusUp);
                log.LogInfo("  RegionRadiusDown    = " + BellyMorphController.RegionRadiusDown);
                log.LogInfo("  ThighGuardSpeed     = " + BellyMorphController.ThighGuardSpeed);
                log.LogInfo("  TopEdgeTaper        = " + BellyMorphController.TopEdgeTaper);
                log.LogInfo("  BottomEdgeTaper     = " + BellyMorphController.BottomEdgeTaper);
                log.LogInfo("  SideSmoothWidth     = " + BellyMorphController.SideSmoothWidth);
                log.LogInfo("  SideSmoothStrength  = " + BellyMorphController.SideSmoothStrength);
                log.LogInfo("  BreastGuardStrength = " + BellyMorphController.BreastGuardStrength);
                log.LogInfo("  OuterClothPregnancyScale = " + BellyMorphController.OuterClothPregnancyScale);
                log.LogInfo("  OuterClothSkirtDrape = " + BellyMorphController.OuterClothSkirtDrape);
                log.LogInfo("  OuterClothLayerGuard = " + BellyMorphController.OuterClothLayerGuard);
                log.LogInfo("  InnerClothOffset    = " + BellyMorphController.InnerClothOffset);
                log.LogInfo("  OuterClothOffset    = " + BellyMorphController.OuterClothOffset);
                log.LogInfo("  ClothThicknessPreserve = " + BellyMorphController.ClothThicknessPreserve);
                log.LogInfo("  ClothOffsetSideRatio = " + BellyMorphController.ClothOffsetSideRatio);
                log.LogInfo("  ClothBackOffsetBoost = " + BellyMorphController.ClothBackOffsetBoost);
                log.LogInfo("  ClothDepthStretch   = " + BellyMorphController.ClothDepthStretch);
                log.LogInfo("=========================");
            }

            GUI.EndScrollView();
        }

        static void DrawField(ref float y, float x, float lw, float fx, float fw, string label, ref string value)
        {
            GUI.Label(new Rect(x, y, lw, 18f), label);
            value = GUI.TextField(new Rect(fx, y, fw, 20f), value);
            y += 24f;
        }

        void DrawDrop()
        {
            GUI.Box(_dropRect, "");
            float h = 26f;
            for (int i = 0; i < _names.Count; i++)
            {
                float iy = _dropRect.y + 3f + i * h;
                if (iy + h > _dropRect.y + _dropRect.height) break;
                Rect r = new Rect(_dropRect.x + 4f, iy, _dropRect.width - 8f, h - 2f);
                if (GUI.Button(r, _names[i]))
                {
                    if (i != _sel) BellyMorphController.Reset(_curMaid);
                    _sel = i;
                    _curMaid = _maids[i];
                    _curPreg = PregnancyManager.GetPregnant(_curMaid);
                    _curProg = PregnancyManager.GetProgress(_curMaid);
                    _curCycle = PregnancyManager.EnsureCycleProgress(_curMaid);
                    _dropOpen = false;
                }
            }
        }

        void ScanMaids()
        {
            _maids.Clear();
            _names.Clear();
            _sel = -1;
            _curMaid = null;

            var cm = GameMain.Instance?.CharacterMgr;
            if (cm == null) return;

            int cnt = cm.GetMaidCount();
            for (int i = 0; i < cnt; i++)
            {
                Maid m = cm.GetMaid(i);
                if (m == null || m.body0 == null) continue;
                _maids.Add(m);
                _names.Add(MaidName(m, i));
            }
            if (_maids.Count > 0)
            {
                _sel = 0;
                _curMaid = _maids[0];
                _curPreg = PregnancyManager.GetPregnant(_curMaid);
                _curProg = PregnancyManager.GetProgress(_curMaid);
                _curCycle = PregnancyManager.EnsureCycleProgress(_curMaid);
            }
        }

        void SyncShapeFieldsFromController()
        {
            _sInflationMultiplier = FormatShape(BellyMorphController.InflationMultiplier);
            _sInflationMoveY = FormatShape(BellyMorphController.InflationMoveY);
            _sInflationMoveZ = FormatShape(BellyMorphController.InflationMoveZ);
            _sInflationStretchX = FormatShape(BellyMorphController.InflationStretchX);
            _sInflationStretchY = FormatShape(BellyMorphController.InflationStretchY);
            _sInflationStretchZ = FormatShape(BellyMorphController.InflationStretchZ);
            _sInflationShiftY = FormatShape(BellyMorphController.InflationShiftY);
            _sInflationShiftZ = FormatShape(BellyMorphController.InflationShiftZ);
            _sInflationTaperY = FormatShape(BellyMorphController.InflationTaperY);
            _sInflationTaperZ = FormatShape(BellyMorphController.InflationTaperZ);
            _sInflationRoundness = FormatShape(BellyMorphController.InflationRoundness);
            _sInflationDrop = FormatShape(BellyMorphController.InflationDrop);
            _sInflationFatFold = FormatShape(BellyMorphController.InflationFatFold);
            _sInflationFatFoldHeight = FormatShape(BellyMorphController.InflationFatFoldHeight);
            _sInflationFatFoldGap = FormatShape(BellyMorphController.InflationFatFoldGap);
            _sRegionRadiusSide = FormatShape(BellyMorphController.RegionRadiusSide);
            _sRegionRadiusFront = FormatShape(BellyMorphController.RegionRadiusFront);
            _sRegionRadiusBack = FormatShape(BellyMorphController.RegionRadiusBack);
            _sRegionRadiusUp = FormatShape(BellyMorphController.RegionRadiusUp);
            _sRegionRadiusDown = FormatShape(BellyMorphController.RegionRadiusDown);
            _sThighGuardSpeed = FormatShape(BellyMorphController.ThighGuardSpeed);
            _sInnerThighGuardStrength = FormatShape(BellyMorphController.InnerThighGuardStrength);
            _sTopEdgeTaper = FormatShape(BellyMorphController.TopEdgeTaper);
            _sBottomEdgeTaper = FormatShape(BellyMorphController.BottomEdgeTaper);
            _sSideSmoothWidth = FormatShape(BellyMorphController.SideSmoothWidth);
            _sSideSmoothStrength = FormatShape(BellyMorphController.SideSmoothStrength);
            _sBreastGuardStrength = FormatShape(BellyMorphController.BreastGuardStrength);
            _sOuterClothPregnancyScale = FormatShape(BellyMorphController.OuterClothPregnancyScale);
            _outerClothSkirtDrape = BellyMorphController.OuterClothSkirtDrape;
            _sOuterClothLayerGuard = FormatShape(BellyMorphController.OuterClothLayerGuard);
            _sInnerClothOffset = FormatShape(BellyMorphController.InnerClothOffset);
            _sOuterClothOffset = FormatShape(BellyMorphController.OuterClothOffset);
            _sClothThicknessPreserve = FormatShape(BellyMorphController.ClothThicknessPreserve);
            _sClothOffsetSideRatio = FormatShape(BellyMorphController.ClothOffsetSideRatio);
            _sClothBackOffsetBoost = FormatShape(BellyMorphController.ClothBackOffsetBoost);
            _sClothDepthStretch = FormatShape(BellyMorphController.ClothDepthStretch);
        }

        void ApplyShapeFieldsToController()
        {
            float v;
            if (PregnancyManager.TryParseFloat(_sInflationMultiplier, out v)) BellyMorphController.InflationMultiplier = v;
            if (PregnancyManager.TryParseFloat(_sInflationMoveY, out v)) BellyMorphController.InflationMoveY = v;
            if (PregnancyManager.TryParseFloat(_sInflationMoveZ, out v)) BellyMorphController.InflationMoveZ = v;
            if (PregnancyManager.TryParseFloat(_sInflationStretchX, out v)) BellyMorphController.InflationStretchX = v;
            if (PregnancyManager.TryParseFloat(_sInflationStretchY, out v)) BellyMorphController.InflationStretchY = v;
            if (PregnancyManager.TryParseFloat(_sInflationStretchZ, out v)) BellyMorphController.InflationStretchZ = v;
            if (PregnancyManager.TryParseFloat(_sInflationShiftY, out v)) BellyMorphController.InflationShiftY = v;
            if (PregnancyManager.TryParseFloat(_sInflationShiftZ, out v)) BellyMorphController.InflationShiftZ = v;
            if (PregnancyManager.TryParseFloat(_sInflationTaperY, out v)) BellyMorphController.InflationTaperY = v;
            if (PregnancyManager.TryParseFloat(_sInflationTaperZ, out v)) BellyMorphController.InflationTaperZ = v;
            if (PregnancyManager.TryParseFloat(_sInflationRoundness, out v)) BellyMorphController.InflationRoundness = v;
            if (PregnancyManager.TryParseFloat(_sInflationDrop, out v)) BellyMorphController.InflationDrop = v;
            if (PregnancyManager.TryParseFloat(_sInflationFatFold, out v)) BellyMorphController.InflationFatFold = v;
            if (PregnancyManager.TryParseFloat(_sInflationFatFoldHeight, out v)) BellyMorphController.InflationFatFoldHeight = v;
            if (PregnancyManager.TryParseFloat(_sInflationFatFoldGap, out v)) BellyMorphController.InflationFatFoldGap = v;
            if (PregnancyManager.TryParseFloat(_sRegionRadiusSide, out v)) BellyMorphController.RegionRadiusSide = v;
            if (PregnancyManager.TryParseFloat(_sRegionRadiusFront, out v)) BellyMorphController.RegionRadiusFront = v;
            if (PregnancyManager.TryParseFloat(_sRegionRadiusBack, out v)) BellyMorphController.RegionRadiusBack = v;
            if (PregnancyManager.TryParseFloat(_sRegionRadiusUp, out v)) BellyMorphController.RegionRadiusUp = v;
            if (PregnancyManager.TryParseFloat(_sRegionRadiusDown, out v)) BellyMorphController.RegionRadiusDown = v;
            if (PregnancyManager.TryParseFloat(_sThighGuardSpeed, out v)) BellyMorphController.ThighGuardSpeed = v;
            if (PregnancyManager.TryParseFloat(_sInnerThighGuardStrength, out v)) BellyMorphController.InnerThighGuardStrength = v;
            if (PregnancyManager.TryParseFloat(_sTopEdgeTaper, out v)) BellyMorphController.TopEdgeTaper = v;
            if (PregnancyManager.TryParseFloat(_sBottomEdgeTaper, out v)) BellyMorphController.BottomEdgeTaper = v;
            if (PregnancyManager.TryParseFloat(_sSideSmoothWidth, out v)) BellyMorphController.SideSmoothWidth = v;
            if (PregnancyManager.TryParseFloat(_sSideSmoothStrength, out v)) BellyMorphController.SideSmoothStrength = v;
            if (PregnancyManager.TryParseFloat(_sBreastGuardStrength, out v)) BellyMorphController.BreastGuardStrength = v;
            if (PregnancyManager.TryParseFloat(_sOuterClothPregnancyScale, out v)) BellyMorphController.OuterClothPregnancyScale = v;
            BellyMorphController.OuterClothSkirtDrape = _outerClothSkirtDrape;
            if (PregnancyManager.TryParseFloat(_sOuterClothLayerGuard, out v)) BellyMorphController.OuterClothLayerGuard = v;
            if (PregnancyManager.TryParseFloat(_sInnerClothOffset, out v)) BellyMorphController.InnerClothOffset = v;
            if (PregnancyManager.TryParseFloat(_sOuterClothOffset, out v)) BellyMorphController.OuterClothOffset = v;
            if (PregnancyManager.TryParseFloat(_sClothThicknessPreserve, out v)) BellyMorphController.ClothThicknessPreserve = v;
            if (PregnancyManager.TryParseFloat(_sClothOffsetSideRatio, out v)) BellyMorphController.ClothOffsetSideRatio = v;
            if (PregnancyManager.TryParseFloat(_sClothBackOffsetBoost, out v)) BellyMorphController.ClothBackOffsetBoost = v;
            if (PregnancyManager.TryParseFloat(_sClothDepthStretch, out v)) BellyMorphController.ClothDepthStretch = v;
        }

        public static void TriggerApplyBelly(Maid maid, float progress)
        {
            if (maid == null) return;

            PregnancyManager.CaptureCurrentBellySettings();
            BellyMorphController.Reset(maid);
            BellyMorphController.ApplyProgress(maid, progress);
        }

        static string FormatShape(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static string MaidName(Maid m, int idx)
        {
            try
            {
                string n = (m.status.lastName + " " + m.status.firstName).Trim();
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            return "Maid #" + idx;
        }
    }
}
