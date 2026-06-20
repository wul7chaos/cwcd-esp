using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 状态面板（左侧居中竖向排列）。
    /// 永远绘制（不受 F6/F7/F8 影响），让用户知道 ESP 是否注入成功。
    /// 显示：标题、各功能开关状态、敌人/物资计数、热键提示。
    /// </summary>
    public static class StatusOverlay
    {
        private static readonly Color BgColor = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color TitleColor = new Color(0.3f, 1f, 0.5f, 1f);
        private static readonly Color OnColor = new Color(0.3f, 1f, 0.5f, 1f);
        private static readonly Color OffColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color DimColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        public static void Draw()
        {
            if (!EspConfig.OverlayVisible) return;

            float x = 10f;
            float lineH = 18f;
            float w = 170f;

            int enemyCount = 0;
            int lootCount = 0;
            try { enemyCount = EnemyCache.Instance.GetReadBuffer()?.Count ?? 0; } catch { }
            try { lootCount = LootCache.Instance.GetSnapshot()?.Count ?? 0; } catch { }

            // 标题(1) + 3个功能开关 + 敌人物资(1) + 热键提示(1) = 6 行
            int lines = 6;
            float h = lines * lineH + 12f;

            // 左侧垂直居中
            float y = (Screen.height - h) * 0.5f;
            if (y < 10f) y = 10f;

            // 半透明背景
            DrawBgRect(x, y, w, h);

            float cy = y + 6f;
            DrawText(x + 8, cy, "CWCD-ESP v4", TitleColor, true); cy += lineH;
            DrawToggleLine(x + 8, cy, "方框透视", EspConfig.BoxEspEnabled); cy += lineH;
            DrawToggleLine(x + 8, cy, "物资透视", EspConfig.LootEspEnabled); cy += lineH;
            DrawToggleLine(x + 8, cy, "子弹追踪", EspConfig.BulletTrackingEnabled); cy += lineH;
            DrawText(x + 8, cy, $"敌人 {enemyCount}  物资 {lootCount}", DimColor, false); cy += lineH;
            DrawText(x + 8, cy, "Home 面板 · Insert 配置", DimColor, false); cy += lineH;
        }

        /// <summary>绘制 "名称 已开启/已关闭" 格式的开关行，颜色随状态变化。</summary>
        private static void DrawToggleLine(float x, float y, string name, bool on)
        {
            string state = on ? "已开启" : "已关闭";
            Color c = on ? OnColor : OffColor;
            DrawText(x, y, $"{name}  {state}", c, false);
        }

        private static void DrawBgRect(float x, float y, float w, float h)
        {
            Texture2D tex = GetWhiteTex();
            Color prevColor = GUI.color;
            GUI.color = BgColor;
            GUI.DrawTexture(new Rect(x, y, w, h), tex);
            GUI.color = prevColor;
        }

        private static void DrawText(float x, float y, string text, Color color, bool bold)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.normal.textColor = color;

            // 阴影
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(x + 1, y + 1, 300, 18), text, shadowStyle);
            GUI.Label(new Rect(x, y, 300, 18), text, style);
        }

        private static Texture2D _whiteTex;
        private static Texture2D GetWhiteTex()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
                _whiteTex.hideFlags = HideFlags.HideAndDontSave;
            }
            return _whiteTex;
        }
    }
}
