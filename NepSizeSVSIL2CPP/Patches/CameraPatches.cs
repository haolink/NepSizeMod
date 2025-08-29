using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class CameraPatches
{
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

        __instance.InterpolationRun();
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
            Vector3 csetPosition = __instance.camera_set_.position_;
            __instance.camera_set_.field_of_view_ = FieldOfViewAdjustment.GetFieldOfViewPosition(ref original_camera_position, ref csetPosition);
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
        if (!__instance.IsReady() || !(__instance.RecoveryRun()))
        {
            return false;
        }

        // Original code start.
        if (__instance.delegate_move_ != null && !__instance.IsPause())
        {
            __instance.delegate_move_.Invoke(__instance);
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
        float ms = __instance.move_speed_;
        Vector3 op = __instance.old_position_;
        ms += GeometryUtility.GetDistanceXZ(ref op, ref position_);
        ms *= 0.5f;
        __instance.move_speed_ = ms;
        __instance.old_position_ = position_;
        
        if (!__instance.IsPause())
        {
            Quaternion rr = __instance.rotation_request_;
            float rs = __instance.rotation_speed_;
            __instance.transform.localRotation = __instance.RotationFrameRun(__instance.transform.localRotation, rr, rs * GameTime.DeltaTime);
            //RotationFrameRun(base.transform.localRotation, rotation_request_, rotation_speed_ * GameTime.DeltaTime);
            __instance.AnimationFrameRun();
        }

        float distance = MGS_GM.GetDistance(__instance.GetPositionCenter(), SystemCamera3D.GetCamera().transform.position);
        // Original code end.

        // Adjust distance.
        distance -= (__instance.GetRadius() * scale);

        float acd = __instance.alpha_camera_distance_;

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
        __instance.alpha_camera_distance_ = acd;

        float ab = __instance.alpha_base_;//  (float) MUB_ALPHA_CAMERA_BASE.GetValue(__instance);

        if (__instance.delegate_set_alpha_ != null)
        {
            __instance.delegate_set_alpha_.Invoke(__instance, ab * acd);
            //__instance.delegate_set_alpha_.Invoke(__instance, ab * acd);
        }
        
        __instance.IconUpdate();

        // Don't execute original method.
        return false;
    }
}