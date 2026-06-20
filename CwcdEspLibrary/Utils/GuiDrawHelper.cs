using UnityEngine;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// GUI 绘制辅助（替代 GL 绘制，确保与 GUI.Label 坐标系一致）。
    /// 所有坐标为 GUI 像素坐标：原点左上，y 向下。
    /// </summary>
    public static class GuiDrawHelper
    {
        private static Texture2D _whiteTex;

        /// <summary>1x1 白色透明纹理（懒加载）。</summary>
        public static Texture2D WhiteTex
        {
            get
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                    _whiteTex.hideFlags = HideFlags.HideAndDontSave;
                }
                return _whiteTex;
            }
        }

        /// <summary>绘制矩形边框。坐标为 GUI 像素（原点左上，y 向下）。</summary>
        public static void DrawBox(float left, float top, float right, float bottom, Color color, float thickness = 2f)
        {
            if (right <= left || bottom <= top) return;
            float w = right - left;
            float h = bottom - top;
            Color prevColor = GUI.color;
            GUI.color = color;

            // 上边
            GUI.DrawTexture(new Rect(left, top, w, thickness), WhiteTex);
            // 下边
            GUI.DrawTexture(new Rect(left, bottom - thickness, w, thickness), WhiteTex);
            // 左边
            GUI.DrawTexture(new Rect(left, top, thickness, h), WhiteTex);
            // 右边
            GUI.DrawTexture(new Rect(right - thickness, top, thickness, h), WhiteTex);

            GUI.color = prevColor;
        }

        /// <summary>绘制填充矩形。坐标为 GUI 像素（原点左上，y 向下）。</summary>
        public static void DrawRect(float left, float top, float right, float bottom, Color color)
        {
            if (right <= left || bottom <= top) return;
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(left, top, right - left, bottom - top), WhiteTex);
            GUI.color = prevColor;
        }

        /// <summary>绘制虚线矩形边框。dashLen=实线段长，gapLen=间隔长。</summary>
        public static void DrawDashedBox(float left, float top, float right, float bottom, Color color, float thickness = 2f, float dashLen = 6f, float gapLen = 4f)
        {
            if (right <= left || bottom <= top) return;
            float w = right - left;
            float h = bottom - top;
            Color prevColor = GUI.color;
            GUI.color = color;

            // 上边、下边（沿 x 方向画虚线段）
            DrawDashedHLine(left, top, w, thickness, dashLen, gapLen);
            DrawDashedHLine(left, bottom - thickness, w, thickness, dashLen, gapLen);
            // 左边、右边（沿 y 方向画虚线段）
            DrawDashedVLine(left, top, h, thickness, dashLen, gapLen);
            DrawDashedVLine(right - thickness, top, h, thickness, dashLen, gapLen);

            GUI.color = prevColor;
        }

        /// <summary>绘制线段（实线）。坐标为 GUI 像素（原点左上，y 向下）。</summary>
        public static void DrawLine(float x1, float y1, float x2, float y2, Color color, float thickness = 1.5f)
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            float dx = x2 - x1;
            float dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) { GUI.color = prevColor; return; }
            // 用旋转的矩形（通过 GUIUtility 不可直接旋转 DrawTexture），改用沿线段画多个小矩形近似
            float step = Mathf.Max(thickness, 2f);
            int segs = Mathf.CeilToInt(len / step);
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float px = x1 + dx * t;
                float py = y1 + dy * t;
                GUI.DrawTexture(new Rect(px - thickness * 0.5f, py - thickness * 0.5f, thickness, thickness), WhiteTex);
            }
            GUI.color = prevColor;
        }

        /// <summary>绘制虚线线段。dashLen=实线段长，gapLen=间隔长。</summary>
        public static void DrawDashedLine(float x1, float y1, float x2, float y2, Color color, float thickness = 1.5f, float dashLen = 8f, float gapLen = 5f)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) return;
            Color prevColor = GUI.color;
            GUI.color = color;
            float ux = dx / len;
            float uy = dy / len;
            float drawn = 0f;
            bool on = true;
            while (drawn < len)
            {
                float seg = on ? dashLen : gapLen;
                if (drawn + seg > len) seg = len - drawn;
                if (on)
                {
                    float sx = x1 + ux * drawn;
                    float sy = y1 + uy * drawn;
                    float ex = x1 + ux * (drawn + seg);
                    float ey = y1 + uy * (drawn + seg);
                    // 用小矩形填充这一段
                    int n = Mathf.CeilToInt(seg / Mathf.Max(thickness, 2f));
                    for (int i = 0; i <= n; i++)
                    {
                        float t = (float)i / n;
                        float px = sx + (ex - sx) * t;
                        float py = sy + (ey - sy) * t;
                        GUI.DrawTexture(new Rect(px - thickness * 0.5f, py - thickness * 0.5f, thickness, thickness), WhiteTex);
                    }
                }
                drawn += seg;
                on = !on;
            }
            GUI.color = prevColor;
        }

        private static void DrawDashedHLine(float x, float y, float totalW, float thickness, float dashLen, float gapLen)
        {
            float drawn = 0f;
            bool on = true;
            while (drawn < totalW)
            {
                float seg = on ? dashLen : gapLen;
                if (drawn + seg > totalW) seg = totalW - drawn;
                if (on)
                {
                    GUI.DrawTexture(new Rect(x + drawn, y, seg, thickness), WhiteTex);
                }
                drawn += seg;
                on = !on;
            }
        }

        private static void DrawDashedVLine(float x, float y, float totalH, float thickness, float dashLen, float gapLen)
        {
            float drawn = 0f;
            bool on = true;
            while (drawn < totalH)
            {
                float seg = on ? dashLen : gapLen;
                if (drawn + seg > totalH) seg = totalH - drawn;
                if (on)
                {
                    GUI.DrawTexture(new Rect(x, y + drawn, thickness, seg), WhiteTex);
                }
                drawn += seg;
                on = !on;
            }
        }

        /// <summary>世界坐标 → GUI 像素坐标（原点左上，y 向下）。</summary>
        public static Vector3 WorldToGuiPoint(Camera cam, Vector3 world)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            // WorldToScreenPoint: (0,0)=左下, y向上
            // GUI: (0,0)=左上, y向下
            sp.y = Screen.height - sp.y;
            return sp;
        }
    }
}
