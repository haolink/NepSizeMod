using DbModel;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
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

    /*[HarmonyPatch(typeof(DbLibrary<DbStructBase>), "SetDatas")]
    [HarmonyPrefix]
    static void PrefixGetRidOfNoire(ref List<List<DbStructBase>> lists)
    {
        if (lists == null || lists.Count == 0)
        {
            return;
        }
        foreach (List<DbStructBase> innerlist in lists)
        {
            foreach (DbStructBase o in innerlist)
            {
                if (o is DbChara)
                {
                    DbChara c = (DbChara)o;
                    if (c.BaseModelNum == 200)
                    {
                        //Debug.Log("FOUND NOIRE!!!");
                        c.BaseModelNum = 5504;
                        c.TransModelNum = 5505;
                    }
                }
                else if (o is DbModelSetting)
                {
                    DbModelSetting setting = (DbModelSetting)o;
                    if (setting.ModelNo == 200)
                    {
                        Debug.Log("SET N");
                        setting.ModelNo = 5504;
                    }
                    if (setting.ModelNo == 210)
                    {
                        Debug.Log("SET GS");
                        setting.ModelNo = 5505;
                    }
                }                
            }
        }
    }

    [HarmonyPatch(typeof(DbDataObject<DbCharaMake>), "GetData")]
    [HarmonyPatch(typeof(DbDataObject<DbCharaMake>), "GetDataAsMake")]
    [HarmonyPostfix]
    static void PostfixDontLoadNoire(ref object __result)
    {
        if (__result is DbCharaMake)
        {
            DbCharaMake c = (DbCharaMake)__result;
            if (c.BaseModelNum == 200)
            {
                Debug.Log("NOOO");
                c.BaseModelNum = 5504;
            }
        }
    }

    [HarmonyPatch(typeof(DbModelBase), "GetModelID")]
    [HarmonyPostfix]
    static void ReturnTheWrong(ref ModelID __result)
    {
        if (__result.GetModel() == 200)
        {
            __result.SetModelData(5504, 1);
        }
    }*/
}
