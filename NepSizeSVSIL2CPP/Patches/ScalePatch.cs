using CharaIK;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ScalePatch
{
    /* 
     * Shared variables with other patches.
     */

    /// <summary>
    /// The model ID of the currently active player.
    /// </summary>
    public static uint ACTIVE_PLAYER_UID = 0;

    /// <summary>
    /// The scale of the current player.
    /// </summary>
    public static float ACTIVE_PLAYER_SCALE = 1.0f;

    /// <summary>
    /// Check if a parent object of the GameObject go has a component of type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="child"></param>
    /// <returns></returns>
    private static T GetComponentInParentChain<T>(GameObject go) where T : Component
    {
        Transform current = go.transform.parent;

        while (current != null)
        {
            T component = current.GetComponent<T>();
            if (component != null)
            {
                return component;
            }
            current = current.parent;
        }

        return null;
    }

    /// <summary>
    /// Foot IK cache object.
    /// </summary>
    internal class FootIKDefaults
    {
        public FootIK footIK;
        public float fPutDefault;
        public float fUpDefault;
    }    

    /// <summary>
    /// Foot IK cache.
    /// </summary>
    private static Dictionary<uint, FootIKDefaults> _footIKStatus = new Dictionary<uint, FootIKDefaults>();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DbModelChara), "Update")]
    public static void DbModelBasePrefix(DbModelChara __instance)
    {
        if (__instance.model_id_ == null || __instance.gameObject == null || !__instance.gameObject.activeInHierarchy || !__instance.enabled)
        {
            return;
        }

        uint mdlId = __instance.model_id_.model_;

        if (mdlId == 0) //Character not fully loaded yet.
        {
            return;
        }

        NepSizePlugin.Instance.MarkCharacterIdActive(mdlId);

        bool isPlayer = (GetComponentInParentChain<MapUnitTypePlayerComponent>(__instance.gameObject) != null);
        if (isPlayer)
        {
            ACTIVE_PLAYER_UID = mdlId;
        }

        float? scaleParameter = NepSizePlugin.Instance.FetchScale(mdlId);
        if (scaleParameter == null)
        {
            return;
        }

        float scale = scaleParameter.Value;

        if (isPlayer)
        {
            ACTIVE_PLAYER_SCALE = scale;
        }

        DbModelBase.DbModelBaseObjectManager om = __instance.component_model_base_object_manager_; //Load her object manager

        if (om != null && om.transform.localScale.x != scale)
        {
            om.transform.localScale = new Vector3(scale, scale, scale);
        }

        FootIKDefaults footIKDefaults = null;

        if (!_footIKStatus.TryGetValue(mdlId, out footIKDefaults))
        {
            FootIK footIK = __instance.transform.GetComponentInChildren<FootIK>(); //This will run a few times, once a character is initialised it WILL have a FootIK object.

            if (footIK != null)
            {
                footIKDefaults = new FootIKDefaults()
                {
                    footIK = footIK,
                    fPutDefault = footIK.putOffset_.y,
                    fUpDefault = footIK.footLiftupLimit_
                };
                _footIKStatus.Add(mdlId, footIKDefaults);
            }
        }

        if (footIKDefaults != null)
        {
            float expectedPut = scale * footIKDefaults.fPutDefault;
            
            if (footIKDefaults.footIK.putOffset_.y != expectedPut)
            {
                footIKDefaults.footIK.putOffset_ = new Vector3(footIKDefaults.footIK.putOffset_.x, expectedPut, footIKDefaults.footIK.putOffset_.z);
                footIKDefaults.footIK.SetFootLiftupLimit(scale * footIKDefaults.fUpDefault);
            }
        }
    }
}
