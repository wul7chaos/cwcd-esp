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
