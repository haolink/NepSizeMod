using BepInEx;
using BepInEx.Logging;

/// <summary>
/// Basic plugin info.
/// </summary>
public static class PluginInfo
{
    public const string PLUGIN_GUID = "NepSize";
    public const string PLUGIN_NAME = "NepSize";
    public const string PLUGIN_VERSION = "0.0.0.2";

    public static Plugin Instance;
    public static string AssetsFolder = Paths.PluginPath + "\\" + PluginInfo.PLUGIN_GUID + "\\Assets";
}

/// <summary>
/// Initialiser.
/// </summary>
[BepInPlugin("net.gamindustri.plugins.nepsize.svsmono", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    /// <summary>
    /// Logger.
    /// </summary>
    internal static new ManualLogSource Logger;

#pragma warning disable IDE0051
    /// <summary>
    /// Plugin loader.
    /// </summary>
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        PluginInfo.Instance = this;

        this.gameObject.AddComponent<NepSizePlugin>();
    }
#pragma warning restore IDE0051
}
