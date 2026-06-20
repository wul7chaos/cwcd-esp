using CwcdEsp.Esp;
using UnityEngine;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch GameController.OnGUI —— 仅 Postfix（方案 v3 修复 #12）。
    /// 不在 Prefix 中拦截原生事件，只在 Postfix 内用 Event.current.type == Repaint 过滤，
    /// 守卫范围仅限本 Postfix，不影响游戏原生 UI。
    ///
    /// 注意：ConfigMenu 使用 GUILayout（两阶段：Layout + Repaint），必须在所有事件中调用，
    /// 否则 GUILayout 缺少 Layout 阶段的布局信息会导致窗口内容为空、拖拽失效。
    /// 其余 ESP 绘制（BoxESP/LootESP/StatusOverlay 用 GUI.DrawTexture/GUI.Label）只在 Repaint 调用。
    /// </summary>
    public static class Patch_OnGUI
    {
        public static void Postfix()
        {
            Event ev = Event.current;
            if (ev == null) return;

            // 配置菜单（GUILayout 交互式 UI）：Layout + Repaint + MouseDrag 都需要调用
            try
            {
                ConfigMenu.Draw();
            }
            catch (System.Exception e)
            {
                EntryPoint.Log("ConfigMenu 异常: " + e.Message);
            }

            // 仅在 Repaint 帧绘制其余 ESP 内容（OnGUI 每帧被调用多次：Layout/MouseMove/Repaint…）
            if (ev.type != EventType.Repaint) return;

            try
            {
                EspRenderer.DrawAll();
            }
            catch (System.Exception e)
            {
                EntryPoint.Log("OnGUI Postfix 绘制异常: " + e.Message);
            }
        }
    }
}
