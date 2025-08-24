using CharaIK;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static System.Net.WebRequestMethods;

/// <summary>
/// Handles the actual scaling of the characters.
/// </summary>
public class ScalePatch
{
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
    private static ConditionalWeakTable<DbModelChara, FootIKDefaults> _footIKStatus = new ConditionalWeakTable<DbModelChara, FootIKDefaults>();       

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

        float? scaleParameter = NepSizePlugin.Instance.FetchScale(mdlId);
        if (scaleParameter == null)
        {
            return;
        }

        float scale = scaleParameter.Value;

        DbModelBase.DbModelBaseObjectManager om = __instance.component_model_base_object_manager_; //Load her object manager

        if (om != null && om.transform.localScale.x != scale)
        {
            om.transform.localScale = new Vector3(scale, scale, scale);            
        }

        FootIKDefaults footIKDefaults = null;

        if (!_footIKStatus.TryGetValue(__instance, out footIKDefaults))
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

