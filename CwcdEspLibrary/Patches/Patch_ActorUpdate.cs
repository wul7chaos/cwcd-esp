using CwcdEsp.Data;
using CwcdEsp.Tracking;
using CwcdEsp.Utils;
using MorbidOptimism.Client;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch ActorViewerManager.Update —— Postfix（方案 3.3 数据流向 / v3 双缓冲）。
    /// 主线程每帧执行：填充写缓冲 → 指针交换 → 物资脏标记刷新 → 目标选择 → 热键轮询。
    /// </summary>
    public static class Patch_ActorUpdate
    {
        public static void Postfix(ActorViewerManager __instance)
        {
            try
            {
                // 1. 热键轮询（主线程）
                HotkeyManager.Update();

                // 2. 敌人缓存：写缓冲填充
                EnemyCache.Instance.Update(__instance);

                // 3. 双缓冲指针交换（主线程末尾，O(1)）
                EnemyCache.Instance.SwapBuffers();

                // 4. 物资：仅刷新脏 Actor + 重建绘制快照
                LootCache.Instance.UpdateDirtyActors();
                LootCache.Instance.RebuildSnapshot();

                // 5. 目标选择（O(N)，含降频视线检测）
                TargetSelector.Update();
            }
            catch (System.Exception e)
            {
                EntryPoint.Log("ActorUpdate Postfix 异常: " + e.Message);
            }
        }
    }
}
