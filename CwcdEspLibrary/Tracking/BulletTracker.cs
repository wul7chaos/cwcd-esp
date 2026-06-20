using System;
using CwcdEsp.Utils;
using HarmonyLib;
using UnityEngine;

namespace CwcdEsp.Tracking
{
    /// <summary>
    /// 子弹方向修正（Hook GetFacingPoint Postfix，强类型 ref ValueTuple）。
    ///
    /// GetFacingPoint 返回 ValueTuple&lt;Vector3, Vector3&gt; (from, to)，
    /// 修改 to → 子弹自动飞向目标。ESP 穿墙追踪，无需视线检测。
    /// </summary>
    public static class BulletTracker
    {
        public static void TryRegister(Harmony harmony, ref int success, ref int fail)
        {
            try
            {
                Type targetType = AccessTools.TypeByName("MorbidOptimism.Server.LogicControllerEquipTrigger");
                if (targetType == null)
                {
                    FileLogger.Warn("[BulletTracker] 类型 LogicControllerEquipTrigger 未找到，跳过");
                    fail++;
                    return;
                }

                var original = AccessTools.Method(targetType, "GetFacingPoint");
                if (original == null)
                {
                    FileLogger.Warn("[BulletTracker] 方法 GetFacingPoint 未找到，跳过");
                    fail++;
                    return;
                }

                var postfix = new HarmonyMethod(typeof(BulletTracker), nameof(Postfix));
                harmony.Patch(original, postfix: postfix);
                success++;
                FileLogger.Info("[BulletTracker] OK: Patch GetFacingPoint Postfix");
            }
            catch (Exception ex)
            {
                FileLogger.Error("[BulletTracker] 注册失败: " + ex.Message, ex);
                fail++;
            }
        }

        /// <summary>
        /// GetFacingPoint Postfix：修改返回的 (from, to) 的 to 为目标位置。
        /// 强类型 ref ValueTuple&lt;Vector3, Vector3&gt; 直接赋值，无装箱问题。
        /// </summary>
        public static void Postfix(ref ValueTuple<Vector3, Vector3> __result)
        {
            if (!EspConfig.BulletTrackingEnabled) return;

            var target = TargetSelector.CachedTarget;
            if (!target.IsValid) return;
            if (!TargetSelector.IsTargetAlive()) return;

            try
            {
                // 直接修正：TargetSelector 已用相机方向+距离筛选过目标
                __result.Item2 = target.Position;
            }
            catch (Exception ex)
            {
                FileLogger.Error("[BulletTracker] Postfix 异常: " + ex.Message);
            }
        }
    }
}
