using System;
using System.IO;
using System.Linq;
using NLog;
using SoundBoard.Model;

namespace SoundBoard
{
    /// <summary>
    /// Reads and writes <c>soundboard.config</c> files on disk: directory creation, timestamped <c>.bak</c> copies before
    /// each write, and pruning of old backups. The XML itself is handled by <see cref="ConfigSerializer"/>.
    /// </summary>
    internal static class ConfigStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Number of <c>.bak</c> files kept by <see cref="CleanupBackups"/>.
        /// </summary>
        public const int MaxBackupFiles = 5;

        /// <summary>
        /// Reads a config file.
        /// </summary>
        /// <param name="path">File to read.</param>
        /// <param name="defaults">Settings to fall back on for values the file does not specify (see <see cref="ConfigSerializer.Read(string, Action{string}, BoardSettings)"/>).</param>
        /// <exception cref="Exception">Any failure to read or parse the file propagates to the caller.</exception>
        public static SoundBoardConfig Load(string path, BoardSettings defaults = null)
        {
            return ConfigSerializer.Read(path, warning => Logger.Warn("{0}: {1}", path, warning), defaults);
        }

        /// <summary>
        /// Writes a config file, first copying any existing file at <paramref name="path"/> to a timestamped <c>.bak</c> next to it.
        /// </summary>
        /// <exception cref="Exception">Any failure to write propagates to the caller.</exception>
        public static void Save(string path, SoundBoardConfig config)
        {
            // Ensure that the directory for the given config file exists
            try
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                // Ignored: if the directory really couldn't be created, opening the file below will fail with a better error.
                Logger.Warn(ex, "Could not create config directory for {0}", path);
            }

            // If a config file already exists, create a backup in case any part of the saving fails
            if (File.Exists(path))
            {
                File.Copy(path, $"{path}-{DateTimeStamp()}.bak", true);
            }

            ConfigSerializer.Write(config, path);
        }

        /// <summary>
        /// Deletes all but the newest <see cref="MaxBackupFiles"/> <c>.bak</c> files in the directory of <paramref name="configPath"/>.
        /// Never throws.
        /// </summary>
        public static void CleanupBackups(string configPath)
        {
            // This typically runs on a background task with nothing awaiting it, so any exception here would otherwise be lost.
            try
            {
                string directory = Path.GetDirectoryName(configPath);
                if (directory != null && Directory.Exists(directory))
                {
                    var files = Directory.GetFiles(directory, "*.bak").OrderByDescending(File.GetCreationTime).ToList();
                    if (files.Count > MaxBackupFiles)
                    {
                        files.Skip(MaxBackupFiles).ToList().ForEach(File.Delete);
                        Logger.Debug("Deleted {0} old config backup(s)", files.Count - MaxBackupFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to clean up old config backups");
            }
        }

        /// <summary>
        /// A file-name-safe timestamp, e.g. <c>2026-08-21T13.40.00</c>.
        /// </summary>
        public static string DateTimeStamp() => DateTime.Now.ToString(@"s").Replace(@":", @".");
    }
}
