using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 统一渲染调度（方案 3.3 数据流向）。
    /// 由 Patch_OnGUI.Postfix 在 EventType.Repaint 时调用。
    /// GL 趟（方框/血条）与 Label 趟（文字）分离，避免矩阵混用。
    /// 状态面板始终绘制，让用户知道 ESP 已加载。
    /// </summary>
    public static class EspRenderer
    {
        public static void DrawAll()
        {
            // 状态面板始终绘制（不受开关影响）
            try { StatusOverlay.Draw(); }
            catch (System.Exception e) { FileLogger.Error("StatusOverlay 异常: " + e.Message); }

            Camera cam = ScreenTools.Cam;

            bool boxOn = EspConfig.BoxEspEnabled;
            bool lootOn = EspConfig.LootEspEnabled;
            if (!boxOn && !lootOn) return;
            if (cam == null) return;

            // ===== GL 趟：方框 + 血条 =====
            if (boxOn)
            {
                GlDrawHelper.BeginGlContext();
                try { BoxESP.DrawGl(); }
                catch (System.Exception e) { FileLogger.Error("BoxESP.DrawGl 异常: " + e.Message); }
                GlDrawHelper.EndGlContext();
            }

            // ===== Label 趟：文字（GUI 矩阵独立）=====
            if (boxOn)
            {
                try { BoxESP.DrawLabels(); }
                catch (System.Exception e) { FileLogger.Error("BoxESP.DrawLabels 异常: " + e.Message); }
            }
            if (lootOn)
            {
                try { LootESP.DrawLabels(); }
                catch (System.Exception e) { FileLogger.Error("LootESP.DrawLabels 异常: " + e.Message); }
            }
        }
    }
}
