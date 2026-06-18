using CwcdEsp.Data;
using MorbidOptimism.Common;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch ItemContainer_Grid.PutItem / _RemoveItem —— 逐 Actor 脏标记（方案 5.1 / v3 修复 #14）。
    /// 容器变动时只标记该容器所属 Actor 为脏，避免全局全量扫描。
    /// ItemContainer_Grid.keeper 即拥有该容器的 ActorId（public int）。
    /// </summary>
    public static class Patch_ItemContainer
    {
        public static void Postfix(ItemContainer_Grid __instance)
        {
            if (__instance == null) return;
            try
            {
                int actorId = __instance.keeper;
                if (actorId > 0)
                {
                    LootCache.Instance.MarkDirty(actorId);
                }
            }
            catch
            {
                // 容器变动频繁，单次标记失败不影响整体
            }
        }
    }
}
