using Battle.BattleAI.AIBase.AiBattleBase;
using Battle.BattleAI.AIBase.AIBattleBaseEnemy;
using Battle.BattleAI.AIBase.AIBattleBasePlayer;
using HarmonyLib;
using IF.Battle.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using static MapUnitBaseComponent;

public class ColliderUnitUpdate
{
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

        return v; // oder irgendetwas Dynamisches
    }

    /// <summary>
    /// Some storage method magic.
    /// </summary>
    /// <param name="instruction"></param>
    /// <returns></returns>
    private static int? TryGetStlocIndex(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Stloc_0) {
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

    [HarmonyPatch(typeof(MoveInputAccept), "Apply")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> TranspileMovementSpeed(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

        // Instance variable battlePlayer must be found.
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

        // Variable not identified, return.
        if (battlePlayerLocalIndex == null)
        {
            Debug.LogError("Unable to find battlePlayer variable.");
            foreach (var code in codes)
                yield return code;
            yield break;
        }

        // The constant factor 0.1f will be replaced by a call to 
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.1f)
            {
                // Load battleplay.
                yield return new CodeInstruction(OpCodes.Ldloc, battlePlayerLocalIndex.Value);

                // Call AdjustSpeedMultiplier
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(ColliderUnitUpdate), nameof(ColliderUnitUpdate.AdjustSpeedMultiplier)));
            }
            else
            {
                yield return codes[i];
            }
        }
    }

    internal class SpeedUidHolder
    {
        public float OriginalSpeed;
        public uint CharacterId;
    }

    private static readonly FieldInfo MOVE_SPEED_FIELD = typeof(FollowTarget).GetField("move_speed_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly ConditionalWeakTable<AIBattleBasePlayer, SpeedUidHolder> AIPLAYER_BACKUP = new();

    private static void PatchFollowTargetMoveSpeed(AIBattleBasePlayer aiPlayer, bool patch)
    {
        SpeedUidHolder holder = null;
        if (!AIPLAYER_BACKUP.TryGetValue(aiPlayer, out holder))
        {
            DbModelChara c = aiPlayer.OwnRpgUnit.gameObject.GetComponentInChildren<DbModelChara>();
            if (c != null)
            {
                holder = new SpeedUidHolder { OriginalSpeed = (float)MOVE_SPEED_FIELD.GetValue(aiPlayer.NowAIData.Follow), CharacterId = c.GetModelID().GetModel() };
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
            
            MOVE_SPEED_FIELD.SetValue(aiPlayer.NowAIData.Follow, value);
        }
    }

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ActionCommonMove")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAppropriateDistance")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromAlly")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "MoveAwayFromEnemy")]
    [HarmonyPatch(typeof(AIBattleBasePlayer), "ReturnBattleArea")]
    static void UnPatchAIMovespeed(ref AIBattleBasePlayer __instance)
    {
        if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
        {
            PatchFollowTargetMoveSpeed(__instance, false);
        }
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
                holder = new SpeedUidHolder { OriginalSpeed = (float)MOVE_SPEED_FIELD.GetValue(aiEnemy.NowAIData.Follow), CharacterId = c.GetModelID().GetModel() };
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

            MOVE_SPEED_FIELD.SetValue(aiEnemy.NowAIData.Follow, value);
        }
    }


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
            PatchFollowAITargetMoveSpeed(__instance, true);
        }
    }

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
            PatchFollowAITargetMoveSpeed(__instance, false);
        }
    }


    private static readonly FieldInfo UNIT_FIELD_POINT_ROUTE = typeof(MapMovePointRoute).GetField("unit_base_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly ConditionalWeakTable<PointRoutePlay, SpeedUidHolder> MAP_ROUTE_TO_SPEED = new();

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

    [HarmonyPatch(typeof(MapUnitCollision), "SetMoveVector")]
    [HarmonyPrefix]
    static void Prefix(ref MapUnitCollision __instance, ref Vector3 move_vector)
    {       
        if (__instance == null || move_vector == null)
        {
            return;
        }

        BattleInputStatus bpp = new BattleInputStatus();
        bpp.Update();

        GameObject go = __instance.unit_base_.gameObject;
        if (go.GetComponentInChildren<DbModelChara>() is DbModelChara c) {
            uint charId = c.GetModelID().GetModel();

            float? f = NepSizePlugin.Instance.FetchScale(charId);
            if (f != null)
            {
                if (charId == NepSizePlugin.Instance.PlayerModelId)
                {
                    if (NepSizePlugin.Instance.ExtraSettings.AdjustSpeedPlayer)
                    {
                        move_vector *= f.Value;
                    }
                } 
                else if(NepSizePlugin.Instance.ExtraSettings.AdjustSpeedNPC)
                {
                    move_vector *= f.Value;
                }                
            }
        }
    }

    /*[HarmonyPatch(typeof(DungeonCamera), "GetCameraLocalPosition")]
    [HarmonyPostfix]
    static void PostfixLocalCameraPosition(ref DungeonCamera __instance, ref Vector3 __result)
    {
        float s = NepSizePlugin.Instance.PlayerScale;

        if (s != 1.0f && s != 0.0f) {
            __result *= s;

            float camHeight = __instance.now_.position_.y;
            float adjHeight = camHeight * s;

            __result.y += (adjHeight - camHeight);
        }        
    }*/

    private static readonly MethodInfo IntpRun = typeof(DungeonCamera).GetMethod("InterpolationRun", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(DungeonCamera), "SetCameraParamDefault")]
    [HarmonyPrefix]
    public static bool OverruleSetCameraParamDefault(ref DungeonCamera __instance, ref bool field_of_view_auto)
    {
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustCamera)
        {
            return true;
        }

        float s = NepSizePlugin.Instance.PlayerScale;

        IntpRun.Invoke(__instance, null);
        Vector3 offset = __instance.GetCameraLocalPosition();
        Vector3 scaledOffset = offset * 1.0f;

        Vector3 scaledVerticalOffset = Vector3.zero;

        float pushDistance = 0.5f;

        if (s != 1.0f && s != 0.0f)
        {
            scaledOffset *= s;
            scaledVerticalOffset.y = (s - 1.0f) * 1.2f;            

            pushDistance *= s;
        }

        if (s != 0.0f)
        {
            scaledVerticalOffset.y += NepSizePlugin.Instance.ExtraSettings.CameraOffset * s;
        }

        Vector3 scaled_camera_position = __instance.camera_set_.position_ + scaledOffset + scaledVerticalOffset;
        Vector3 original_camera_position = __instance.camera_set_.position_ + offset;

        bool collissionSetting = __instance.collision_use_;
        if (NepSizePlugin.Instance.ExtraSettings.DisableCameraCollission)
        {
            collissionSetting = false;
        }

        Vector3 position = DungeonCamera.LineCollisionExtrusion(__instance.camera_set_.position_, scaled_camera_position, collissionSetting, pushDistance);
        
        if (field_of_view_auto)
        {
            __instance.camera_set_.field_of_view_ = FieldOfViewAdjustment.GetFieldOfViewPosition(in original_camera_position, in __instance.camera_set_.position_);
        }
        
        __instance.SetCameraParam(position, __instance.camera_set_.rotation_ + __instance.local_.rotation_, __instance.camera_set_.field_of_view_);

        if (NepSizePlugin.Instance.ExtraSettings.UnrestrictedCamera)
        {
            __instance.rotation_x_minimum_ = -720.0f;
            __instance.rotation_x_maximum_ = 720.0f;
        }
        

        return false;
    }

    /*[HarmonyPatch(typeof(MapUnitBaseComponent), "GetHeight")]
    [HarmonyPrefix]
    public static bool OverrideGetHeight(ref MapUnitBaseComponent __instance, ref float __result)
    {
        __result = 1.5f;
    
        DbModelChara dbc = __instance.gameObject.GetComponentInChildren<DbModelChara>();
        if (dbc != null)
        {
            uint? mdlId = dbc.GetModelID()?.GetModel();
            if (mdlId != null)
            {
                float? f = NepSizePlugin.Instance.FetchScale(mdlId.Value);
                if (f != null)
                {
                    __result *= f.Value;
                }
            }
        }

        return false;
    }*/

    private static readonly FieldInfo MUB_MOVE_SPEED = typeof(MapUnitBaseComponent).GetField("move_speed_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_OLD_POSITION = typeof(MapUnitBaseComponent).GetField("old_position_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ALPHA_CAMERA_DIST = typeof(MapUnitBaseComponent).GetField("alpha_camera_distance_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ALPHA_CAMERA_BASE = typeof(MapUnitBaseComponent).GetField("alpha_base_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ROTATION_REQUEST = typeof(MapUnitBaseComponent).GetField("rotation_request_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ROTATION_SPEED = typeof(MapUnitBaseComponent).GetField("rotation_speed_", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo MUB_RECOVERY_RUN = typeof(MapUnitBaseComponent).GetMethod("RecoveryRun", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo MUB_ROT_FRAME_RUN = typeof(MapUnitBaseComponent).GetMethod("RotationFrameRun", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPatch(typeof(MapUnitBaseComponent), "Update")]
    [HarmonyPrefix]
    public static bool OverrideMapUnitUpdate(ref MapUnitBaseComponent __instance)
    {
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustCamera)
        {
            return true;
        }

        if (!__instance.IsReady() || !((bool)MUB_RECOVERY_RUN.Invoke(__instance, null)))
        {
            return false;
        }

        if (__instance.delegate_move_ != null && !__instance.IsPause())
        {
            __instance.delegate_move_(__instance);
        }
        else
        {
            __instance.MoveVector(Vector3.zero, rotation_change_: false);
        }

        float scale = 1.0f;
        
        float ps = NepSizePlugin.Instance.PlayerScale;
        if (ps > 0.0f && ps < 1.0f)
        {
            scale = ps;
        }

        Vector3 position_ = __instance.GetPosition();
        float ms = (float)MUB_MOVE_SPEED.GetValue(__instance);
        Vector3 op = (Vector3)MUB_OLD_POSITION.GetValue(__instance);
        ms += GeometryUtility.GetDistanceXZ(in op, in position_);
        ms *= 0.5f;
        MUB_MOVE_SPEED.SetValue(__instance, ms);
        MUB_OLD_POSITION.SetValue(__instance, position_);        
        if (!__instance.IsPause())
        {
            Quaternion rr = (Quaternion)MUB_ROTATION_REQUEST.GetValue(__instance);
            float rs = (float)MUB_ROTATION_SPEED.GetValue(__instance);
            __instance.transform.localRotation = (Quaternion)MUB_ROT_FRAME_RUN.Invoke(__instance, new object[] {
                __instance.transform.localRotation, rr, rs * GameTime.DeltaTime
            });
            //RotationFrameRun(base.transform.localRotation, rotation_request_, rotation_speed_ * GameTime.DeltaTime);
            __instance.AnimationFrameRun();
        }

        float distance = MGS_GM.GetDistance(__instance.GetPositionCenter(), SystemCamera3D.GetCamera().transform.position);
        distance -= (__instance.GetRadius() * scale);

        float acd = (float)MUB_ALPHA_CAMERA_DIST.GetValue(__instance);

        if (distance < 0f)
        {
            acd = 0f;
        }
        else if (distance < (0.5f * scale))
        {
            acd -= GameTime.DeltaTime * 3f;
            if (acd < 0f)
            {
                acd = 0f;
            }
        }
        else
        {
            acd += GameTime.DeltaTime * 3f;
            if (acd > 1f)
            {
                acd = 1f;
            }
        }

        MUB_ALPHA_CAMERA_DIST.SetValue(__instance, acd);
        float ab = (float)MUB_ALPHA_CAMERA_BASE.GetValue(__instance);

        if (__instance.delegate_set_alpha_ != null)
        {
            __instance.delegate_set_alpha_(__instance, ab * acd);
        }

        __instance.IconUpdate();

        return false;
    }
}
