using HarmonyLib;
using IF.Battle.Camera;

/// <summary>
/// Basic patches for the game. Currently only patches a camera bug caused by scales of characters.
/// </summary>
public class BasicPatches
{
    /// <summary>
    /// Patches a stuck camera if the character is just too large.
    /// Forces the game to believe the camera transitioning to focus the character is already complete.
    /// </summary>
    /// <param name="__result"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BattleCameraBase), "IsTransitioning")]
    static void YouDoNotTransition(ref bool __result)
    {
        __result = false;
    }

}