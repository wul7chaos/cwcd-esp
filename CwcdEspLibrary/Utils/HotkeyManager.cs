using UnityEngine;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// 快捷键系统（方案 9.1）。在主线程 Update 中轮询 Input.GetKeyDown 切换开关。
    /// 由 Patch_ActorUpdate.Postfix 每帧调用（ActorViewerManager.Update 每帧主线程执行）。
    /// </summary>
    public static class HotkeyManager
    {
        private static bool _started;

        public static void Start()
        {
            _started = true;
            EntryPoint.Log($"热键：F6=方框[{(EspConfig.BoxEspEnabled ? "ON" : "OFF")}] " +
                           $"F7=物资[{(EspConfig.LootEspEnabled ? "ON" : "OFF")}] " +
                           $"F8=追踪[{(EspConfig.BulletTrackingEnabled ? "ON" : "OFF")}] " +
                           $"Home=面板 Insert=配置菜单");
        }

        public static void Update()
        {
            if (!_started) return;

            try
            {
                if (Input.GetKeyDown(EspConfig.ToggleBoxKey))
                {
                    EspConfig.BoxEspEnabled = !EspConfig.BoxEspEnabled;
                    EntryPoint.Log($"方框透视: {(EspConfig.BoxEspEnabled ? "ON" : "OFF")}");
                }
                if (Input.GetKeyDown(EspConfig.ToggleLootKey))
                {
                    EspConfig.LootEspEnabled = !EspConfig.LootEspEnabled;
                    EntryPoint.Log($"物资透视: {(EspConfig.LootEspEnabled ? "ON" : "OFF")}");
                }
                if (Input.GetKeyDown(EspConfig.ToggleTrackingKey))
                {
                    EspConfig.BulletTrackingEnabled = !EspConfig.BulletTrackingEnabled;
                    EntryPoint.Log($"子弹追踪: {(EspConfig.BulletTrackingEnabled ? "ON" : "OFF")}");
                }
                if (Input.GetKeyDown(EspConfig.ToggleMenuKey))
                {
                    EspConfig.OverlayVisible = !EspConfig.OverlayVisible;
                    EntryPoint.Log($"状态面板: {(EspConfig.OverlayVisible ? "ON" : "OFF")}");
                }
                if (Input.GetKeyDown(EspConfig.ToggleConfigMenuKey))
                {
                    EspConfig.ConfigMenuVisible = !EspConfig.ConfigMenuVisible;
                    EntryPoint.Log($"配置菜单: {(EspConfig.ConfigMenuVisible ? "ON" : "OFF")}");
                }
            }
            catch { /* Input 在某些时序下可能不可用，忽略 */ }
        }
    }
}
