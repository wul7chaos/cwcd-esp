using CwcdEsp.Tracking;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// 子弹飞行修正（方案 6.1 Patch C，可选）—— 针对 LogicActionPJT.Update 的 Prefix。
    ///
    /// 说明：
    /// - 主要的方向修正在 Parse 阶段完成（见 <see cref="BulletTracker"/>，已注册）。
    /// - 本 Patch 为可选的"飞行中微调"，每帧在碰撞检测前再次把子弹朝目标修正，增加追踪感。
    /// - LogicActionPJT.Update 内部已含命中吸附逻辑（equipSnapshot.to = from + dir*dist），
    ///   飞行微调需谨慎读取 move/lastPos，避免与原生命中冲突。
    /// - 默认【未注册】（EntryPoint 中注释），需要时取消注释并在 EntryPoint 注册。
    /// </summary>
    public static class Patch_Bullet
    {
        public static void Prefix(object __instance)
        {
            if (!EspConfig.BulletTrackingEnabled) return;

            var target = TargetSelector.CachedTarget;
            if (target == null || !target.IsValid) return;
            if (!TargetSelector.IsTargetAlive()) return;

            // TODO: 实现飞行微调（可选）。
            // 思路：通过 Traverse(__instance) 读取 bullet/move/lastPos，
            //   将 bullet.data.position 或 move.moveData 的方向朝 target.Position 微调。
            //   注意不要破坏原生 _UpdateHits / 命中吸附流程。
        }
    }
}
