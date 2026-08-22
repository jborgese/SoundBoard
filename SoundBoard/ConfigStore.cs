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
        /// Loads the config at startup, migrating a pre-1.3 <paramref name="legacyPath"/> config (next to the exe) to
        /// <paramref name="configPath"/> (in AppData) if one is found.
        /// </summary>
        /// <remarks>
        /// Loading and saving are delegated so that the caller can route them through the UI exactly as it always has:
        /// <paramref name="load"/> is expected to populate the UI from the file, and <paramref name="save"/> to write the UI back.
        /// When a legacy file exists it is loaded, saved to <paramref name="configPath"/>, and then moved to
        /// <paramref name="tempPath"/> (replacing any file already there) so that nothing is lost if the new file turns out
        /// to be bad. Otherwise <paramref name="configPath"/> is loaded directly.
        /// </remarks>
        /// <param name="legacyPath">The legacy config path.</param>
        /// <param name="configPath">The current config path.</param>
        /// <param name="tempPath">Where the legacy file is parked once migrated.</param>
        /// <param name="load">Loads the config at the given path.</param>
        /// <param name="save">Saves the current config to the given path.</param>
        /// <returns><see langword="true"/> if a legacy config was found and migrated.</returns>
        public static bool LoadWithLegacyMigration(string legacyPath, string configPath, string tempPath, Action<string> load, Action<string> save)
        {
            // For backwards compatibility, see if the legacy config file exists.
            if (File.Exists(legacyPath))
            {
                Logger.Info("Legacy config found at {0}; migrating to {1}", Path.GetFullPath(legacyPath), configPath);

                // Load the settings with the legacy path
                load(legacyPath);

                // Save the settings to the new path
                save(configPath);

                // Save the legacy config file in case there is an error
                if (File.Exists(tempPath)) File.Delete(tempPath);
                File.Move(legacyPath, tempPath);
                return true;
            }

            // The legacy file doesn't exist, so go ahead and load the new one
            load(configPath);
            return false;
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
