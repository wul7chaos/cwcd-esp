using UnityEngine;

namespace CwcdEsp.Esp
{
    /// <summary>
    /// 世界坐标 → 屏幕坐标转换 + 缓存相机（方案 Phase2 / 坚决不用 Camera.main）。
    /// 相机引用来自 CamController.camMain（public 字段），通过 Patch_Camera 缓存，
    /// 此处提供懒加载兜底：若缓存为空则尝试从 MonoSingleton 取。
    /// </summary>
    public static class ScreenTools
    {
        private static Camera _cachedCam;
        private static float _camCheckTime;

        /// <summary>当前主相机（带懒加载 + 定期重检，防止场景切换后引用失效）。</summary>
        public static Camera Cam
        {
            get
            {
                // 缓存有效直接返回
                if (_cachedCam != null) return _cachedCam;

                // 节流：避免每帧反射式查找
                if (Time.time - _camCheckTime < 0.5f) return null;
                _camCheckTime = Time.time;

                try
                {
                    // CamController : MonoSingleton<CamController>，camMain 为 public 字段
                    var cc = MonoSingleton<CamController>.GetInstance();
                    if (cc != null) _cachedCam = cc.camMain;
                }
                catch { /* 游戏未就绪时忽略 */ }
                return _cachedCam;
            }
        }

        /// <summary>由 Patch_Camera.LateUpdate Postfix 调用，主动刷新缓存。</summary>
        public static void SetCamera(Camera cam)
        {
            _cachedCam = cam;
        }

        /// <summary>世界坐标 → 屏幕像素坐标（原点左下，y 向上，z>0 表示在前方）。</summary>
        public static Vector3 WorldToScreen(Vector3 world)
        {
            Camera cam = Cam;
            if (cam == null) return new Vector3(-1, -1, -1);
            return cam.WorldToScreenPoint(world);
        }

        /// <summary>该点是否在相机视锥内（用于 Update 阶段剔除）。</summary>
        public static bool IsInFrustum(Camera cam, Vector3 world)
        {
            if (cam == null) return true; // 无相机时不剔除
            Vector3 sp = cam.WorldToScreenPoint(world);
            return sp.z > 0 && sp.x >= 0 && sp.x <= Screen.width && sp.y >= 0 && sp.y <= Screen.height;
        }

        /// <summary>GL 像素 y（向上）转 GUI.Label y（向下）。</summary>
        public static float GlYToGuiY(float glY) => Screen.height - glY;
    }
}
