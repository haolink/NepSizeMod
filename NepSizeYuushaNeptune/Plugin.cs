using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NepSizeCore;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NepSizeYuushaNeptune;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
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

    private const int CHAR_NEPTUNE = 100;
    private const int CHAR_NOIRE = 200;
    private const int CHAR_BLANC = 300;
    private const int CHAR_VERT = 400;

    private const int CHAR_IF = 500;
    private const int CHAR_COMPA = 600;

    private const int CHAR_CHROME = 1100;
    private const int CHAR_ARTISAN = 1200;

    private DateTime _lastUp = DateTime.Now;

    private readonly Dictionary<string, uint> _uidToTex2DNames = new Dictionary<string, uint>()
    {
        { "neptune", CHAR_NEPTUNE }, { "neptune_battle", CHAR_NEPTUNE },
        { "noire", CHAR_NOIRE }, { "noire_battle", CHAR_NOIRE },
        { "blanc", CHAR_BLANC }, { "blanc_battle", CHAR_BLANC },
        { "vert", CHAR_VERT }, { "vert_battle", CHAR_VERT },
        { "compa", CHAR_COMPA }, { "compa_battle", CHAR_COMPA },
        { "if", CHAR_IF }, { "if_battle", CHAR_IF },
        { "chrome", CHAR_CHROME }, { "chrome_battle", CHAR_CHROME },
        { "artisan", CHAR_ARTISAN }, { "artisan_battle", CHAR_ARTISAN },
    };

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        CoreConfig.SERVER_IP = "0.0.0.0";
        CoreConfig.SERVER_LOCAL_SUBNET_ONLY = true;
        CoreConfig.SERVER_PORT = 7878;
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

    CharacterList INepSizeGamePlugin.GetCharacterList()
    {
        return new CharacterList()
        {
            { "Main", new List<CharacterData>()
                {
                    new CharacterData(id: CHAR_NEPTUNE, text: "Neptune", name: "Neptune"),
                    new CharacterData(id: CHAR_NOIRE, text: "Noire", name: "Noire"),
                    new CharacterData(id: CHAR_BLANC, text: "Blanc", name: "Blanc"),
                    new CharacterData(id: CHAR_VERT, text: "Vert", name: "Vert")
                }
            },
            { "Support", new List<CharacterData>()
                {
                    new CharacterData(id: CHAR_IF, text: "IF", name: "IF"),
                    new CharacterData(id: CHAR_COMPA, text: "Compa", name: "Compa"),
                    new CharacterData(id: CHAR_ARTISAN, text: "Artisan", name: "Artisan"),
                    new CharacterData(id: CHAR_CHROME, text: "Chrome", name: "Chrome"), 
                }
            }
        };
    }

    Dictionary<uint, float> INepSizeGamePlugin.GetCharacterSizes()
    {        
        return this._sizeMemoryStorage.SizeValues;
    }

    private void FixedUpdate()
    {
        Time.fixedDeltaTime = 1.0f / 240.0f;
        this._sizeDataThread.HandleConnectionQueue();
        _lastUp = DateTime.Now;
    }   

    private void Update()
    {
        TimeSpan tsSinceUp = DateTime.Now - _lastUp;
        if (tsSinceUp.TotalMilliseconds > 50)
        {
            this._sizeDataThread.HandleConnectionQueue();
            _lastUp = DateTime.Now;
        }

        Dictionary<uint, float> scales = this._sizeMemoryStorage.SizeValues;

        MeshRenderer[] mrs = FindObjectsOfType<MeshRenderer>();
        
        string matname;
        uint cid;
        float f;

        List<uint> activeCharacters = new List<uint>();

        foreach (MeshRenderer mr in mrs)
        {
            if (mr.enabled) 
            {
                matname = mr?.material?.mainTexture?.name.ToLowerInvariant();
                if (matname != null && _uidToTex2DNames.TryGetValue(matname, out cid)) 
                {
                    if (!activeCharacters.Contains(cid))
                    {
                        activeCharacters.Add(cid);
                    }

                    if (scales.TryGetValue(cid, out f))
                    {
                        mr.gameObject.transform.localScale = new Vector3(f, f, f);

                        if (mr.transform?.parent?.parent?.Find("FeedbackBtns") is Transform btn)
                        {
                            btn.localPosition = new Vector3(0, f * 12.23f + 5.0f, 0);
                        }
                    }
                }
            }            
        }

        SkeletonGraphic[] grs = FindObjectsOfType<SkeletonGraphic>();

        foreach (SkeletonGraphic g in grs)
        {
            if (g.enabled)
            {
                matname = g?.mainTexture?.name.ToLowerInvariant();
                if (matname != null && _uidToTex2DNames.TryGetValue(matname, out cid))
                {
                    if (!activeCharacters.Contains(cid))
                    {
                        activeCharacters.Add(cid);
                    }

                    if (scales.TryGetValue(cid, out f))
                    {
                        g.gameObject.transform.localScale = new Vector3(f, f, f);
                    }
                }
            }
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

    void INepSizeGamePlugin.UpdateSizes(Dictionary<uint, float> sizes, bool overwrite)
    {
        Debug.Log("Received " + sizes.Count + " scales!");
        this._sizeMemoryStorage.UpdateSizes(sizes, overwrite);
    }
}
