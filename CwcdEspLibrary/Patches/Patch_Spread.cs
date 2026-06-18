using CwcdEsp;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch LogicControllerEquipReload.GetSpreadAngle —— 散布归零（方案 6.2 / Patch A）。
    /// Postfix 直接 ref __result = 0f，保证所有子弹指向准星中心。
    /// 仅在子弹追踪开启时生效（追踪关闭时恢复原生散布）。
    /// </summary>
    public static class Patch_Spread
    {
        public static void Postfix(ref float __result)
        {
            if (EspConfig.BulletTrackingEnabled)
            {
                __result = 0f;
            }
        }
    }
}
