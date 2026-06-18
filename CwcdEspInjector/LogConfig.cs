using Serilog;
using Serilog.Events;
using System.IO;

namespace CwcdEsp.Injector;

/// <summary>
/// Serilog 日志配置：同时输出到控制台和文件。
/// 日志文件位于注入器同目录下 cwcd-esp-injector.log，滚动保留 3 个。
/// </summary>
internal static class LogConfig
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        string logPath = Path.Combine(AppContext.BaseDirectory, "cwcd-esp-injector.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 3,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("========== CWCD-ESP Injector 日志启动 ==========");
        Log.Information("日志文件: {LogPath}", logPath);
    }

    public static void Shutdown()
    {
        Log.Information("========== 日志关闭 ==========");
        Log.CloseAndFlush();
    }
}
