using Battle.BattleAI.AIBase.AiBattleBase;
using Battle.BattleAI.AIBase.AIBattleBaseEnemy;
using Battle.BattleAI.AIBase.AIBattleBasePlayer;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
    /// Some storage method magic. Identifies Stloc IL code to be able to patch it properly.
    /// </summary>
    /// <param name="instruction"></param>
    /// <returns></returns>
    private static int? TryGetStlocIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0)
        {
            return 0;
        }
        else if (instruction.opcode == OpCodes.Stloc_1)
        {
            return 1;
        }
        else if (instruction.opcode == OpCodes.Stloc_2)
        {
            return 2;
        }
        else if (instruction.opcode == OpCodes.Stloc_3)
        {
            return 3;
        }
        else if (instruction.opcode == OpCodes.Stloc_S || instruction.opcode == OpCodes.Stloc)
        {
            if (instruction.operand is int)
            {
                return (int)instruction.operand;
            }
        }
        return null;
    }

    /// <summary>
    /// Modifies the IL to adjust player movement speed in battles.
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(MoveInputAccept), "Apply")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> TranspileMovementSpeed(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

        // Instance variable battlePlayer must be found.
        // C# code should be `BattlePlayerProduction battlePlayer = SingletonMonoBehaviour<BattleMain>.Instance.GetBattlePlayer(battle_status.RpgUnit);`
        var battlePlayerType = typeof(BattlePlayerProduction);
        int? battlePlayerLocalIndex = null;

        for (int i = 0; i < codes.Count - 1; i++)
        {
            // variable is written by a method GetBattlePlayer
            if ((codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt) &&
                codes[i].operand is MethodInfo mi &&
                mi.Name == "GetBattlePlayer" &&
                mi.DeclaringType == typeof(BattleMain))
            {
                var storeInstr = codes[i + 1];
                var index = TryGetStlocIndex(storeInstr);
                if (index.HasValue)
                {
                    battlePlayerLocalIndex = index.Value;
                    break;
                }
            }
        }

        // Variable not identified, return and cancel - this shouldn't happen unless the game is patched.
        if (battlePlayerLocalIndex == null)
        {
            Debug.LogError("Unable to find battlePlayer variable.");
            foreach (var code in codes)
                yield return code;
            yield break;
        }

        // The constant factor 0.1f will be replaced by a call to AdjustSpeedMultiplayer
        // Original C#: battle_status.RpgUnit.transform.position += vector * 0.1f * BattleSystem.GetAdjustElapseFrame();
        // New C# after patch: battle_status.RpgUnit.transform.position += vector * SpeedPatches.AdjustSpeedMultiplier(battlePlayer) * BattleSystem.GetAdjustElapseFrame();
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.1f)
            {
                // Load battleplay.
                yield return new CodeInstruction(OpCodes.Ldloc, battlePlayerLocalIndex.Value);

                // Call AdjustSpeedMultiplier
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(SpeedPatches), nameof(SpeedPatches.AdjustSpeedMultiplier)));
            }
            else
            {
                yield return codes[i];
            }
        }
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
    /// Access to private variable move_speed_ of FollowTarget is needed.
    /// </summary>
    private static readonly FieldInfo MOVE_SPEED_FIELD = typeof(FollowTarget).GetField("move_speed_", BindingFlags.NonPublic | BindingFlags.Instance);

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
                holder = new SpeedUidHolder { OriginalSpeed = (float)MOVE_SPEED_FIELD.GetValue(npc.NowAIData.Follow), CharacterId = c.GetModelID().GetModel() };
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

            MOVE_SPEED_FIELD.SetValue(npc.NowAIData.Follow, value);
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
    /// MapMovePointRoute are the routes of city NPCs. They refer to the unit which moves
    /// in the private field unit_base_ which needs to be available.
    /// </summary>
    private static readonly FieldInfo UNIT_FIELD_POINT_ROUTE = typeof(MapMovePointRoute).GetField("unit_base_", BindingFlags.NonPublic | BindingFlags.Instance);

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
            if (!(__instance is MapMovePointRoute i))
            {
                return;
            }

            var fieldVal = UNIT_FIELD_POINT_ROUTE.GetValue(i);
            if (!(fieldVal is MapUnitBaseComponent mubc))
            {
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
    [HarmonyPatch(typeof(MapUnitCollision), "SetMoveVector")]
    [HarmonyPrefix]
    static void Prefix(ref MapUnitCollision __instance, ref Vector3 move_vector)
    {
        if (__instance == null || move_vector == null)
        {
            return;
        }

        GameObject go = __instance.unit_base_.gameObject;
        if (go.GetComponentInChildren<DbModelChara>() is DbModelChara c) //Considerably fast, it is in the first child usually.
        {
            uint charId = c.GetModelID().GetModel();

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
