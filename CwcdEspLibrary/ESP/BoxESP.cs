using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 方框透视（修复版：使用 Collider.bounds 的 8 角点投影到屏幕画 2D 包围框）。
    ///
    /// 之前的方案用脚底+固定高度投影，在俯视角下方框偏移。
    /// 现在用 Collider.bounds 的 8 个角点投影到屏幕，取 min/max 画 2D 包围框，
    /// 确保方框精确框住角色的渲染碰撞体。
    /// </summary>
    public static class BoxESP
    {
        /// <summary>GL 趟：绘制所有敌人的方框与血条。</summary>
        public static void DrawGl()
        {
            var enemies = EnemyCache.Instance.GetReadBuffer();
            if (enemies == null || enemies.Count == 0) return;
            Camera cam = ScreenTools.Cam;
            if (cam == null) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyBoxGl(cam, enemies[i]);
            }
        }

        private static void DrawEnemyBoxGl(Camera cam, EnemyData e)
        {
            // 用 bounds 中心 + size 构建 8 角点
            Vector3 center = e.BoundsCenter;
            Vector3 size = e.BoundsSize;

            // 如果 bounds 无效（size≈0），回退到 position + height + radius
            if (size.sqrMagnitude < 0.01f)
            {
                center = e.Position + Vector3.up * (e.Height * 0.5f);
                size = new Vector3(e.Radius * 2f, e.Height, e.Radius * 2f);
            }

            Vector3 ext = size * 0.5f;
            // 8 个角点
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-ext.x, -ext.y, -ext.z);
            corners[1] = center + new Vector3( ext.x, -ext.y, -ext.z);
            corners[2] = center + new Vector3(-ext.x,  ext.y, -ext.z);
            corners[3] = center + new Vector3( ext.x,  ext.y, -ext.z);
            corners[4] = center + new Vector3(-ext.x, -ext.y,  ext.z);
            corners[5] = center + new Vector3( ext.x, -ext.y,  ext.z);
            corners[6] = center + new Vector3(-ext.x,  ext.y,  ext.z);
            corners[7] = center + new Vector3( ext.x,  ext.y,  ext.z);

            // 全部投影到屏幕，取 min/max 画 2D 包围框
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool anyVisible = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(corners[i]);
                if (sp.z < 0) continue; // 在相机后方
                anyVisible = true;
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            if (!anyVisible) return;

            Color c = EspConfig.ColorForFraction(e.FractionValue);

            // 方框（GL 像素坐标：原点左下，y 向上）
            GlDrawHelper.DrawBox(minX, maxY, maxX, minY, c);

            // 血条（方框上方）
            if (e.HasHp)
            {
                DrawHealthBar(minX, maxX, maxY, e.HpRatio);
            }
        }

        private static void DrawHealthBar(float left, float right, float boxTop, float ratio)
        {
            float barH = 3f;
            float gap = 2f;
            float barTop = boxTop + gap;
            float barBottom = barTop + barH;
            GlDrawHelper.DrawRect(left, barBottom, right, barTop, new Color(0.15f, 0.15f, 0.15f, 0.85f));
            float fillRight = left + (right - left) * Mathf.Clamp01(ratio);
            GlDrawHelper.DrawRect(left, barBottom, fillRight, barTop, Colors.HpColor(ratio));
        }

        /// <summary>Label 趟：绘制所有敌人的名字/血量文本。</summary>
        public static void DrawLabels()
        {
            var enemies = EnemyCache.Instance.GetReadBuffer();
            if (enemies == null || enemies.Count == 0) return;
            Camera cam = ScreenTools.Cam;
            if (cam == null) return;

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyLabel(cam, enemies[i]);
            }
        }

        private static void DrawEnemyLabel(Camera cam, EnemyData e)
        {
            // 文字放在 bounds 顶部上方
            Vector3 topPos = e.BoundsCenter + Vector3.up * (e.BoundsSize.y * 0.5f);
            if (e.BoundsSize.sqrMagnitude < 0.01f)
                topPos = e.Position + Vector3.up * e.Height;

            Vector3 sp = cam.WorldToScreenPoint(topPos);
            if (sp.z < 0) return;

            float topGlY = sp.y + 8f;
            float guiY = ScreenTools.GlYToGuiY(topGlY);
            float centerX = sp.x;

            Color c = EspConfig.ColorForFraction(e.FractionValue);
            string text = e.HasHp ? $"{e.Name}  {e.Hp:0}/{e.MaxHp:0}" : e.Name;

            GUIStyle style = Colors.GetStyle(c);
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
            Colors.LabelWithShadow(rect, text, c);
        }
    }
}
