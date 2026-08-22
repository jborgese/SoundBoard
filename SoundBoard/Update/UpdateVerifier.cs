using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bluegrams.Application;

namespace SoundBoard.Update
{
    /// <summary>
    /// Decides whether a downloaded update may be trusted. The policy is deliberately fail-closed: the manifest must
    /// name a SHA-256 hash and the file must match it. Anything else (no hash, an empty hash, MD5/SHA-1, a malformed
    /// value) is a verification failure, never a pass.
    /// </summary>
    /// <remarks>
    /// This exists because the Bluegrams <c>UpdateCheckerBase.VerifyHash</c> returns <c>true</c> for an empty hash,
    /// skips verification entirely when the <c>&lt;FileHash&gt;</c> element is absent, and defaults the algorithm to
    /// MD5 when the <c>algorithm</c> attribute is missing.
    /// </remarks>
    internal static class UpdateVerifier
    {
        /// <summary>
        /// The only hash algorithm the updater accepts. Matches what <c>scripts/New-VersionInfo.ps1</c> writes.
        /// </summary>
        public const string RequiredAlgorithm = "SHA256";

        private const int Sha256HexLength = 64;

        /// <summary>
        /// Extracts the expected SHA-256 hash from a manifest entry.
        /// </summary>
        /// <param name="fileHash">The <c>&lt;FileHash&gt;</c> element of the chosen download entry, or null if absent.</param>
        /// <param name="expectedHash">On success, the upper-case hex hash.</param>
        /// <param name="failureReason">On failure, a human-readable reason suitable for the log and the error dialog.</param>
        public static bool TryGetExpectedHash(FileHash fileHash, out string expectedHash, out string failureReason)
        {
            expectedHash = null;

            if (fileHash == null)
            {
                failureReason = "The update manifest does not contain a file hash for this download.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(fileHash.Hash))
            {
                failureReason = "The update manifest contains an empty file hash for this download.";
                return false;
            }

            if (!string.Equals(fileHash.HashAlgorithm, RequiredAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"The update manifest uses hash algorithm '{fileHash.HashAlgorithm}'; only {RequiredAlgorithm} is accepted.";
                return false;
            }

            string hash = fileHash.Hash.Trim().ToUpperInvariant();

            if (hash.Length != Sha256HexLength || !hash.All(IsHexDigit))
            {
                failureReason = "The update manifest contains a malformed SHA-256 hash.";
                return false;
            }

            expectedHash = hash;
            failureReason = null;
            return true;
        }

        /// <summary>
        /// Computes the upper-case hex SHA-256 of a file.
        /// </summary>
        public static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// Returns true if the file at <paramref name="path"/> hashes to <paramref name="expectedHash"/>.
        /// </summary>
        public static bool FileMatches(string path, string expectedHash)
        {
            return HashesEqual(ComputeSha256(path), expectedHash);
        }

        /// <summary>
        /// Case-insensitive comparison of two hex hashes.
        /// </summary>
        public static bool HashesEqual(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
        }
    }
}
