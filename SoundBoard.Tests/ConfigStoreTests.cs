using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SoundBoard.Model;
using Xunit;

namespace SoundBoard.Tests
{
    public class ConfigStoreTests : IDisposable
    {
        #region Scratch directory

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SoundBoard.Tests", Guid.NewGuid().ToString("N"));

        public ConfigStoreTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { /* best effort */ }
        }

        private string P(string name) => Path.Combine(_dir, name);

        private static SoundBoardConfig SampleConfig(string pageName = "Sounds")
        {
            var config = new SoundBoardConfig();
            var page = new Page(pageName, 1, 2);
            page[0, 0].Name = "Airhorn";
            page[0, 0].Path = @"C:\airhorn.mp3";
            config.Pages.Add(page);
            return config;
        }

        private const string LegacyXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><tabs><tab><name>Old</name><button0 name=\"One\" path=\"C:\\one.mp3\" /></tab></tabs>";

        #endregion

        #region Save / Load

        [Fact]
        public void Save_CreatesDirectoryAndFile_AndLoadReadsItBack()
        {
            string path = P(Path.Combine("nested", "deeper", "soundboard.config"));

            ConfigStore.Save(path, SampleConfig());

            Assert.True(File.Exists(path));
            SoundBoardConfig loaded = ConfigStore.Load(path);
            Assert.Equal("Airhorn", Assert.Single(loaded.Pages)[0, 0].Name);
            Assert.Equal(SoundBoardConfig.CurrentSchemaVersion, loaded.SchemaVersion);
        }

        [Fact]
        public void Save_OverExistingFile_LeavesATimestampedBackupOfTheOldContent()
        {
            string path = P("soundboard.config");
            ConfigStore.Save(path, SampleConfig("First"));
            string firstContent = File.ReadAllText(path);

            ConfigStore.Save(path, SampleConfig("Second"));

            string backup = Assert.Single(Directory.GetFiles(_dir, "*.bak"));
            Assert.Matches(@"^soundboard\.config-\d{4}-\d{2}-\d{2}T\d{2}\.\d{2}\.\d{2}\.bak$", Path.GetFileName(backup));
            Assert.Equal(firstContent, File.ReadAllText(backup));
            Assert.Contains("<name>Second</name>", File.ReadAllText(path));
        }

        [Fact]
        public void Save_ToNewFile_MakesNoBackup()
        {
            ConfigStore.Save(P("soundboard.config"), SampleConfig());

            Assert.Empty(Directory.GetFiles(_dir, "*.bak"));
        }

        [Fact]
        public void Load_MissingFile_Throws()
        {
            Assert.ThrowsAny<IOException>(() => ConfigStore.Load(P("nope.config")));
        }

        [Fact]
        public void Load_MalformedFile_Throws()
        {
            File.WriteAllText(P("bad.config"), "<tabs><tab>");

            Assert.ThrowsAny<Exception>(() => ConfigStore.Load(P("bad.config")));
        }

        [Fact]
        public void Load_LegacyFile_UsesDefaultsForMissingSettings()
        {
            File.WriteAllText(P("legacy.config"), LegacyXml, Encoding.UTF8);
            var defaults = new BoardSettings { NewPageDefaultRows = 1, NewPageDefaultColumns = 3, AudioPassthroughLatency = 77 };

            SoundBoardConfig config = ConfigStore.Load(P("legacy.config"), defaults);

            Assert.Equal(77, config.Settings.AudioPassthroughLatency);
            Assert.Equal(1, Assert.Single(config.Pages).Rows);
            Assert.Equal(3, config.Pages[0].Columns);
        }

        [Fact]
        public void DateTimeStamp_IsFileNameSafe()
        {
            string stamp = ConfigStore.DateTimeStamp();

            Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}\.\d{2}\.\d{2}$", stamp);
            Assert.Equal(-1, stamp.IndexOfAny(Path.GetInvalidFileNameChars()));
        }

        #endregion

        #region CleanupBackups

        [Fact]
        public void CleanupBackups_KeepsTheNewestMaxBackupFiles()
        {
            var created = new List<string>();
            for (int i = 0; i < ConfigStore.MaxBackupFiles + 3; i++)
            {
                string file = P($"soundboard.config-{i:00}.bak");
                File.WriteAllText(file, i.ToString());
                File.SetCreationTime(file, new DateTime(2026, 1, 1).AddDays(i));
                created.Add(file);
            }
            File.WriteAllText(P("soundboard.config"), "<tabs />");

            ConfigStore.CleanupBackups(P("soundboard.config"));

            string[] remaining = Directory.GetFiles(_dir, "*.bak").OrderBy(f => f).ToArray();
            Assert.Equal(created.Skip(3).ToArray(), remaining); // the 3 oldest are gone
            Assert.True(File.Exists(P("soundboard.config")));    // the config itself is untouched
        }

        [Fact]
        public void CleanupBackups_WithFewFiles_DeletesNothing()
        {
            for (int i = 0; i < ConfigStore.MaxBackupFiles; i++)
            {
                File.WriteAllText(P($"soundboard.config-{i}.bak"), "");
            }

            ConfigStore.CleanupBackups(P("soundboard.config"));

            Assert.Equal(ConfigStore.MaxBackupFiles, Directory.GetFiles(_dir, "*.bak").Length);
        }

        [Fact]
        public void CleanupBackups_MissingDirectoryOrBadPath_NeverThrows()
        {
            ConfigStore.CleanupBackups(P(Path.Combine("does-not-exist", "soundboard.config")));
            ConfigStore.CleanupBackups("soundboard.config"); // no directory component
            ConfigStore.CleanupBackups(null);
        }

        #endregion

        #region LoadWithLegacyMigration

        [Fact]
        public void NoLegacyFile_LoadsCurrentPathOnly()
        {
            var loads = new List<string>();
            var saves = new List<string>();

            bool migrated = ConfigStore.LoadWithLegacyMigration(P("legacy.config"), P("current.config"), P("current.config.temp"), loads.Add, saves.Add);

            Assert.False(migrated);
            Assert.Equal(new[] { P("current.config") }, loads);
            Assert.Empty(saves);
            Assert.False(File.Exists(P("current.config.temp")));
        }

        [Fact]
        public void LegacyFile_IsLoadedSavedToCurrentPath_ThenParkedAtTempPath()
        {
            File.WriteAllText(P("legacy.config"), LegacyXml, Encoding.UTF8);
            var events = new List<string>();

            bool migrated = ConfigStore.LoadWithLegacyMigration(
                P("legacy.config"), P("current.config"), P("current.config.temp"),
                path => events.Add("load:" + path),
                path => events.Add("save:" + path));

            Assert.True(migrated);
            Assert.Equal(new[] { "load:" + P("legacy.config"), "save:" + P("current.config") }, events);
            Assert.False(File.Exists(P("legacy.config")));
            Assert.Equal(LegacyXml, File.ReadAllText(P("current.config.temp"), Encoding.UTF8));
        }

        [Fact]
        public void LegacyFile_ReplacesAnExistingTempFile()
        {
            File.WriteAllText(P("legacy.config"), LegacyXml);
            File.WriteAllText(P("current.config.temp"), "stale");

            ConfigStore.LoadWithLegacyMigration(P("legacy.config"), P("current.config"), P("current.config.temp"), _ => { }, _ => { });

            Assert.Equal(LegacyXml, File.ReadAllText(P("current.config.temp")));
            Assert.False(File.Exists(P("legacy.config")));
        }

        [Fact]
        public void LegacyFile_EndToEnd_ProducesAnEquivalentCurrentFormatConfig()
        {
            File.WriteAllText(P("legacy.config"), LegacyXml, Encoding.UTF8);
            string newPath = P(Path.Combine("AppData", "current.config"));
            SoundBoardConfig loaded = null;

            ConfigStore.LoadWithLegacyMigration(
                P("legacy.config"), newPath, P("current.config.temp"),
                path => loaded = ConfigStore.Load(path),
                path => ConfigStore.Save(path, loaded));

            SoundBoardConfig migrated = ConfigStore.Load(newPath);
            Assert.Equal(SoundBoardConfig.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.Equal("Old", Assert.Single(migrated.Pages).Name);
            Assert.Equal("One", migrated.Pages[0][0, 0].Name);
            Assert.Equal(loaded.Pages[0][0, 0].Id, migrated.Pages[0][0, 0].Id); // ids assigned during migration are persisted
        }

        [Fact]
        public void LegacyFile_WhenSaveThrows_LegacyFileIsLeftInPlace()
        {
            File.WriteAllText(P("legacy.config"), LegacyXml);

            Assert.Throws<InvalidOperationException>(() => ConfigStore.LoadWithLegacyMigration(
                P("legacy.config"), P("current.config"), P("current.config.temp"),
                _ => { },
                _ => throw new InvalidOperationException("disk full")));

            Assert.True(File.Exists(P("legacy.config")));
            Assert.False(File.Exists(P("current.config.temp")));
        }

        #endregion
    }
}
