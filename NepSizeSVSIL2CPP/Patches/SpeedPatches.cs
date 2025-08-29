using Battle.BattleAI.AIBase.AiBattleBase;
using Battle.BattleAI.AIBase.AIBattleBaseEnemy;
using Battle.BattleAI.AIBase.AIBattleBasePlayer;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystem.Runtime.CompilerServices;
using UnityEngine;

public class SpeedPatches
{
    /// <summary>
    /// Takes the speed multiplayer from MoveInputAccept.Apply and adjusts it based on player scale.
    /// Method must be public, it is injected by a Transpiler into the MoveInputAccept class.
    /// </summary>
    /// <param name="player">player object</param>
    /// <returns></returns>
    public static float AdjustSpeedMultiplier(BattlePlayerProduction player)
    {
        float v = 0.1f; // Base multiplayer

        if (!NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
        { 
            // Return base value if we don't adjust player speeds.
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
    /// Modifies the IL to adjust player movement speed in battles.
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(MoveInputAccept), "Apply")]
    [HarmonyPrefix]
    static bool ApplyMoveInput(MoveInputAccept __instance, ref BattleInputStatus battle_status)
    {
        if (battle_status == null || battle_status.IsDeathOrResurrection())
        {
            return false;
        }

        BattlePlayerProduction battlePlayer = SingletonMonoBehaviour<BattleMain>.Instance.GetBattlePlayer(battle_status.RpgUnit);
        if (!(null == battlePlayer) && !battle_status.BattleActionFlag.UniqueCheckAll())
        {
            Vector2 normalized = battle_status.MoveVectorInput.normalized;
            Vector3 forward = SystemCamera3D.GetCamera().transform.forward;
            Vector3 vector = SystemCamera3D.GetCamera().transform.right * normalized.x + forward * normalized.y;
            vector.y = 0f;
            float magnitude = BattleGeometryCalculate.GetMagnitude(vector);
            if (vector != Vector3.zero)
            {
                battle_status.RpgUnit.transform.rotation = Quaternion.LookRotation(vector);
            }

            battle_status.RpgUnit.transform.position += vector * AdjustSpeedMultiplier(battlePlayer) * BattleSystem.GetAdjustElapseFrame();
            Vector3 position = battle_status.RpgUnit.transform.position;
            bool flag = __instance.CheckBattleAreaEscapeFlagUpdate(battle_status);
            battle_status.RpgUnit.transform.position = BattleSystem.PositionBattleAreaFix(battle_status.RpgUnit.transform, flag);
            if (battle_status.RpgUnit.transform.position - position != Vector3.zero)
            {
                battle_status.BattlePlayerProduction.CreateOutFieldEffect(flag);
            }

            __instance.ChangeSpeedAnimation(magnitude, battle_status);
        }
        return false;
    }

    /// <summary>
    /// Cache for identifying Speed and model uid.
    /// </summary>
    internal class SpeedUidHolder
    {
        public float OriginalSpeed;
        public uint CharacterId;
    }

    /// <summary>
    /// Cache for battle NPCs to the speed cache.
    /// </summary>
    private static readonly ConditionalWeakTable<AIBattleBase, SpeedUidHolder> AINPC_CACHE = new();

    /// <summary>
    /// Patch the speed of an NPC in battles.
    /// </summary>
    /// <param name="npc">NPC to patch.</param>
    /// <param name="patch">Patch (true) or unpatch/revert to original (false)</param>
    private static void PatchFollowTargetMoveSpeed(AIBattleBase npc, bool patch)
    {
        SpeedUidHolder holder = null;
        // Check if in cache or cache if reured.
        if (!AINPC_CACHE.TryGetValue(npc, out holder))
        {
            DbModelChara c = npc.OwnRpgUnit.gameObject.GetComponentInChildren<DbModelChara>();
            if (c != null)
            {
                holder = new SpeedUidHolder { OriginalSpeed = npc.NowAIData.Follow.move_speed_, CharacterId = c.GetModelID().GetModel() };
                AINPC_CACHE.Add(npc, holder);
            }
        }

        if (holder != null)
        {
            float? f = NepSizePlugin.Instance.FetchScale(holder.CharacterId);

            float value;
            if (f != null && patch)
            {
                // Scale the speed.
                value = holder.OriginalSpeed * f.Value;
            }
            else
            {
                // Restore original speed.
                value = holder.OriginalSpeed;
            }

            npc.NowAIData.Follow.move_speed_ = value;
        }
    }

    /// <summary>
    /// Patch the speed of NPCs who assist the player (AIBattleBasePlayer).
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void PatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC) //Only patch if enabled.
        {
            PatchFollowTargetMoveSpeed(__instance, true);
        }
    }

    /// <summary>
    /// Restore the original speed of NPCs who assist the player (AIBattleBasePlayer).
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void UnPatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer) //Only unpatch if enabled.
        {
            PatchFollowTargetMoveSpeed(__instance, false);
        }
    }

    /// <summary>
    /// Patch the speed of enemy NPCs (AIBattleBaseEnemy).
    /// </summary>
    /// <param name="__instance"></param>
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
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC) //Only patch if enabled.
        {
            PatchFollowTargetMoveSpeed(__instance, true);
        }
    }

    /// <summary>
    /// Restore the speed of enemy NPCs (AIBattleBaseEnemy).
    /// </summary>
    /// <param name="__instance"></param>
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
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC) //Only unpatch if enabled.
        {
            PatchFollowTargetMoveSpeed(__instance, false);
        }
    }

    /// <summary>
    /// Cache map for City NPCs and following NPCs.
    /// </summary>
    private static readonly ConditionalWeakTable<PointRoutePlay, SpeedUidHolder> MAP_ROUTE_TO_SPEED = new ConditionalWeakTable<PointRoutePlay, SpeedUidHolder>();

    /// <summary>
    /// Adjust the speed of city NPCs.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PointRoutePlay), "MoveRun")]
    static void AdjustNpcSpeed(ref PointRoutePlay __instance)
    {
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
        {
            return;
        }

        if (!MAP_ROUTE_TO_SPEED.TryGetValue(__instance, out SpeedUidHolder suh))
        {
            if (!(__instance.GetIl2CppType().IsAssignableFrom(Il2CppType.From(typeof(MapMovePointRoute)))))
            {
                return;
            }

            MapMovePointRoute i = new MapMovePointRoute(__instance.Pointer);

            var fieldVal = i.unit_base_;
            if (!(fieldVal.GetIl2CppType().IsAssignableFrom(Il2CppType.From(typeof(MapUnitBaseComponent)))))
            {
                return;
            }
            MapUnitBaseComponent mubc = new MapUnitBaseComponent(fieldVal.Pointer);

            DbModelChara c = mubc.gameObject.GetComponentInChildren<DbModelChara>();
            if (c == null)
            {
                return;
            }

            uint? u = c.GetModelID()?.GetModel();
            if (u == null)
            {
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

    /// <summary>
    /// Restore the speed after patching.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PointRoutePlay), "MoveRun")]
    static void PostAdjustNpcSpeed(ref PointRoutePlay __instance)
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
    /// Patch the speed of the player or their companions outside battles.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="move_vector"></param>
    [HarmonyPatch(typeof(MapUnitBaseComponent), "MoveVector")]
    [HarmonyPrefix]
    static void Prefix(ref MapUnitBaseComponent __instance, ref Vector3 move_vector)
    {
        if (__instance == null || move_vector.Equals(Vector3.zero) || !NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
        {
            return;
        }

        GameObject go = __instance.gameObject;
        if (go.GetComponentInChildren<DbModelChara>() is DbModelChara c) //Considerably fast, it is in the first child usually.
        {
            uint charId = c.model_id_.model_;

            float? f = NepSizePlugin.Instance.FetchScale(charId);
            if (f != null)
            {
                if (charId == ScalePatch.ACTIVE_PLAYER_UID)
                { // We're currently adjusting the player speed.
                    if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
                    {
                        move_vector *= f.Value;
                    }
                } // We're currently adjusting the speed of a following NPC.
                else if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
                {
                    move_vector *= f.Value;
                }
            }
        }
    }
}
