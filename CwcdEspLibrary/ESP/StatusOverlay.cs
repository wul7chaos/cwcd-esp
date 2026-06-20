using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 屏幕左上角状态面板（方案 10.1 可视化调试面板）。
    /// 永远绘制（不受 F6/F7/F8 影响），让用户知道 ESP 是否注入成功。
    /// 显示：标题、各 Patch 注册状态、敌人/物资计数、功能开关状态、热键提示。
    /// </summary>
    public static class StatusOverlay
    {
        // 由 EntryPoint 在注册完成后写入
        public static int PatchSuccess;
        public static int PatchFail;
        public static string PatchDetails = "";

        private static readonly Color BgColor = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        private static readonly Color TitleColor = new Color(0.3f, 1f, 0.5f, 1f);
        private static readonly Color OkColor = new Color(0.3f, 1f, 0.5f, 1f);
        private static readonly Color FailColor = new Color(1f, 0.3f, 0.3f, 1f);
        private static readonly Color DimColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        public static void Draw()
        {
            if (!EspConfig.OverlayVisible) return;

            float x = 10f;
            float y = 10f;
            float w = 220f;
            float lineH = 16f;

            int enemyCount = 0;
            int lootCount = 0;
            try { enemyCount = EnemyCache.Instance.GetReadBuffer()?.Count ?? 0; } catch { }
            try { lootCount = LootCache.Instance.GetSnapshot()?.Count ?? 0; } catch { }

            // 计算面板高度
            int lines = 4;
            float h = lines * lineH + 12f;

            // 半透明背景
            DrawBgRect(x, y, w, h);

            float cy = y + 6f;
            DrawText(x + 8, cy, "CWCD-ESP v4  [已加载]", TitleColor, true); cy += lineH;

            // 功能开关状态（紧凑单行）
            string status = $"{(EspConfig.BoxEspEnabled ? "[ESP]" : " esp ")} " +
                            $"{(EspConfig.LootEspEnabled ? "[物资]" : " 物资 ")} " +
                            $"{(EspConfig.BulletTrackingEnabled ? "[追踪]" : " 追踪 ")}";
            DrawText(x + 8, cy, status, OkColor, false); cy += lineH;
            DrawText(x + 8, cy, $"敌人: {enemyCount}  物资: {lootCount}", DimColor, false); cy += lineH;
            DrawText(x + 8, cy, "Home 面板 · Insert 配置", DimColor, false); cy += lineH;
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
