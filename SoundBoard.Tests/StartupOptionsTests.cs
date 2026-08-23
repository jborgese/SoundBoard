using System;
using System.IO;
using Xunit;

namespace SoundBoard.Tests
{
    /// <summary>
    /// The <c>--config</c> switch, which points the app at a configuration file of the caller's choosing.
    /// </summary>
    /// <remarks>
    /// It exists so that a scratch board can be run without touching the real one. That makes the interesting cases
    /// the ones where it does <em>not</em> quietly fall back to the default config, because falling back would write
    /// a test board over the user's own — which is what happened before the switch existed.
    /// </remarks>
    public class StartupOptionsTests : IDisposable
    {
        // ConfigStore's override is process-wide, so every test here has to put it back.
        public void Dispose() => ConfigStore.UseConfigFile(null);

        private static string DefaultConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ConfigStore.ApplicationName, "soundboard.config");

        [Fact]
        public void NoArguments_LeavesTheDefaultConfigInPlace()
        {
            Assert.Null(StartupOptions.Apply(new string[0]));
            Assert.Null(StartupOptions.Apply(null));

            Assert.False(ConfigStore.IsConfigFileOverridden);
            Assert.Equal(DefaultConfigPath, ConfigStore.ConfigFilePath);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ConfigSwitch_PointsEverythingAtTheGivenFile(bool joinedWithEquals)
        {
            string path = Path.Combine(Path.GetTempPath(), "SoundBoard.Tests", "scratch.config");

            string[] args = joinedWithEquals
                ? new[] { StartupOptions.ConfigSwitch + "=" + path }
                : new[] { StartupOptions.ConfigSwitch, path };

            Assert.Null(StartupOptions.Apply(args));

            Assert.True(ConfigStore.IsConfigFileOverridden);
            Assert.Equal(path, ConfigStore.ConfigFilePath);

            // The .temp and .bak files have to follow the config, or a scratch run would still litter %AppData%
            Assert.Equal(path + ".temp", ConfigStore.TempConfigFilePath);
        }

        [Fact]
        public void ConfigSwitch_MakesARelativePathAbsoluteImmediately()
        {
            // The working directory does not stay put — opening a file dialog is enough to move it — so a path kept
            // relative would resolve somewhere else later in the same run.
            Assert.Null(StartupOptions.Apply(new[] { StartupOptions.ConfigSwitch, "scratch.config" }));

            Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "scratch.config"), ConfigStore.ConfigFilePath);
            Assert.True(Path.IsPathRooted(ConfigStore.ConfigFilePath));
        }

        [Fact]
        public void ConfigSwitch_WithNoPath_IsAnErrorAndChangesNothing()
        {
            // Never a silent fall back to the default: the caller asked not to use the real config, and using it
            // anyway is the accident this switch is here to stop.
            Assert.NotNull(StartupOptions.Apply(new[] { StartupOptions.ConfigSwitch }));
            Assert.False(ConfigStore.IsConfigFileOverridden);

            Assert.NotNull(StartupOptions.Apply(new[] { StartupOptions.ConfigSwitch + "=" }));
            Assert.False(ConfigStore.IsConfigFileOverridden);
        }

        [Fact]
        public void ConfigSwitch_FollowedByAnotherSwitch_IsAnErrorRatherThanTakingItAsThePath()
        {
            Assert.NotNull(StartupOptions.Apply(new[] { StartupOptions.ConfigSwitch, "--something-else" }));

            Assert.False(ConfigStore.IsConfigFileOverridden);
        }

        [Fact]
        public void ConfigSwitch_WithAnUnusablePath_IsAnErrorAndChangesNothing()
        {
            Assert.NotNull(StartupOptions.Apply(new[] { StartupOptions.ConfigSwitch, "\"" }));

            Assert.False(ConfigStore.IsConfigFileOverridden);
        }

        [Fact]
        public void UnknownArguments_AreIgnored()
        {
            // The shell starts the app with file arguments and the updater with switches of its own; neither is this
            // class's business, and neither should stop the app.
            Assert.Null(StartupOptions.Apply(new[] { @"C:\some\sound.mp3", "--apply-update", "a", "b" }));

            Assert.False(ConfigStore.IsConfigFileOverridden);
        }

        [Fact]
        public void ConfigSwitch_IsStillFoundAfterOtherArguments()
        {
            string path = Path.Combine(Path.GetTempPath(), "later.config");

            Assert.Null(StartupOptions.Apply(new[] { @"C:\some\sound.mp3", StartupOptions.ConfigSwitch, path }));

            Assert.Equal(path, ConfigStore.ConfigFilePath);
        }

        [Fact]
        public void UseConfigFile_WithNothing_GoesBackToTheDefault()
        {
            ConfigStore.UseConfigFile(Path.Combine(Path.GetTempPath(), "scratch.config"));
            Assert.True(ConfigStore.IsConfigFileOverridden);

            ConfigStore.UseConfigFile(null);
            Assert.False(ConfigStore.IsConfigFileOverridden);
            Assert.Equal(DefaultConfigPath, ConfigStore.ConfigFilePath);

            ConfigStore.UseConfigFile("   ");
            Assert.False(ConfigStore.IsConfigFileOverridden);
        }
    }
}
