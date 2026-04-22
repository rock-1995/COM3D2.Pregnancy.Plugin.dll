using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    public class PregnancyUI : MonoBehaviour
    {
        private bool _visible = false;
        private Rect _win     = new Rect(120, 120, 380, 230);
        private int  _winId;

        private readonly List<Maid>   _maids = new List<Maid>();
        private readonly List<string> _names = new List<string>();
        private int   _sel  = -1;
        private Maid  _curMaid     = null;
        private bool  _curPregnant = false;
        private float _curProgress = 0f;

        private bool _dropOpen = false;
        private Rect _dropRect;

        void Awake() => _winId = GetHashCode();

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

            // ── メイド選択 ────────────────────────────────────────
            GUI.Label(new Rect(x, y, 40f, 18f), "Maid:");
            string btnLabel = (_sel >= 0 && _sel < _names.Count)
                ? _names[_sel] : "(no maid in scene)";
            if (GUI.Button(new Rect(x + 44f, y, w - 76f, 24f), btnLabel + "  v"))
            {
                _dropOpen = !_dropOpen;
                if (_dropOpen)
                    _dropRect = new Rect(_win.x + x + 44f, _win.y + y + 26f,
                                        w - 76f, Mathf.Min(_names.Count * 26f + 6f, 180f));
            }
            if (GUI.Button(new Rect(x + w - 28f, y, 26f, 24f), "R"))
            { ScanMaids(); _dropOpen = false; }
            y += 30f;

            // ── 選択メイドなし ────────────────────────────────────
            if (object.ReferenceEquals(_curMaid, null))
            {
                GUI.Label(new Rect(x, y, w, 18), "No maid selected.");
                GUI.DragWindow(new Rect(0, 0, _win.width, 20f));
                return;
            }

            // ── Pregnant チェック ─────────────────────────────────
            bool newPreg = GUI.Toggle(new Rect(x, y, 160f, 22f), _curPregnant, " Pregnant");
            if (newPreg != _curPregnant)
            {
                _curPregnant = newPreg;
                PregnancyManager.SetPregnant(_curMaid, _curPregnant);
            }
            y += 28f;

            // ── Progress スライダー ──────────────────────────────
            int totalDays  = PregnancyPlugin.CfgPregnancyWeeks.Value * 7;
            int currentDay = Mathf.RoundToInt(_curProgress * totalDays);
            GUI.Label(new Rect(x, y, w, 18),
                string.Format("Progress: {0:F3}  (day {1} / {2})",
                    _curProgress, currentDay, totalDays));
            y += 20f;

            float newProg = GUI.HorizontalSlider(new Rect(x, y, w, 18), _curProgress, 0f, 1f);
            if (Mathf.Abs(newProg - _curProgress) > 0.0005f)
            {
                _curProgress = (float)System.Math.Round(newProg, 3);
                PregnancyManager.SetProgress(_curMaid, _curProgress);
            }
            y += 26f;

            // ── ボタン ────────────────────────────────────────────
            if (GUI.Button(new Rect(x, y, 110f, 24f), "Apply Belly"))
                BellyMorphController.ApplyProgress(_curMaid, _curProgress);
            if (GUI.Button(new Rect(x + 118f, y, 80f, 24f), "Reset Belly"))
                BellyMorphController.Reset(_curMaid);

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
                Rect r = new Rect(_dropRect.x + 4, iy, _dropRect.width - 8, h - 2);
                if (GUI.Button(r, _names[i]))
                {
                    if (i != _sel) BellyMorphController.Reset(_curMaid);
                    _sel = i; _curMaid = _maids[i];
                    _curPregnant = PregnancyManager.GetPregnant(_curMaid);
                    _curProgress = PregnancyManager.GetProgress(_curMaid);
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
                _sel = 0; _curMaid = _maids[0];
                _curPregnant = PregnancyManager.GetPregnant(_curMaid);
                _curProgress = PregnancyManager.GetProgress(_curMaid);
            }
        }

        static string MaidName(Maid m, int idx)
        {
            try { string n = (m.status.lastName + " " + m.status.firstName).Trim();
                  if (!string.IsNullOrEmpty(n)) return n; }
            catch { }
            return "Maid #" + idx;
        }
    }
}
