using System;
using System.Reflection;
using CwcdEsp.Utils;
using HarmonyLib;
using UnityEngine;

namespace CwcdEsp.Tracking
{
    /// <summary>
    /// 子弹方向修正（修复版：Hook GetFacingPoint Postfix 而非 Parse Prefix）。
    ///
    /// 根据反编译分析，最佳 Hook 点是 LogicControllerEquipTrigger.GetFacingPoint()，
    /// 它返回 ValueTuple&lt;Vector3, Vector3&gt; (from, to)，在子弹创建之前就决定瞄准方向。
    /// 修改返回值的 to → 子弹自动飞向目标，零开销。
    ///
    /// 但 ValueTuple 是 ref return，Harmony Postfix 难以直接修改。
    /// 改为 Postfix + ref __result 方式：需要用 Harmony 的 ref 参数修饰。
    /// </summary>
    public static class BulletTracker
    {
        private static float _lastLogTime = 0f;
        private static int _logCount = 0;

        /// <summary>注册子弹方向修正 Patch。</summary>
        public static void TryRegister(Harmony harmony, ref int success, ref int fail)
        {
            // 方案A：Hook GetFacingPoint（服务器端，在子弹创建前修正方向）
            // GetFacingPoint 返回 ValueTuple<Vector3, Vector3>，Postfix 用 ref __result 修改
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
        /// GetFacingPoint 的 Postfix：修改返回的 (from, to) 的 to 为目标位置。
        /// __result 是 ref ValueTuple&lt;Vector3, Vector3&gt;。
        /// </summary>
        public static void Postfix(ref object __result)
        {
            if (!EspConfig.BulletTrackingEnabled) return;

            var target = TargetSelector.CachedTarget;
            if (target == null || !target.IsValid) return;
            if (!TargetSelector.IsTargetAlive()) return;

            try
            {
                // __result 是 ValueTuple<Vector3, Vector3>
                // 用反射获取 Item1 (from) 和 Item2 (to)
                Type t = __result.GetType();
                var fromField = t.GetField("Item1");
                var toField = t.GetField("Item2");
                if (fromField == null || toField == null) return;

                Vector3 from = (Vector3)fromField.GetValue(__result);
                Vector3 targetPos = target.Position;

                // 角度筛选
                Vector3 aimDir = (Vector3)toField.GetValue(__result) - from;
                if (aimDir.sqrMagnitude < 1e-4f) return;
                aimDir.Normalize();

                Vector3 targetDir = targetPos - from;
                if (targetDir.sqrMagnitude < 1e-4f) return;
                targetDir.Normalize();

                if (Vector3.Angle(aimDir, targetDir) > EspConfig.TrackingAngle) return;

                // 修改 to
                toField.SetValue(__result, targetPos);

                // 限频日志
                if (_logCount < 5 || Time.realtimeSinceStartup - _lastLogTime > 10f)
                {
                    _lastLogTime = Time.realtimeSinceStartup;
                    _logCount++;
                    FileLogger.Info($"[BulletTracker] 修正瞄准方向: from={from} → target={targetPos} (ActorId={target.ActorId})");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("[BulletTracker] Postfix 异常: " + ex.Message);
            }
        }
    }
}
