#region Usings

using System;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;

#endregion

namespace SoundBoard
{
    /// <summary>
    /// Owns the application's file logging. Configuration is done entirely in code (rather than via NLog.config)
    /// so that it survives being packed into a single executable by Costura.
    /// </summary>
    /// <remarks>
    /// Use <c>LogManager.GetCurrentClassLogger()</c> in each class to get a logger. Logging must never be able to
    /// crash the app, so everything here is best-effort: NLog itself swallows write failures by default, and
    /// <see cref="Initialize"/> swallows configuration failures.
    /// </remarks>
    internal static class Log
    {
        #region Public properties

        /// <summary>
        /// The folder that log files are written to. This is the same folder as the config file.
        /// </summary>
        public static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APPLICATION_NAME);

        /// <summary>
        /// Full path of the active log file.
        /// </summary>
        public static string LogFilePath => Path.Combine(LogDirectory, LOG_FILE_NAME);

        #endregion

        #region Public methods

        /// <summary>
        /// Sets up the rolling file target. Safe to call more than once; only the first call does anything.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                FileTarget fileTarget = new FileTarget("file")
                {
                    FileName = LogFilePath,
                    Layout = "${longdate} [${level:uppercase=true:padding=-5}] [${threadid}] ${logger:shortName=true}: ${message}${onexception:${newline}${exception:format=ToString}}",
                    ArchiveAboveSize = MAX_LOG_FILE_SIZE_BYTES,
                    MaxArchiveFiles = MAX_ARCHIVED_LOG_FILES,
                    ArchiveFileName = Path.Combine(LogDirectory, ARCHIVE_FILE_NAME),
                    ArchiveNumbering = ArchiveNumberingMode.Rolling,
                    KeepFileOpen = true,
                    ConcurrentWrites = false,
                    AutoFlush = true,
                    CreateDirs = true,
                    Encoding = System.Text.Encoding.UTF8
                };

                LoggingConfiguration config = new LoggingConfiguration();
                config.AddTarget(fileTarget);
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);

                LogManager.Configuration = config;
            }
            catch (Exception ex)
            {
                // We can't log that logging failed. Make a note in the debugger output and move on;
                // the app must keep working without a log file.
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logging: {ex}");
            }
        }

        /// <summary>
        /// Writes the startup banner: version, runtime, and environment details that are useful when reading a bug report.
        /// </summary>
        public static void LogStartup()
        {
            Logger logger = LogManager.GetLogger(nameof(App));

            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = assembly.GetName().Version?.ToString() ?? "unknown";

            logger.Info("================ SoundBoard {0} starting ================", version);
            logger.Info("Executable: {0}", assembly.Location);
            logger.Info("OS: {0}, CLR: {1}, 64-bit OS: {2}, 64-bit process: {3}", GetWindowsVersion(), Environment.Version, Environment.Is64BitOperatingSystem, Environment.Is64BitProcess);
            logger.Info("Elevated: {0}, UAC enabled: {1}", UACHelper.UACHelper.IsElevated, UACHelper.UACHelper.IsUACEnable);
            logger.Info("Log file: {0}", LogFilePath);
        }

        /// <summary>
        /// Returns a human-readable Windows version. <see cref="Environment.OSVersion"/> reports 6.2 on anything newer
        /// than Windows 8 unless the app carries a compatibility manifest, so read the registry instead.
        /// </summary>
        private static string GetWindowsVersion()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        string product = key.GetValue("ProductName") as string;
                        string display = key.GetValue("DisplayVersion") as string ?? key.GetValue("ReleaseId") as string;
                        string build = key.GetValue("CurrentBuild") as string;
                        string ubr = key.GetValue("UBR")?.ToString();
                        return $"{product} {display} (build {build}.{ubr})";
                    }
                }
            }
            catch
            {
                // Fall through to the (possibly inaccurate) managed value.
            }

            return Environment.OSVersion.VersionString;
        }

        /// <summary>
        /// Writes the shutdown line and flushes/closes the log file.
        /// </summary>
        public static void LogShutdownAndFlush(int exitCode)
        {
            try
            {
                LogManager.GetLogger(nameof(App)).Info("SoundBoard exiting with code {0}", exitCode);
                LogManager.Shutdown();
            }
            catch
            {
                // Nothing sensible to do if we can't flush on the way out.
            }
        }

        /// <summary>
        /// Flushes any buffered log output. Call before the process might be torn down (e.g. an unhandled exception).
        /// </summary>
        public static void Flush()
        {
            try
            {
                LogManager.Flush(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Best effort only.
            }
        }

        #endregion

        #region Private fields

        private static bool _initialized;

        #endregion

        #region Private consts

        private const string APPLICATION_NAME = @"SoundBoard";

        private const string LOG_FILE_NAME = @"soundboard.log";

        private const string ARCHIVE_FILE_NAME = @"soundboard.{#}.log";

        private const long MAX_LOG_FILE_SIZE_BYTES = 2 * 1024 * 1024; // 2 MB per file

        private const int MAX_ARCHIVED_LOG_FILES = 4; // Plus the active file = ~10 MB worst case

        #endregion
    }
}
