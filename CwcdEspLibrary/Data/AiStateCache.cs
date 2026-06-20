using System.Collections.Generic;

namespace CwcdEsp.Data
{
    /// <summary>
    /// 敌人 AI 状态缓存（按 actorId 索引）。
    /// 由 Patch_AiUpdate（Patch LogicControllerActorAI.Update Postfix）每帧写入：
    ///   focusTarget == null → Idle（未发现玩家）
    ///   focusTarget != null → Combat（发现/锁定/攻击玩家）
    /// 由 EnemyCache.Update 读取，填入 EnemyData.AiState 供 BoxESP 绘制。
    ///
    /// 说明：不做主动清除。EnemyCache.Update 只缓存存活敌人（actor.dead 过滤），
    /// 已死亡敌人的残留状态不会被读取；单局敌人数量有限，内存可接受。
    /// 若某敌人未被本帧 Patch 写入（非 AI 敌人/AI 未运行），Get 返回 Unknown，
    /// BoxESP 按 Idle（绿色虚线）处理。
    /// </summary>
    public static class AiStateCache
    {
        private static readonly Dictionary<int, AiState> _states = new Dictionary<int, AiState>(128);

        /// <summary>设置某敌人的 AI 状态（由 Patch_AiUpdate 调用）。</summary>
        public static void Set(int actorId, AiState state)
        {
            if (actorId <= 0) return;
            _states[actorId] = state;
        }

        /// <summary>读取某敌人的 AI 状态（由 EnemyCache.Update 调用）。</summary>
        public static AiState Get(int actorId)
        {
            AiState s;
            return _states.TryGetValue(actorId, out s) ? s : AiState.Unknown;
        }

        public static int Count => _states.Count;
    }
}
