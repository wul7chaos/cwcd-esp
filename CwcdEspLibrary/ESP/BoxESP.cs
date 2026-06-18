using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 方框透视（方案 4.2 脚底+身高方案）。
    /// 绘制分两趟：GL 趟画方框+血条，Label 趟画名字，避免 GL 矩阵与 GUI 矩阵混用。
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

            float half = EspConfig.DefaultHalfWidth;
            Vector3 right = cam.transform.right * half;

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyBoxGl(cam, ref right, half, enemies[i]);
            }
        }

        private static void DrawEnemyBoxGl(Camera cam, ref Vector3 right, float half, EnemyData e)
        {
            Vector3 foot = e.Position;
            Vector3 head = foot + Vector3.up * e.Height;

            // 4 个角点投影到屏幕
            Vector3 fl = cam.WorldToScreenPoint(foot - right);
            Vector3 fr = cam.WorldToScreenPoint(foot + right);
            Vector3 hl = cam.WorldToScreenPoint(head - right);
            Vector3 hr = cam.WorldToScreenPoint(head + right);

            // 任一在相机后方则整框跳过（方案 4.2）
            if (fl.z < 0 || fr.z < 0 || hl.z < 0 || hr.z < 0) return;

            float minX = Min4(fl.x, fr.x, hl.x, hr.x);
            float maxX = Max4(fl.x, fr.x, hl.x, hr.x);
            float minY = Min4(fl.y, fr.y, hl.y, hr.y);
            float maxY = Max4(fl.y, fr.y, hl.y, hr.y);

            Color c = EspConfig.ColorForFraction(e.FractionValue);

            // 方框（GL 像素坐标：原点左下，y 向上 → top=maxY, bottom=minY）
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
            // 背景
            GlDrawHelper.DrawRect(left, barBottom, right, barTop, new Color(0.15f, 0.15f, 0.15f, 0.85f));
            // 填充
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

            float half = EspConfig.DefaultHalfWidth;
            Vector3 right = cam.transform.right * half;

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyLabel(cam, ref right, half, enemies[i]);
            }
        }

        private static void DrawEnemyLabel(Camera cam, ref Vector3 right, float half, EnemyData e)
        {
            Vector3 head = e.Position + Vector3.up * e.Height;
            Vector3 hr = cam.WorldToScreenPoint(head + right);
            Vector3 hl = cam.WorldToScreenPoint(head - right);
            if (hr.z < 0 || hl.z < 0) return;

            float maxX = Mathf.Max(hr.x, hl.x);
            float minX = Mathf.Min(hr.x, hl.x);
            float topGlY = Mathf.Max(hr.y, hl.y) + (e.HasHp ? 8f : 2f);

            // 转 GUI 坐标（y 向下）
            float guiY = ScreenTools.GlYToGuiY(topGlY);
            float centerX = (minX + maxX) * 0.5f;

            Color c = EspConfig.ColorForFraction(e.FractionValue);
            string text = e.HasHp ? $"{e.Name}  {e.Hp:0}/{e.MaxHp:0}" : e.Name;

            GUIStyle style = Colors.GetStyle(c);
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
            Colors.LabelWithShadow(rect, text, c);
        }

        private static float Min4(float a, float b, float c, float d)
            => Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
        private static float Max4(float a, float b, float c, float d)
            => Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
    }
}
