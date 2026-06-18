using System;
using System.IO;
using System.Text;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// 自包含文件日志器（不引入外部 NuGet 依赖，避免注入到 Unity Mono 时依赖解析失败）。
    ///
    /// 设计原则：
    /// - 线程安全（lock）
    /// - 全 try-catch，日志本身绝不能导致崩溃
    /// - 带时间戳 + 级别
    /// - 日志文件位于注入器同目录（CwcdEspLibrary.dll 旁边）
    /// - 超过 2MB 自动滚动（cwcd-esp.log → cwcd-esp.1.log）
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string _logPath;
        private static long _maxSize = 2 * 1024 * 1024; // 2MB

        /// <summary>初始化日志文件路径。在 EntryPoint.Load 开头调用。</summary>
        public static void Init()
        {
            try
            {
                // 日志文件放在 CwcdEspLibrary.dll 同目录（即 deploy/ 目录）
                string dir = Path.GetDirectoryName(typeof(FileLogger).Assembly.Location);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                _logPath = Path.Combine(dir, "cwcd-esp.log");
                RawLog("INFO", "========== CWCD-ESP 日志启动 ==========");
                RawLog("INFO", $"日志文件: {_logPath}");
                RawLog("INFO", $"运行时: {AppDomain.CurrentDomain.FriendlyName}");
                RawLog("INFO", $".NET 版本: {Environment.Version}");
            }
            catch
            {
                // 初始化失败时静默，后续 RawLog 会兜底
            }
        }

        public static void Info(string msg) => RawLog("INFO", msg);
        public static void Warn(string msg) => RawLog("WARN", msg);
        public static void Error(string msg) => RawLog("ERROR", msg);
        public static void Error(string msg, Exception ex) => RawLog("ERROR", msg + "\n" + ex);

        private static void RawLog(string level, string msg)
        {
            try
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(_logPath)) return;

                    // 滚动检查
                    try
                    {
                        var fi = new FileInfo(_logPath);
                        if (fi.Exists && fi.Length > _maxSize)
                        {
                            string backup = _logPath.Replace(".log", ".1.log");
                            if (File.Exists(backup)) File.Delete(backup);
                            File.Move(_logPath, backup);
                        }
                    }
                    catch { /* 滚动失败不影响写入 */ }

                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}\n";
                    File.AppendAllText(_logPath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志绝不能导致崩溃
            }
        }
    }
}
