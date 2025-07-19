using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

public static class PluginInfo
{
    public const string PLUGIN_GUID = "NepSize";
    public const string PLUGIN_NAME = "NepSize";
    public const string PLUGIN_VERSION = "0.0.0.1";

    public static Plugin Instance;
    public static string AssetsFolder = Paths.PluginPath + "\\" + PluginInfo.PLUGIN_GUID + "\\Assets";
}

[BepInPlugin("net.gamindustri.plugins.nepsize.svsmono", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        PluginInfo.Instance = this;

        this.gameObject.AddComponent<NepSizePlugin>();

        Harmony.CreateAndPatchAll(typeof(DontPause));
    }
}
