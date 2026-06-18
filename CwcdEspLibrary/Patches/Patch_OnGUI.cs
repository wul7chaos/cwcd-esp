using CwcdEsp.Esp;
using UnityEngine;

namespace CwcdEsp.Patches
{
    /// <summary>
    /// Patch GameController.OnGUI —— 仅 Postfix（方案 v3 修复 #12）。
    /// 不在 Prefix 中拦截原生事件，只在 Postfix 内用 Event.current.type == Repaint 过滤，
    /// 守卫范围仅限本 Postfix，不影响游戏原生 UI。
    /// </summary>
    public static class Patch_OnGUI
    {
        public static void Postfix()
        {
            // 仅在 Repaint 帧绘制（OnGUI 每帧被调用多次：Layout/MouseMove/Repaint…）
            Event ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint) return;

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
