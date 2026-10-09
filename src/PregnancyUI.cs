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

        private Growth.VtxSettings _shape;
        private readonly Dictionary<string,string> _shapeText = new Dictionary<string,string>();
        private readonly Dictionary<string,float> _shapeLastValue = new Dictionary<string,float>();
        private float _shapeContentHeight = 2250f;

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
            float contentH = _shapeContentHeight;

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
                string.Format("Growth stage: {0:F3}  (day {1}/{2})", _curProg, curDay, totalDays));
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

            var p = _shape;
            GUI.Label(new Rect(x,y,w,22),"Type any finite value; sliders are only suggested ranges."); y+=24;
            GUI.Label(new Rect(x,y,w,22),"Press Apply Belly to apply edited values."); y+=26;
            GUI.Label(new Rect(x,y,w,22),"Shape timing (pregnancy duration stays the same)");y+=24;
            p.SubtleStageProgress = ShapeSlider("Subtle belly at progress (~3 months)", p.SubtleStageProgress, .15f, .6f, x, w, ref y);
            p.VisibleStageProgress = ShapeSlider("Visible belly at progress (~4 months)", p.VisibleStageProgress, .2f, .75f, x, w, ref y);
            p.MidStageProgress = ShapeSlider("Mid belly at progress (~5 months)", p.MidStageProgress, .3f, .9f, x, w, ref y);
            if(!Growth.GrowthTimeline.Valid(p))
            {GUI.Label(new Rect(x,y,w,44),"Need 0 < subtle < visible < mid < 1.\nInvalid timing: using default stage positions.");y+=46;}
            p.GrowthFullness = ShapeSlider("Forward fullness", p.GrowthFullness, 0.5f, 1.6f, x, w, ref y);
            p.GrowthWidth = ShapeSlider("Belly width", p.GrowthWidth, 0.5f, 2f, x, w, ref y);
            p.UpperReach = ShapeSlider("Upper abdomen reach (after 5 months)", p.UpperReach, 0.75f, 1.2f, x, w, ref y);
            p.VerticalRange = ShapeSlider("Vertical influence range", p.VerticalRange, .6f, 1.2f, x, w, ref y);
            p.WallSmoothing = ShapeSlider("Whole-abdomen smoothing", p.WallSmoothing, 0, 2, x, w, ref y);
            p.SagStrength = ShapeSlider("Belly sag (0 = off)", p.SagStrength, 0, 2, x, w, ref y);
            p.MidVolume = ShapeSlider("Second-stage volume (near 4 months)", p.MidVolume, .75f, 1.5f, x, w, ref y);
            p.LowerPoleLift = ShapeSlider("Late lower-pole lift (0 = cervix)", p.LowerPoleLift, 0, 2, x, w, ref y);
            p.LateForwardShift = ShapeSlider("Late whole-volume forward / torso span", p.LateForwardShift, -.1f, .2f, x, w, ref y);
            p.GrowthAxisTilt = ShapeSlider("All-stage axis tilt (0 = upright)", p.GrowthAxisTilt, 0, 1, x, w, ref y);
            p.LateHeightScale = ShapeSlider("Late volume height multiplier", p.LateHeightScale, .8f, 1.4f, x, w, ref y);
            p.LateDepthScale = ShapeSlider("Late front-back depth multiplier", p.LateDepthScale, .8f, 1.4f, x, w, ref y);
            p.LateWidthScale = ShapeSlider("Late left-right width multiplier", p.LateWidthScale, .8f, 1.4f, x, w, ref y);
            p.SkinClearance = ShapeSlider("Skin clearance", p.SkinClearance, 0, .2f, x, w, ref y);
            p.LateSettle = ShapeSlider("Late upper settling (base fixed)", p.LateSettle, 0, 1, x, w, ref y);
            p.ClothOffset = ShapeSlider("Clothing displacement", p.ClothOffset, 0.8f, 1.3f, x, w, ref y);
            p.ThighGuardSpeed = ShapeSlider("Thigh Guard Speed (0 = off)", p.ThighGuardSpeed, 0, 8, x, w, ref y);
            p.InnerThighGuardStrength = ShapeSlider("Inner Thigh Guard (0 = off)", p.InnerThighGuardStrength, 0, 4, x, w, ref y);
            p.ThighGuardSmoothStrength = ShapeSlider("Thigh Guard Smooth (0 = off)", p.ThighGuardSmoothStrength, 0, 1, x, w, ref y);
            GUI.Label(new Rect(x,y,w,22),"Guard smoothing requires Speed or Inner Guard above 0.");y+=24;
            GUI.Label(new Rect(x,y,w,22),"Binding controls affect bending poses.");y+=24;
            p.VirtualAxisStrength = ShapeSlider("Virtual axis strength (0 = native)", p.VirtualAxisStrength, 0, 1, x, w, ref y);
            p.AxisBlendStart = ShapeSlider("Blend start / torso span", p.AxisBlendStart, 0, .15f, x, w, ref y);
            p.AxisBlendFull = ShapeSlider("Full blend / torso span", p.AxisBlendFull, .05f, .6f, x, w, ref y);
            p.LowerTransitionStart = ShapeSlider("Lower start above pelvic floor / span", p.LowerTransitionStart, -.50f, .75f, x, w, ref y);
            p.LowerTransitionWidth = ShapeSlider("Lower transition width / span", p.LowerTransitionWidth, .02f, 1.50f, x, w, ref y);
            p.LowerTransitionBias = ShapeSlider("Lower curve bias (- earlier / + later)", p.LowerTransitionBias, -2, 2, x, w, ref y);
            p.UpperTransitionStart = ShapeSlider("Upper start above waist datum / span", p.UpperTransitionStart, -.50f, 1f, x, w, ref y);
            p.UpperTransitionWidth = ShapeSlider("Upper transition width / span", p.UpperTransitionWidth, .02f, 1.50f, x, w, ref y);
            p.UpperTransitionBias = ShapeSlider("Upper curve bias (- hold / + release)", p.UpperTransitionBias, -2, 2, x, w, ref y);
            p.UpperTransitionJoin = ShapeSlider("Upper join / transition width", p.UpperTransitionJoin, .02f, 1f, x, w, ref y);
            p.UpperTransitionActivation = ShapeSlider("Upper activation displacement / span", p.UpperTransitionActivation, .001f, .15f, x, w, ref y);
            p.AxisPullLow = ShapeSlider("Pull at small angles", p.AxisPullLow, 0, 1, x, w, ref y);
            p.AxisPullHigh = ShapeSlider("Pull at large angles", p.AxisPullHigh, 0, 1, x, w, ref y);
            p.AxisPullAngle = ShapeSlider("Full-pull angle (degrees)", p.AxisPullAngle, 10, 120, x, w, ref y);
            p.AxisAnchorY = ShapeSlider("Anchor height / torso span", p.AxisAnchorY, -.2f, .2f, x, w, ref y);
            p.AxisAnchorZ = ShapeSlider("Anchor forward / torso span", p.AxisAnchorZ, -.2f, .2f, x, w, ref y);
            GUI.Label(new Rect(x,y,w,22),"Navel start only applies with full navel preview OFF.");y+=24;
            p.NavelEversion = ShapeSlider("Navel eversion (0 = off)", p.NavelEversion, 0, 2, x, w, ref y);
            p.NavelStart = ShapeSlider("Navel change starts at stage", p.NavelStart, .3f, .95f, x, w, ref y);
            p.NavelHeight = ShapeSlider("Navel protrusion depth / torso span", p.NavelHeight, 0, .03f, x, w, ref y);
            p.NavelVerticalOffset = ShapeSlider("Navel vertical offset / torso span (- = down)", p.NavelVerticalOffset, -.03f, .03f, x, w, ref y);
            p.NavelRadius = ShapeSlider("Navel patch radius / torso span", p.NavelRadius, .015f, .08f, x, w, ref y);
            p.NavelProportion = ShapeSlider("Navel proportion retention", p.NavelProportion, 0, 1, x, w, ref y);
            p.BreastExclusionEnabled = GUI.Toggle(new Rect(x,y,w,24),p.BreastExclusionEnabled,"Exclude breast-weighted vertices"); y+=28;
            p.UpperBoneFilterEnabled = GUI.Toggle(new Rect(x,y,w,24),p.UpperBoneFilterEnabled,"Upper abdomen bone-weight filter"); y+=28;
            p.UpperFieldFadeEnabled = GUI.Toggle(new Rect(x,y,w,24),p.UpperFieldFadeEnabled,"Extra shape-field top fade"); y+=28;
            p.NavelPreviewFull = GUI.Toggle(new Rect(x,y,w,24),p.NavelPreviewFull,"Preview full navel response at current belly size"); y+=28;
            GUI.Label(new Rect(x,y,w,24),BellyMorphController.GetALNavelStatus(_curMaid)); y+=28;
            GUI.Label(new Rect(x,y,w,24),BellyMorphController.GetALAnchorStatus(_curMaid)); y+=28;
            GUI.Label(new Rect(x,y,w,24),string.Format("Navel stage response: {0:F1}%",Growth.BellyShape.NavelStageResponse(_curProg,p)*100)); y+=28;
            if (GUI.Button(new Rect(x,y,w/2-4,26),"Navel visible preset"))
            { p.NavelPreviewFull=true; p.NavelEversion=1; p.NavelHeight=.012f; p.NavelRadius=.06f; p.NavelProportion=.85f; }
            if (GUI.Button(new Rect(x+w/2+4,y,w/2-4,26),"Navel off")) {p.NavelEversion=0;p.NavelProportion=0;}
            y+=32;
            if (GUI.Button(new Rect(x,y,120,22),"Log to BepInEx"))
                BellyMorphController.LogShapeParameters(_shape);
            if (GUI.Button(new Rect(x + 128f, y, 150f, 22f), "Dump Skirt Verts"))
            {
                var log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
                try
                {
                    string path = BellyMorphController.ExportSkirtVertexMorphDump(_curMaid);
                    log.LogInfo("[Pregnancy] Dump Skirt Verts: " + path);
                }
                catch (System.Exception e)
                {
                    log.LogWarning("[Pregnancy] Dump Skirt Verts failed: " + e);
                }
            }
            if (GUI.Button(new Rect(x+286,y,w-286,22),"Dump All Verts"))
                DumpDiagnostic("All");
            y += 26f;

            if (GUI.Button(new Rect(x,y,180,24),"Dump Upper Verts"))
                DumpDiagnostic("Upper");
            if (GUI.Button(new Rect(x+188,y,190,24),"Dump Navel Accessory"))
                DumpDiagnostic("Navel");
            y += 30f;

            _shapeContentHeight = y + 20f;
            GUI.EndScrollView();
        }

        float ShapeSlider(string label,float value,float min,float max,float x,float width,ref float y)
        {
            GUI.Label(new Rect(x,y,width,20),label);
            string text;
            if(!_shapeText.TryGetValue(label,out text))text=value.ToString("R",CultureInfo.InvariantCulture);
            float previous;
            if(_shapeLastValue.TryGetValue(label,out previous) && previous!=value)text=value.ToString("R",CultureInfo.InvariantCulture);
            float shown=Mathf.Clamp(value,min,max);
            float slide=GUI.HorizontalSlider(new Rect(x,y+25,width-182,18),shown,min,max);
            // Merely repainting a slider must never overwrite an out-of-range number.
            if(slide!=shown){value=slide;text=value.ToString("R",CultureInfo.InvariantCulture);}
            string edited=GUI.TextField(new Rect(x+width-174,y+22,116,23),text);
            float parsed;
            bool valid=TryShapeNumber(edited,out parsed);
            if(valid)value=parsed;
            float step=(float)System.Math.Pow(10,System.Math.Floor(System.Math.Log10((max-min)/100f)));
            if(GUI.Button(new Rect(x+width-52,y+22,24,23),"-")){value-=step;edited=value.ToString("R",CultureInfo.InvariantCulture);}
            if(GUI.Button(new Rect(x+width-26,y+22,24,23),"+")){value+=step;edited=value.ToString("R",CultureInfo.InvariantCulture);}
            _shapeText[label]=edited;
            _shapeLastValue[label]=value;
            if(!valid){GUI.Label(new Rect(x,y+47,width,18),"Enter a finite number (scientific notation is supported).");y+=20;}
            y+=52;
            return value;
        }
        void DumpDiagnostic(string selection)
        {
            var log=BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
            try { log.LogInfo("[Pregnancy] Dump: "+(selection=="All"?BellyMorphController.ExportAllVertexMorphDump(_curMaid):selection=="Navel"?BellyMorphController.ExportNavelAccessoryTriangleDump(_curMaid):BellyMorphController.ExportUpperClothVertexMorphDump(_curMaid))); }
            catch(System.Exception e){log.LogWarning("[Pregnancy] Dump failed: "+e);}
        }
        static bool TryShapeNumber(string text,out float value)
            => float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);

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

        void SyncShapeFieldsFromController() { _shape = BellyMorphController.Shape.Copy(); _shapeText.Clear(); _shapeLastValue.Clear(); }

        void ApplyShapeFieldsToController() { BellyMorphController.Shape = _shape.Copy(); }

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
