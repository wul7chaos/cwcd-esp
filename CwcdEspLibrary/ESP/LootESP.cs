using System.Text;
using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 物资透视（修复版：使用 GUI 坐标系）。
    /// 在容器/掉落物上方列出物品名称与稀有度，逐行排列、稀有度着色。
    /// 物品已按价值降序排序，可通过 MaxLootItemsPerContainer 限制显示数量。
    /// </summary>
    public static class LootESP
    {
        private static readonly StringBuilder _sb = new StringBuilder(256);

        // 固定行高：fontSize=12 粗体中文实际渲染约 20px，用 22px 行高确保不重叠不裁剪
        private const float LineHeight = 22f;
        private const float LineGap = 2f;
        private const float RectHeight = 20f; // Rect 高度（略小于行高，给行间距留空间）
        private const float MinLabelWidth = 160f; // 最小显示宽度，防止短文本被 wordWrap 截断
        private const float MaxLabelWidth = 600f; // 最大显示宽度上限

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
                Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, RectHeight);
                Colors.LabelWithShadow(rect, entry.ContainerName, new Color(1f, 1f, 1f, 0.9f));
                guiY -= LineHeight + LineGap;
            }

            // 逐物品行（稀有度着色），从下往上排列 —— 使用过滤+排序+限量后的物品列表
            var items = entry.FilteredItems;
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
                // 强制单行：关闭 wordWrap，并给宽度加最小/最大限制，确保长文本也有足够空间
                float width = Mathf.Clamp(size.x, MinLabelWidth, MaxLabelWidth);
                Rect rect = new Rect(centerX - width * 0.5f, guiY, width, RectHeight);
                Colors.LabelWithShadow(rect, text, c);
                guiY -= LineHeight + LineGap;
            }
        }
    }
}
