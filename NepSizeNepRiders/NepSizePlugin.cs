using BepInEx.Configuration;
using NepSizeCore;
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
        CoreConfig.SERVER_IP = listenAddress.Value;
        CoreConfig.SERVER_PORT = listenPort.Value;
        CoreConfig.SERVER_LOCAL_SUBNET_ONLY = listenSubnetOnly.Value;

        _instance = this;

        // Initiliase thread and storage.
        this._sizeMemoryStorage = SizeMemoryStorage.Instance(this);
        this._sizeDataThread = new SizeDataThread(this, this._sizeMemoryStorage);
    }

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

    /// <summary>
    /// Fixed update.
    /// </summary>
    private void FixedUpdate()
    {
        // Fire actions of the pipe thread on the Unity main queue.
        this._sizeDataThread.HandleConnectionQueue();
    }

    /// <summary>
    /// Check if a parent object of the GameObject go has a component of type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="child"></param>
    /// <returns></returns>
    public T GetComponentInParentChain<T>(GameObject go) where T : Component
    {
        Transform current = go.transform.parent;

        while (current != null)
        {
            T component = current.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// Update: read active characters and set their scales.
    /// </summary>
    private void Update()
    {
        // Read scales from memory
        Dictionary<uint, float> scales = this._sizeMemoryStorage.SizeValues;

        // Determine active characters
        List<uint> activeCharacters = new List<uint>();
        DbModelChara[] o = GameObject.FindObjectsOfType<DbModelChara>().ToArray(); //Search for characters

        foreach (DbModelChara c in o) //Inspect
        {
            if (c.model_id_ != null) //Character has a valid id
            {
                uint mdlId = c.model_id_.model_;
                if (!activeCharacters.Contains(mdlId))
                {
                    activeCharacters.Add(mdlId);
                }
                if (scales.ContainsKey(mdlId))
                {
                    DbModelBase.DbModelBaseObjectManager om = c.transform.GetComponentInChildren<DbModelBase.DbModelBaseObjectManager>(); //Load her object manager
                    float s = scales[mdlId];
                    if (om != null && om.transform.localPosition.x != s)
                    {
                        DbModelVehicle v = GetComponentInParentChain<DbModelVehicle>(c.gameObject);
                        if (v != null)
                        {
                            // On vechicle
                            om.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                            v.gameObject.transform.localScale = new Vector3(s, s, s); 
                        } else
                        {
                            // Not on vehicle
                            om.transform.localScale = new Vector3(s, s, s);
                        }                        
                    }
                }
            }
        }

        // Store the character ID into memory.
        this._sizeMemoryStorage.UpdateCharacterList(activeCharacters);
    }

    /// <summary>
    /// Debug output.
    /// </summary>
    /// <param name="message">Debug message</param>
    public void DebugLog(string message)
    {
        Debug.Log(message);
    }

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
        };
    }
}