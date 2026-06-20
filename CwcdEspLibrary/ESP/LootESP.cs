using System.Text;
using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 物资透视（修复版：使用 GUI 坐标系）。
    /// 在容器/掉落物上方列出物品名称与稀有度，逐行排列、稀有度着色。
    /// </summary>
    public static class LootESP
    {
        private static readonly StringBuilder _sb = new StringBuilder(256);

        /// <summary>绘制所有物资容器的物品列表。</summary>
        public static void DrawLabels()
        {
            var snapshot = LootCache.Instance.GetSnapshot();
            if (snapshot == null || snapshot.Count == 0) return;
            Camera cam = ScreenTools.Cam;
            if (cam == null) return;

            for (int i = 0; i < snapshot.Count; i++)
            {
                DrawLootLabel(cam, snapshot[i]);
            }
        }

        private static void DrawLootLabel(Camera cam, LootEntry entry)
        {
            // 用 GUI 坐标系（原点左上，y 向下）
            Vector3 sp = GuiDrawHelper.WorldToGuiPoint(cam, entry.Position);
            if (sp.z < 0) return; // 在相机后方

            float centerX = sp.x;
            float guiY = sp.y - 20f; // 容器上方 20px

            // 容器名
            if (!string.IsNullOrEmpty(entry.ContainerName))
            {
                GUIStyle nameStyle = Colors.GetStyle(new Color(1f, 1f, 1f, 0.9f));
                Vector2 size = nameStyle.CalcSize(new GUIContent(entry.ContainerName));
                Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
                Colors.LabelWithShadow(rect, entry.ContainerName, new Color(1f, 1f, 1f, 0.9f));
                guiY -= size.y + 1f; // 向上排列（y 减小）
            }

            // 逐物品行（稀有度着色），从下往上排列
            var items = entry.Items;
            for (int i = 0; i < items.Count; i++)
            {
                LootItem item = items[i];
                _sb.Clear();
                _sb.Append(item.Name);
                if (item.Count > 1) _sb.Append(" x").Append(item.Count);

                // 显示价值（购买价）
                if (EspConfig.ShowItemValue && item.BuyPrice > 0)
                {
                    _sb.Append(" ¥").Append(item.BuyPrice);
                }

                Color c = item.Rarity >= 0 && item.Rarity < EspConfig.RarityColors.Length
                    ? EspConfig.RarityColors[item.Rarity]
                    : Color.white;

                string text = _sb.ToString();
                GUIStyle style = Colors.GetStyle(c);
                Vector2 size = style.CalcSize(new GUIContent(text));
                Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
                Colors.LabelWithShadow(rect, text, c);
                guiY -= size.y + 1f;
            }
        }
    }
}
