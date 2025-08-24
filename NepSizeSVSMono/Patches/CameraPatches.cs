using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class CameraPatches
{
    /// <summary>
    /// Private method InterpolationRun of DungeonCamera needs to be made accessible.
    /// </summary>
    private static readonly MethodInfo IntpRun = typeof(DungeonCamera).GetMethod("InterpolationRun", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Adjust camera height via its parameters.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="field_of_view_auto"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(DungeonCamera), "SetCameraParamDefault")]
    [HarmonyPrefix]
    public static bool OverruleSetCameraParamDefault(ref DungeonCamera __instance, ref bool field_of_view_auto)
    {
        // Or don't adjust it if the user has this disabled.
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustCamera)
        {
            return true;
        }

        // This method is based on a dnSpy decompile.

        // Get the scale of the player
        float scale = ScalePatch.ACTIVE_PLAYER_SCALE;

        IntpRun.Invoke(__instance, null);
        Vector3 offset = __instance.GetCameraLocalPosition();
        Vector3 scaledOffset = offset * 1.0f;

        Vector3 scaledVerticalOffset = Vector3.zero;

        float pushDistance = 0.5f;

        if (scale != 1.0f && scale > 0.0f) //Don't adjust it if the scale doesn't make sense (0 or negative) or is default.
        {
            scaledOffset *= scale;
            scaledVerticalOffset.y = (scale - 1.0f) * 1.2f; //1.2f is the default camera height of the game - 1.2 metres. It's consistent for all characters, no matter their height.

            pushDistance *= scale;
        }

        if (scale > 0.0f) // Adjust the offset.
        {
            scaledVerticalOffset.y += NepSizePlugin.Instance.ExtraSettings.CameraOffset * scale;
        }

        // Original: Vector3 scaled_camera_position = __instance.camera_set_.position_ + scaledOffset
        // We add a scale based offset.
        Vector3 scaled_camera_position = __instance.camera_set_.position_ + scaledOffset + scaledVerticalOffset;
        Vector3 original_camera_position = __instance.camera_set_.position_ + offset;

        // Usually collissionSetting is forced to true.
        bool collissionSetting = __instance.collision_use_;
        if (NepSizePlugin.Instance.ExtraSettings.DisableCameraCollission)
        {
            collissionSetting = false;
        }

        // Allow the game to calculate the camera position.
        Vector3 position = DungeonCamera.LineCollisionExtrusion(__instance.camera_set_.position_, scaled_camera_position, collissionSetting, pushDistance);

        // Original code below

        if (field_of_view_auto)
        {
            __instance.camera_set_.field_of_view_ = FieldOfViewAdjustment.GetFieldOfViewPosition(in original_camera_position, in __instance.camera_set_.position_);
        }

        __instance.SetCameraParam(position, __instance.camera_set_.rotation_ + __instance.local_.rotation_, __instance.camera_set_.field_of_view_);

        // Allow extreme angles if the user wishes so.

        if (NepSizePlugin.Instance.ExtraSettings.UnrestrictedCamera)
        {
            __instance.rotation_x_minimum_ = -720.0f;
            __instance.rotation_x_maximum_ = 720.0f;
        }

        // Suppress execution of original method.
        return false;
    }

    /**
     * Now we adjust camera distance, rotation and other aspects.
     */

    /**
     * The original method accesses a huge amount of private fields. In a Mono context we cannot bypass those, we must make them available first.
     */
    private static readonly FieldInfo MUB_MOVE_SPEED = typeof(MapUnitBaseComponent).GetField("move_speed_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_OLD_POSITION = typeof(MapUnitBaseComponent).GetField("old_position_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ALPHA_CAMERA_DIST = typeof(MapUnitBaseComponent).GetField("alpha_camera_distance_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ALPHA_CAMERA_BASE = typeof(MapUnitBaseComponent).GetField("alpha_base_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ROTATION_REQUEST = typeof(MapUnitBaseComponent).GetField("rotation_request_", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MUB_ROTATION_SPEED = typeof(MapUnitBaseComponent).GetField("rotation_speed_", BindingFlags.NonPublic | BindingFlags.Instance);

    /**
     * And also two private methods.
     */
    private static readonly MethodInfo MUB_RECOVERY_RUN = typeof(MapUnitBaseComponent).GetMethod("RecoveryRun", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo MUB_ROT_FRAME_RUN = typeof(MapUnitBaseComponent).GetMethod("RotationFrameRun", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// And now.. let's patch camera distance.
    /// </summary>
    /// <param name="__instance"></param>
    /// <returns></returns>
    [HarmonyPatch(typeof(MapUnitBaseComponent), "Update")]
    [HarmonyPrefix]
    public static bool OverrideMapUnitUpdate(ref MapUnitBaseComponent __instance)
    {
        // Return to original if disabled.
        if (!NepSizePlugin.Instance.ExtraSettings.AdjustCamera)
        {
            return true;
        }

        // Camera verification.
        if (!__instance.IsReady() || !((bool)MUB_RECOVERY_RUN.Invoke(__instance, null)))
        {
            return false;
        }

        // Original code start.
        if (__instance.delegate_move_ != null && !__instance.IsPause())
        {
            __instance.delegate_move_(__instance);
        }
        else
        {
            __instance.MoveVector(Vector3.zero, rotation_change_: false);
        }
        // Original code end.

        float scale = 1.0f;

        float ps = ScalePatch.ACTIVE_PLAYER_SCALE;
        if (ps > 0.0f && ps < 1.0f)
        {
            scale = ps;
        }

        // Original code continue (private method and variable access has been replaced).
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
        // Original code end.

        // Adjust distance.
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

        // Original code continue (private method and variable access has been replaced).
        MUB_ALPHA_CAMERA_DIST.SetValue(__instance, acd);
        float ab = (float)MUB_ALPHA_CAMERA_BASE.GetValue(__instance);

        if (__instance.delegate_set_alpha_ != null)
        {
            __instance.delegate_set_alpha_(__instance, ab * acd);
        }
        
        __instance.IconUpdate();

        // Don't execute original method.
        return false;
    }
}