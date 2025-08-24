using Battle.BattleAI.AIBase.AiBattleBase;
using Battle.BattleAI.AIBase.AIBattleBaseEnemy;
using Battle.BattleAI.AIBase.AIBattleBasePlayer;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Patches the speed of characters. Taller characters -> longer steps -> higher speeds.
/// </summary>
public class SpeedPatches
{    
    /// <summary>
    /// Takes the speed multiplayer from MoveInputAccept.Apply and adjusts it based on player scale.
    /// </summary>
    /// <param name="player">player object</param>
    /// <returns></returns>
    public static float AdjustSpeedMultiplier(BattlePlayerProduction player)
    {
        float v = 0.1f; // Base multiplayer, taken from game.

        // Omit if we shouldn't change.
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
        {
            return v;
        }

        DbModelChara c = player.gameObject.GetComponentInChildren<DbModelChara>();

        uint charId = c.GetModelID().GetModel();

        float? f = NepSizePlugin.Instance.FetchScale(charId);
        if (f != null)
        {
            v *= f.Value;
        }

        return v;
    }

    /// <summary>
    /// Replaces the actual method Apply of MoveInputAccept. 
    /// The code below was dumped from an actual leaked Mono build of the game.
    /// In the IL2CPP shipped version we use Prefix to completely replace the method
    /// and return false to prevent execution of the original.
    /// 
    /// It handles running speeds in battles.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="battle_status"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPatch(typeof(MoveInputAccept), "Apply")]
    [HarmonyPrefix]
    static bool ReplaceMethodApply(ref MoveInputAccept __instance, ref BattleInputStatus battle_status)
    {
        // Minimal code commenting as this is dnSpy code
        if (battle_status == null)
        {
            return false;
        }
        BattlePlayerProduction battle_player = battle_status.BattlePlayerProduction;
        if (battle_status.IsDeathOrResurrection())
        {
            __instance.OffRelateRunEndMotionFlug(battle_player);
            return false;
        }
        if (null == battle_player)
        {
            return false;
        }
        if (battle_status.BattleActionFlag.UniqueCheckAll())
        {
            if (!battle_status.BattleActionFlag.FlagBattleMenu.FlagAction.Flag)
            {
                __instance.OffRelateRunEndMotionFlug(battle_player);
            }
            return false;
        }
        if (battle_status.BattlePlayerProduction.IsStun())
        {
            __instance.OffRelateRunEndMotionFlug(battle_player);
            return false;
        }
        Vector3 move_position = MoveInputAccept.GetMovePosition(battle_status);
        if (BattleSystem.IsArenaChallenge(DefDatabase.BattleArenaChallenge.UNMOVABLE))
        {
            move_position = Vector3.zero;
        }
        float magnitude = BattleGeometryCalculate.GetMagnitude(move_position);
        if (move_position != Vector3.zero)
        {
            battle_status.RpgUnit.transform.rotation = Quaternion.LookRotation(move_position);
        }
        bool is_input = !Mathf.Approximately(magnitude, 0f);
        if (!is_input && battle_player.IsRunPreviousFrame())
        {
            battle_player.ReadyRunEnd();
        }
        bool is_move;
        Vector3 add_pos;
        __instance.ChangeAnimation(is_input, battle_status, out is_move, out add_pos);
        float elapsed_frame = BattleSystem.GetAdjustElapseFrame(false);
        if (is_input)
        {
            battle_player.OnRunPreviousFrame();
            __instance.UpdateTutorial(elapsed_frame);
        }
        else
        {
            battle_player.OffRunPreviousFrame();
        }
        if (!is_move)
        {
            if (!is_input)
            {
                return false;
            }
            // Edit here - adjusting speed of the player.
            add_pos = move_position * AdjustSpeedMultiplier(battle_player) * elapsed_frame;
        }
        Vector3 old_pos = battle_status.RpgUnit.transform.position + add_pos;
        bool is_escape = __instance.CheckBattleAreaEscapeFlagUpdate(battle_status, old_pos);
        battle_status.RpgUnit.transform.position = BattleSystem.PositionBattleAreaFix(old_pos, is_escape);
        if (battle_status.RpgUnit.transform.position - old_pos != Vector3.zero)
        {
            battle_status.BattlePlayerProduction.CreateOutFieldEffect(is_escape);
        }

        return false;
    }

    /// <summary>
    /// Helper class to store data from the prefix in.
    /// </summary>
    internal class SpeedUidHolder
    {
        public float OriginalSpeed;
        public uint CharacterId;
    }

    /// <summary>
    /// Dictionary to store speeds of AI assists.
    /// </summary>
    private static readonly ConditionalWeakTable<AIBattleBase, SpeedUidHolder> AIPLAYER_BACKUP = new();

    /// <summary>
    /// Patches the speed of NPCs in battle.
    /// </summary>
    /// <param name="aiPlayer">NPCs in the battle.</param>
    /// <param name="patch">Patch (true) or Restore (false)</param>
    private static void PatchFollowTargetMoveSpeed(AIBattleBase aiPlayer, bool patch)
    {
        SpeedUidHolder holder = null;
        if (!AIPLAYER_BACKUP.TryGetValue(aiPlayer, out holder))
        {
            //Store the move_speed and model ID for now.
            DbModelChara c = aiPlayer.OwnRpgUnit.gameObject.GetComponentInChildren<DbModelChara>(); //expensive but only necessary once per battle per NPC.
            if (c != null)
            {
                holder = new SpeedUidHolder { OriginalSpeed = aiPlayer.NowAIData.Follow.move_speed_, CharacterId = c.GetModelID().GetModel() };
                AIPLAYER_BACKUP.Add(aiPlayer, holder);
            }
        }

        if (holder != null)
        {
            // Patch - or unpatch the value.
            float? f = NepSizePlugin.Instance.FetchScale(holder.CharacterId);

            float value;
            if (f != null && patch)
            {
                value = holder.OriginalSpeed * f.Value;
            }
            else
            {
                value = holder.OriginalSpeed;
            }

            aiPlayer.NowAIData.Follow.move_speed_ = value;
        }
    }

    /// <summary>
    /// Bunch of AI instructions to patch - in the prefix we update the values so the 
    /// main method gets patched values.
    /// </summary>
    /// <param name="__instance"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void PatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            PatchFollowTargetMoveSpeed(__instance, true);
        }
    }

    /// <summary>
    /// After execution (in postfix) we restore the values.
    /// </summary>
    /// <param name="__instance"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void UnPatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            PatchFollowTargetMoveSpeed(__instance, false);
        }
    }

    /// <summary>
    /// We repeat the same for Enemy NPCs. Prefix to patch.
    /// </summary>
    /// <param name="__instance"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "BackwardAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CircularOrbitAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CircularOrbitRoundTripAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CloseApproach")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "EscapeAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "ExeSpecificApproach")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "LeaveForFriend")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "MoveStraightAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "MoveWhenFollowSummoner")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "ReturnBattleArea")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "StraightRoundTripMove")]
    static void PatchAINonPlayerMovespeed(ref AIBattleBaseEnemy __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            PatchFollowTargetMoveSpeed(__instance, true);
        }
    }

    /// <summary>
    /// We repeat the same for Enemy NPCs. Psotfix to unpatch.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "BackwardAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CircularOrbitAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CircularOrbitRoundTripAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "CloseApproach")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "EscapeAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "ExeSpecificApproach")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "LeaveForFriend")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "MoveStraightAction")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "MoveWhenFollowSummoner")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "ReturnBattleArea")]
    [HarmonyPatch(typeof(AIBattleBaseEnemy), "StraightRoundTripMove")]
    static void UnPatchAINonPlayerMovespeed(ref AIBattleBaseEnemy __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            PatchFollowTargetMoveSpeed(__instance, false);
        }
    }

    /// <summary>
    /// unit_base_ is a private variable.
    /// </summary>
    private static readonly FieldInfo UNIT_FIELD_POINT_ROUTE = typeof(MapMovePointRoute).GetField("unit_base_", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Following AI NPCs use the dictionary.
    /// </summary>
    private static readonly ConditionalWeakTable<PointRoutePlayDistance, SpeedUidHolder> MAP_ROUTE_TO_SPEED = new();

    /// <summary>
    /// MoveRun is for NPCs following outside the battle.
    /// Again we patch values before running this method.
    /// </summary>
    /// <param name="__instance"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PointRoutePlayDistance), "MoveRun")]
    static void AdjustNpcSpeed(ref PointRoutePlayDistance __instance)
    {
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            return;
        }

        SpeedUidHolder suh = null;
        if (!(MAP_ROUTE_TO_SPEED.TryGetValue(__instance, out suh)))
        {
            // Determine Speed Holder for safety.
            if (!(__instance is MapMovePointRoute))
            {
                MAP_ROUTE_TO_SPEED.Add(__instance, null);
                return;
            }

            MapMovePointRoute i = __instance as MapMovePointRoute;

            MapUnitBaseComponent mubc = (MapUnitBaseComponent)UNIT_FIELD_POINT_ROUTE.GetValue(i);
            if (mubc == null)
            {
                MAP_ROUTE_TO_SPEED.Add(__instance, null);
                return;
            }

            DbModelChara c = mubc.gameObject.GetComponentInChildren<DbModelChara>(); //Expensive, only needed once.
            if (c == null)
            {
                return;
            }

            // Determine model ID.
            uint? u = c.GetModelID()?.GetModel();
            if (u == null)
            {
                MAP_ROUTE_TO_SPEED.Add(__instance, null);
                return;
            }

            suh = new SpeedUidHolder()
            {
                OriginalSpeed = i.speed_,
                CharacterId = u.Value
            };

            MAP_ROUTE_TO_SPEED.Add(__instance, suh);
        }

        if (suh == null)
        {
            // If not identified - return.
            return;
        }

        // Otherwise patch.
        float? f = NepSizePlugin.Instance.FetchScale(suh.CharacterId);

        if (f != null)
        {
            __instance.speed_ = suh.OriginalSpeed * f.Value;
        }
    }

    /// <summary>
    /// Restore after MoveRun.
    /// </summary>
    /// <param name="__instance"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PointRoutePlayDistance), "MoveRun")]
    static void PostAdjustNpcSpeed(ref PointRoutePlayDistance __instance)
    {
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            return;
        }

        SpeedUidHolder suh = null;
        if ((MAP_ROUTE_TO_SPEED.TryGetValue(__instance, out suh)))
        {
            if (suh == null)
            {
                return;
            }

            __instance.speed_ = suh.OriginalSpeed;
        }
    }

    /// <summary>
    /// Player movement outside battle.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="move_vector"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPatch(typeof(MapUnitBaseComponent), "MoveVector")]
    [HarmonyPrefix]
    static void Prefix(ref MapUnitBaseComponent __instance, ref Vector3 move_vector)
    {
        if (__instance == null || move_vector.Equals(Vector3.zero) || !NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
        {
            return;
        }

        GameObject go = __instance.gameObject;
        if (go.GetComponentInChildren<DbModelChara>() is DbModelChara c)
        {
            uint charId = c.GetModelID().GetModel();

            float? f = NepSizePlugin.Instance.FetchScale(charId);
            if (f != null)
            {
                move_vector *= f.Value;
            }
        }
    }
}
