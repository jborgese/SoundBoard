using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using SoundBoard.Model;
using Xunit;

namespace SoundBoard.Tests
{
    public class ConfigSerializerTests
    {
        #region Helpers

        private static string FixturePath(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", name);

        private static string ReadFixture(string name) => File.ReadAllText(FixturePath(name), Encoding.UTF8);

        private static SoundBoardConfig Read(string xml, List<string> warnings = null)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return ConfigSerializer.Read(stream, warnings is null ? (Action<string>)null : warnings.Add);
            }
        }

        private static byte[] WriteBytes(SoundBoardConfig config)
        {
            using (var stream = new MemoryStream())
            {
                ConfigSerializer.Write(config, stream);
                return stream.ToArray();
            }
        }

        private static string Write(SoundBoardConfig config) => Encoding.UTF8.GetString(WriteBytes(config));

        /// <summary>
        /// Normalizes the things that legitimately differ between a fixture checked into git and what the writer emits:
        /// line endings (git may convert them) and a trailing newline (editors add one; XmlTextWriter does not).
        /// </summary>
        private static string Normalize(string xml) => xml.Replace("\r\n", "\n").TrimEnd('\n');

        /// <summary>
        /// Empty cells get a fresh id every time they are loaded (as the UI has always done), so they can never round-trip
        /// byte-for-byte. Replace their ids with a placeholder before comparing.
        /// </summary>
        private static string MaskEmptyCellIds(string xml) =>
            Regex.Replace(xml, "(<button\\d+ name=\"\" path=\"\"[^>]*? id=\")[^\"]*(\")", "$1<empty>$2");

        private static string InsertSchemaVersion(string xml) => xml.Replace("<tabs>", $"<tabs schemaVersion=\"{SoundBoardConfig.CurrentSchemaVersion}\">");

        #endregion

        #region Golden files

        [Fact]
        public void CurrentFormat_RoundTripsByteForByte_ExceptSchemaVersionAndEmptyCellIds()
        {
            string original = ReadFixture("current-1.10.2.config");

            string written = Write(Read(original));

            Assert.Equal(
                MaskEmptyCellIds(Normalize(InsertSchemaVersion(original))),
                MaskEmptyCellIds(Normalize(written)));
        }

        [Theory]
        [InlineData("current-1.10.2.config")]
        [InlineData("legacy-minimal.config")]
        [InlineData("extras-and-duplicates.config")]
        [InlineData("missing-attributes.config")]
        public void Write_IsIdempotent(string fixture)
        {
            string once = Write(Read(ReadFixture(fixture)));
            string twice = Write(Read(once));

            Assert.Equal(MaskEmptyCellIds(once), MaskEmptyCellIds(twice));
        }

        [Fact]
        public void Write_UsesUtf8WithoutBom_AndCrLf()
        {
            byte[] bytes = WriteBytes(Read(ReadFixture("current-1.10.2.config")));

            Assert.Equal((byte)'<', bytes[0]); // no BOM
            string text = Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<tabs schemaVersion=\"2\">\r\n    <GlobalSettings ", text);
            Assert.EndsWith("</tabs>", text);
            Assert.All(text.Split(new[] { "\r\n" }, StringSplitOptions.None), line => Assert.Matches("^( {4})*<", line)); // 4-space indentation only
        }

        #endregion

        #region Reading the current format

        [Fact]
        public void CurrentFormat_ReadsEverything()
        {
            SoundBoardConfig config = Read(ReadFixture("current-1.10.2.config"));

            Assert.Equal(SoundBoardConfig.LegacySchemaVersion, config.SchemaVersion);

            BoardSettings settings = config.Settings;
            Assert.Equal(new[] { Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001"), Guid.Parse("a1b2c3d4-0000-0000-0000-000000000002") }, settings.OutputDevices.OrderBy(g => g));
            Assert.Equal(new[] { Guid.Parse("b1b2c3d4-0000-0000-0000-000000000003") }, settings.InputDevices);
            Assert.Equal(new[] { Guid.Parse("c1b2c3d4-0000-0000-0000-000000000004") }, settings.PassthroughOutputDevices);
            Assert.Equal(25, settings.AudioPassthroughLatency);
            Assert.Equal(3, settings.NewPageDefaultRows);
            Assert.Equal(4, settings.NewPageDefaultColumns);

            Assert.Equal(2, config.Pages.Count);

            Page first = config.Pages[0];
            Assert.Equal("Sounds", first.Name);
            Assert.True(first.IsFocused);
            Assert.Equal(2, first.Rows);
            Assert.Equal(2, first.Columns);
            Assert.Equal(4, first.Sounds.Count);

            Sound airhorn = first[0, 0];
            Assert.Equal("Airhorn", airhorn.Name);
            Assert.Equal(@"C:\Sounds\airhorn.mp3", airhorn.Path);
            Assert.Equal(new SoundColor(0xFF, 0xFF, 0x00, 0x00), airhorn.Color);
            Assert.Equal(2, airhorn.VolumeOffset);
            Assert.False(airhorn.Loop);
            Assert.True(airhorn.StopAllSounds);
            Assert.Equal("22222222-2222-2222-2222-222222222222", airhorn.NextSoundId);
            Assert.Equal("11111111-1111-1111-1111-111111111111", airhorn.Id);
            Assert.Equal(new Hotkey(Key.A, ModifierKeys.Control), airhorn.LocalHotkey);
            Assert.Equal(new Hotkey(Key.F5, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows), airhorn.GlobalHotkey);

            Sound crickets = first[0, 1];
            Assert.Null(crickets.Color);
            Assert.Equal(-3, crickets.VolumeOffset);
            Assert.True(crickets.Loop);
            Assert.Null(crickets.NextSoundId);
            Assert.Null(crickets.LocalHotkey);
            Assert.Null(crickets.GlobalHotkey);

            Sound empty = first[1, 0];
            Assert.True(empty.IsEmpty);
            Assert.NotEqual("33333333-3333-3333-3333-333333333333", empty.Id); // empty cells get a fresh id
            Assert.True(Guid.TryParse(empty.Id, out _));

            Sound dangling = first[1, 1];
            Assert.Equal(new SoundColor(0x80, 0x12, 0x34, 0x56), dangling.Color);
            Assert.Equal("99999999-9999-9999-9999-999999999999", dangling.NextSoundId);
            Assert.Null(config.FindSound(dangling.NextSoundId)); // tolerated, not repaired
            Assert.Equal(new Hotkey(Key.F1, ModifierKeys.None), dangling.LocalHotkey);

            Page second = config.Pages[1];
            Assert.Equal("Second & \"quoted\" page", second.Name);
            Assert.False(second.IsFocused);
            Assert.Same(second[0, 0], config.FindSound("55555555-5555-5555-5555-555555555555"));
            Assert.Equal(new Hotkey(Key.Z, ModifierKeys.Windows), second[0, 0].GlobalHotkey);
        }

        [Fact]
        public void AllSounds_IsPageOrderThenRowMajor()
        {
            SoundBoardConfig config = Read(ReadFixture("current-1.10.2.config"));

            Assert.Equal(
                new[] { (0, 0, 0), (0, 0, 1), (0, 1, 0), (0, 1, 1), (1, 0, 0) },
                config.AllSounds().Select(s => (config.Pages.FindIndex(p => p.Sounds.Contains(s)), s.Row, s.Column)));
        }

        #endregion

        #region Legacy and malformed input

        [Fact]
        public void LegacyMinimal_FallsBackToDefaultsAndIndexPositions()
        {
            SoundBoardConfig config = Read(ReadFixture("legacy-minimal.config"));

            Assert.Equal(SoundBoardConfig.LegacySchemaVersion, config.SchemaVersion);
            Assert.Empty(config.Settings.OutputDevices);
            Assert.Equal(BoardSettings.DefaultNewPageRows, config.Settings.NewPageDefaultRows);
            Assert.Equal(BoardSettings.DefaultNewPageColumns, config.Settings.NewPageDefaultColumns);

            Page page = Assert.Single(config.Pages);
            Assert.Equal("Old", page.Name);
            Assert.False(page.IsFocused);
            Assert.Equal(BoardSettings.DefaultNewPageRows, page.Rows);
            Assert.Equal(BoardSettings.DefaultNewPageColumns, page.Columns);

            // Index-derived positions in a 2-column grid
            Assert.Equal("One", page[0, 0].Name);
            Assert.Equal("Two", page[0, 1].Name);
            Assert.Equal("Three", page[1, 0].Name);
            Assert.True(page[1, 1].IsEmpty);
            Assert.Equal(3, page.SoundCount);

            // Every sound has a usable id even though none was stored
            Assert.All(page.Sounds, s => Assert.True(Guid.TryParse(s.Id, out _)));
        }

        [Fact]
        public void LegacyMinimal_TabWithoutRows_UsesDefaultsFromSameFile()
        {
            SoundBoardConfig config = Read("<tabs><GlobalSettings NewPageDefaultRows=\"7\" NewPageDefaultColumns=\"3\" /><tab><name>x</name></tab></tabs>");

            Assert.Equal(7, config.Pages[0].Rows);
            Assert.Equal(3, config.Pages[0].Columns);
        }

        [Fact]
        public void Language_RoundTrips()
        {
            SoundBoardConfig config = Read("<tabs><GlobalSettings Language=\"es\" /><tab><name>x</name></tab></tabs>");
            Assert.Equal("es", config.Settings.Language);

            Assert.Contains(" Language=\"es\"", Write(config));
        }

        [Fact]
        public void Language_IsNotWrittenWhenNoneHasBeenChosen()
        {
            // Absent means "follow the OS". Writing it out anyway would rewrite the GlobalSettings line of every
            // existing user's config for no reason; see EmptyConfig_WritesOnlyGlobalSettings.
            Assert.DoesNotContain("Language=", Write(new SoundBoardConfig()));

            var config = new SoundBoardConfig();
            config.Settings.Language = "es";
            config.Settings.Language = string.Empty;
            Assert.DoesNotContain("Language=", Write(config));
        }

        [Fact]
        public void Language_MissingFromTheFile_MeansFollowTheOperatingSystem()
        {
            Assert.Equal(string.Empty, Read(ReadFixture("current-1.10.2.config")).Settings.Language);
            Assert.Equal(string.Empty, Read(ReadFixture("legacy-minimal.config")).Settings.Language);
        }

        [Fact]
        public void Language_IsKeptEvenWhenThisBuildHasNoSuchTranslation()
        {
            // The reader must not second-guess the tag: a config shared with a machine (or a build) that does have the
            // translation has to keep working, and the app falls back to the OS language in the meantime.
            SoundBoardConfig config = Read("<tabs><GlobalSettings Language=\"qps-ploc\" /></tabs>");

            Assert.Equal("qps-ploc", config.Settings.Language);
            Assert.Contains(" Language=\"qps-ploc\"", Write(config));
        }

        [Fact]
        public void Muted_RoundTrips()
        {
            SoundBoardConfig config = Read(ReadFixture("current-1.10.2.config"));
            config.Pages[0][0, 0].Muted = true;

            string written = Write(config);
            Assert.Contains(" muted=\"True\"", written);

            Assert.True(Read(written).Pages[0][0, 0].Muted);
        }

        [Fact]
        public void Muted_IsNotWrittenWhenNothingIsMuted()
        {
            // Every other addition to the format earns its bytes the same way, so that a config from a user who has never
            // touched the setting stays exactly what previous releases wrote. CurrentFormat_RoundTripsByteForByte would
            // catch this too, but only as a wall of diff.
            Assert.DoesNotContain("muted=", Write(Read(ReadFixture("current-1.10.2.config"))));
        }

        [Fact]
        public void Theme_RoundTrips()
        {
            // The whole point of storing it: the theme the user picked has to come back the next time the app starts.
            SoundBoardConfig config = Read("<tabs><GlobalSettings Theme=\"Dark\" /><tab><name>x</name></tab></tabs>");
            Assert.Equal("Dark", config.Settings.Theme);

            Assert.Contains(" Theme=\"Dark\"", Write(config));
        }

        [Fact]
        public void Theme_IsNotWrittenWhenNoneHasBeenChosen()
        {
            // Absent means the default (light) theme, exactly as with Language.
            Assert.DoesNotContain("Theme=", Write(new SoundBoardConfig()));

            var config = new SoundBoardConfig();
            config.Settings.Theme = "Dark";
            config.Settings.Theme = string.Empty;
            Assert.DoesNotContain("Theme=", Write(config));
        }

        [Fact]
        public void Theme_MissingFromTheFile_MeansTheDefaultTheme()
        {
            Assert.Equal(string.Empty, Read(ReadFixture("current-1.10.2.config")).Settings.Theme);
            Assert.Equal(string.Empty, Read(ReadFixture("legacy-minimal.config")).Settings.Theme);
        }

        [Fact]
        public void Defaults_FillSettingsTheFileDoesNotSpecify_ButNeverOverrideIt()
        {
            var defaults = new BoardSettings { AudioPassthroughLatency = 50, NewPageDefaultRows = 4, NewPageDefaultColumns = 3, Language = "es" };
            defaults.OutputDevices.Add(Guid.NewGuid());

            // No GlobalSettings at all, and a tab with no rows/columns: everything comes from the defaults
            SoundBoardConfig legacy = ReadWithDefaults(ReadFixture("legacy-minimal.config"), defaults);
            Assert.Equal(50, legacy.Settings.AudioPassthroughLatency);
            Assert.Equal(4, legacy.Settings.NewPageDefaultRows);
            Assert.Equal(3, legacy.Settings.NewPageDefaultColumns);
            Assert.Equal("es", legacy.Settings.Language);
            Assert.Empty(legacy.Settings.OutputDevices); // device lists are not inherited
            Assert.Equal(4, legacy.Pages[0].Rows);
            Assert.Equal(3, legacy.Pages[0].Columns);
            Assert.Equal("Three", legacy.Pages[0][0, 2].Name); // index positions follow the inherited column count

            // The file's own values win when present
            SoundBoardConfig current = ReadWithDefaults(ReadFixture("current-1.10.2.config"), defaults);
            Assert.Equal(25, current.Settings.AudioPassthroughLatency);
            Assert.Equal(3, current.Settings.NewPageDefaultRows);
            Assert.Equal(4, current.Settings.NewPageDefaultColumns);
            Assert.Equal(2, current.Pages[0].Rows);
        }

        private static SoundBoardConfig ReadWithDefaults(string xml, BoardSettings defaults)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return ConfigSerializer.Read(stream, null, defaults);
            }
        }

        [Fact]
        public void NegativeOrZeroDimensions_DoNotFailTheLoad()
        {
            var warnings = new List<string>();
            SoundBoardConfig config = Read(
                "<tabs>" +
                "<tab rows=\"-1\" columns=\"2\"><name>neg</name><button0 name=\"a\" path=\"C:\\a.mp3\" /></tab>" +
                "<tab rows=\"1\" columns=\"0\"><name>zero</name><button0 name=\"b\" path=\"C:\\b.mp3\" /><button1 name=\"c\" path=\"C:\\c.mp3\" /></tab>" +
                "</tabs>", warnings);

            Assert.Equal(2, config.Pages.Count);
            Assert.Equal(0, config.Pages[0].Rows);
            Assert.Empty(config.Pages[0].Sounds);
            Assert.Equal(0, config.Pages[1].Columns);
            Assert.Empty(config.Pages[1].Sounds);
            Assert.Contains(warnings, w => w.Contains("neg"));
            Assert.Contains("rows=\"0\" columns=\"2\"", Write(config));
        }

        [Fact]
        public void NegativeNewPageDefaults_AreIgnored()
        {
            var warnings = new List<string>();
            SoundBoardConfig config = Read("<tabs><GlobalSettings NewPageDefaultRows=\"-1\" NewPageDefaultColumns=\"-2\" /><tab><name>n</name></tab></tabs>", warnings);

            Assert.Equal(BoardSettings.DefaultNewPageRows, config.Settings.NewPageDefaultRows);
            Assert.Equal(BoardSettings.DefaultNewPageColumns, config.Settings.NewPageDefaultColumns);
            Assert.Equal(BoardSettings.DefaultNewPageRows, config.Pages[0].Rows);
            Assert.Equal(2, warnings.Count);
        }

        [Fact]
        public void ExtrasAndDuplicates_FirstClaimWins_OutOfRangeDropped()
        {
            var warnings = new List<string>();
            SoundBoardConfig config = Read(ReadFixture("extras-and-duplicates.config"), warnings);

            Page page = Assert.Single(config.Pages);
            Assert.Equal(2, page.Sounds.Count);
            Assert.Equal("First", page[0, 0].Name);
            Assert.Equal("Second", page[0, 1].Name);
            Assert.Equal(3, warnings.Count);
            Assert.Contains(warnings, w => w.Contains("button2"));
            Assert.Contains(warnings, w => w.Contains("button3"));
            Assert.Contains(warnings, w => w.Contains("button4"));
        }

        [Fact]
        public void EmptyOutputDeviceList_IsWrittenAsDefaultDeviceGuid()
        {
            SoundBoardConfig config = Read(ReadFixture("extras-and-duplicates.config"));

            // Guid.Empty in the file means "default device"; it is kept as-is because that is what the UI has always stored.
            Assert.Equal(new[] { Guid.Empty }, config.Settings.OutputDevices);

            config.Settings.OutputDevices.Clear();
            Assert.Contains("OutputDeviceGuid=\"00000000-0000-0000-0000-000000000000\"", Write(config));
        }

        [Fact]
        public void MissingAttributes_AreLenient()
        {
            SoundBoardConfig config = Read(ReadFixture("missing-attributes.config"));

            // Bad guid skipped, good one kept, unparseable latency ignored
            Assert.Equal(new[] { Guid.Parse("a1b2c3d4-0000-0000-0000-000000000001") }, config.Settings.OutputDevices);
            Assert.Equal(BoardSettings.DefaultAudioPassthroughLatency, config.Settings.AudioPassthroughLatency);

            Page sparse = config.Pages[0];
            Assert.True(sparse[0, 0].IsEmpty);                         // <button0 /> with no name/path: an empty cell, not a crash
            Assert.Equal(@"C:\only-path.mp3", sparse[0, 1].Path);      // path without name
            Assert.Equal(string.Empty, sparse[0, 1].Name);
            Assert.True(sparse[1, 0].IsEmpty);                         // button2 absent

            Sound weird = sparse[1, 1];
            Assert.Equal("Weird", weird.Name);
            Assert.Null(weird.Color);
            Assert.Equal(0, weird.VolumeOffset);
            Assert.False(weird.Loop);
            Assert.False(weird.Muted);                                 // muted predates nothing: a file without it is unmuted
            Assert.False(weird.StopAllSounds);
            Assert.Null(weird.NextSoundId);
            Assert.True(Guid.TryParse(weird.Id, out _));               // empty id attribute -> fresh id
            Assert.Null(weird.LocalHotkey);                            // unknown key name
            Assert.Null(weird.GlobalHotkey);                           // modifier with no key

            Page unnamed = config.Pages[1];
            Assert.Equal(string.Empty, unnamed.Name);
            Assert.Contains("<name />", Write(config));
        }

        [Fact]
        public void NewerSchemaVersion_LoadsBestEffortWithWarning()
        {
            var warnings = new List<string>();
            SoundBoardConfig config = Read("<tabs schemaVersion=\"99\"><tab rows=\"1\" columns=\"1\"><name>n</name></tab></tabs>", warnings);

            Assert.Equal(99, config.SchemaVersion);
            Assert.Single(config.Pages);
            Assert.Contains(warnings, w => w.Contains("99"));
        }

        [Fact]
        public void SchemaVersion_IsAlwaysWrittenAsCurrent()
        {
            var config = new SoundBoardConfig { SchemaVersion = SoundBoardConfig.LegacySchemaVersion };

            Assert.Contains($"<tabs schemaVersion=\"{SoundBoardConfig.CurrentSchemaVersion}\">", Write(config));
        }

        [Fact]
        public void EmptyConfig_WritesOnlyGlobalSettings()
        {
            string written = Write(new SoundBoardConfig());

            Assert.Equal(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<tabs schemaVersion=\"2\">\r\n" +
                "    <GlobalSettings OutputDeviceGuid=\"00000000-0000-0000-0000-000000000000\" InputDeviceGuid=\"\" PassthroughOutputDeviceGuid=\"\" AudioPassthroughLatency=\"10\" NewPageDefaultRows=\"5\" NewPageDefaultColumns=\"2\" />\r\n" +
                "</tabs>",
                written);
        }

        [Fact]
        public void MalformedXml_Throws()
        {
            Assert.ThrowsAny<System.Xml.XmlException>(() => Read("<tabs><tab>"));
        }

        [Fact]
        public void UnparseableColor_Throws()
        {
            // Preserved from the legacy reader: a bad color fails the load rather than silently dropping the color.
            Assert.ThrowsAny<Exception>(() => Read("<tabs><tab rows=\"1\" columns=\"1\"><name>n</name><button0 name=\"a\" path=\"C:\\a.mp3\" color=\"#GGGGGG\" /></tab></tabs>"));
        }

        #endregion
    }
}
