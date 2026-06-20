using System;
using System.IO;

namespace CwcdEsp.Injector;

/// <summary>
/// 轻量日志：输出到控制台 + 文件，支持 AOT，零依赖。
/// 日志文件在注入器同目录 cwcd-esp-injector.log。
/// </summary>
internal static class Log
{
    private static readonly object _lock = new();
    private static string? _filePath;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _filePath = Path.Combine(AppContext.BaseDirectory, "cwcd-esp-injector.log");

        // 启动时截断旧日志（保留最近 200KB）
        try
        {
            if (File.Exists(_filePath) && new FileInfo(_filePath).Length > 200 * 1024)
                File.WriteAllText(_filePath, "");
        }
        catch { /* 忽略 */ }

        Info("========== CWCD-ESP Injector 日志启动 ==========");
        Info($"日志文件: {_filePath}");
    }

    public static void Shutdown()
    {
        Info("========== 日志关闭 ==========");
    }

    public static void Debug(string msg) => Write("DBG", msg);
    public static void Info(string msg)  => Write("INF", msg);
    public static void Warn(string msg)  => Write("WRN", msg);
    public static void Error(string msg) => Write("ERR", msg);

    public static void Debug(string fmt, params object[] args) => Write("DBG", string.Format(fmt, args));
    public static void Info(string fmt, params object[] args)  => Write("INF", string.Format(fmt, args));
    public static void Warn(string fmt, params object[] args)  => Write("WRN", string.Format(fmt, args));
    public static void Error(string fmt, params object[] args) => Write("ERR", string.Format(fmt, args));

    public static void Error(string msg, Exception ex)
    {
        Write("ERR", msg);
        Write("ERR", $"  Exception: {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace != null)
            Write("ERR", $"  StackTrace:\n{ex.StackTrace}");
    }

    private static void Write(string level, string msg)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}";

        // 控制台
        Console.WriteLine(line);

        // 文件（加锁写入）
        if (_filePath == null) return;
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }
        catch { /* 忽略写入失败 */ }
    }
}
