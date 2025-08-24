using Artisan.Neptunia;
using Artisan.Neptunia.Plateform;
using HarmonyLib;


namespace NepSizeYuushaNeptune
{
    /// <summary>
    /// Some harmony patches to prevent game pausing. The game quite aggressively tries to pause itself and also minimise which is very annoying when switching between the WebUI and the main window.
    /// </summary>
    public class DontPause
    {        
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
    }
}
