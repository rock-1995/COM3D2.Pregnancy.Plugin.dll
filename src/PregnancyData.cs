using System;
using UnityEngine;

namespace COM3D2.Pregnancy.Plugin
{
    [Serializable]
    public class PregSettings
    {
        public int growthModelVersion = 1;
        public Growth.VtxSettings growth = new Growth.VtxSettings();

    }
}
