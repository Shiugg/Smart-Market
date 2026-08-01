using System;
using System.IO;
using System.Text;
using UnityEngine;
using MelonLoader;

namespace SmartMarket
{
    public static class SmartMarketLogger
    {
        private static readonly object _lock = new object();
        private static StreamWriter _writer;
        private static string _path;
        private static bool _initialized;

        static SmartMarketLogger()
        {
            Init();
        }

        public static string LogFilePath => _path;

        public static void Init()
        {
            try
            {
                if (_initialized && _writer != null) return;

                var baseDir = Application.persistentDataPath;
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = Environment.CurrentDirectory;
                }

                Directory.CreateDirectory(baseDir);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _path = Path.Combine(baseDir, $"SmartMarket_{timestamp}.log");

                var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
                _initialized = true;

                WriteInternal("INFO", "Logger initialized.");
                var initMessage = $"[SmartMarket][LOGGER] Log file: {_path}";
                WriteInternal("INFO", initMessage);
                MelonLoader.MelonLogger.Msg(initMessage);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[SmartMarket][LOGGER] No se pudo inicializar SmartMarketLogger: {ex.Message}");
            }
        }

        private static void WriteInternal(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    if (_writer == null) Init();
                    if (_writer == null || string.IsNullOrEmpty(message)) return;

                    var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _writer.WriteLine($"[{ts}] [{level}] {message}");
                }
            }
            catch (Exception ex)
            {
                try { MelonLoader.MelonLogger.Warning($"[SmartMarket][LOGGER] Error writing log: {ex.Message}"); } catch { }
            }
        }

        private static string ApplyCategory(string category, string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            if (string.IsNullOrEmpty(category)) return message;

            var normalizedCategory = category.Trim().Trim('[', ']').ToUpperInvariant();
            if (string.IsNullOrEmpty(normalizedCategory)) return message;

            if (message.StartsWith("[", StringComparison.Ordinal))
            {
                var end = message.IndexOf(']');
                if (end > 0)
                {
                    var tag = message.Substring(1, end - 1).Trim().ToUpperInvariant();
                    if (tag.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
                        return message;
                }
            }

            return $"[{normalizedCategory}] {message}";
        }

        public static void Msg(string message)
        {
            Msg(null, message);
        }

        public static void Msg(string category, string message)
        {
            var formattedMessage = ApplyCategory(category, message);
            WriteInternal("INFO", formattedMessage);
            try { MelonLoader.MelonLogger.Msg(formattedMessage); } catch { }
        }

        public static void Warning(string message)
        {
            Warning(null, message);
        }

        public static void Warning(string category, string message)
        {
            var formattedMessage = ApplyCategory(category, message);
            WriteInternal("WARN", formattedMessage);
            try { MelonLoader.MelonLogger.Warning(formattedMessage); } catch { }
        }

        public static void Error(string message)
        {
            Error(null, message);
        }

        public static void Error(string category, string message)
        {
            var formattedMessage = ApplyCategory(category, message);
            WriteInternal("ERROR", formattedMessage);
            try { MelonLoader.MelonLogger.Error(formattedMessage); } catch { }
        }

        public static void Debug(string message)
        {
            Debug(null, message);
        }

        public static void Debug(string category, string message)
        {
            var formattedMessage = ApplyCategory(category, message);
            WriteInternal("DEBUG", formattedMessage);
            try { MelonLoader.MelonLogger.Msg(formattedMessage); } catch { }
        }

        public static void Flush()
        {
            try
            {
                lock (_lock)
                {
                    _writer?.Flush();
                }
            }
            catch { }
        }
    }
}
