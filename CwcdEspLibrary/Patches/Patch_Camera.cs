using CwcdEsp.Esp;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch CamController —— 缓存 camMain 引用（方案 Phase2）。
    /// 注意：反编译源码中 CamController 无 Start()，但有 LateUpdate()；
    /// 故改 Patch LateUpdate Postfix 主动刷新相机缓存（ScreenTools 也有懒加载兜底）。
    /// camMain 为 public 字段，直接读取。
    /// </summary>
    public static class Patch_Camera
    {
        public static void Postfix(CamController __instance)
        {
            if (__instance == null) return;
            if (__instance.camMain != null)
            {
                ScreenTools.SetCamera(__instance.camMain);
            }
        }
    }
}
