using System;
using System.IO;
using System.Text;

namespace CwcdEsp.Utils
{
    /// <summary>
    /// 配置持久化：将 EspConfig 的可调参数保存到文本文件 / 从文本文件加载。
    ///
    /// 文件位置：CwcdEspLibrary.dll 同目录（即 deploy/ 目录）。
    /// DLL 被注入到游戏进程内运行，但 typeof(EspConfig).Assembly.Location
    /// 返回的是 DLL 加载时的路径（注入器指定的绝对路径），所以配置文件
    /// 始终在 deploy/ 目录，与游戏运行目录无关。
    ///
    /// 文件格式：简单的 key=value 文本，UTF-8 编码，每行一个配置项。
    /// </summary>
    public static class ConfigFile
    {
        private static string _configPath;
        private static readonly object _lock = new object();

        /// <summary>配置文件完整路径。</summary>
        public static string ConfigPath
        {
            get
            {
                if (_configPath == null)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(typeof(EspConfig).Assembly.Location);
                        if (string.IsNullOrEmpty(dir)) dir = ".";
                        _configPath = Path.Combine(dir, "cwcd-esp-config.txt");
                    }
                    catch
                    {
                        _configPath = "cwcd-esp-config.txt";
                    }
                }
                return _configPath;
            }
        }

        /// <summary>加载配置文件。若文件不存在则使用默认值（不报错）。</summary>
        public static void Load()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path))
                {
                    FileLogger.Info($"[ConfigFile] 配置文件不存在，使用默认值: {path}");
                    return;
                }

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int loaded = 0;
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (TryApply(key, val)) loaded++;
                }
                FileLogger.Info($"[ConfigFile] 加载完成: {loaded} 项配置从 {path}");
            }
            catch (Exception ex)
            {
                FileLogger.Warn($"[ConfigFile] 加载失败: {ex.Message}");
            }
        }

        /// <summary>保存当前配置到文件。</summary>
        public static void Save()
        {
            try
            {
                lock (_lock)
                {
                    string path = ConfigPath;
                    var sb = new StringBuilder(512);
                    sb.AppendLine("# CWCD-ESP 配置文件");
                    sb.AppendLine("# 由程序自动生成，修改后重启游戏生效（或重新注入）");
                    sb.AppendLine();

                    sb.AppendLine("# ===== 功能开关 =====");
                    sb.AppendLine($"BoxEspEnabled = {EspConfig.BoxEspEnabled}");
                    sb.AppendLine($"LootEspEnabled = {EspConfig.LootEspEnabled}");
                    sb.AppendLine($"BulletTrackingEnabled = {EspConfig.BulletTrackingEnabled}");
                    sb.AppendLine($"OverlayVisible = {EspConfig.OverlayVisible}");
                    sb.AppendLine($"DrawPlayerEnemyLines = {EspConfig.DrawPlayerEnemyLines}");
                    sb.AppendLine($"DashedBoxWhenIdle = {EspConfig.DashedBoxWhenIdle}");
                    sb.AppendLine();

                    sb.AppendLine("# ===== 物资透视 =====");
                    sb.AppendLine($"EnableLootFilter = {EspConfig.EnableLootFilter}");
                    sb.AppendLine($"MinItemValue = {EspConfig.MinItemValue}");
                    sb.AppendLine($"ShowItemValue = {EspConfig.ShowItemValue}");
                    sb.AppendLine($"MinRarity = {EspConfig.MinRarity}");
                    sb.AppendLine($"MaxLootItemsPerContainer = {EspConfig.MaxLootItemsPerContainer}");
                    sb.AppendLine();

                    sb.AppendLine("# ===== 方框透视 =====");
                    sb.AppendLine($"BoxThickness = {EspConfig.BoxThickness}");
                    sb.AppendLine();

                    sb.AppendLine("# ===== 子弹追踪 =====");
                    sb.AppendLine($"TrackingAngle = {EspConfig.TrackingAngle}");
                    sb.AppendLine($"TrackingDistance = {EspConfig.TrackingDistance}");

                    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                    FileLogger.Info($"[ConfigFile] 配置已保存到 {path}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Warn($"[ConfigFile] 保存失败: {ex.Message}");
            }
        }

        /// <summary>尝试将单个 key=value 应用到 EspConfig。未知 key 忽略。</summary>
        private static bool TryApply(string key, string val)
        {
            try
            {
                switch (key)
                {
                    // bool
                    case "BoxEspEnabled": EspConfig.BoxEspEnabled = ParseBool(val); return true;
                    case "LootEspEnabled": EspConfig.LootEspEnabled = ParseBool(val); return true;
                    case "BulletTrackingEnabled": EspConfig.BulletTrackingEnabled = ParseBool(val); return true;
                    case "OverlayVisible": EspConfig.OverlayVisible = ParseBool(val); return true;
                    case "DrawPlayerEnemyLines": EspConfig.DrawPlayerEnemyLines = ParseBool(val); return true;
                    case "DashedBoxWhenIdle": EspConfig.DashedBoxWhenIdle = ParseBool(val); return true;
                    case "EnableLootFilter": EspConfig.EnableLootFilter = ParseBool(val); return true;
                    case "ShowItemValue": EspConfig.ShowItemValue = ParseBool(val); return true;
                    // int
                    case "MinItemValue": EspConfig.MinItemValue = int.Parse(val); return true;
                    case "MinRarity": EspConfig.MinRarity = int.Parse(val); return true;
                    case "MaxLootItemsPerContainer": EspConfig.MaxLootItemsPerContainer = int.Parse(val); return true;
                    case "BoxThickness": EspConfig.BoxThickness = int.Parse(val); return true;
                    // float
                    case "TrackingAngle": EspConfig.TrackingAngle = float.Parse(val); return true;
                    case "TrackingDistance": EspConfig.TrackingDistance = float.Parse(val); return true;
                }
            }
            catch
            {
                // 解析失败忽略，保留默认值
            }
            return false;
        }

        private static bool ParseBool(string val)
        {
            if (string.IsNullOrEmpty(val)) return false;
            val = val.Trim().ToLowerInvariant();
            return val == "true" || val == "1" || val == "on" || val == "yes";
        }
    }
}
