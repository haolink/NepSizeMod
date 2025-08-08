using Battle.BattleAI.AIBase.AiBattleBase;
using Battle.BattleAI.AIBase.AIBattleBaseEnemy;
using Battle.BattleAI.AIBase.AIBattleBasePlayer;
using HarmonyLib;
using IF.Battle.Camera;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

public class ColliderUnitUpdate
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BattleCameraBase), "IsTransitioning")]
    static void YouDoNotTransition(ref bool __result)
    {
        __result = false;
    }

    /*[HarmonyPatch(typeof(BattleInputStatus), "MoveVectorInput", MethodType.Getter)]
    [HarmonyPostfix]
    static void GetDoubleVector(ref Vector2 __result)
    {
        __result.x *= 20.0f;
        __result.y *= 20.0f;
    }*/

    /// <summary>
    /// Takes the speed multiplayer from MoveInputAccept.Apply and adjusts it based on player scale.
    /// </summary>
    /// <param name="player">player object</param>
    /// <returns></returns>
    public static float AdjustSpeedMultiplier(BattlePlayerProduction player)
    {
        float v = 0.1f; // Base multiplayer
        DbModelChara c = player.gameObject.GetComponentInChildren<DbModelChara>();

        uint charId = c.GetModelID().GetModel();

        float? f = NepSizePlugin.Instance.FetchScale(charId);
        if (f != null)
        {
            v *= f.Value;
        }            

        return v; // oder irgendetwas Dynamisches
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPatch(typeof(MoveInputAccept), "Apply")]
    [HarmonyPrefix]
    static bool ReplaceMethodApply(ref MoveInputAccept __instance, ref BattleInputStatus battle_status)
    {
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

    internal class SpeedUidHolder
    {
        public float OriginalSpeed;
        public uint CharacterId;
    }

    private static readonly ConditionalWeakTable<AIBattleBasePlayer, SpeedUidHolder> AIPLAYER_BACKUP = new();

    private static void PatchFollowTargetMoveSpeed(AIBattleBasePlayer aiPlayer, bool patch)
    {
        SpeedUidHolder holder = null;
        if (!AIPLAYER_BACKUP.TryGetValue(aiPlayer, out holder))
        {
            DbModelChara c = aiPlayer.OwnRpgUnit.gameObject.GetComponentInChildren<DbModelChara>();
            if (c != null)
            {
                holder = new SpeedUidHolder { OriginalSpeed = aiPlayer.NowAIData.Follow.move_speed_, CharacterId = c.GetModelID().GetModel() };
                AIPLAYER_BACKUP.Add(aiPlayer, holder);
            }
        }

        if (holder != null)
        {
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void PatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {        
        PatchFollowTargetMoveSpeed(__instance, true);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void UnPatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        PatchFollowTargetMoveSpeed(__instance, false);
    }


    private static readonly ConditionalWeakTable<AIBattleBaseEnemy, SpeedUidHolder> AIENEMY_BACKUP = new();

    private static void PatchFollowAITargetMoveSpeed(AIBattleBaseEnemy aiEnemy, bool patch)
    {
        SpeedUidHolder holder = null;
        if (!AIENEMY_BACKUP.TryGetValue(aiEnemy, out holder))
        {
            DbModelChara c = aiEnemy.OwnRpgUnit.gameObject.GetComponentInChildren<DbModelChara>();
            if (c != null)
            {
                holder = new SpeedUidHolder { OriginalSpeed = aiEnemy.NowAIData.Follow.move_speed_, CharacterId = c.GetModelID().GetModel() };
                AIENEMY_BACKUP.Add(aiEnemy, holder);
            }
        }

        if (holder != null)
        {
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

            aiEnemy.NowAIData.Follow.move_speed_ = value;
        }
    }

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
        PatchFollowAITargetMoveSpeed(__instance, true);
    }

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
        PatchFollowAITargetMoveSpeed(__instance, false);
    }


    private static readonly FieldInfo UNIT_FIELD_POINT_ROUTE = typeof(MapMovePointRoute).GetField("unit_base_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly ConditionalWeakTable<PointRoutePlayDistance, SpeedUidHolder> MAP_ROUTE_TO_SPEED = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PointRoutePlayDistance), "MoveRun")]
    static void AdjustNpcSpeed(ref PointRoutePlayDistance __instance)
    {
        SpeedUidHolder suh = null;
        if (!(MAP_ROUTE_TO_SPEED.TryGetValue(__instance, out suh)))
        {
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

            DbModelChara c = mubc.gameObject.GetComponentInChildren<DbModelChara>();
            if (c == null)
            {
                return;
            }


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
            return;
        }
        
        float? f = NepSizePlugin.Instance.FetchScale(suh.CharacterId);

        if (f != null)
        {
            __instance.speed_ = suh.OriginalSpeed * f.Value;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PointRoutePlayDistance), "MoveRun")]
    static void PostAdjustNpcSpeed(ref PointRoutePlayDistance __instance)
    {
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051")]
    [HarmonyPatch(typeof(MapUnitBaseComponent), "MoveVector")]
    [HarmonyPrefix]
    static void Prefix(ref MapUnitBaseComponent __instance, ref Vector3 move_vector)
    {
        if (__instance == null || move_vector.Equals(Vector3.zero))
        {
            return;
        }

        GameObject go = __instance.gameObject;
        if (go.GetComponentInChildren<DbModelChara>() is DbModelChara c) {
            uint charId = c.GetModelID().GetModel();

            float? f = NepSizePlugin.Instance.FetchScale(charId);
            if (f != null)
            {
                move_vector *= f.Value;
            }
        }
    }
}
