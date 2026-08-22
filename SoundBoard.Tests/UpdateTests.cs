using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bluegrams.Application;
using SoundBoard.Update;
using Xunit;

namespace SoundBoard.Tests
{
    public class UpdateVerifierTests
    {
        // SHA-256 of the empty string.
        private const string EmptySha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";

        // The rejection reasons are shown to the user, so they come from the resources and change with the UI
        // language. Comparing against the resource keeps these tests about the policy rather than about English.
        private static class Res
        {
            internal static string UpdateManifestNoFileHash => SoundBoard.Properties.Resources.UpdateManifestNoFileHash;
            internal static string UpdateManifestEmptyFileHash => SoundBoard.Properties.Resources.UpdateManifestEmptyFileHash;
            internal static string UpdateManifestWrongHashAlgorithm => SoundBoard.Properties.Resources.UpdateManifestWrongHashAlgorithm;
            internal static string UpdateManifestMalformedHash => SoundBoard.Properties.Resources.UpdateManifestMalformedHash;
        }

        [Fact]
        public void AbsentFileHash_IsFailure()
        {
            Assert.False(UpdateVerifier.TryGetExpectedHash(null, out string hash, out string reason));
            Assert.Null(hash);
            Assert.Equal(Res.UpdateManifestNoFileHash, reason);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void EmptyFileHash_IsFailure(string value)
        {
            FileHash fileHash = new FileHash { Hash = value, HashAlgorithm = "SHA256" };

            Assert.False(UpdateVerifier.TryGetExpectedHash(fileHash, out _, out string reason));
            Assert.Equal(Res.UpdateManifestEmptyFileHash, reason);
        }

        [Theory]
        [InlineData("MD5")]
        [InlineData("SHA1")]
        [InlineData("")]
        [InlineData(null)]
        public void NonSha256Algorithm_IsFailure(string algorithm)
        {
            FileHash fileHash = new FileHash { Hash = EmptySha256, HashAlgorithm = algorithm };

            Assert.False(UpdateVerifier.TryGetExpectedHash(fileHash, out _, out string reason));
            Assert.Equal(string.Format(Res.UpdateManifestWrongHashAlgorithm, fileHash.HashAlgorithm, UpdateVerifier.RequiredAlgorithm), reason);
            Assert.Contains(UpdateVerifier.RequiredAlgorithm, reason);
        }

        [Fact]
        public void LibraryDefaultAlgorithmIsMd5_AndIsRejected()
        {
            // Documents the library behaviour this class guards against: no algorithm attribute means MD5.
            FileHash fileHash = new FileHash { Hash = EmptySha256 };

            Assert.Equal("MD5", fileHash.HashAlgorithm);
            Assert.False(UpdateVerifier.TryGetExpectedHash(fileHash, out _, out _));
        }

        [Theory]
        [InlineData("E3B0C442")]                                                            // too short
        [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855AA")]   // too long
        [InlineData("G3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]     // non-hex
        public void MalformedHash_IsFailure(string value)
        {
            FileHash fileHash = new FileHash { Hash = value, HashAlgorithm = "SHA256" };

            Assert.False(UpdateVerifier.TryGetExpectedHash(fileHash, out _, out string reason));
            Assert.Equal(Res.UpdateManifestMalformedHash, reason);
        }

        [Theory]
        [InlineData("SHA256")]
        [InlineData("sha256")]
        public void ValidHash_IsNormalisedToUpperCase(string algorithm)
        {
            FileHash fileHash = new FileHash { Hash = " " + EmptySha256.ToLowerInvariant() + " ", HashAlgorithm = algorithm };

            Assert.True(UpdateVerifier.TryGetExpectedHash(fileHash, out string hash, out string reason));
            Assert.Equal(EmptySha256, hash);
            Assert.Null(reason);
        }

        [Fact]
        public void ComputeSha256_MatchesKnownValue()
        {
            string path = Path.GetTempFileName();
            try
            {
                Assert.Equal(EmptySha256, UpdateVerifier.ComputeSha256(path));
                Assert.True(UpdateVerifier.FileMatches(path, EmptySha256.ToLowerInvariant()));

                File.WriteAllText(path, "abc");
                Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", UpdateVerifier.ComputeSha256(path));
                Assert.False(UpdateVerifier.FileMatches(path, EmptySha256));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void HashesEqual_IsCaseInsensitive_AndRejectsEmpty()
        {
            Assert.True(UpdateVerifier.HashesEqual("abc", "ABC"));
            Assert.False(UpdateVerifier.HashesEqual("", ""));
            Assert.False(UpdateVerifier.HashesEqual(null, null));
        }
    }

    public class CommandLineTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData(@"C:\Users\jared\SoundBoard.exe", @"C:\Users\jared\SoundBoard.exe")]
        [InlineData("", "\"\"")]
        [InlineData("has space", "\"has space\"")]
        [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [InlineData(@"C:\path with space\", "\"C:\\path with space\\\\\"")]
        [InlineData(@"a\\""b", "\"a\\\\\\\\\\\"b\"")]
        public void Quote_ProducesExpectedText(string input, string expected)
        {
            Assert.Equal(expected, CommandLine.Quote(input));
        }

        [Theory]
        [InlineData("plain")]
        [InlineData("has space")]
        [InlineData("trailing\\")]
        [InlineData("path with space\\")]
        [InlineData("embedded \"quote\"")]
        [InlineData("back\\slash \"then\\\" quote\\\\")]
        [InlineData("cmd & metachars | ^ % don't matter\" here")]
        [InlineData("C:\\Users\\J Doe\\Down\"loads\\SoundBoard.exe")]
        public void Quote_RoundTripsThroughTheNativeParser(string arg)
        {
            // cmd is never involved (UseShellExecute only adds the runas verb), so the only parser that matters is
            // CommandLineToArgvW, which is what Main(string[]) sees.
            string[] parsed = RunEchoArgs(new[] { "--apply-update", arg, "E3B0C442" });

            Assert.Equal(new[] { "--apply-update", arg, "E3B0C442" }, parsed);
        }

        private static string[] RunEchoArgs(string[] args)
        {
            // Parse with the real Win32 API, which is what CreateProcess-started .NET apps use for Main(string[]).
            return CommandLineToArgv(CommandLine.Join(args)).Skip(1).ToArray();
        }

        private static string[] CommandLineToArgv(string arguments)
        {
            IntPtr argv = NativeMethods.CommandLineToArgvW("x.exe " + arguments, out int argc);
            if (argv == IntPtr.Zero)
            {
                throw new System.ComponentModel.Win32Exception();
            }

            try
            {
                string[] result = new string[argc];
                for (int i = 0; i < argc; i++)
                {
                    IntPtr p = System.Runtime.InteropServices.Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    result[i] = System.Runtime.InteropServices.Marshal.PtrToStringUni(p);
                }

                return result;
            }
            finally
            {
                NativeMethods.LocalFree(argv);
            }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            public static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            public static extern IntPtr LocalFree(IntPtr hMem);
        }
    }

    public class UpdateApplierTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SoundBoard.Tests." + Guid.NewGuid().ToString("N"));

        public UpdateApplierTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Swap_RenamesTargetToBackupAndMovesSourceIntoPlace()
        {
            string source = Write("download.exe", "new");
            string target = Write("app.exe", "old");

            UpdateApplier.Swap(source, target);

            Assert.False(File.Exists(source));
            Assert.Equal("new", File.ReadAllText(target));
            Assert.Equal("old", File.ReadAllText(UpdateApplier.GetBackupPath(target)));
        }

        [Fact]
        public void Swap_ReplacesStaleBackup()
        {
            string source = Write("download.exe", "new");
            string target = Write("app.exe", "old");
            Write("app.exe.old", "stale");

            UpdateApplier.Swap(source, target);

            Assert.Equal("old", File.ReadAllText(UpdateApplier.GetBackupPath(target)));
        }

        [Fact]
        public void Swap_RollsBackWhenSourceIsMissing()
        {
            string target = Write("app.exe", "old");
            string missing = Path.Combine(_dir, "does-not-exist.exe");

            Assert.Throws<FileNotFoundException>(() => UpdateApplier.Swap(missing, target));

            Assert.Equal("old", File.ReadAllText(target));
            Assert.False(File.Exists(UpdateApplier.GetBackupPath(target)));
        }

        [Fact]
        public void Swap_WorksWhileTargetIsARunningExecutable()
        {
            // The whole design rests on Windows permitting a running image to be renamed. Prove it with a real
            // process rather than trusting the documentation.
            string target = Path.Combine(_dir, "sleeper.exe");
            File.Copy(Path.Combine(Environment.SystemDirectory, "timeout.exe"), target);
            string source = Write("download.exe", "new");

            using (Process sleeper = Process.Start(new ProcessStartInfo
            {
                FileName = target,
                Arguments = "/t 30 /nobreak",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
            }))
            {
                try
                {
                    UpdateApplier.Swap(source, target);

                    Assert.Equal("new", File.ReadAllText(target));
                    Assert.True(File.Exists(UpdateApplier.GetBackupPath(target)));
                }
                finally
                {
                    sleeper.Kill();
                    sleeper.WaitForExit();
                }
            }
        }

        [Fact]
        public void ApplyUpdateMode_RejectsBadArguments()
        {
            string target = Write("app.exe", "old");

            Assert.Equal(2, UpdateApplier.RunApplyUpdateMode(null, target));
            Assert.Equal(2, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update" }, target));
            Assert.Equal(2, UpdateApplier.RunApplyUpdateMode(new[] { "--other", "a", "b" }, target));
            Assert.Equal(3, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update", "relative.exe", "E3B0" }, target));
            Assert.Equal(3, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update", Path.Combine(_dir, "missing.exe"), "E3B0" }, target));
            Assert.Equal("old", File.ReadAllText(target));
        }

        [Fact]
        public void ApplyUpdateMode_RejectsMalformedOrMismatchedHash()
        {
            string target = Write("app.exe", "old");
            string source = Write("download.exe", "new");
            string wrongHash = UpdateVerifier.ComputeSha256(target);

            Assert.Equal(4, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update", source, "not-a-hash" }, target));
            Assert.Equal(5, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update", source, wrongHash }, target));
            Assert.Equal("old", File.ReadAllText(target));
            Assert.True(File.Exists(source));
        }

        [Fact]
        public void ApplyUpdateMode_SwapsWhenHashMatches()
        {
            string target = Write("app.exe", "old");
            string source = Write("download.exe", "new");
            string hash = UpdateVerifier.ComputeSha256(source).ToLowerInvariant();

            Assert.Equal(0, UpdateApplier.RunApplyUpdateMode(new[] { "--apply-update", source, hash }, target));
            Assert.Equal("new", File.ReadAllText(target));
        }
    }
}
