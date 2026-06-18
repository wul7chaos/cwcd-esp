using System;
using System.Diagnostics;

namespace CwcdEsp.Injector;

/// <summary>
/// 进程查找：按名称定位游戏进程，并定位 mono 运行时模块基址。
/// </summary>
internal static class ProcessFinder
{
    /// <summary>查找名为 processName 的进程（不区分大小写）。返回 null 表示未找到。</summary>
    public static Process? Find(string processName)
    {
        // 去掉可能的 .exe 后缀
        string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        foreach (var p in Process.GetProcessesByName(name))
        {
            return p; // 取第一个匹配
        }
        return null;
    }

    /// <summary>在目标进程中查找 mono 运行时模块基址与大小。</summary>
    public static bool TryFindMonoModule(Process process, out IntPtr baseAddress, out nuint size)
    {
        baseAddress = IntPtr.Zero;
        size = 0;

        // 候选模块名（Unity Mono 常见命名，覆盖多个 Unity 版本）
        //  - mono-2.0-sgen.dll : 旧版 Unity（SGEN GC）
        //  - mono-2.0-bdwgc.dll : Unity 6 / 新版 MonoBleedingEdge（BDWGC，查无此地 Demo 实测）
        //  - mono.dll           : 极旧版本
        string[] candidates = { "mono-2.0-bdwgc.dll", "mono-2.0-sgen.dll", "mono.dll" };

        try
        {
            foreach (ProcessModule module in process.Modules)
            {
                foreach (var c in candidates)
                {
                    if (string.Equals(module.ModuleName, c, StringComparison.OrdinalIgnoreCase))
                    {
                        baseAddress = module.BaseAddress;
                        size = (nuint)(ulong)module.ModuleMemorySize;
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] 枚举模块失败（可能权限不足或架构不匹配）: {ex.Message}");
        }
        return false;
    }
}
