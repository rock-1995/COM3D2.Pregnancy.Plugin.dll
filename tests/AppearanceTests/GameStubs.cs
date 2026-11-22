using System.Globalization;
namespace BepInEx.Logging
{
    public class ManualLogSource { public void LogInfo(object message) { } }
    public static class Logger { public static ManualLogSource CreateLogSource(string name) => new(); }
}
namespace COM3D2.Pregnancy.Plugin
{
    internal class Config<T>(T value) { internal T Value = value; }
    internal static class PregnancyPlugin
    {
        public const string PluginGuid = "com.pregnancymod.com3d2";
        internal static Config<int> CfgBodyChangeBirths = new(5);
    }
    internal static class PregnancyManager
    {
        internal static bool GetPregnant(Maid maid) => ExSaveDataBridge.Get(maid, "isPregnant", "false") == "true";
        internal static float GetProgress(Maid maid) => float.Parse(ExSaveDataBridge.Get(maid, "progress", "0"), CultureInfo.InvariantCulture);
        internal static int GetBirthCount(Maid maid) => int.Parse(ExSaveDataBridge.Get(maid, "birthCount", "0"), CultureInfo.InvariantCulture);
    }
}
