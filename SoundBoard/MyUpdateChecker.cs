using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Bluegrams.Application;
using SoundBoard.Update;

namespace SoundBoard
{
    /// <summary>
    /// Update checker that verifies the download against the manifest's SHA-256 and then replaces the running
    /// executable in place (see <see cref="UpdateApplier"/>) instead of showing the file in Explorer.
    /// </summary>
    /// <remarks>
    /// Verification is fail-closed at two points. <see cref="VerifyHash"/> tightens the library's own check (which
    /// passes an empty hash). <see cref="ShowUpdateDownload"/> then independently re-derives the expected hash from
    /// the manifest entry, which also catches the cases the library never hashes at all: a missing
    /// <c>&lt;FileHash&gt;</c> element, or a fallback to <c>&lt;DownloadLink&gt;</c> because the download key was not
    /// found.
    /// </remarks>
    public class MyUpdateChecker : WpfUpdateChecker
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The manifest of the update currently being offered; captured so the download can be verified against it.
        /// </summary>
        private AppUpdate _pendingUpdate;

        /// <inheritdoc/>
        public MyUpdateChecker(string url, Window owner = null, string identifier = null) : base(url, owner, identifier)
        {
        }

        /// <inheritdoc/>
        protected override Task OnUpdateCheckCompleted(UpdateCheckEventArgs e)
        {
            _pendingUpdate = e.Successful ? e.Update : null;
            return base.OnUpdateCheckCompleted(e);
        }

        /// <inheritdoc/>
        protected override bool VerifyHash(FileHash fileHash, string fileName)
        {
            if (!UpdateVerifier.TryGetExpectedHash(fileHash, out string expectedHash, out string reason))
            {
                Logger.Error("Update verification failed: {0}", reason);
                return false;
            }

            bool matches = UpdateVerifier.FileMatches(fileName, expectedHash);

            if (!matches)
            {
                Logger.Error("Update verification failed: {0} does not match the manifest SHA-256", fileName);
            }

            return matches;
        }

        /// <inheritdoc/>
        public override void ShowUpdateDownload(string file)
        {
            string expectedHash = null;
            string failureReason = null;

            if (_pendingUpdate == null)
            {
                failureReason = Properties.Resources.UpdateNoManifest;
            }
            else
            {
                DownloadEntry entry = ResolveDownloadEntry(_pendingUpdate);
                UpdateVerifier.TryGetExpectedHash(entry.FileHash, out expectedHash, out failureReason);
            }

            if (failureReason == null && !UpdateVerifier.FileMatches(file, expectedHash))
            {
                failureReason = Properties.Resources.UpdateHashMismatch;
            }

            if (failureReason != null)
            {
                Logger.Error("Refusing to apply update {0}: {1}", file, failureReason);
                TryDelete(file);
                ShowError(string.Format(Properties.Resources.UpdateVerificationFailed, failureReason));
                return;
            }

            try
            {
                Logger.Info("Applying verified update {0}", file);
                UpdateApplier.ApplyAndRestart(file, expectedHash);
            }
            catch (UpdateApplyException ex)
            {
                Logger.Error(ex, "Failed to apply update {0}", file);
                TryDelete(file);
                ShowError(string.Format(Properties.Resources.UpdateApplyFailed, ex.Message));
            }
        }

        private void ShowError(string message)
        {
            if (Owner != null)
            {
                MessageBox.Show(Owner, message, Properties.Resources.UpdateFailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(message, Properties.Resources.UpdateFailedTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not delete rejected update {0}", file);
            }
        }
    }
}
