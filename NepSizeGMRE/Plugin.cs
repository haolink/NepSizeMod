using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

/// <summary>
/// Basic plugin info.
/// </summary>
public static class PluginInfo
{
    public const string PLUGIN_GUID = "NepSizeGMRE";
    public const string PLUGIN_NAME = "NepSize";
    public const string PLUGIN_VERSION = "0.0.0.2";

    public static Plugin Instance;
    public static string AssetsFolder = Paths.PluginPath + "\\" + PluginInfo.PLUGIN_GUID + "\\Assets";
}

/// <summary>
/// Initialiser.
/// </summary>
[BepInPlugin("net.gamindustri.plugins.nepsize.gmre", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    /// <summary>
    /// Logger.
    /// </summary>
    internal static new ManualLogSource Log;

    /// <summary>
    /// Plugin loader.
    /// </summary>
    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        PluginInfo.Instance = this;

        IL2CPPChainloader.AddUnityComponent(typeof(NepSizePlugin));
    }
}
