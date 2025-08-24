using CharaIK;
using HarmonyLib;
using MagicaCloth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Il2CppInterop;
using Il2CppInterop.Runtime;

namespace NepSizeNepRiders
{
    public static class ScaleDbModelChara
    {
        /// <summary>
        /// Check if a parent object of the GameObject go has a component of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        public static T GetComponentInParentChain<T>(GameObject go) where T : Component
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
        /// Foot IK offsets.
        /// </summary>
        //private static Dictionary<uint, float> _footIKOffsets = new Dictionary<uint, float>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DbModelChara), "OnEarlyUpdate")]
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

            float scale = 1.0f;

            if (!NepSizePlugin.Instance.SizeMemoryStorage.SizeValues.TryGetValue(mdlId, out scale))
            {
                return;
            }
            
            DbModelBase.DbModelBaseObjectManager om = __instance.component_model_base_object_manager_; //Load her object manager
            if (om != null)
            {
                DbModelVehicle v = GetComponentInParentChain<DbModelVehicle>(__instance.gameObject); // Should be not the slowest method fortunately
                if (v != null)
                {
                    // On vechicle
                    if (v.transform.localScale.x != scale || om.transform.localScale.x != 1.0f)
                    {
                        om.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                        v.gameObject.transform.localScale = new Vector3(scale, scale, scale);
                    }
                }
                else
                {
                    // Not on vehicle
                    if (om.transform.localScale.x != scale)
                    {
                        om.transform.localScale = new Vector3(scale, scale, scale);
                    }                    
                }
            }

            /**
            // Unecessary in Nep Riders. It does not make use FootIK. Leaving here to reenable should people find instances where it does.
            FootIK footIK = instance.transform.GetComponentInChildren<FootIK>();
            if (footIK != null)
            {
                if (!_footIKOffsets.ContainsKey(mdlId))
                {
                    _footIKOffsets.Add(mdlId, footIK.put_offset_.y);
                }
                footIK.put_offset_ = new Vector3(footIK.put_offset_.x, scale * _footIKOffsets[mdlId], footIK.put_offset_.z);
            }
            */
        }
    }
}
