using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [Serializable]
    public class PregSettings
    {
        public int toggleKeyInt = (int)KeyCode.F8;

        [NonSerialized] private KeyCode _key = KeyCode.None;
        public KeyCode toggleKey
        {
            get { if (_key == KeyCode.None) _key = (KeyCode)toggleKeyInt; return _key; }
            set { _key = value; toggleKeyInt = (int)value; }
        }
    }
}
