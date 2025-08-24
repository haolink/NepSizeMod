using HarmonyLib;
using IF.Battle.Camera;

/// <summary>
/// Basic game patches.
/// </summary>
public class BasicPatches
{
    /// <summary>
    /// Game uses OnApplicationFocus to determine whether it should pause or not (pretty sensible).
    /// Overwriting the focus variable and it stops pausing.
    /// </summary>
    /// <param name="focus"></param>
    [HarmonyPatch(typeof(ApplicationManager), "OnApplicationFocus")]
    [HarmonyPrefix]
    static void Prefix(ref bool focus)
    {
        focus = true; //Of course we're focusing the window, no need to pause. 👌
    }

    /// <summary>
    /// Patches a stuck camera if the character is just too large.
    /// Forces the game to believe the camera transitioning to focus the character is already complete.
    /// </summary>
    /// <param name="__result"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BattleCameraBase), "IsTransitioning")]
    static void YouDoNotTransition(ref bool __result)
    {
        __result = false;
    }
}
