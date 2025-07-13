using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using NepSizeCore;

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

        // Initiliase thread and storage.
        this._sizeMemoryStorage = SizeMemoryStorage.Instance(this);
        this._sizeDataThread = new SizeDataThread(new ServerCommands("NSVS", this));
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
            if (c.GetModelID()  != null) //Character has a valid id
            {
                uint mdlId = c.GetModelID().GetModel();
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
}