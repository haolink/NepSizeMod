using BepInEx.Configuration;
using CharaIK;
using Game.UI;
using NepSizeCore;
using NepSizeSVSMono;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Main plugin for Sisters vs Sisters.
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
    /// Additional Settings.
    /// </summary>
    private AddtlSettings _extraSettings;

    /// <summary>
    /// Getter for the settings.
    /// </summary>
    public AddtlSettings ExtraSettings { get { return _extraSettings; } }

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
        _instance = this;

        ConfigEntry<string> listenAddress = PluginInfo.Instance.Config.Bind<string>("Server", "ListenIp", null, "IP which the web UI will listen on. Leave blank to listen on all IPs.");
        ConfigEntry<int> listenPort = PluginInfo.Instance.Config.Bind<int>("Server", "Port", 8989, "Listen port - default is 8989");
        ConfigEntry<bool> listenSubnetOnly = PluginInfo.Instance.Config.Bind<bool>("Server", "RestrictListenSubnet", true, "Only listen in the local IPv4 subnet, disable this if you wish to allow global access (you must know what you're doing!).");

        CoreConfig.GAMENAME = "NSVS";
        CoreConfig.SERVER_IP = listenAddress.Value;
        CoreConfig.SERVER_PORT = listenPort.Value;
        CoreConfig.SERVER_LOCAL_SUBNET_ONLY = listenSubnetOnly.Value;

        this._extraSettings = new AddtlSettings();

        // Initiliase thread and storage.
        this._sizeMemoryStorage = SizeMemoryStorage.Instance(this);
        this._sizeDataThread = new SizeDataThread(this, this._sizeMemoryStorage, this._extraSettings);
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
    /// Foot IK offsets.
    /// </summary>
    private Dictionary<uint, (float, float)> _footIKOffsets = new Dictionary<uint, (float, float)>();

    private float ReadFootLiftupLimit(FootIK footIK)
    {
        FieldInfo fi = typeof(FootIK).GetField("footLiftupLimit_", BindingFlags.NonPublic | BindingFlags.Instance);
        return (float)(fi.GetValue(footIK));
    }

    private Dictionary<uint, float> _scaleCache;

    public float? FetchScale(uint characterId)
    {
        if (this._scaleCache == null)
        {
            return null;
        }

        if (this._scaleCache.ContainsKey(characterId))
        {
            return this._scaleCache[characterId];
        }

        return null;
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

    public float PlayerScale = 1.0f;

    public uint PlayerModelId = 0;

    /// <summary>
    /// Update: read active characters and set their scales.
    /// </summary>
    private void Update()
    {
        // Read scales from memory
        Dictionary<uint, float> scales = this._sizeMemoryStorage.SizeValues;
        this._scaleCache = scales;

        // Determine active characters
        List<uint> activeCharacters = new List<uint>();
        DbModelChara[] o = GameObject.FindObjectsOfType<DbModelChara>().ToArray(); //Search for characters

        /*MapUnitCollision muc = new MapUnitCollision();
        muc.SetMoveVector()*/

        float nPlayerScale = 1.0f;
        uint? playerModelId = null;
        bool isPlayer = false;

        GameUi ui = GameObject.FindObjectOfType<GameUi>(true);
        if (ui != null)
        {
            bool shouldBeOn = !this.ExtraSettings.DisableUI;
            if (ui.gameObject.activeSelf != shouldBeOn)
            {
                ui.gameObject.SetActive(shouldBeOn);
            }
        }

        foreach (DbModelChara c in o) //Inspect
        {
            if (c.GetModelID()  != null) //Character has a valid id
            {
                uint mdlId = c.GetModelID().GetModel();
                if (!activeCharacters.Contains(mdlId))
                {
                    activeCharacters.Add(mdlId);
                }

                isPlayer = (GetComponentInParentChain<MapUnitTypePlayerComponent>(c.gameObject) != null);
                if (isPlayer)
                {
                    playerModelId = mdlId;
                }

                if (scales.ContainsKey(mdlId))
                {
                    DbModelBase.DbModelBaseObjectManager om = c.transform.GetComponentInChildren<DbModelBase.DbModelBaseObjectManager>(); //Load her object manager
                    float s = scales[mdlId];
                    if (om != null && om.transform.localPosition.x != s)
                    {
                        om.transform.localScale = new Vector3(s, s, s);
                    }

                    if (isPlayer)
                    {
                        nPlayerScale = s;
                    }

                    FootIK footIK = c.transform.GetComponentInChildren<FootIK>();
                    if (footIK != null)
                    {
                        if (!_footIKOffsets.ContainsKey(mdlId))
                        {
                            _footIKOffsets.Add(mdlId, (footIK.putOffset_.y, ReadFootLiftupLimit(footIK)));
                        }

                        (float fPut, float fUp) = _footIKOffsets[mdlId];
                        footIK.putOffset_ = new Vector3(footIK.putOffset_.x, s * fPut, footIK.putOffset_.z);
                        footIK.SetFootLiftupLimit(s * fUp);
                    }
                }
            }
        }

        //DungeonCamera dg = new DungeonCamera();        

        // Store the character ID into memory.
        this._sizeMemoryStorage.UpdateCharacterList(activeCharacters);

        this.PlayerScale = nPlayerScale;
        if (playerModelId != null)
        {
            this.PlayerModelId = playerModelId.Value;
        }
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
            { "Nepgear", new List<CharacterData>()
                {
                    new CharacterData(id: 500, text: "Default Outfit", name: "Nepgear"),
                    new CharacterData(id: 510, text: "Purple Sister", name: "Purple Sister"),
                    new CharacterData(id: 550, text: "Swimsuit Outfit", name: "Nepgear (Swimsuit)"),
                }
            },
            { "Uni", new List<CharacterData>()
                {
                    new CharacterData(id: 600, text: "Default Outfit", name: "Uni"),
                    new CharacterData(id: 610, text: "Black Sister", name: "Black Sister"),
                    new CharacterData(id: 650, text: "Swimsuit Outfit", name: "Uni (Swimsuit)"),
                }
            },
            { "Rom", new List<CharacterData>()
                {
                    new CharacterData(id: 700, text: "Default Outfit", name: "Rom"),
                    new CharacterData(id: 710, text: "White Sister", name: "White Sister Rom"),
                    new CharacterData(id: 750, text: "Swimsuit Outfit", name: "Rom (Swimsuit)"),
                }
            },
            { "Ram", new List<CharacterData>()
                {
                    new CharacterData(id: 800, text: "Default Outfit", name: "Ram"),
                    new CharacterData(id: 810, text: "White Sister", name: "White Sister Ram"),
                    new CharacterData(id: 850, text: "Swimsuit Outfit", name: "Ram (Swimsuit)"),
                }
            },
            { "Neptune", new List<CharacterData>()
                {
                    new CharacterData(id: 100, text: "Default Outfit", name: "Neptune"),
                    new CharacterData(id: 110, text: "Purple Heart", name: "Purple Heart"),
                    new CharacterData(id: 150, text: "Swimsuit Outfit", name: "Neptune (Swimsuit)"),
                }
            },
            { "Noire", new List<CharacterData>()
                {
                    new CharacterData(id: 200, text: "Default Outfit", name: "Noire"),
                    new CharacterData(id: 210, text: "Black Heart", name: "Black Heart"),
                    new CharacterData(id: 250, text: "Swimsuit Outfit", name: "Noire (Swimsuit)"),
                }
            },
            { "Blanc", new List<CharacterData>()
                {
                    new CharacterData(id: 300, text: "Default Outfit", name: "Blanc"),
                    new CharacterData(id: 310, text: "White Heart", name: "White Heart"),
                    new CharacterData(id: 350, text: "Swimsuit Outfit", name: "Blanc (Swimsuit)"),
                }
            },
            { "Vert", new List<CharacterData>()
                {
                    new CharacterData(id: 400, text: "Default Outfit", name: "Vert"),
                    new CharacterData(id: 410, text: "Green Heart", name: "Green Heart"),
                    new CharacterData(id: 450, text: "Swimsuit Outfit", name: "Vert (Swimsuit)"),
                }
            },
            { "Friends", new List<CharacterData>()
                {
                    new CharacterData(id: 5504, text: "Maho", name: "Maho"),
                    new CharacterData(id: 5505, text: "Grey Sister", name: "Grey Sister"),
                    new CharacterData(id: 5516, text: "Anri", name: "Anri"),
                    new CharacterData(id: 3000, text: "Shanghai Alice", name: "Shanghai Alice"),
                    new CharacterData(id: 3100, text: "Higurashi", name: "Higurashi"),
                }
            },
            { "Antagonists", new List<CharacterData>()
                {
                    new CharacterData(id: 5503, text: "Arfoire", name: "Arfoire"),
                }
            },
            { "Male Citizens", new List<CharacterData>()
                {
                    new CharacterData(id: 9001, text: "Male 1", name: "Male Citizen 1"),
                    new CharacterData(id: 9002, text: "Male 2", name: "Male Citizen 2"),
                    new CharacterData(id: 9003, text: "Male 3", name: "Male Citizen 3"),
                    new CharacterData(id: 9007, text: "Male 4 (Elder)", name: "Elderly Male 4"),
                    new CharacterData(id: 9008, text: "Male 5 (Elder)", name: "Elderly Male 5"),
                    new CharacterData(id: 9009, text: "Male 6 (Elder)", name: "Elderly Male 6"),
                    new CharacterData(id: 9013, text: "Male 7 (Young)", name: "Young Boy 7"),
                    new CharacterData(id: 9014, text: "Male 8 (Young)", name: "Young Boy 8"),
                    new CharacterData(id: 9015, text: "Male 9 (Young)", name: "Young Boy 9"),
                }
            },
            { "Female Citizens", new List<CharacterData>()
                {
                    new CharacterData(id: 9004, text: "Female 1", name: "Female Citizen 1"),
                    new CharacterData(id: 9005, text: "Female 2", name: "Female Citizen 2"),
                    new CharacterData(id: 9006, text: "Female 3", name: "Female Citizen 3"),
                    new CharacterData(id: 9010, text: "Female 4 (Elder)", name: "Elderly Female 4"),
                    new CharacterData(id: 9011, text: "Female 5 (Elder)", name: "Elderly Female 5"),
                    new CharacterData(id: 9012, text: "Female 6 (Elder)", name: "Elderly Female 6"),
                    new CharacterData(id: 9016, text: "Female 7 (Young)", name: "Young Girl 7"),
                    new CharacterData(id: 9017, text: "Female 8 (Young)", name: "Young Girl 8"),
                    new CharacterData(id: 9018, text: "Female 9 (Young)", name: "Young Girl 9"),
                    new CharacterData(id: 9000, text: "Vert", name: "Vert (Citizen)"),
                }
            }
        };
    }
}