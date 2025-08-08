using BepInEx.Configuration;
using CharaIK;
using NepSizeCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static System.Net.WebRequestMethods;

/// <summary>
/// Main plugin for Game Maker.
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
        ConfigEntry<int> listenPort = PluginInfo.Instance.Config.Bind<int>("Server", "Port", 7979, "Listen port - default is 7979");
        ConfigEntry<bool> listenSubnetOnly = PluginInfo.Instance.Config.Bind<bool>("Server", "RestrictListenSubnet", true, "Only listen in the local IPv4 subnet, disable this if you wish to allow global access (you must know what you're doing!).");

        CoreConfig.GAMENAME = "GMRE";
        CoreConfig.WEBUI_TITLE = "Nep GMR:E Size mod";
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
    /// Foot IK offsets.
    /// </summary>
    private Dictionary<uint, (float, float)> _footIKOffsets = new Dictionary<uint, (float, float)>();

    private float ReadFootLiftupLimit(FootIK footIK)
    {
        return footIK.footLiftupLimit_; // Reflection not needed in IL2CPP context.
        /*FieldInfo fi = typeof(FootIK).GetField("footLiftupLimit_", BindingFlags.NonPublic | BindingFlags.Instance);
        return (float)(fi.GetValue(footIK));*/
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
                        om.transform.localScale = new Vector3(s, s, s);
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
            { "Adult Neptune", new List<CharacterData>()
                {
                    new CharacterData(id: 1500, text: "Default Outfit", name: "Adult Neptune"),
                    new CharacterData(id: 1510, text: "Generator Unit", name: "Adult Neptune (Generator Unit)"),
                    new CharacterData(id: 1560, text: "Swimsuit Outfit", name: "Adult Neptune (Swimsuit)"),
                }
            },
            { "Pippih", new List<CharacterData>()
                {
                    new CharacterData(id: 1600, text: "Default Outfit", name: "Pippih"),
                    new CharacterData(id: 1610, text: "Goddess Form", name: "Pippih (Goddess Form)"),
                    new CharacterData(id: 1660, text: "Swimsuit Outfit", name: "Pippih (Swimsuit)"),
                }
            },
            { "Jagga", new List<CharacterData>()
                {
                    new CharacterData(id: 1700, text: "Default Outfit", name: "Jagga"),
                    new CharacterData(id: 1710, text: "Goddess Form", name: "Jagga (Goddess Form)"),
                    new CharacterData(id: 1760, text: "Swimsuit Outfit", name: "Jagga (Swimsuit)"),
                }
            },
            { "Reedio", new List<CharacterData>()
                {
                    new CharacterData(id: 1800, text: "Default Outfit", name: "Reedio"),
                    new CharacterData(id: 1810, text: "Goddess Form", name: "Reedio (Goddess Form)"),
                    new CharacterData(id: 1860, text: "Swimsuit Outfit", name: "Reedio (Swimsuit)"),
                }
            },
            { "Neptune", new List<CharacterData>()
                {
                    new CharacterData(id: 100, text: "Default Outfit", name: "Neptune"),
                    new CharacterData(id: 110, text: "Purple Heart", name: "Purple Heart"),
                    new CharacterData(id: 160, text: "Swimsuit Outfit", name: "Neptune (Swimsuit)"),
                }
            },
            { "Noire", new List<CharacterData>()
                {
                    new CharacterData(id: 200, text: "Default Outfit", name: "Noire"),
                    new CharacterData(id: 210, text: "Black Heart", name: "Black Heart"),
                    new CharacterData(id: 260, text: "Swimsuit Outfit", name: "Noire (Swimsuit)"),
                }
            },
            { "Blanc", new List<CharacterData>()
                {
                    new CharacterData(id: 300, text: "Default Outfit", name: "Blanc"),
                    new CharacterData(id: 310, text: "White Heart", name: "White Heart"),
                    new CharacterData(id: 360, text: "Swimsuit Outfit", name: "Blanc (Swimsuit)"),
                }
            },
            { "Vert", new List<CharacterData>()
                {
                    new CharacterData(id: 400, text: "Default Outfit", name: "Vert"),
                    new CharacterData(id: 410, text: "Green Heart", name: "Green Heart"),
                    new CharacterData(id: 460, text: "Swimsuit Outfit", name: "Vert (Swimsuit)"),
                }
            },
            { "Nepgear", new List<CharacterData>()
                {
                    new CharacterData(id: 500, text: "Default Outfit", name: "Nepgear"),
                    new CharacterData(id: 510, text: "Purple Sister", name: "Purple Sister"),
                    new CharacterData(id: 560, text: "Swimsuit Outfit", name: "Nepgear (Swimsuit)"),
                }
            },
            { "Uni", new List<CharacterData>()
                {
                    new CharacterData(id: 600, text: "Default Outfit", name: "Uni"),
                    new CharacterData(id: 610, text: "Black Sister", name: "Black Sister"),
                    new CharacterData(id: 660, text: "Swimsuit Outfit", name: "Uni (Swimsuit)"),
                }
            },
            { "Rom", new List<CharacterData>()
                {
                    new CharacterData(id: 700, text: "Default Outfit", name: "Rom"),
                    new CharacterData(id: 710, text: "White Sister", name: "White Sister Rom"),
                    new CharacterData(id: 760, text: "Swimsuit Outfit", name: "Rom (Swimsuit)"),
                }
            },
            { "Ram", new List<CharacterData>()
                {
                    new CharacterData(id: 800, text: "Default Outfit", name: "Ram"),
                    new CharacterData(id: 810, text: "White Sister", name: "White Sister Ram"),
                    new CharacterData(id: 860, text: "Swimsuit Outfit", name: "Ram (Swimsuit)"),
                }
            },
            { "Shas", new List<CharacterData>()
                {
                    new CharacterData(id: 1900, text: "F-Sha", name: "F-Sha"),
                    new CharacterData(id: 2000, text: "B-Sha", name: "B-Sha"),
                    new CharacterData(id: 2100, text: "C-Sha", name: "C-Sha"),
                    new CharacterData(id: 2200, text: "K-Sha", name: "K-Sha"),
                    new CharacterData(id: 2300, text: "S-Sha", name: "S-Sha"),
                }
            },
            { "Antagonists", new List<CharacterData>()
                {
                    new CharacterData(id: 5503, text: "Arfoire", name: "Arfoire"),
                    new CharacterData(id: 5506, text: "Copy-The-Hard", name: "Copy-The-Hard"),
                    new CharacterData(id: 5507, text: "Copy-The-Code", name: "Copy-The-Code"),
                    new CharacterData(id: 5508, text: "Copy-The-Art", name: "Copy-The-Art"),
                    new CharacterData(id: 2700, text: "Croire", name: "Croire"),
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
                }
            }
        };
    }
}