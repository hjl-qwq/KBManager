using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KBManager.core
{
    /// <summary>
    /// Minimal append-only diagnostic log.
    ///
    /// Written to a <c>logs/</c> folder next to the executable (falling back to the
    /// per-user data folder when the install directory is read-only) so that a user
    /// who hits a failure can always find out what happened. Every entry is also
    /// kept in a bounded in-memory ring buffer so the GUI can display recent
    /// activity without opening a file.
    ///
    /// This type never throws: diagnostics must not be able to break the app.
    /// </summary>
    public static class AppLog
    {
        private const int RecentLimit = 800;
        private const string LogFolderName = "logs";

        private static readonly object Gate = new();
        private static readonly Queue<string> Recent = new();
        private static readonly List<string> SessionErrors = new();

        private static bool _initialized;
        private static string? _logFilePath;

        /// <summary>Full path of the log file currently being written.</summary>
        public static string LogFilePath
        {
            get
            {
                EnsureInitialized();
                return _logFilePath ?? "(日志不可用)";
            }
        }

        /// <summary>Folder holding the log file.</summary>
        public static string LogDirectory => Path.GetDirectoryName(LogFilePath) ?? string.Empty;

        /// <summary>Create the log file. Safe to call more than once.</summary>
        public static void Initialize(string? logDirectory = null)
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var directory = logDirectory ?? PickLogDirectory();
                Directory.CreateDirectory(directory);
                var fileName = $"kbm-{DateTime.Now:yyyyMMdd}.log";
                _logFilePath = Path.Combine(directory, fileName);
                Write("INFO", $"==== KBManager 日志开始 · {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====");
            }
            catch
            {
                // No writable location: keep going with the in-memory buffer only.
                _logFilePath = null;
            }
        }

        /// <summary>Prefer the app folder; fall back to user data if it is read-only.</summary>
        private static string PickLogDirectory()
        {
            var besideExe = Path.Combine(AppContext.BaseDirectory, LogFolderName);
            try
            {
                Directory.CreateDirectory(besideExe);
                var probe = Path.Combine(besideExe, ".writable");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return besideExe;
            }
            catch
            {
                var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(userRoot))
                    userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userRoot, "KBManager", LogFolderName);
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message, Exception? exception = null)
        {
            var detail = exception == null
                ? message
                : $"{message} :: {exception.GetType().Name}: {exception.Message}";

            if (exception != null) detail += Environment.NewLine + exception;

            lock (Gate) SessionErrors.Add(detail);
            Write("ERROR", detail);
        }

        /// <summary>Recent log lines, newest last (for the in-app log viewer).</summary>
        public static IReadOnlyList<string> Snapshot()
        {
            lock (Gate) return Recent.ToArray();
        }

        /// <summary>Number of errors recorded this session.</summary>
        public static int ErrorCount
        {
            get { lock (Gate) return SessionErrors.Count; }
        }

        /// <summary>Everything written this session as one string.</summary>
        public static string SnapshotText()
        {
            lock (Gate) return string.Join(Environment.NewLine, Recent);
        }

        private static void Write(string level, string message)
        {
            // Initialise on first use so entries are never silently dropped just
            // because a caller logged before the app got around to initialising.
            EnsureInitialized();

            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";

            try
            {
                lock (Gate)
                {
                    Recent.Enqueue(line);
                    while (Recent.Count > RecentLimit) Recent.Dequeue();
                }
            }
            catch
            {
                // ignored
            }

            try
            {
                if (_logFilePath != null)
                    File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>Ensure a log file exists even if the caller never initialized.</summary>
        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }
    }
}
