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
                FileLogger.Info("Load 已执行过，跳过重复初始化");
                return;
            }

            // ===== 日志系统最先初始化，后续所有步骤都有日志 =====
            try
            {
                FileLogger.Init();
            }
            catch (Exception ex)
            {
                // 极端情况：日志初始化本身失败。用 UnityEngine.Debug 兜底
                try { UnityEngine.Debug.Log("[CWCD-ESP] FileLogger.Init 失败: " + ex); } catch { }
            }

            FileLogger.Info("========== CWCD-ESP v4 启动 ==========");

            try
            {
                _loaded = true;

                // 0. 加载配置文件（在所有功能初始化前，让用户配置生效）
                try
                {
                    ConfigFile.Load();
                }
                catch (Exception ex)
                {
                    FileLogger.Warn("配置文件加载失败（使用默认值）: " + ex.Message);
                }

                // 1. 初始化缓存
                FileLogger.Info("初始化 EnemyCache / LootCache...");
                EnemyCache.Instance.Init();
                LootCache.Instance.Init();
                FileLogger.Info("缓存初始化完成");

                // 2. Harmony 实例
                FileLogger.Info("创建 Harmony 实例 (id=" + HarmonyId + ")...");
                _harmony = new Harmony(HarmonyId);
                FileLogger.Info("Harmony 实例创建完成");

                int success = 0, fail = 0;

                // 3. 逐条注册 Patch（每条独立容错）
                FileLogger.Info("===== 开始注册 Patch =====");

                // OnGUI
                RegisterPatch("GameController", "OnGUI",
                    ref success, ref fail,
                    postfixType: typeof(Patch_OnGUI));

                // ActorUpdate
                RegisterPatch("MorbidOptimism.Client.ActorViewerManager", "Update",
                    ref success, ref fail,
                    postfixType: typeof(Patch_ActorUpdate));

                // Camera (LateUpdate)
                RegisterPatch("CamController", "LateUpdate",
                    ref success, ref fail,
                    postfixType: typeof(Patch_Camera));

                // Spread
                RegisterPatch("MorbidOptimism.Server.LogicControllerEquipReload", "GetSpreadAngle",
                    ref success, ref fail,
                    postfixType: typeof(Patch_Spread));

                // AI 状态采集（Patch LogicControllerActorAI.Update → 捕获 focusTarget）
                RegisterPatch("MorbidOptimism.Server.LogicControllerActorAI", "Update",
                    ref success, ref fail,
                    postfixType: typeof(Patch_AiUpdate));

                // Bullet tracking
                FileLogger.Info("注册 BulletTracker...");
                try
                {
                    BulletTracker.TryRegister(_harmony, ref success, ref fail);
                }
                catch (Exception ex)
                {
                    FileLogger.Error("BulletTracker.TryRegister 异常", ex);
                    fail++;
                }

                // ItemContainer PutItem
                RegisterPatch("MorbidOptimism.Common.ItemContainer_Grid", "PutItem",
                    ref success, ref fail,
                    postfixType: typeof(Patch_ItemContainer));

                // ItemContainer _RemoveItem
                RegisterPatch("MorbidOptimism.Common.ItemContainer_Grid", "_RemoveItem",
                    ref success, ref fail,
                    postfixType: typeof(Patch_ItemContainer));

                FileLogger.Info($"===== Patch 注册完成：成功 {success}，失败 {fail} =====");

                // 4. 热键监听
                FileLogger.Info("启动热键监听...");
                HotkeyManager.Start();
                FileLogger.Info("热键监听已启动");

                FileLogger.Info("========== CWCD-ESP 启动完成 ==========");
            }
            catch (Exception ex)
            {
                FileLogger.Error("========== EntryPoint.Load 发生未捕获异常 ==========", ex);
                FileLogger.Error("ESP 功能可能不完整，请查看上方日志定位问题");
                // 不 re-throw：吞掉异常避免游戏崩溃
            }
        }

        /// <summary>单条 Patch 注册 + 日志。</summary>
        private static void RegisterPatch(
            string typeName, string methodName,
            ref int success, ref int fail,
            Type prefixType = null, Type postfixType = null)
        {
            FileLogger.Info($"注册 Patch: {typeName}.{methodName}...");
            try
            {
                PatchGuard.TryPatch(_harmony, typeName, methodName,
                    ref success, ref fail,
                    prefixType, postfixType);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"RegisterPatch({typeName}.{methodName}) 异常", ex);
                fail++;
            }
        }

        /// <summary>卸载（可选）：移除所有 Patch。</summary>
        public static void Unload()
        {
            FileLogger.Info("===== 开始卸载 =====");
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                FileLogger.Info("已卸载所有 Patch");
            }
            catch (Exception e)
            {
                FileLogger.Error("Unload 异常", e);
            }
            _loaded = false;
            FileLogger.Info("===== 卸载完成 =====");
        }

        /// <summary>统一日志输出（兼容旧调用，转发到 FileLogger + UnityEngine.Debug）。</summary>
        public static void Log(string msg)
        {
            FileLogger.Info(msg);
            try { UnityEngine.Debug.Log("[CWCD-ESP] " + msg); } catch { }
        }
    }
}
