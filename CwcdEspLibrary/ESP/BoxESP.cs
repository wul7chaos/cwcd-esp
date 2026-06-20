using CwcdEsp.Data;
using CwcdEsp.Utils;
using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 方框透视（修复版：改用 GUI.DrawTexture 替代 GL 绘制）。
    ///
    /// v4 增强：
    /// 1) 按 AI 状态变色 + 线型：
    ///    - Idle（未发现玩家）→ 虚线绿色方框
    ///    - Combat（发现/攻击玩家）→ 实线红色方框
    ///    - Unknown（无 AI 数据）→ 按 Idle 处理
    /// 2) 玩家-敌人连线：从屏幕中心（准星）到敌人方框中心画线段，颜色与方框同步。
    /// </summary>
    public static class BoxESP
    {
        /// <summary>绘制所有敌人的方框与血条（GUI 坐标系）。</summary>
        public static void DrawBoxes()
        {
            var enemies = EnemyCache.Instance.GetReadBuffer();
            if (enemies == null || enemies.Count == 0) return;
            Camera cam = ScreenTools.Cam;
            if (cam == null) return;

            // 连线起点：屏幕中心（俯视角准星位置）
            Vector2 lineOrigin = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            for (int i = 0; i < enemies.Count; i++)
            {
                DrawEnemyBox(cam, enemies[i], lineOrigin);
            }
        }

        private static void DrawEnemyBox(Camera cam, EnemyData e, Vector2 lineOrigin)
        {
            Vector3 center = e.BoundsCenter;
            Vector3 size = e.BoundsSize;

            // 如果 bounds 无效，回退到 position + height + radius
            if (size.sqrMagnitude < 0.01f)
            {
                center = e.Position + Vector3.up * (e.Height * 0.5f);
                size = new Vector3(e.Radius * 2f, e.Height, e.Radius * 2f);
            }

            Vector3 ext = size * 0.5f;
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-ext.x, -ext.y, -ext.z);
            corners[1] = center + new Vector3( ext.x, -ext.y, -ext.z);
            corners[2] = center + new Vector3(-ext.x,  ext.y, -ext.z);
            corners[3] = center + new Vector3( ext.x,  ext.y, -ext.z);
            corners[4] = center + new Vector3(-ext.x, -ext.y,  ext.z);
            corners[5] = center + new Vector3( ext.x, -ext.y,  ext.z);
            corners[6] = center + new Vector3(-ext.x,  ext.y,  ext.z);
            corners[7] = center + new Vector3( ext.x,  ext.y,  ext.z);

            // 全部投影到 GUI 坐标（原点左上，y 向下），取 min/max
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool anyVisible = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 sp = GuiDrawHelper.WorldToGuiPoint(cam, corners[i]);
                if (sp.z < 0) continue; // 在相机后方
                anyVisible = true;
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            if (!anyVisible) return;

            // === AI 状态决定颜色与线型 ===
            // Idle / Unknown → 虚线绿色；Combat → 实线红色
            bool isCombat = e.AiState == AiState.Combat;
            Color boxColor = isCombat ? EspConfig.ColorAiCombat : EspConfig.ColorAiIdle;
            float thickness = EspConfig.BoxThickness;

            if (isCombat || !EspConfig.DashedBoxWhenIdle)
            {
                // 实线方框
                GuiDrawHelper.DrawBox(minX, minY, maxX, maxY, boxColor, thickness);
            }
            else
            {
                // 虚线方框（未发现玩家）
                GuiDrawHelper.DrawDashedBox(minX, minY, maxX, maxY, boxColor, thickness);
            }

            // === 玩家-敌人连线（颜色与方框同步）===
            if (EspConfig.DrawPlayerEnemyLines)
            {
                float boxCenterX = (minX + maxX) * 0.5f;
                float boxCenterY = (minY + maxY) * 0.5f;
                if (isCombat)
                {
                    GuiDrawHelper.DrawLine(lineOrigin.x, lineOrigin.y, boxCenterX, boxCenterY, boxColor, 1.5f);
                }
                else
                {
                    GuiDrawHelper.DrawDashedLine(lineOrigin.x, lineOrigin.y, boxCenterX, boxCenterY, boxColor, 1.5f);
                }
            }

            // 血条（方框上方）
            if (e.HasHp)
            {
                DrawHealthBar(minX, maxX, minY, e.HpRatio);
            }
        }

        private static void DrawHealthBar(float left, float right, float boxTop, float ratio)
        {
            float barH = 3f;
            float gap = 2f;
            float barTop = boxTop - gap - barH;
            float barBottom = barTop + barH;
            GuiDrawHelper.DrawRect(left, barTop, right, barBottom, new Color(0.15f, 0.15f, 0.15f, 0.85f));
            float fillRight = left + (right - left) * Mathf.Clamp01(ratio);
            GuiDrawHelper.DrawRect(left, barTop, fillRight, barBottom, Colors.HpColor(ratio));
        }

        /// <summary>绘制所有敌人的名字/血量文本。</summary>
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

            Vector3 sp = GuiDrawHelper.WorldToGuiPoint(cam, topPos);
            if (sp.z < 0) return;

            float centerX = sp.x;
            float guiY = sp.y + 2f; // 略微下移

            Color c = EspConfig.ColorForFraction(e.FractionValue);
            string text = e.HasHp ? $"{e.Name}  {e.Hp:0}/{e.MaxHp:0}" : e.Name;

            GUIStyle style = Colors.GetStyle(c);
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(centerX - size.x * 0.5f, guiY, size.x, size.y);
            Colors.LabelWithShadow(rect, text, c);
        }
    }
}
