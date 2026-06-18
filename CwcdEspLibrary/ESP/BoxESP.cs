using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 方框透视（修复版：基于屏幕投影的固定像素宽度方框）。
    ///
    /// 旧方案用 cam.transform.right * halfWidth（世界空间）计算方框左右边界，
    /// 在透视相机下远处方框会缩成几个像素且视觉上"偏离"敌人。
    ///
    /// 新方案：用 WorldToScreenPoint(foot) 和 WorldToScreenPoint(head) 得到脚底和头顶的屏幕坐标，
    /// 方框宽度 = 屏幕上脚到头距离的固定比例（如 0.6），确保远近都视觉正确。
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
            // 脚底和头顶投影到屏幕
            Vector3 footScreen = cam.WorldToScreenPoint(e.Position);
            Vector3 headScreen = cam.WorldToScreenPoint(e.Position + Vector3.up * e.Height);

            // 任一在相机后方则跳过
            if (footScreen.z < 0) return;

            // 屏幕上的脚底和头顶 y 坐标（GL 坐标，y 向上）
            float footY = footScreen.y;
            float headY = headScreen.z > 0 ? headScreen.y : footY + 30f; // 头顶在后方时用估算

            // 方框高度 = 屏幕上脚到头的距离（至少 20px）
            float boxHeight = Mathf.Max(Mathf.Abs(headY - footY), 20f);
            // 方框宽度 = 高度的 0.6 倍（人体比例），至少 12px
            float boxWidth = Mathf.Max(boxHeight * 0.6f, 12f);

            float centerX = footScreen.x;
            float bottomY = Mathf.Min(footY, headY);
            float topY = bottomY + boxHeight;
            float leftX = centerX - boxWidth * 0.5f;
            float rightX = centerX + boxWidth * 0.5f;

            Color c = EspConfig.ColorForFraction(e.FractionValue);

            // 方框
            GlDrawHelper.DrawBox(leftX, topY, rightX, bottomY, c);

            // 血条（方框上方）
            if (e.HasHp)
            {
                DrawHealthBar(leftX, rightX, topY, e.HpRatio);
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

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyLabel(cam, enemies[i]);
            }
        }

        private static void DrawEnemyLabel(Camera cam, EnemyData e)
        {
            Vector3 headScreen = cam.WorldToScreenPoint(e.Position + Vector3.up * e.Height);
            if (headScreen.z < 0) return;

            // 文字放在头顶上方
            float topGlY = headScreen.y + 8f;
            float guiY = ScreenTools.GlYToGuiY(topGlY);
            float centerX = headScreen.x;

            Color c = EspConfig.ColorForFraction(e.FractionValue);
            string text = e.HasHp ? $"{e.Name}  {e.Hp:0}/{e.MaxHp:0}" : e.Name;

            GUIStyle style = Colors.GetStyle(c);
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
            Colors.LabelWithShadow(rect, text, c);
        }
    }
}
