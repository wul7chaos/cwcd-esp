using CwcdEsp.Data;
using MorbidOptimism.Server;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch LogicControllerActorAI.Update —— Postfix。
    /// 读取 this.focusTarget（public ActorController）判断敌人是否发现/锁定玩家：
    ///   focusTarget == null → AiState.Idle（未发现，绿色虚线方框）
    ///   focusTarget != null → AiState.Combat（发现/攻击，红色实线方框）
    /// 写入 AiStateCache，供 EnemyCache.Update 读取。
    ///
    /// 类型说明：
    ///   LogicControllerActorAI 继承 LogicControllerActor，后者有 public ActorController actorController。
    ///   ActorController.data (ActorData) 有 public int id。
    ///   focusTarget 在 LogicControllerActorAI 上为 public ActorController。
    /// </summary>
    public static class Patch_AiUpdate
    {
        public static void Postfix(LogicControllerActorAI __instance)
        {
            try
            {
                if (__instance == null) return;
                ActorController ac = __instance.actorController;
                if (ac == null || ac.data == null) return;

                int actorId = ac.data.id;
                AiState state = __instance.focusTarget != null ? AiState.Combat : AiState.Idle;
                AiStateCache.Set(actorId, state);
            }
            catch
            {
                // AI 更新时序异常时忽略，不影响其他模块
            }
        }
    }
}
