using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public class PregnancyUI : MonoBehaviour
    {
        private bool _visible = false;
        private Rect _win     = new Rect(120, 120, 420, 720);
        private int  _winId;

        private readonly List<Maid>   _maids = new List<Maid>();
        private readonly List<string> _names = new List<string>();
        private int   _sel     = -1;
        private Maid  _curMaid = null;
        private bool  _curPreg = false;
        private float _curProg = 0f;

        private bool _dropOpen = false;
        private Rect _dropRect;

        private string _sSpineLerp   = "1";
        private string _sFwdOffset   = "-0.055";
        private string _sSideOffset  = "0";
        private string _sRadiusH     = "0.22";
        private string _sRadiusV     = "0.21";
        private string _sPushOut     = "1.5";
        private string _sDropDown    = "0.2";
        private string _sTaperZ      = "-1.5";
        private string _sTaperTop    = "0";
        private string _sStretchX    = "-0.8";
        private string _sStretchY    = "0";
        private string _sStretchZ    = "0";
        private string _sFadeStart   = "10";
        private string _sFadeCutoff  = "0";
        private string _sClothOD     = "1.03";
        private string _sOuterOD     = "1.2";
        private string _sOuterThresh = "0.3";

        void Awake() { _winId = GetHashCode(); }

        void Update()
        {
            if (Input.GetKeyDown(PregnancyPlugin.CfgToggleKey.Value))
            {
                _visible = !_visible;
                if (_visible) ScanMaids();
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
            float x = 8f, y = 22f, w = _win.width - 16f;
            float lw = 165f;
            float fx = x + lw + 2f;
            float fw = w - lw - 4f;

            // ── Maid selection ────────────────────────────────────────────
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
            { ScanMaids(); _dropOpen = false; }
            y += 30f;

            if (_curMaid == null)
            {
                GUI.Label(new Rect(x, y, w, 18f), "No maid selected.");
                GUI.DragWindow(new Rect(0, 0, _win.width, 20f));
                return;
            }

            // ── Pregnant ────────────────────────────────────────────
            bool newPreg = GUI.Toggle(new Rect(x, y, 160f, 22f), _curPreg, " Pregnant");
            if (newPreg != _curPreg)
            {
                _curPreg = newPreg;
                PregnancyManager.SetPregnant(_curMaid, _curPreg);
            }
            y += 28f;

            // ── Progress ────────────────────────────────────────────
            int totalDays = PregnancyPlugin.CfgPregnancyWeeks.Value * 7;
            int curDay    = Mathf.RoundToInt(_curProg * totalDays);
            GUI.Label(new Rect(x, y, w, 18f),
                string.Format("Progress: {0:F3}  (day {1}/{2})", _curProg, curDay, totalDays));
            y += 20f;

            float newProg = GUI.HorizontalSlider(new Rect(x, y, w, 18f), _curProg, 0f, 1f);
            if (Mathf.Abs(newProg - _curProg) > 0.0005f)
            {
                _curProg = (float)System.Math.Round(newProg, 3);
                PregnancyManager.SetProgress(_curMaid, _curProg);
            }
            y += 26f;

            // ── Apply / Reset ────────────────────────────────────────
            if (GUI.Button(new Rect(x, y, 110f, 24f), "Apply Belly"))
            {
                float v;
                if (float.TryParse(_sSpineLerp,  out v)) BellyMorphController.SpineLerpT     = v;
                if (float.TryParse(_sFwdOffset,  out v)) BellyMorphController.OffsetForward  = v;
                if (float.TryParse(_sSideOffset, out v)) BellyMorphController.OffsetSide     = v;
                if (float.TryParse(_sRadiusH,    out v)) BellyMorphController.RadiusH        = v;
                if (float.TryParse(_sRadiusV,    out v)) BellyMorphController.RadiusV        = v;
                if (float.TryParse(_sPushOut,    out v)) BellyMorphController.MaxPushOut     = v;
                if (float.TryParse(_sDropDown,   out v)) BellyMorphController.MaxDropDown    = v;
                if (float.TryParse(_sTaperZ,     out v)) BellyMorphController.TaperZ         = v;
                if (float.TryParse(_sTaperTop,   out v)) BellyMorphController.TaperTop       = v;
                if (float.TryParse(_sStretchX,   out v)) BellyMorphController.StretchX       = v;
                if (float.TryParse(_sStretchY,   out v)) BellyMorphController.StretchY       = v;
                if (float.TryParse(_sStretchZ,   out v)) BellyMorphController.StretchZ       = v;
                if (float.TryParse(_sFadeStart,  out v)) BellyMorphController.SideFadeStart       = v;
                if (float.TryParse(_sFadeCutoff, out v)) BellyMorphController.SideFadeCutoff      = v;
                if (float.TryParse(_sClothOD,    out v)) BellyMorphController.ClothOverdrive      = v;
                if (float.TryParse(_sOuterOD,    out v)) BellyMorphController.OuterClothOverdrive = v;
                if (float.TryParse(_sOuterThresh,out v)) BellyMorphController.OuterClothThreshold = v;

                BellyMorphController.Reset(_curMaid);
                BellyMorphController.ApplyProgress(_curMaid, _curProg);
            }
            if (GUI.Button(new Rect(x + 118f, y, 80f, 24f), "Reset Belly"))
                BellyMorphController.Reset(_curMaid);
            y += 32f;

            // ── Parameter inputs ─────────────────────────────────────────────
            GUI.Label(new Rect(x, y, w, 18f), "─── Belly Shape Params ──────────────────");
            y += 22f;

            GUI.Label(new Rect(x, y, lw, 18f), "Height (0=pelvis 2=up)");
            _sSpineLerp  = GUI.TextField(new Rect(fx, y, fw, 20f), _sSpineLerp);  y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Forward Offset");
            _sFwdOffset  = GUI.TextField(new Rect(fx, y, fw, 20f), _sFwdOffset);  y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Side Offset");
            _sSideOffset = GUI.TextField(new Rect(fx, y, fw, 20f), _sSideOffset); y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Radius H");
            _sRadiusH    = GUI.TextField(new Rect(fx, y, fw, 20f), _sRadiusH);    y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Radius V");
            _sRadiusV    = GUI.TextField(new Rect(fx, y, fw, 20f), _sRadiusV);    y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Push Out");
            _sPushOut    = GUI.TextField(new Rect(fx, y, fw, 20f), _sPushOut);    y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Drop Down");
            _sDropDown   = GUI.TextField(new Rect(fx, y, fw, 20f), _sDropDown);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Taper Z (bottom)");
            _sTaperZ     = GUI.TextField(new Rect(fx, y, fw, 20f), _sTaperZ);     y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Taper Top (top edge)");
            _sTaperTop   = GUI.TextField(new Rect(fx, y, fw, 20f), _sTaperTop);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Stretch X (left/right)");
            _sStretchX   = GUI.TextField(new Rect(fx, y, fw, 20f), _sStretchX);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Stretch Y (up/down)");
            _sStretchY   = GUI.TextField(new Rect(fx, y, fw, 20f), _sStretchY);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Stretch Z (fwd/back)");
            _sStretchZ   = GUI.TextField(new Rect(fx, y, fw, 20f), _sStretchZ);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Side Fade Start");
            _sFadeStart  = GUI.TextField(new Rect(fx, y, fw, 20f), _sFadeStart);  y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Side Fade Cutoff");
            _sFadeCutoff = GUI.TextField(new Rect(fx, y, fw, 20f), _sFadeCutoff); y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Inner Cloth OD");
            _sClothOD    = GUI.TextField(new Rect(fx, y, fw, 20f), _sClothOD);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Outer Cloth OD");
            _sOuterOD    = GUI.TextField(new Rect(fx, y, fw, 20f), _sOuterOD);   y += 24f;

            GUI.Label(new Rect(x, y, lw, 18f), "Outer Threshold");
            _sOuterThresh= GUI.TextField(new Rect(fx, y, fw, 20f), _sOuterThresh);y += 28f;

            if (GUI.Button(new Rect(x, y, 120f, 22f), "Log to BepInEx"))
            {
                var log = BepInEx.Logging.Logger.CreateLogSource("Pregnancy");
                log.LogInfo("[Pregnancy] ===== Shape Params =====");
                log.LogInfo("  SpineLerpT     = " + BellyMorphController.SpineLerpT);
                log.LogInfo("  OffsetForward  = " + BellyMorphController.OffsetForward);
                log.LogInfo("  OffsetSide     = " + BellyMorphController.OffsetSide);
                log.LogInfo("  RadiusH        = " + BellyMorphController.RadiusH);
                log.LogInfo("  RadiusV        = " + BellyMorphController.RadiusV);
                log.LogInfo("  MaxPushOut     = " + BellyMorphController.MaxPushOut);
                log.LogInfo("  MaxDropDown    = " + BellyMorphController.MaxDropDown);
                log.LogInfo("  TaperZ         = " + BellyMorphController.TaperZ);
                log.LogInfo("  TaperTop       = " + BellyMorphController.TaperTop);
                log.LogInfo("  StretchX       = " + BellyMorphController.StretchX);
                log.LogInfo("  StretchY       = " + BellyMorphController.StretchY);
                log.LogInfo("  StretchZ       = " + BellyMorphController.StretchZ);
                log.LogInfo("  SideFadeStart       = " + BellyMorphController.SideFadeStart);
                log.LogInfo("  SideFadeCutoff      = " + BellyMorphController.SideFadeCutoff);
                log.LogInfo("  ClothOverdrive      = " + BellyMorphController.ClothOverdrive);
                log.LogInfo("  OuterClothOverdrive = " + BellyMorphController.OuterClothOverdrive);
                log.LogInfo("  OuterClothThreshold = " + BellyMorphController.OuterClothThreshold);
                log.LogInfo("=========================");
            }

            GUI.DragWindow(new Rect(0, 0, _win.width, 20f));
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
                    _sel      = i;
                    _curMaid  = _maids[i];
                    _curPreg  = PregnancyManager.GetPregnant(_curMaid);
                    _curProg  = PregnancyManager.GetProgress(_curMaid);
                    _dropOpen = false;
                }
            }
        }

        void ScanMaids()
        {
            _maids.Clear(); _names.Clear();
            _sel = -1; _curMaid = null;

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
                _sel     = 0;
                _curMaid = _maids[0];
                _curPreg = PregnancyManager.GetPregnant(_curMaid);
                _curProg = PregnancyManager.GetProgress(_curMaid);
            }
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
