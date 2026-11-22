public enum MPN { None }
public class MaidProp { public int type, value, idx; public string name; }
public class MaidParts
{
    public enum PARTS_COLOR { NIPPLE }
    public struct PartsColor
    {
        public int m_nMainHue, m_nMainChroma, m_nMainBrightness, m_nMainContrast;
        public int m_nShadowRate, m_nShadowHue, m_nShadowChroma, m_nShadowBrightness, m_nShadowContrast;
    }
}
