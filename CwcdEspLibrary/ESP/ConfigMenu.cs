using CwcdEsp.Data;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 配置菜单（IMGUI 可拖动窗口）。
    /// 按 Insert 键切换显示。提供物资价值过滤、稀有度、方框厚度、连线等参数的实时调整。
    /// 由 EspRenderer.DrawAll 在 OnGUI Repaint 时调用。
    /// </summary>
    public static class ConfigMenu
    {
        private static Rect _windowRect = new Rect(260f, 40f, 300f, 360f);
        private static bool _inited;

        public static void Draw()
        {
            if (!EspConfig.ConfigMenuVisible) return;

            // 首次显示时确保窗口在可见区域
            if (!_inited)
            {
                _inited = true;
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
            }

            _windowRect = GUI.Window(0x4357, _windowRect, WindowFunc, "CWCD-ESP 配置 (Insert 关闭)");
        }

        private static void WindowFunc(int id)
        {
            GUILayout.BeginVertical();

            // ===== 物资透视 =====
            SectionLabel("物资透视");
            EspConfig.LootEspEnabled = GUILayout.Toggle(EspConfig.LootEspEnabled, "启用物资透视");
            EspConfig.EnableLootFilter = GUILayout.Toggle(EspConfig.EnableLootFilter, "启用价值过滤");
            GUILayout.BeginHorizontal();
            GUILayout.Label("最低价值:", GUILayout.Width(70));
            string valStr = GUILayout.TextField(EspConfig.MinItemValue.ToString(), GUILayout.Width(80));
            int v;
            if (int.TryParse(valStr, out v) && v >= 0) EspConfig.MinItemValue = v;
            GUILayout.EndHorizontal();
            EspConfig.ShowItemValue = GUILayout.Toggle(EspConfig.ShowItemValue, "显示物品价值");

            GUILayout.BeginHorizontal();
            GUILayout.Label("最小稀有度:", GUILayout.Width(70));
            EspConfig.MinRarity = Mathf.RoundToInt(GUILayout.HorizontalSlider(EspConfig.MinRarity, 0, 4));
            GUILayout.Label(EspConfig.MinRarity.ToString(), GUILayout.Width(20));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ===== 方框透视 =====
            SectionLabel("方框透视");
            EspConfig.BoxEspEnabled = GUILayout.Toggle(EspConfig.BoxEspEnabled, "启用方框透视");
            EspConfig.DrawPlayerEnemyLines = GUILayout.Toggle(EspConfig.DrawPlayerEnemyLines, "玩家-敌人连线");
            EspConfig.DashedBoxWhenIdle = GUILayout.Toggle(EspConfig.DashedBoxWhenIdle, "未发现用虚线方框");

            GUILayout.BeginHorizontal();
            GUILayout.Label("方框厚度:", GUILayout.Width(70));
            EspConfig.BoxThickness = Mathf.RoundToInt(GUILayout.HorizontalSlider(EspConfig.BoxThickness, 1, 4));
            GUILayout.Label(EspConfig.BoxThickness + "px", GUILayout.Width(30));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ===== 子弹追踪 =====
            SectionLabel("子弹追踪");
            EspConfig.BulletTrackingEnabled = GUILayout.Toggle(EspConfig.BulletTrackingEnabled, "启用子弹追踪 (F8)");

            GUILayout.Space(8);

            // ===== 状态信息 =====
            SectionLabel("状态");
            int enemyCount = 0, lootCount = 0, aiCount = 0;
            try { enemyCount = EnemyCache.Instance.GetReadBuffer()?.Count ?? 0; } catch { }
            try { lootCount = LootCache.Instance.GetSnapshot()?.Count ?? 0; } catch { }
            try { aiCount = AiStateCache.Count; } catch { }
            GUILayout.Label($"敌人: {enemyCount}  物资: {lootCount}  AI: {aiCount}");

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private static void SectionLabel(string title)
        {
            GUILayout.Label(title, GUILayout.Height(18));
        }
    }
}
