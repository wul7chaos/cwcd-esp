using System.Collections.Generic;
using UnityEngine;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// 颜色工具 + GUIStyle 字典缓存（方案 4.3 / v3 修复 #18）。
    /// 关键：按颜色缓存 GUIStyle，绝不运行时修改已缓存 Style 的 textColor，
    /// 否则同一帧多次绘制会出现颜色错位/闪烁。
    /// </summary>
    public static class Colors
    {
        private static readonly Dictionary<Color, GUIStyle> _styleCache = new Dictionary<Color, GUIStyle>(32);

        /// <summary>取（或创建并缓存）指定颜色的粗体 GUIStyle。</summary>
        public static GUIStyle GetStyle(Color color)
        {
            if (!_styleCache.TryGetValue(color, out GUIStyle style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,             // 强制单行，防止物资标签自动换行
                };
                // 清除 padding，让 CalcSize 返回的尺寸更接近实际渲染高度，
                // 避免行高不足导致底部被裁剪（中文字体行高 > fontSize）
                style.padding = new RectOffset(0, 0, 0, 0);
                style.margin = new RectOffset(0, 0, 0, 0);
                style.normal.textColor = color;
                _styleCache[color] = style;
            }
            return style;
        }

        /// <summary>带阴影的文字（黑底描边），提升可读性。</summary>
        public static void LabelWithShadow(Rect rect, string text, Color color)
        {
            GUIStyle shadow = GetStyle(new Color(0, 0, 0, 0.85f));
            Rect shadowRect = rect;
            shadowRect.x += 1; shadowRect.y += 1;
            GUI.Label(shadowRect, text, shadow);
            GUI.Label(rect, text, GetStyle(color));
        }

        /// <summary>根据血量比例返回绿→黄→红渐变色。</summary>
        public static Color HpColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio > 0.5f)
            {
                // 绿 -> 黄
                return Color.Lerp(new Color(1f, 0.92f, 0.2f), new Color(0.2f, 1f, 0.3f), (ratio - 0.5f) * 2f);
            }
            // 黄 -> 红
            return Color.Lerp(new Color(1f, 0.15f, 0.15f), new Color(1f, 0.92f, 0.2f), ratio * 2f);
        }
    }
}
