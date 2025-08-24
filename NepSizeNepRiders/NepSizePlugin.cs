using BepInEx.Configuration;
using CharaIK;
using HarmonyLib;
using NepSizeCore;
using NepSizeNepRiders;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Main plugin for Neptunia riders.
/// </summary>
public class NepSizePlugin : MonoBehaviour, INepSizeGamePlugin
{
    /// <summary>
    /// Background thread.
    /// </summary>
    private SizeDataThread _sizeDataThread;

    /// <summary>
    /// Self instance.
    /// </summary>
    private static NepSizePlugin _instance;

    /// <summary>
    /// Self instance.
    /// </summary>
    public static NepSizePlugin Instance { get { return _instance; } }

    /// <summary>
    /// Reserved memory.
    /// </summary>
    private SizeMemoryStorage _sizeMemoryStorage;

    /// <summary>
    /// Size memory.
    /// </summary>
    public SizeMemoryStorage SizeMemoryStorage {  get { return _sizeMemoryStorage; } }

#pragma warning disable IDE0051
    /// <summary>
    /// Init on Unity side.
    /// </summary>
    private void Start()
    {
        if (_instance != null)
        {
            // This should not happen, the Plugin should only be loaded once.
            Debug.Log("Tried to start another object of this? Unity, water u doing?");
            return;
        }

        ConfigEntry<string> listenAddress = PluginInfo.Instance.Config.Bind<string>("Server", "ListenIp", null, "IP which the web UI will listen on. Leave blank to listen on all IPs.");
        ConfigEntry<int> listenPort = PluginInfo.Instance.Config.Bind<int>("Server", "Port", 9898, "Listen port - default is 9898");
        ConfigEntry<bool> listenSubnetOnly = PluginInfo.Instance.Config.Bind<bool>("Server", "RestrictListenSubnet", true, "Only listen in the local IPv4 subnet, disable this if you wish to allow global access (you must know what you're doing!).");

        CoreConfig.GAMENAME = "NPRD";
        CoreConfig.WEBUI_TITLE = "Neptunia Riders";
        CoreConfig.SERVER_IP = listenAddress.Value;
        CoreConfig.SERVER_PORT = listenPort.Value;
        CoreConfig.SERVER_LOCAL_SUBNET_ONLY = listenSubnetOnly.Value;

        _instance = this;

        // Initiliase thread and storage.
        this._sizeMemoryStorage = SizeMemoryStorage.Instance(this);
        this._sizeDataThread = new SizeDataThread(this, this._sizeMemoryStorage);

        Harmony.CreateAndPatchAll(typeof(ScaleDbModelChara));
    }
#pragma warning restore IDE0051

    /// <summary>
    /// Pass character size updates into memory.
    /// </summary>
    /// <param name="inputCharacterScales">Dictionary of uint to scale</param>
    /// <param name="overwrite">Shoudl all data be overwritten</param>
    public void UpdateSizes(Dictionary<uint, float> inputCharacterScales, bool overwrite = false)
    {
        this._sizeMemoryStorage.UpdateSizes(inputCharacterScales, overwrite);
    }

    /// <summary>
    /// Determine the list of active characters.
    /// </summary>
    /// <returns>List of character IDs.</returns>
    public List<uint> GetActiveCharacterIds()
    {
        return this._sizeMemoryStorage.ActiveCharacters;
    }

    /// <summary>
    /// Determine the current character scales.
    /// </summary>
    /// <returns>Dictiony of character ID to scale.</returns>
    public Dictionary<uint, float> GetCharacterSizes()
    {
        return this._sizeMemoryStorage.SizeValues;
    }

    /// <summary>
    /// Clean up the thread.
    /// </summary>
    protected void OnDestroy()
    {
        _sizeDataThread.CloseThread();
    }

#pragma warning disable IDE0051
    /// <summary>
    /// Fixed update.
    /// </summary>
    private void FixedUpdate()
    {
        // Fire actions of the pipe thread on the Unity main queue.
        this._sizeDataThread.HandleConnectionQueue();
    }
#pragma warning restore IDE0051

    /// <summary>
    /// Active characters - cleared in every Update() as written by ScaleDbModelChara.
    /// </summary>
    private List<uint> _activeCharacterCache = new List<uint>();

    /// <summary>
    /// Writes an entry to the active character list.
    /// </summary>
    /// <param name="id"></param>
    public void MarkCharacterIdActive(uint id)
    {
        if (!this._activeCharacterCache.Contains(id))
        {
            this._activeCharacterCache.Add(id);
        }
    }

#pragma warning disable IDE0051
    /// <summary>
    /// Update: submit active characters to Size Memory Storage.
    /// </summary>
    private void Update()
    {
        // Store the character ID into memory.
        this._sizeMemoryStorage.UpdateCharacterList(_activeCharacterCache);

        // Early update in the hooks can update again!
        _activeCharacterCache.Clear();
    }
#pragma warning restore IDE0051


    /// <summary>
    /// Debug output.
    /// </summary>
    /// <param name="message">Debug message</param>
    public void DebugLog(string message)
    {
        Debug.Log(message);
    }

    /// <summary>
    /// Character list for the Web UI.
    /// </summary>
    /// <returns></returns>
    public CharacterList GetCharacterList()
    {
        return new CharacterList()
        {
            { "Uzume", new List<CharacterData>()
                {
                    new CharacterData(id: 501, text: "Rider", name: "Uzume Rider"),
                    new CharacterData(id: 502, text: "Swimsuit", name: "Uzume Rider"),
                    new CharacterData(id: 503, text: "Apocalyptic Costume", name: "Uzume Apocalyptic Costume"),
                    new CharacterData(id: 504, text: "Race Queen", name: "Uzume Race Queen"),
                }
            },
            { "Neptune", new List<CharacterData>()
                {
                    new CharacterData(id: 101, text: "Parka Rider", name: "Neptune Rider"),
                    new CharacterData(id: 102, text: "Swimsuit", name: "Neptune Swimsuit"),
                    new CharacterData(id: 103, text: "Apocalyptic Costume", name: "Neptune Apocalyptic Costume"),
                    new CharacterData(id: 104, text: "Race Queen", name: "Neptune Race Queen"),
                }
            },
            { "Noire", new List<CharacterData>()
                {
                    new CharacterData(id: 201, text: "Clear Rider", name: "Noire Rider"),
                    new CharacterData(id: 202, text: "Swimsuit", name: "Noire Swimsuit"),
                    new CharacterData(id: 203, text: "Apocalyptic Costume", name: "Noire Apocalyptic Costume"),
                    new CharacterData(id: 204, text: "Race Queen", name: "Noire Race Queen"),
                }
            },            
            { "Blanc", new List<CharacterData>()
                {
                    new CharacterData(id: 301, text: "White Rider", name: "Blanc Rider"),
                    new CharacterData(id: 302, text: "Swimsuit", name: "Blanc Swimsuit"),
                    new CharacterData(id: 303, text: "Apocalyptic Costume", name: "Blanc Apocalyptic Costume"),
                    new CharacterData(id: 304, text: "Race Queen", name: "Blanc Race Queen"),
                }
            },
            { "Vert", new List<CharacterData>()
                {
                    new CharacterData(id: 401, text: "Princess Rider", name: "Vert Rider"),
                    new CharacterData(id: 402, text: "Swimsuit", name: "Vert Swimsuit"),
                    new CharacterData(id: 403, text: "Apocalyptic Costume", name: "Vert Apocalyptic Costume"),
                    new CharacterData(id: 404, text: "Race Queen", name: "Vert Race Queen"),
                }
            },
            { "Adult Neptune", new List<CharacterData>()
                {
                    new CharacterData(id: 1500, text: "Sailor Dress", name: "Adult Neptune")                    
                }
            },
        };
    }
}