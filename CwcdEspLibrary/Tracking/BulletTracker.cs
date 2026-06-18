using CwcdEsp.Utils;
using HarmonyLib;
using MorbidOptimism.Server;
using UnityEngine;

namespace CwcdEsp.Tracking
{
    /// <summary>
    /// 子弹方向修正（方案 6.3）。
    ///
    /// 实现说明（基于反编译源码确认）：
    /// - EquipSnapshot 是 public struct，from/to 均为 public 字段；
    /// - equipSnapshot 是 LogicActionEquipBase 的 public 字段；
    /// - LogicActionBulletBase.Parse 内部用 (equipSnapshot.to - equipSnapshot.from) 计算子弹飞行方向
    ///   (bullet.data.facing / rotation / moveDir 均由 to-from 派生)。
    /// 因此在 Parse 的 Prefix 中把 equipSnapshot.to 改为目标坐标（保持 from=枪口不变），
    /// 原方法随后的计算会自动让子弹飞向目标。这满足方案"不改 from、不改 FunctionalString"的约束。
    ///
    /// 注：方案 v3 建议改 Patch 调用方而非 Parse；本实现因 to 为 public 数值字段、修改安全可靠，
    ///     选择直接 Prefix Parse，并在注释中标注此偏离。若实测时序异常，可改 Patch 上层触发方法。
    /// </summary>
    public static class BulletTracker
    {
        /// <summary>注册子弹方向修正 Patch（Prefix）。</summary>
        public static void TryRegister(Harmony harmony, ref int success, ref int fail)
        {
            PatchGuard.TryPatch(
                harmony,
                "MorbidOptimism.Server.LogicActionBulletBase",
                "Parse",
                ref success,
                ref fail,
                prefixType: typeof(BulletTracker));
        }

        /// <summary>
        /// Parse 的 Prefix：在原方法计算速度向量前修正 to。
        /// 仅在追踪开启、有有效存活目标、且偏差角在吸附范围内时干预，否则不干预（保持原弹道）。
        /// </summary>
        public static void Prefix(LogicActionBulletBase __instance)
        {
            if (!EspConfig.BulletTrackingEnabled) return;

            var target = TargetSelector.CachedTarget;
            if (target == null || !target.IsValid) return;
            if (!TargetSelector.IsTargetAlive()) return; // 存活验证（方案 6.4）

            // equipSnapshot 为 public 字段，from=枪口（保持不变），to=目标点
            Vector3 muzzle = __instance.equipSnapshot.from;
            Vector3 targetPos = target.Position;

            // 角度筛选：原瞄准方向 vs 目标方向
            Vector3 aimDir = __instance.equipSnapshot.dir;       // (to-from).normalized，原瞄准
            Vector3 targetDir = (targetPos - muzzle);
            if (targetDir.sqrMagnitude < 1e-4f) return;
            targetDir.Normalize();

            if (Vector3.Angle(aimDir, targetDir) > EspConfig.TrackingAngle) return;

            // 修正 to，from 不变；原 Parse 随后用 (to-from) 计算飞行方向
            __instance.equipSnapshot.to = targetPos;
        }
    }
}
