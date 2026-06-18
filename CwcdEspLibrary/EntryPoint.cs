using System;
using CwcdEsp.Data;
using CwcdEsp.Patches;
using CwcdEsp.Tracking;
using CwcdEsp.Utils;
using HarmonyLib;

namespace CwcdEsp
{
    /// <summary>
    /// 注入入口：由注入器通过 Mono API 调用 <see cref="Load"/>。
    /// 所有 Harmony Patch 采用手动注册 + 逐条 try-catch，单个失败不影响其他模块（方案 7.2）。
    /// </summary>
    public static class EntryPoint
    {
        public const string HarmonyId = "cwcd.esp";
        private static Harmony _harmony;
        private static bool _loaded;

        /// <summary>注入器调用入口。</summary>
        public static void Load()
        {
            if (_loaded)
            {
                Log("Load 已执行过，跳过重复初始化");
                return;
            }
            _loaded = true;

            Log("========== CWCD-ESP v3 启动 ==========");

            // 1. 初始化缓存
            EnemyCache.Instance.Init();
            LootCache.Instance.Init();

            // 2. Harmony 实例
            _harmony = new Harmony(HarmonyId);

            int success = 0, fail = 0;

            // 3. 逐条注册 Patch（每条独立容错）
            //    OnGUI 仅 Postfix，在 Postfix 内用 Event.current.type 过滤 Repaint（方案 v3 修复 #12）
            PatchGuard.TryPatch(_harmony, "GameController", "OnGUI",
                ref success, ref fail,
                postfixType: typeof(Patch_OnGUI));

            // ActorViewerManager.Update Postfix → 填充写缓冲 + Swap + 物资脏标记刷新
            PatchGuard.TryPatch(_harmony, "MorbidOptimism.Client.ActorViewerManager", "Update",
                ref success, ref fail,
                postfixType: typeof(Patch_ActorUpdate));

            // CamController：缓存 camMain（注意：源码无 Start()，改用 LateUpdate Postfix，方案 v3 偏离说明）
            PatchGuard.TryPatch(_harmony, "CamController", "LateUpdate",
                ref success, ref fail,
                postfixType: typeof(Patch_Camera));

            // 散布归零
            PatchGuard.TryPatch(_harmony, "MorbidOptimism.Server.LogicControllerEquipReload", "GetSpreadAngle",
                ref success, ref fail,
                postfixType: typeof(Patch_Spread));

            // 子弹方向修正：Patch LogicActionBulletBase.Parse Prefix（方案 v3 偏离说明：直接改 equipSnapshot.to）
            BulletTracker.TryRegister(_harmony, ref success, ref fail);

            // 容器变动 → 逐 Actor 脏标记
            PatchGuard.TryPatch(_harmony, "MorbidOptimism.Common.ItemContainer_Grid", "PutItem",
                ref success, ref fail,
                postfixType: typeof(Patch_ItemContainer));
            // 注：源码 ItemContainer_Grid 无公开 TakeItem，移除走私有 _RemoveItem；如需覆盖可改 Patch _RemoveItem
            PatchGuard.TryPatch(_harmony, "MorbidOptimism.Common.ItemContainer_Grid", "_RemoveItem",
                ref success, ref fail,
                postfixType: typeof(Patch_ItemContainer));

            Log($"Patch 注册完成：成功 {success}，失败 {fail}");

            // 4. 热键监听（挂在 OnGUI 流里轮询，无需额外线程）
            HotkeyManager.Start();

            Log("========== CWCD-ESP 启动完成 ==========");
        }

        /// <summary>卸载（可选）：移除所有 Patch。</summary>
        public static void Unload()
        {
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                Log("已卸载所有 Patch");
            }
            catch (Exception e)
            {
                Log("Unload 异常: " + e.Message);
            }
            _loaded = false;
        }

        /// <summary>统一日志输出（写入口志 + 控制台）。</summary>
        public static void Log(string msg)
        {
            // 游戏内 Debug.Log 会在控制台与日志文件同时输出
            try { UnityEngine.Debug.Log("[CWCD-ESP] " + msg); } catch { }
        }
    }
}
