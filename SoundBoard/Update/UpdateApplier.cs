using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Bluegrams.Application;

namespace SoundBoard.Update
{
    /// <summary>
    /// Replaces the running executable with a verified update and restarts the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows allows a running executable to be <em>renamed</em> (but not deleted or overwritten), so the swap is
    /// done in-process with two file moves and no shell: <c>SoundBoard.exe → SoundBoard.exe.old</c>, then the
    /// download into <c>SoundBoard.exe</c>. For a portable exe in a user-writable folder this needs no elevation.
    /// </para>
    /// <para>
    /// Only if the folder is not writable (e.g. the exe lives under Program Files) is elevation requested, and then
    /// the elevated process is this same executable started with <see cref="ApplyUpdateSwitch"/>. That mode is
    /// tightly constrained: it can only replace <em>its own</em> image, and only with a file whose SHA-256 equals the
    /// hash passed on the command line, which the non-elevated caller has already verified against the manifest.
    /// </para>
    /// <para>
    /// The new executable is always started by the non-elevated process so it runs with the user's normal token
    /// (an elevated instance breaks drag-and-drop because of UIPI).
    /// </para>
    /// </remarks>
    internal static class UpdateApplier
    {
        /// <summary>
        /// Command-line switch that turns the executable into the elevated swap helper.
        /// Usage: <c>SoundBoard.exe --apply-update &lt;downloaded file&gt; &lt;sha256&gt;</c>.
        /// </summary>
        public const string ApplyUpdateSwitch = "--apply-update";

        /// <summary>
        /// Extension appended to the previous executable when it is renamed out of the way.
        /// </summary>
        public const string BackupExtension = ".old";

        private const int ErrorCancelled = 1223;

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Full path of the running executable. <see cref="Assembly.Location"/> is a real file system path;
        /// <c>Assembly.CodeBase</c> is a URI and mangles characters such as '#' and '%'.
        /// </summary>
        public static string CurrentExecutablePath => Assembly.GetExecutingAssembly().Location;

        /// <summary>
        /// Path the previous executable is renamed to.
        /// </summary>
        public static string GetBackupPath(string executablePath) => executablePath + BackupExtension;

        #region Normal (non-elevated) flow

        /// <summary>
        /// Verifies <paramref name="downloadedFile"/> against <paramref name="expectedHash"/>, swaps it into place
        /// (elevating only if required), then starts the new executable and shuts this instance down cleanly so that
        /// settings are saved.
        /// </summary>
        /// <exception cref="UpdateApplyException">If any step fails. The running executable is left intact.</exception>
        public static void ApplyAndRestart(string downloadedFile, string expectedHash)
        {
            string target = CurrentExecutablePath;

            // Re-verify immediately before touching anything. The file sits in %TEMP% between the library's
            // download and this point, so this closes the window for it to have been replaced.
            if (!UpdateVerifier.FileMatches(downloadedFile, expectedHash))
            {
                throw new UpdateApplyException(Properties.Resources.UpdateHashChangedBeforeApply);
            }

            try
            {
                Swap(downloadedFile, target);
                Logger.Info("Replaced {0} in-process (no elevation needed)", target);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Info(ex, "Cannot write to {0}; requesting elevation", Path.GetDirectoryName(target));
                SwapElevated(downloadedFile, target, expectedHash);
            }
            catch (IOException ex)
            {
                throw new UpdateApplyException(string.Format(Properties.Resources.UpdateCouldNotReplaceExecutable, ex.Message), ex);
            }

            if (!UpdateVerifier.FileMatches(target, expectedHash))
            {
                throw new UpdateApplyException(Properties.Resources.UpdateReplacedExecutableHashMismatch);
            }

            RestartInto(target);
        }

        /// <summary>
        /// Starts <paramref name="executable"/> once this application has shut down, then begins the shutdown. The
        /// ordering matters: windows close (and save settings) before the new instance starts and reads them.
        /// </summary>
        private static void RestartInto(string executable)
        {
            Application app = Application.Current;

            if (app == null)
            {
                StartNewInstance(executable);
                return;
            }

            app.Exit += (_, __) => StartNewInstance(executable);
            app.Dispatcher.BeginInvoke(new Action(app.Shutdown));
        }

        private static void StartNewInstance(string executable)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable) ?? string.Empty,
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                // Nothing sensible can be shown at this point; the user can simply start the app again.
                Logger.Error(ex, "Failed to start the updated executable {0}", executable);
            }
        }

        #endregion

        #region Swap

        /// <summary>
        /// Renames <paramref name="target"/> to its backup path and moves <paramref name="source"/> into its place.
        /// If the second move fails, the first is rolled back so the target is never left missing.
        /// </summary>
        /// <exception cref="UnauthorizedAccessException">If the target's directory is not writable.</exception>
        /// <exception cref="IOException">If a move fails for another reason.</exception>
        internal static void Swap(string source, string target)
        {
            string backup = GetBackupPath(target);

            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(target, backup);

            try
            {
                // File.Move copies and deletes when source and target are on different volumes (%TEMP% is usually
                // on the system drive; the exe may not be).
                File.Move(source, target);
            }
            catch
            {
                try
                {
                    File.Move(backup, target);
                }
                catch (Exception rollbackEx)
                {
                    Logger.Error(rollbackEx, "Rollback of {0} failed", target);
                }

                throw;
            }
        }

        #endregion

        #region Elevated flow

        private static void SwapElevated(string downloadedFile, string target, string expectedHash)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = target,
                Arguments = CommandLine.Join(new[] { ApplyUpdateSwitch, downloadedFile, expectedHash }),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            Process helper;
            try
            {
                helper = Process.Start(startInfo);
            }
            catch (Win32Exception ex)
            {
                throw new UpdateApplyException(ex.NativeErrorCode == ErrorCancelled
                    ? Properties.Resources.UpdateElevationCancelled
                    : string.Format(Properties.Resources.UpdateElevatedHelperStartFailed, ex.Message), ex);
            }

            using (helper)
            {
                if (helper == null)
                {
                    throw new UpdateApplyException(Properties.Resources.UpdateElevatedHelperNotStarted);
                }

                helper.WaitForExit();

                if (helper.ExitCode != 0)
                {
                    throw new UpdateApplyException(string.Format(Properties.Resources.UpdateElevatedHelperFailed, helper.ExitCode));
                }
            }
        }

        /// <summary>
        /// Entry point for <c>SoundBoard.exe --apply-update &lt;source&gt; &lt;sha256&gt;</c>. Replaces this process's
        /// own executable with <paramref name="source"/> after verifying its hash. Never starts the new executable;
        /// the non-elevated caller does that.
        /// </summary>
        /// <returns>The process exit code: 0 on success.</returns>
        public static int RunApplyUpdateMode(string[] args)
        {
            return RunApplyUpdateMode(args, CurrentExecutablePath);
        }

        /// <summary>
        /// Testable core of <see cref="RunApplyUpdateMode(string[])"/> with the target executable made explicit.
        /// </summary>
        internal static int RunApplyUpdateMode(string[] args, string target)
        {
            if (args == null || args.Length != 3 || args[0] != ApplyUpdateSwitch)
            {
                Logger.Error("{0} expects exactly two arguments: <source> <sha256>", ApplyUpdateSwitch);
                return 2;
            }

            string source = args[1];
            string expectedHash = args[2];

            try
            {
                if (!Path.IsPathRooted(source) || !File.Exists(source))
                {
                    Logger.Error("Update source {0} is not an existing absolute path", source);
                    return 3;
                }

                FileHash hashArgument = new FileHash { Hash = expectedHash, HashAlgorithm = UpdateVerifier.RequiredAlgorithm };
                if (!UpdateVerifier.TryGetExpectedHash(hashArgument, out string normalizedHash, out string reason))
                {
                    Logger.Error("Rejected expected hash argument: {0}", reason);
                    return 4;
                }

                if (!UpdateVerifier.FileMatches(source, normalizedHash))
                {
                    Logger.Error("Update source {0} does not match the expected SHA-256", source);
                    return 5;
                }

                Swap(source, target);
                Logger.Info("Elevated helper replaced {0}", target);
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Elevated helper failed to replace {0}", target);
                return 1;
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Deletes the previous executable left behind by the last update, if any. Best effort: the old process may
        /// still be exiting, in which case the file is removed on the next start instead.
        /// </summary>
        public static void CleanupBackup()
        {
            string backup = GetBackupPath(CurrentExecutablePath);

            try
            {
                if (File.Exists(backup))
                {
                    File.Delete(backup);
                    Logger.Info("Deleted previous executable {0}", backup);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Could not delete {0} yet", backup);
            }
        }

        #endregion
    }

    /// <summary>
    /// Thrown when an update cannot be applied. The message is suitable for showing to the user.
    /// </summary>
    internal class UpdateApplyException : Exception
    {
        public UpdateApplyException(string message) : base(message)
        {
        }

        public UpdateApplyException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
