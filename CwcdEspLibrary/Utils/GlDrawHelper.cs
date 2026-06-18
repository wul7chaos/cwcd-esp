using UnityEngine;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// GL.LINES / GL.QUADS 即时绘制辅助 + 材质管理（方案 4.3 / v3 修复 #15）。
    /// 所有 GL 绘制在 GL.PushMatrix + GL.LoadPixelMatrix + material.SetPass(0) 上下文中进行。
    /// 注意：LoadPixelMatrix 原点在左下角，y 向上；GUI.Label 原点在左上角，y 向下。
    /// </summary>
    public static class GlDrawHelper
    {
        private static Material _espMaterial;

        /// <summary>获取（懒加载）透明无光照材质。</summary>
        public static Material EspMaterial
        {
            get
            {
                if (_espMaterial == null)
                {
                    Shader shader = Shader.Find("Hidden/Internal-Colored");
                    if (shader == null) shader = Shader.Find("Unlit/Transparent");
                    _espMaterial = new Material(shader);
                    _espMaterial.hideFlags = HideFlags.HideAndDontSave;
                    // 关闭深度写入/测试，确保覆盖绘制
                    _espMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _espMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _espMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    _espMaterial.SetInt("_ZWrite", 0);
                    _espMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
                return _espMaterial;
            }
        }

        /// <summary>进入 GL 绘制上下文（像素坐标，原点左下）。</summary>
        public static void BeginGlContext()
        {
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            EspMaterial.SetPass(0);
        }

        /// <summary>退出 GL 绘制上下文。</summary>
        public static void EndGlContext()
        {
            GL.PopMatrix();
        }

        /// <summary>绘制矩形边框（4 条边）。坐标为像素，原点左下，y 向上。</summary>
        public static void DrawBox(float left, float top, float right, float bottom, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);
            // top
            GL.Vertex3(left, top, 0); GL.Vertex3(right, top, 0);
            // right
            GL.Vertex3(right, top, 0); GL.Vertex3(right, bottom, 0);
            // bottom
            GL.Vertex3(right, bottom, 0); GL.Vertex3(left, bottom, 0);
            // left
            GL.Vertex3(left, bottom, 0); GL.Vertex3(left, top, 0);
            GL.End();
        }

        /// <summary>绘制填充矩形（用于血条）。坐标为像素，原点左下。</summary>
        public static void DrawRect(float left, float top, float right, float bottom, Color color)
        {
            GL.Begin(GL.QUADS);
            GL.Color(color);
            GL.Vertex3(left, bottom, 0);
            GL.Vertex3(right, bottom, 0);
            GL.Vertex3(right, top, 0);
            GL.Vertex3(left, top, 0);
            GL.End();
        }
    }
}
