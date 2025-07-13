using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

public class DontPause
{
    [HarmonyPatch(typeof(ApplicationManager), "OnApplicationFocus")]
    [HarmonyPrefix]
    static void Prefix(ref bool focus)
    {
        Debug.Log("Focussing: " + (focus ? "J" : "N"));
        focus = true;
    }
}

