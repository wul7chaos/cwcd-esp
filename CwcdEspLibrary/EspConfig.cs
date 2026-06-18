using UnityEngine;

namespace CwcdEsp
{
    /// <summary>
    /// 全局配置：功能开关、快捷键、颜色、可调参数。
    /// 所有字段均为静态，供 Patches / ESP / Tracking 共享读取。
    /// 快捷键通过 HotkeyManager 在主线程 Update 中轮询切换。
    /// </summary>
    public static class EspConfig
    {
        // ===== 功能开关（默认值见方案 9.1）=====
        public static bool BoxEspEnabled = true;          // F6
        public static bool LootEspEnabled = true;         // F7
        public static bool BulletTrackingEnabled = false; // F8（默认关）
        public static bool OverlayVisible = true;         // F9 面板可见性

        // ===== 快捷键 =====
        public static KeyCode ToggleBoxKey = KeyCode.F6;
        public static KeyCode ToggleLootKey = KeyCode.F7;
        public static KeyCode ToggleTrackingKey = KeyCode.F8;
        public static KeyCode ToggleMenuKey = KeyCode.Home; // Home 键：面板显示/隐藏

        // ===== 可调参数（方案 9.2）=====
        public static int BoxThickness = 2;            // 1~4 px
        public static float TrackingAngle = 15f;       // 0~45°，准星吸附范围
        public static float TrackingDistance = 64f;    // 0~100m，最大追踪距离
        public static int MinRarity = 0;               // 0~4，物资过滤
        public static float SightCheckInterval = 0.2f; // 0.1~1s，视线物理检测降频

        // ===== 渲染距离 =====
        public static float EnemyMaxDistance = 80f;    // 方框透视最大距离
        public static float LootMaxDistance = 64f;     // 物资透视最大距离
        public static float DefaultHalfWidth = 0.5f;   // 方框半宽（米）

        // ===== 阵营颜色（方案 4.4）=====
        public static readonly Color ColorMonster = Hex(0xFF, 0x33, 0x33); // 红
        public static readonly Color ColorEnemy = Hex(0xB3, 0x33, 0xFF);   // 紫
        public static readonly Color ColorPlayer = Hex(0x33, 0xDD, 0xFF);  // 青
        public static readonly Color ColorPartner = Hex(0x33, 0xFF, 0x55); // 绿

        // ===== 稀有度颜色（0~4）=====
        public static readonly Color[] RarityColors =
        {
            Hex(0xCC, 0xCC, 0xCC), // 0 普通 灰
            Hex(0x85, 0xC1, 0xE9), // 1 蓝绿
            Hex(0x2E, 0x86, 0xC1), // 2 蓝
            Hex(0x8E, 0x44, 0xAD), // 3 紫
            Hex(0xF1, 0xC4, 0x0F), // 4 金
        };

        /// <summary>根据阵营枚举值返回对应颜色（Fraction 为 [Flags]）。</summary>
        public static Color ColorForFraction(int fractionValue)
        {
            // Fraction.Monster=2, Enemy=4（MorbidOptimism.Common.Fraction）
            if ((fractionValue & 2) != 0) return ColorMonster;
            if ((fractionValue & 4) != 0) return ColorEnemy;
            if ((fractionValue & 1) != 0) return ColorPlayer;
            if ((fractionValue & 8) != 0) return ColorPartner;
            return Color.white;
        }

        private static Color Hex(int r, int g, int b, int a = 255)
            => new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
