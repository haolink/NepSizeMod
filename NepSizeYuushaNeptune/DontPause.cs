using Artisan.Neptunia;
using Artisan.Neptunia.Explo.Controller;
using Artisan.Neptunia.Plateform;
using Artisan.Unity.Sound;
using HarmonyLib;
using Rewired.Integration.UnityUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NepSizeYuushaNeptune
{
    public class DontPause
    {
        /*[HarmonyPrefix]
        [HarmonyPatch(typeof(Application), "runInBackground", MethodType.Getter)]
        static bool PrefixRUB(ref bool __result)
        {            
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Application), "isFocused", MethodType.Getter)]
        static bool WeAreofCourseFocused(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Application), "runInBackground", MethodType.Setter)]
        static bool PrefixSRUB(ref bool value)
        {
            value = true;
            return true;
        }

        [HarmonyPatch(typeof(EventSystem), "OnApplicationFocus")]
        [HarmonyPatch(typeof(RewiredStandaloneInputModule), "OnApplicationFocus")]
        [HarmonyPrefix]
        static void EventHasFocus(ref bool hasFocus)
        {
            hasFocus = true;            
        }*/

        /*[HarmonyPatch(typeof(Time), "fixedDeltaTime", MethodType.Getter)]
        [HarmonyPrefix]
        static bool enforceFixedUpdateRate(ref float __result)
        {
            //__result = 1.0f / 240.0f;            
            return true;
        }*/

        /*[HarmonyPatch(typeof(Time), "timeScale", MethodType.Setter)]
        [HarmonyPrefix]
        static bool tsIsAlwaysSet(ref float value)
        {
            value = 1.0f;            
            return true;
        }

        [HarmonyPatch(typeof(Time), "timeScale", MethodType.Getter)]
        [HarmonyPrefix]
        static bool tsIsAlwaysGet(ref float __result)
        {
            __result = 1.0f;
            return false;
        }*/

        [HarmonyPatch(typeof(SteamController), "IsOverlayOpened", MethodType.Getter)]
        [HarmonyPrefix]
        static bool enforceNoOverlay(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(GameController), "IsSystemOverlayOpened", MethodType.Getter)]
        [HarmonyPrefix]
        static bool enforceNoSysOverlay(ref bool __result)
        {
            __result = false;
            return false;
        }

        /*[HarmonyPatch(typeof(GameController), "CheckBackgroundMode")]
        [HarmonyPrefix]
        static bool dontCheckBackground()
        {
            return false;
        }

        [HarmonyPatch(typeof(ExploController), "FreezeExplo")]
        [HarmonyPrefix]
        static void NeverEverFreezeExplo(ref bool bEnable)
        {
            bEnable = true;
        }

        [HarmonyPatch(typeof(Rewired.ReInput), "applicationRunInBackground", MethodType.Getter)]
        [HarmonyPrefix]
        static bool weAreInForeground(ref bool __result)
        {
            __result = false;
            return false;
        }

        [HarmonyPatch(typeof(Rewired.ReInput), "applicationIsFocused", MethodType.Getter)]
        [HarmonyPrefix]
        static bool weAreInFocus(ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPatch(typeof(SoundController), "Pause")]
        [HarmonyPrefix]
        static bool noPauseSound()
        {
            return false;
        }

        [HarmonyPatch(typeof(Rewired.ReInput), "configuration", MethodType.Getter)]
        [HarmonyPostfix]
        static void FixConfig(ref object __result)
        {
            var configType = __result.GetType();
            var ignoreField = configType.GetField("ignoreInputWhenAppNotInFocus");
            if (ignoreField != null)
            {
                ignoreField.SetValue(__result, false);
            }
        }

        [HarmonyPatch(typeof(Rewired.ReInput), "KFIpIhzbLhvVgsdnQzlZyCQDUrP")]
        [HarmonyPrefix]
        static bool DontRunThisCrypticStuff()
        {
            return false;
        }        */
    }
}
