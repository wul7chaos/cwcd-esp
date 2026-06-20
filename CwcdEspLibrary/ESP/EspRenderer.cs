using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 统一渲染调度（修复版：全部改用 GUI 绘制，不再使用 GL）。
    /// 由 Patch_OnGUI.Postfix 在 EventType.Repaint 时调用。
    /// 状态面板始终绘制，让用户知道 ESP 已加载。
    /// </summary>
    public static class EspRenderer
    {
        public static void DrawAll()
        {
            // 状态面板始终绘制（不受开关影响）
            try { StatusOverlay.Draw(); }
            catch (System.Exception e) { FileLogger.Error("StatusOverlay 异常: " + e.Message); }

            // 配置菜单（Insert 切换）
            try { ConfigMenu.Draw(); }
            catch (System.Exception e) { FileLogger.Error("ConfigMenu 异常: " + e.Message); }

            Camera cam = ScreenTools.Cam;

            bool boxOn = EspConfig.BoxEspEnabled;
            bool lootOn = EspConfig.LootEspEnabled;
            if (!boxOn && !lootOn) return;
            if (cam == null) return;

            // ===== 方框 + 血条（GUI 坐标系）=====
            if (boxOn)
            {
                try { BoxESP.DrawBoxes(); }
                catch (System.Exception e) { FileLogger.Error("BoxESP.DrawBoxes 异常: " + e.Message); }
            }

            // ===== 文字标签 =====
            // 敌人文本已注释，只保留方框
            // if (boxOn)
            // {
            //     try { BoxESP.DrawLabels(); }
            //     catch (System.Exception e) { FileLogger.Error("BoxESP.DrawLabels 异常: " + e.Message); }
            // }
            if (lootOn)
            {
                try { LootESP.DrawLabels(); }
                catch (System.Exception e) { FileLogger.Error("LootESP.DrawLabels 异常: " + e.Message); }
            }
        }
    }
}
