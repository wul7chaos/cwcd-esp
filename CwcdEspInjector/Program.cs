using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CwcdEsp.Injector;

/// <summary>
/// CwcdEspInjector 入口。
/// 用法：
///   CwcdEspInjector.exe [进程名] [CwcdEspLibrary.dll 全路径] [命名空间] [类名] [方法名]
/// 默认：
///   进程名=NoSuchPlace  程序集=CwcdEspLibrary.dll  命名空间=CwcdEsp  类名=EntryPoint  方法名=Load
/// </summary>
internal static class Program
{
    private const string DefaultProcessName = "NoSuchPlace";
    private const string DefaultNamespace = "CwcdEsp";
    private const string DefaultClass = "EntryPoint";
    private const string DefaultMethod = "Load";

    private static int Main(string[] args)
    {
        LogConfig.Init();

        string processName = Arg(args, 0, DefaultProcessName);
        string assemblyPath = Arg(args, 1, ResolveDefaultAssemblyPath());
        string ns = Arg(args, 2, DefaultNamespace);
        string className = Arg(args, 3, DefaultClass);
        string methodName = Arg(args, 4, DefaultMethod);

        Log.Information("CWCD-ESP Injector v3 启动");
        Log.Information("  进程名   : {ProcessName}", processName);
        Log.Information("  程序集   : {AssemblyPath}", assemblyPath);
        Log.Information("  入口     : {Namespace}.{Class}.{Method}()", ns, className, methodName);

        if (!File.Exists(assemblyPath))
        {
            Log.Error("找不到程序集文件: {AssemblyPath}", assemblyPath);
            LogConfig.Shutdown();
            return 2;
        }

        // 1. 查找进程（支持等待游戏启动）
        Process? process = WaitForProcess(processName, timeoutSeconds: 60);
        if (process == null)
        {
            Log.Error("未找到进程 {ProcessName}", processName);
            LogConfig.Shutdown();
            return 3;
        }
        Log.Information("已定位进程: {ProcessName} (pid={Pid})", process.ProcessName, process.Id);

        // 1.5 等待游戏窗口加载（确保 Mono 运行时已初始化 root domain）
        WaitForGameWindow(process);

        // 2. 定位 mono 模块
        if (!ProcessFinder.TryFindMonoModule(process, out IntPtr monoBase, out nuint monoSize))
        {
            Log.Error("未在进程中找到 mono 运行时模块（mono-2.0-bdwgc.dll / mono-2.0-sgen.dll）。");
            Log.Error("可能原因：游戏使用 IL2CPP 后端（不兼容本方案），或权限不足。");
            LogConfig.Shutdown();
            return 4;
        }
        Log.Information("mono 模块 @ 0x{MonoBase:X} (size={MonoSize})", monoBase.ToInt64(), monoSize);

        // 3. 注入前：把依赖 DLL（0Harmony.dll）拷贝到游戏 Managed 目录
        EnsureDependenciesInManagedDir(process, assemblyPath);

        // 4. 注入（带重试：Mono root domain 可能尚未就绪）
        int maxRetries = 5;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            Log.Information("===== 注入尝试 {Attempt}/{Max} =====", attempt, maxRetries);

            // 检查进程是否还活着
            if (process.HasExited)
            {
                Log.Error("游戏进程已退出，终止注入");
                LogConfig.Shutdown();
                return 5;
            }

            try
            {
                using var injector = new MonoInjector();
                injector.Open((uint)process.Id);
                bool ok = injector.Inject(monoBase, assemblyPath, ns, className, methodName);
                if (ok)
                {
                    Log.Information("注入成功！");
                    LogConfig.Shutdown();
                    return 0;
                }

                if (attempt < maxRetries)
                {
                    Log.Warning("注入未成功，等待 3 秒后重试（Mono 运行时可能仍在初始化）...");
                    Thread.Sleep(3000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "注入异常 (尝试 {Attempt}/{Max})", attempt, maxRetries);
                if (attempt < maxRetries)
                {
                    Log.Information("等待 3 秒后重试...");
                    Thread.Sleep(3000);
                }
            }
        }

        Log.Error("===== 注入失败（已重试 {Max} 次）=====", maxRetries);
        Log.Error("请检查日志中的诊断信息，常见原因：");
        Log.Error("  1. Mono root domain = 0: 游戏未完全加载，请等游戏进入主菜单后再运行注入器");
        Log.Error("  2. assembly_open = 0: CwcdEspLibrary.dll 路径错误或 0Harmony.dll 缺失");
        Log.Error("  3. class_from_name = 0: 命名空间或类名错误");
        LogConfig.Shutdown();
        return 1;
    }

    /// <summary>
    /// 等待游戏主窗口出现 + 额外延迟，确保 Mono 运行时已初始化 root domain。
    /// 在进程刚启动时立即注入会导致 mono_get_root_domain() 返回 NULL，进而崩溃游戏。
    /// </summary>
    private static void WaitForGameWindow(Process process)
    {
        Log.Information("等待游戏窗口加载...");
        var sw = Stopwatch.StartNew();
        bool windowFound = false;
        while (sw.Elapsed.TotalSeconds < 30)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                windowFound = true;
                break;
            }
            Thread.Sleep(500);
        }

        if (windowFound)
        {
            Log.Information("游戏窗口已出现，额外等待 3 秒确保 Mono 初始化完成...");
            Thread.Sleep(3000);
        }
        else
        {
            Log.Warning("30秒内未检测到游戏窗口，尝试注入（可能失败）...");
        }
    }

    /// <summary>
    /// 把 0Harmony.dll 拷贝到游戏的 Managed 目录，确保 mono_domain_assembly_open
    /// 加载 CwcdEspLibrary.dll 时能解析到 Harmony 依赖。
    /// </summary>
    private static void EnsureDependenciesInManagedDir(Process process, string assemblyPath)
    {
        try
        {
            // 从进程主模块路径推断游戏 Managed 目录
            string gameExeDir = Path.GetDirectoryName(process.MainModule?.FileName) ?? "";
            if (string.IsNullOrEmpty(gameExeDir))
            {
                Log.Warning("无法获取游戏可执行文件路径，跳过依赖拷贝");
                return;
            }

            string gameName = Path.GetFileNameWithoutExtension(process.MainModule?.FileName ?? "Game");
            string managedDir = Path.Combine(gameExeDir, $"{gameName}_Data", "Managed");

            if (!Directory.Exists(managedDir))
            {
                Log.Warning("游戏 Managed 目录不存在: {ManagedDir}，跳过依赖拷贝", managedDir);
                return;
            }

            // 拷贝 0Harmony.dll（与 CwcdEspLibrary.dll 同目录）
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
            string[] deps = { "0Harmony.dll" };
            foreach (var dep in deps)
            {
                string src = Path.Combine(assemblyDir, dep);
                string dst = Path.Combine(managedDir, dep);
                if (!File.Exists(src))
                {
                    Log.Warning("依赖 DLL 不存在于注入器目录: {Src}", src);
                    continue;
                }

                bool needsCopy = true;
                if (File.Exists(dst))
                {
                    // 版本不同才拷贝
                    var srcInfo = new FileInfo(src);
                    var dstInfo = new FileInfo(dst);
                    if (srcInfo.Length == dstInfo.Length &&
                        File.GetLastWriteTime(src) == File.GetLastWriteTime(dst))
                    {
                        needsCopy = false;
                    }
                }

                if (needsCopy)
                {
                    File.Copy(src, dst, overwrite: true);
                    Log.Information("已拷贝 {Dep} → {Dst}", dep, dst);
                }
                else
                {
                    Log.Debug("{Dep} 已存在于 Managed 目录，跳过", dep);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "依赖拷贝失败（非致命，但可能导致注入时依赖解析失败）");
        }
    }

    /// <summary>等待指定进程出现，最多 timeoutSeconds 秒。</summary>
    private static Process? WaitForProcess(string name, int timeoutSeconds)
    {
        Process? p = ProcessFinder.Find(name);
        if (p != null) return p;

        Log.Information("等待进程 {Name} 启动（最多 {Timeout}s）...", name, timeoutSeconds);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            Thread.Sleep(500);
            p = ProcessFinder.Find(name);
            if (p != null) return p;
        }
        return null;
    }

    private static string ResolveDefaultAssemblyPath()
    {
        string dir = AppContext.BaseDirectory;
        return Path.Combine(dir, "CwcdEspLibrary.dll");
    }

    private static string Arg(string[] args, int index, string fallback)
        => index < args.Length && !string.IsNullOrWhiteSpace(args[index]) ? args[index]! : fallback;
}
