using System;
using HarmonyLib;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// Harmony 注册容错工具（方案 7.2）。
    /// 用全限定类名字符串定位目标类型，避免编译期硬依赖游戏 DLL（找不到类型时跳过而非崩溃）。
    /// 每条 Patch 独立 try-catch，单个失败不影响其他模块。
    /// </summary>
    public static class PatchGuard
    {
        /// <summary>
        /// 尝试注册一条 Patch。
        /// </summary>
        /// <param name="harmony">Harmony 实例</param>
        /// <param name="targetTypeName">目标类型全限定名（如 "GameController"）</param>
        /// <param name="targetMethodName">目标方法名（如 "OnGUI"）</param>
        /// <param name="prefixType">Prefix 所在类型（可空）</param>
        /// <param name="postfixType">Postfix 所在类型（可空）</param>
        /// <param name="success">成功计数</param>
        /// <param name="fail">失败计数</param>
        /// <param name="prefixMethodName">Prefix 方法名，默认 "Prefix"</param>
        /// <param name="postfixMethodName">Postfix 方法名，默认 "Postfix"</param>
        public static void TryPatch(
            Harmony harmony,
            string targetTypeName,
            string targetMethodName,
            ref int success,
            ref int fail,
            Type prefixType = null,
            Type postfixType = null,
            string prefixMethodName = "Prefix",
            string postfixMethodName = "Postfix")
        {
            try
            {
                Type targetType = AccessTools.TypeByName(targetTypeName);
                if (targetType == null)
                {
                    FileLogger.Warn($"类型 {targetTypeName} 未找到，跳过 {targetMethodName}");
                    fail++;
                    return;
                }
                var original = AccessTools.Method(targetType, targetMethodName);
                if (original == null)
                {
                    FileLogger.Warn($"方法 {targetTypeName}.{targetMethodName} 未找到，跳过");
                    fail++;
                    return;
                }

                HarmonyMethod prefix = prefixType != null
                    ? new HarmonyMethod(prefixType, prefixMethodName)
                    : null;
                HarmonyMethod postfix = postfixType != null
                    ? new HarmonyMethod(postfixType, postfixMethodName)
                    : null;

                harmony.Patch(original, prefix, postfix);
                success++;
                FileLogger.Info($"OK: Patch {targetTypeName}.{targetMethodName}");
            }
            catch (Exception e)
            {
                FileLogger.Error($"注册 {targetTypeName}.{targetMethodName} 失败: {e.Message}", e);
                fail++;
            }
        }
    }
}
