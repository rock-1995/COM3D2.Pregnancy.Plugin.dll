namespace COM3D2.Pregnancy.Plugin.Growth
{
    internal static class RigBoneNames
    {
        // COM3D2 scaling bones and animation bones refer to the same rest landmark.
        internal static string Canonical(string name) => name == null ? null : name.Replace("_SCL_", "");
    }
}
