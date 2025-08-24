using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NepSizeCore;
using NepSizeYuushaNeptune;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PluginInfo
{
    public const string PLUGIN_GUID = "NepSizeSnepRPG";
    public const string PLUGIN_NAME = "NepSize";
    public const string PLUGIN_VERSION = "0.0.0.1";

    public static Plugin Instance;
    public static string AssetsFolder = Paths.PluginPath + "\\" + PluginInfo.PLUGIN_GUID + "\\Assets";
}

[BepInPlugin("net.gamindustri.plugins.nepsize.sneprpg", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin, INepSizeGamePlugin
{
    internal static new ManualLogSource Logger;

    /// <summary>
    /// Background thread.
    /// </summary>
    private SizeDataThread _sizeDataThread;

    /// <summary>
    /// Reserved memory.
    /// </summary>
    private SizeMemoryStorage _sizeMemoryStorage;
    
    /// <summary>
    /// Time stamp of last WebUI data update.
    /// </summary>
    private DateTime _lastUp = DateTime.Now;

    /// <summary>
    /// Last scan for active characters.
    /// </summary>
    private DateTime _lastObjectScan = DateTime.MinValue;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        PluginInfo.Instance = this;

        ConfigEntry<string> listenAddress = Config.Bind<string>("Server", "ListenIp", null, "IP which the web UI will listen on. Leave blank to listen on all IPs.");
        ConfigEntry<int> listenPort = Config.Bind<int>("Server", "Port", 7878, "Listen port - default is 8989");
        ConfigEntry<bool> listenSubnetOnly = Config.Bind<bool>("Server", "RestrictListenSubnet", true, "Only listen in the local IPv4 subnet, disable this if you wish to allow global access (you must know what you're doing!).");

        CoreConfig.SERVER_IP = listenAddress.Value;
        CoreConfig.SERVER_PORT = listenPort.Value;
        CoreConfig.SERVER_LOCAL_SUBNET_ONLY = listenSubnetOnly.Value;

        CoreConfig.GAMENAME = "SRPG";
        CoreConfig.WEBUI_TITLE = "Super Nep RPG";

        Harmony.CreateAndPatchAll(typeof(DontPause));

        // Initiliase thread and storage.
        this._sizeMemoryStorage = SizeMemoryStorage.Instance(this);
        this._sizeDataThread = new SizeDataThread(this, this._sizeMemoryStorage);        
    }

    void INepSizeGamePlugin.DebugLog(string message)
    {
        Debug.Log("Callback: " + message);
    }

    List<uint> INepSizeGamePlugin.GetActiveCharacterIds()
    {
        return this._sizeMemoryStorage.ActiveCharacters;
    }

    /// <summary>
    /// Implementation of INepSizeGamePlugin.GetCharacterList.
    /// Makes use of the compatibility layer mapping.
    /// </summary>
    /// <returns></returns>
    CharacterList INepSizeGamePlugin.GetCharacterList()
    {
        return new CharacterList()
        {
            { "Main", new List<CharacterData>()
                {
                    new CharacterData(id: CompatibilityLayer.CHAR_NEPTUNE, text: "Neptune", name: "Neptune"),
                    new CharacterData(id: CompatibilityLayer.CHAR_NOIRE, text: "Noire", name: "Noire"),
                    new CharacterData(id: CompatibilityLayer.CHAR_BLANC, text: "Blanc", name: "Blanc"),
                    new CharacterData(id: CompatibilityLayer.CHAR_VERT, text: "Vert", name: "Vert")
                }
            },
            { "Support", new List<CharacterData>()
                {
                    new CharacterData(id: CompatibilityLayer.CHAR_IF, text: "IF", name: "IF"),
                    new CharacterData(id: CompatibilityLayer.CHAR_COMPA, text: "Compa", name: "Compa"),
                    new CharacterData(id: CompatibilityLayer.CHAR_ARTISAN, text: "Artisan", name: "Artisan"),
                    new CharacterData(id: CompatibilityLayer.CHAR_CHROME, text: "Chrome", name: "Chrome"), 
                }
            }
        };
    }

    /// <summary>
    /// Implementation of INepSizeGamePlugin.GetCharacterSizes.
    /// </summary>
    /// <returns></returns>
    Dictionary<uint, float> INepSizeGamePlugin.GetCharacterSizes()
    {        
        return this._sizeMemoryStorage.SizeValues;
    }

    /// <summary>
    /// Game can drastically lower the Fixed Update rate if it pauses.
    /// </summary>
    private void FixedUpdate()
    {
        Time.fixedDeltaTime = 1.0f / 240.0f;
        this._sizeDataThread.HandleConnectionQueue();
        // Update this so that Update() wouldn't need to step in.
        _lastUp = DateTime.Now;
    }

    /// <summary>
    /// Mesh renderer cache.
    /// </summary>
    private List<MeshRenderer> _meshRendererCache = new List<MeshRenderer>();

    /// <summary>
    /// Skeleton graphic cache.
    /// </summary>
    private List<SkeletonGraphic> _skeletonGraphicCache = new List<SkeletonGraphic>();

    /// <summary>
    /// Determines if a mesh render is eligible for scaling and scales it.
    /// While the blueprint of this method is very similar to TryScaleSkeletonGraphics unfortunately MeshRenderer and
    /// SkeletonGraphic don't share common parent objects which could be used for one method signature.
    /// </summary>
    /// <param name="mr">Renderer</param>
    /// <param name="scales">Cached scales</param>
    /// <returns>Character ID, should one be detected.</returns>
    private uint? TryScaleMeshRenderer(MeshRenderer mr, Dictionary<uint, float> scales)
    {
        if (mr.enabled)
        {
            string matname = mr?.material?.mainTexture?.name.ToLowerInvariant();
            uint? cid = CompatibilityLayer.UidToTex2DNames(matname);

            if (cid == null)
            {
                return null;
            }

            if (scales.TryGetValue(cid.Value, out float scale))
            {
                mr.gameObject.transform.localScale = new Vector3(scale, scale, scale);

                if (mr.transform?.parent?.parent?.Find("FeedbackBtns") is Transform btn)
                {
                    // These values are trial and error. They describe the input button positioning above the character...
                    // there doesn't seem to be a real structure how the game determines these. This is "working good enough".
                    btn.localPosition = new Vector3(0, scale * 12.23f + 5.0f, 0); 
                }
            }

            return cid; //Always return the ID even if there was nothing to do.
        }

        return null;
    }

    /// <summary>
    /// Determines if a skeleton graphic (used in battles) is eligible for scaling and scales it.
    /// While the blueprint of this method is very similar to TryScaleMeshRenderer unfortunately MeshRenderer and
    /// SkeletonGraphic don't share common parent objects which could be used for one method signature.
    /// </summary>
    /// <param name="sg">Skeleton graphic</param>
    /// <param name="scales">Cached scales</param>
    /// <returns>Character ID, should one be detected.</returns>
    private uint? TryScaleSkeletonGraphics(SkeletonGraphic sg, Dictionary<uint, float> scales)
    {
        if (sg.enabled)
        {
            string matname = sg?.mainTexture?.name.ToLowerInvariant();
            uint? cid = CompatibilityLayer.UidToTex2DNames(matname);

            if (cid == null)
            {
                return null;
            }

            if (scales.TryGetValue(cid.Value, out float scale))
            {
                sg.gameObject.transform.localScale = new Vector3(scale, scale, scale);
            }

            return cid;
        }

        return null;
    }

    private void Update()
    {
        // In other games this is only done in FixedUpdate - again this game might
        // so heavily slow down fixedDeltaTime for no visible reason this is a
        // fallback to make sure inputs are read every 50 ms.
        // This only steps in should FixedUpdate come to a stop.
        TimeSpan tsSinceUp = DateTime.Now - _lastUp;
        if (tsSinceUp.TotalMilliseconds > 50)
        {
            this._sizeDataThread.HandleConnectionQueue();
            _lastUp = DateTime.Now;
        }

        // Cache the scales first.
        Dictionary<uint, float> scales = this._sizeMemoryStorage.SizeValues;

        TimeSpan tsSinceLastScan = DateTime.Now - _lastObjectScan;
        if (tsSinceLastScan.TotalMilliseconds < 50) // We only scan every 50 ms.
        {
            foreach (MeshRenderer mr in _meshRendererCache)
            {
                TryScaleMeshRenderer(mr, scales);
            }
            foreach (SkeletonGraphic sg in _skeletonGraphicCache)
            {
                TryScaleSkeletonGraphics(sg, scales);
            }

            return;
        }

        _lastObjectScan = DateTime.Now;
        _meshRendererCache.Clear();
        _skeletonGraphicCache.Clear();
        
        List<uint> activeCharacters = new List<uint>();

        // These methods are "heavy" hence they only execute every 50 ms
        // (the game has an unlocked framerate and thus could easily run
        // these methods like 100s of times per second otherwise).
        MeshRenderer[] mrs = FindObjectsOfType<MeshRenderer>();
        SkeletonGraphic[] grs = FindObjectsOfType<SkeletonGraphic>();

        // Capsule this in a closure, needed twice.
        Action<uint?> tryAddCharacterId = (uint? characterId) =>
        {
            if (characterId != null && !activeCharacters.Contains(characterId.Value))
            {
                activeCharacters.Add(characterId.Value);
            }
        };

        foreach (MeshRenderer mr in mrs)
        {
            uint? characterId = TryScaleMeshRenderer(mr, scales);
            tryAddCharacterId(characterId);
        }
       
        foreach (SkeletonGraphic sg in grs)
        {
            uint? characterId = TryScaleSkeletonGraphics(sg, scales);
            tryAddCharacterId(characterId);
        }

        // Store the character ID into memory.
        this._sizeMemoryStorage.UpdateCharacterList(activeCharacters);
    }

    /// <summary>
    /// Clean up the thread.
    /// </summary>
    protected void OnDestroy()
    {
        _sizeDataThread.CloseThread();
    }

    /// <summary>
    /// Implementation of INepSizeGamePlugin.UpdateSizes.
    /// </summary>
    /// <param name="sizes"></param>
    /// <param name="overwrite"></param>
    void INepSizeGamePlugin.UpdateSizes(Dictionary<uint, float> sizes, bool overwrite)
    {
        this._sizeMemoryStorage.UpdateSizes(sizes, overwrite);
    }
}
