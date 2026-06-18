using System;
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
        Console.Title = "CWCD-ESP Injector";
        PrintBanner();

        string processName = Arg(args, 0, DefaultProcessName);
        string assemblyPath = Arg(args, 1, ResolveDefaultAssemblyPath());
        string ns = Arg(args, 2, DefaultNamespace);
        string className = Arg(args, 3, DefaultClass);
        string methodName = Arg(args, 4, DefaultMethod);

        Console.WriteLine($"[*] 进程名     : {processName}");
        Console.WriteLine($"[*] 程序集     : {assemblyPath}");
        Console.WriteLine($"[*] 入口       : {ns}.{className}.{methodName}()");

        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"[!] 找不到程序集文件: {assemblyPath}");
            Console.WriteLine("    请先用 build_library.bat 编译 CwcdEspLibrary，或手动指定路径。");
            return 2;
        }

        // 1. 查找进程（支持等待游戏启动）
        Process? process = WaitForProcess(processName, timeoutSeconds: 60);
        if (process == null)
        {
            Console.WriteLine($"[!] 未找到进程 {processName}");
            return 3;
        }
        Console.WriteLine($"[+] 已定位进程: {process.ProcessName} (pid={process.Id})");

        // 2. 定位 mono 模块
        if (!ProcessFinder.TryFindMonoModule(process, out IntPtr monoBase, out nuint monoSize))
        {
            Console.WriteLine("[!] 未在进程中找到 mono 运行时模块（mono-2.0-sgen.dll）。");
            Console.WriteLine("    可能原因：游戏使用 IL2CPP 后端（不兼容本方案），或权限不足。");
            return 4;
        }
        Console.WriteLine($"[+] mono 模块 @ 0x{monoBase.ToInt64():X} (size={monoSize})");

        // 3. 注入
        try
        {
            using var injector = new MonoInjector();
            injector.Open((uint)process.Id);
            injector.Inject(monoBase, assemblyPath, ns, className, methodName);
            Console.WriteLine("[√] 注入流程完成。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[X] 注入失败: {ex.Message}");
            return 1;
        }
    }

    /// <summary>等待指定进程出现，最多 timeoutSeconds 秒。</summary>
    private static Process? WaitForProcess(string name, int timeoutSeconds)
    {
        Process? p = ProcessFinder.Find(name);
        if (p != null) return p;

        Console.WriteLine($"[*] 等待进程 {name} 启动（最多 {timeoutSeconds}s）...");
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
        // 默认与注入器同目录下的 CwcdEspLibrary.dll
        string dir = AppContext.BaseDirectory;
        string p = Path.Combine(dir, "CwcdEspLibrary.dll");
        return p;
    }

    private static string Arg(string[] args, int index, string fallback)
        => index < args.Length && !string.IsNullOrWhiteSpace(args[index]) ? args[index]! : fallback;

    private static void PrintBanner()
    {
        Console.WriteLine("================================================");
        Console.WriteLine("  CWCD-ESP Injector v3  (Mono DLL 注入)");
        Console.WriteLine("  个人单机护肝工具 · 仅限自用");
        Console.WriteLine("================================================");
    }
}
