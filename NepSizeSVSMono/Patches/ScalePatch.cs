using CharaIK;
using HarmonyLib;
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
    /// Foot Liftup limit is private in a Mono context.
    /// </summary>
    private static FieldInfo FOOTIK_FOOTLIFTUPLIMIT = typeof(FootIK).GetField("footLiftupLimit_", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Foot Liftup is private and needs special reading.
    /// </summary>
    /// <param name="footIK"></param>
    /// <returns></returns>
    private static float ReadFootLiftupLimit(FootIK footIK)
    {
        return (float)(FOOTIK_FOOTLIFTUPLIMIT.GetValue(footIK));
    }

    /// <summary>
    /// Foot IK cache.
    /// </summary>
    private static ConditionalWeakTable<DbModelChara, FootIKDefaults> _footIKStatus = new ConditionalWeakTable<DbModelChara, FootIKDefaults>();

    /// <summary>
    /// component_model_base_object_manager_ is private in a Mono context.
    /// </summary>
    private static FieldInfo DB_MODEL_CHARA_COMPONENT_BASE_MANAGER = typeof(DbModelChara).GetField("component_model_base_object_manager_", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DbModelChara), "Update")]
    public static void DbModelBasePrefix(DbModelChara __instance)
    {
        if (__instance.GetModelID() == null || __instance.gameObject == null || !__instance.gameObject.activeInHierarchy || !__instance.enabled)
        {
            return;
        }

        uint mdlId = __instance.GetModelID().GetModel();

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

        DbModelBase.DbModelBaseObjectManager om = (DbModelBase.DbModelBaseObjectManager)DB_MODEL_CHARA_COMPONENT_BASE_MANAGER.GetValue(__instance); //Load her object manager

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
                    fUpDefault = ReadFootLiftupLimit(footIK)
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
