using System;
using System.Linq;
using System.Windows.Input;
using SoundBoard.Model;
using Xunit;

namespace SoundBoard.Tests
{
    public class SoundTests
    {
        [Fact]
        public void NewSound_IsEmptyWithFreshId()
        {
            var sound = new Sound();

            Assert.True(sound.IsEmpty);
            Assert.Equal(string.Empty, sound.Name);
            Assert.Equal(string.Empty, sound.Path);
            Assert.True(Guid.TryParse(sound.Id, out _));
            Assert.NotEqual(sound.Id, new Sound().Id);
        }

        [Fact]
        public void Clear_ResetsEverythingExceptPosition_AndRegeneratesId()
        {
            Sound sound = Filled();
            string oldId = sound.Id;

            sound.Clear();

            Assert.True(sound.IsEmpty);
            Assert.NotEqual(oldId, sound.Id);
            Assert.Equal(string.Empty, sound.Name);
            Assert.Null(sound.Color);
            Assert.Equal(0, sound.VolumeOffset);
            Assert.False(sound.Loop);
            Assert.False(sound.StopAllSounds);
            Assert.Null(sound.NextSoundId);
            Assert.Null(sound.LocalHotkey);
            Assert.Null(sound.GlobalHotkey);
            Assert.Equal(3, sound.Row);
            Assert.Equal(4, sound.Column);
        }

        [Fact]
        public void DeepClone_IsEqualAndIndependent()
        {
            Sound sound = Filled();
            Sound clone = sound.DeepClone();

            Assert.NotSame(sound, clone);
            Assert.Equal(sound.Id, clone.Id);
            Assert.Equal(sound.Name, clone.Name);
            Assert.Equal(sound.Path, clone.Path);
            Assert.Equal(sound.Color, clone.Color);
            Assert.Equal(sound.VolumeOffset, clone.VolumeOffset);
            Assert.Equal(sound.Loop, clone.Loop);
            Assert.Equal(sound.StopAllSounds, clone.StopAllSounds);
            Assert.Equal(sound.NextSoundId, clone.NextSoundId);
            Assert.Equal(sound.LocalHotkey, clone.LocalHotkey);
            Assert.Equal(sound.GlobalHotkey, clone.GlobalHotkey);
            Assert.Equal(sound.Row, clone.Row);
            Assert.Equal(sound.Column, clone.Column);

            clone.Name = "changed";
            clone.Clear();
            Assert.Equal("name", sound.Name);
            Assert.False(sound.IsEmpty);
        }

        [Fact]
        public void CopyDataFrom_KeepsPosition()
        {
            Sound source = Filled();
            var target = new Sound { Row = 7, Column = 8 };

            target.CopyDataFrom(source);

            Assert.Equal(source.Id, target.Id);
            Assert.Equal(source.Path, target.Path);
            Assert.Equal(source.GlobalHotkey, target.GlobalHotkey);
            Assert.Equal(7, target.Row);
            Assert.Equal(8, target.Column);
        }

        [Fact]
        public void Id_NeverNullOrEmpty()
        {
            var sound = new Sound();
            string original = sound.Id;

            sound.Id = null;
            Assert.False(string.IsNullOrEmpty(sound.Id));
            Assert.NotEqual(original, sound.Id);

            sound.Id = "";
            Assert.False(string.IsNullOrEmpty(sound.Id));

            sound.Id = "custom";
            Assert.Equal("custom", sound.Id);
        }

        [Fact]
        public void NameAndPath_NullBecomesEmpty()
        {
            var sound = new Sound { Name = null, Path = null };
            Assert.Equal(string.Empty, sound.Name);
            Assert.Equal(string.Empty, sound.Path);
            Assert.True(sound.IsEmpty);
        }

        [Fact]
        public void PropertyChanged_RaisedOnChangeOnly()
        {
            var sound = new Sound();
            var changed = new System.Collections.Generic.List<string>();
            sound.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            sound.Path = @"C:\a.mp3";
            sound.Path = @"C:\a.mp3"; // no change
            sound.Loop = true;
            sound.Loop = true; // no change

            Assert.Equal(new[] { nameof(Sound.Path), nameof(Sound.IsEmpty), nameof(Sound.Loop) }, changed);
        }

        internal static Sound Filled() => new Sound
        {
            Name = "name",
            Path = @"C:\x.mp3",
            Color = new SoundColor(1, 2, 3, 4),
            VolumeOffset = -2,
            Loop = true,
            StopAllSounds = true,
            NextSoundId = "next",
            LocalHotkey = new Hotkey(Key.A, ModifierKeys.Control),
            GlobalHotkey = new Hotkey(Key.B, ModifierKeys.Windows),
            Row = 3,
            Column = 4,
        };
    }

    public class PageTests
    {
        [Fact]
        public void NewPage_IsDenseRowMajorAndEmpty()
        {
            var page = new Page("p", 2, 3);

            Assert.Equal(6, page.Sounds.Count);
            Assert.All(page.Sounds, s => Assert.True(s.IsEmpty));
            Assert.Equal(new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2) }, page.Sounds.Select(s => (s.Row, s.Column)));
            Assert.Same(page.Sounds[4], page[1, 1]);
            Assert.Equal(0, page.SoundCount);
        }

        [Fact]
        public void Indexer_ThrowsOutOfRange()
        {
            var page = new Page("p", 2, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => page[2, 0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => page[0, -1]);
            Assert.False(page.IsInRange(2, 0));
            Assert.True(page.IsInRange(1, 1));
        }

        [Fact]
        public void Set_ReplacesCell()
        {
            var page = new Page("p", 2, 2);
            var sound = new Sound { Row = 1, Column = 0, Path = @"C:\a.mp3" };

            page.Set(sound);

            Assert.Same(sound, page[1, 0]);
            Assert.Same(sound, page.Sounds[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => page.Set(new Sound { Row = 5, Column = 5 }));
        }

        [Fact]
        public void Resize_KeepsInRange_DropsOutOfRange_FillsNew()
        {
            var page = new Page("p", 2, 2);
            var keep = new Sound { Row = 0, Column = 1, Path = @"C:\keep.mp3" };
            var drop = new Sound { Row = 1, Column = 1, Path = @"C:\drop.mp3" };
            page.Set(keep);
            page.Set(drop);

            var dropped = page.Resize(1, 3);

            Assert.Equal(1, page.Rows);
            Assert.Equal(3, page.Columns);
            Assert.Equal(3, page.Sounds.Count);
            Assert.Same(keep, page[0, 1]);
            Assert.True(page[0, 2].IsEmpty);
            Assert.Equal(2, page[0, 2].Column);
            Assert.Equal(new[] { drop }, dropped.Where(s => !s.IsEmpty));
            Assert.Equal(new[] { (0, 0), (0, 1), (0, 2) }, page.Sounds.Select(s => (s.Row, s.Column)));
        }

        [Fact]
        public void FindSound_ById()
        {
            var page = new Page("p", 1, 2);
            Sound target = page[0, 1];

            Assert.Same(target, page.FindSound(target.Id));
            Assert.Null(page.FindSound("missing"));
            Assert.Null(page.FindSound(null));
        }

        [Fact]
        public void Name_NullBecomesEmpty_AndNotifies()
        {
            var page = new Page(null, 1, 1);
            Assert.Equal(string.Empty, page.Name);

            var changed = new System.Collections.Generic.List<string>();
            page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            page.Name = "x";
            page.Resize(2, 2);

            Assert.Contains(nameof(Page.Name), changed);
            Assert.Contains(nameof(Page.Rows), changed);
            Assert.Contains(nameof(Page.Columns), changed);
            Assert.Contains(nameof(Page.Sounds), changed);
        }

        [Fact]
        public void DeepClone_IsIndependent()
        {
            var page = new Page("p", 1, 2) { IsFocused = true };
            page.Set(new Sound { Row = 0, Column = 0, Path = @"C:\a.mp3", Name = "a" });

            Page clone = page.DeepClone();

            Assert.Equal("p", clone.Name);
            Assert.True(clone.IsFocused);
            Assert.Equal(page[0, 0].Id, clone[0, 0].Id);
            Assert.NotSame(page[0, 0], clone[0, 0]);

            clone[0, 0].Name = "b";
            Assert.Equal("a", page[0, 0].Name);
        }
    }

    public class SoundBoardConfigTests
    {
        [Fact]
        public void FindSound_ByIdAcrossPages_NullWhenMissing()
        {
            var config = new SoundBoardConfig();
            config.Pages.Add(new Page("a", 1, 1));
            config.Pages.Add(new Page("b", 1, 1));
            Sound target = config.Pages[1][0, 0];

            Assert.Same(target, config.FindSound(target.Id));
            Assert.Null(config.FindSound("nope"));
            Assert.Null(config.FindSound(null));
            Assert.Null(config.FindSound(string.Empty));
        }

        [Fact]
        public void DeepClone_IsIndependent()
        {
            var config = new SoundBoardConfig();
            config.Settings.OutputDevices.Add(Guid.NewGuid());
            config.Settings.AudioPassthroughLatency = 42;
            config.Pages.Add(new Page("a", 1, 1));

            SoundBoardConfig clone = config.DeepClone();
            clone.Settings.OutputDevices.Clear();
            clone.Settings.AudioPassthroughLatency = 1;
            clone.Pages.Clear();

            Assert.Single(config.Settings.OutputDevices);
            Assert.Equal(42, config.Settings.AudioPassthroughLatency);
            Assert.Single(config.Pages);
        }

        [Fact]
        public void Settings_CopyFrom_ReplacesEverything()
        {
            var source = new BoardSettings { AudioPassthroughLatency = 7, NewPageDefaultRows = 1, NewPageDefaultColumns = 9 };
            source.OutputDevices.Add(Guid.NewGuid());

            var target = new BoardSettings();
            target.OutputDevices.Add(Guid.NewGuid());
            target.InputDevices.Add(Guid.NewGuid());

            target.CopyFrom(source);

            Assert.Equal(7, target.AudioPassthroughLatency);
            Assert.Equal(1, target.NewPageDefaultRows);
            Assert.Equal(9, target.NewPageDefaultColumns);
            Assert.Equal(source.OutputDevices, target.OutputDevices);
            Assert.Empty(target.InputDevices);
        }

        [Fact]
        public void Settings_OutputDevicesOrDefault()
        {
            var settings = new BoardSettings();
            Assert.Equal(new[] { Guid.Empty }, settings.GetOutputDeviceGuidsOrDefault());

            var guid = Guid.NewGuid();
            settings.OutputDevices.Add(guid);
            Assert.Equal(new[] { guid }, settings.GetOutputDeviceGuidsOrDefault());
        }
    }

    public class HotkeyTests
    {
        [Theory]
        [InlineData(Key.A, ModifierKeys.None, "A")]
        [InlineData(Key.A, ModifierKeys.Control, "Ctrl + A")]
        [InlineData(Key.F5, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows, "Ctrl + Shift + Alt + Win + F5")]
        [InlineData(Key.OemPlus, ModifierKeys.Shift, "Shift + OemPlus")]
        [InlineData(Key.NumPad0, ModifierKeys.Alt, "Alt + NumPad0")]
        public void ToString_FromString_RoundTrip(Key key, ModifierKeys modifiers, string expected)
        {
            var hotkey = new Hotkey(key, modifiers);

            Assert.Equal(expected, hotkey.ToString());
            Assert.Equal(hotkey, Hotkey.FromString(expected));
        }

        [Theory]
        [InlineData("NotAKey")]
        [InlineData("Ctrl + NotAKey")]
        [InlineData("Shift + ")]
        [InlineData("")]
        public void FromString_UnknownKey_ReturnsNull(string text)
        {
            Assert.Null(Hotkey.FromString(text));
        }

        [Fact]
        public void Equality()
        {
            Assert.Equal(new Hotkey(Key.A, ModifierKeys.Control), new Hotkey(Key.A, ModifierKeys.Control));
            Assert.NotEqual(new Hotkey(Key.A, ModifierKeys.Control), new Hotkey(Key.A, ModifierKeys.Shift));
            Assert.NotEqual(new Hotkey(Key.A, ModifierKeys.Control), new Hotkey(Key.B, ModifierKeys.Control));
            Assert.False(new Hotkey(Key.A, ModifierKeys.Control).Equals(null));
        }
    }

    public class SoundColorTests
    {
        [Fact]
        public void ToHtml_MatchesWpfColorToStringFormat()
        {
            Assert.Equal("#FFAABBCC", new SoundColor(0xFF, 0xAA, 0xBB, 0xCC).ToHtml());
            Assert.Equal("#00000000", new SoundColor(0, 0, 0, 0).ToHtml());
            Assert.Equal("#0A0B0C0D", new SoundColor(10, 11, 12, 13).ToHtml());
        }

        [Theory]
        [InlineData("#FFAABBCC", 0xFF, 0xAA, 0xBB, 0xCC)]
        [InlineData("#80123456", 0x80, 0x12, 0x34, 0x56)]
        [InlineData("#AABBCC", 0xFF, 0xAA, 0xBB, 0xCC)]
        [InlineData("Red", 0xFF, 0xFF, 0x00, 0x00)]
        public void Parse_UsesColorTranslatorSemantics(string text, int a, int r, int g, int b)
        {
            Assert.Equal(new SoundColor((byte)a, (byte)r, (byte)g, (byte)b), SoundColor.Parse(text));
        }

        [Fact]
        public void Parse_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(SoundColor.Parse(null));
            Assert.Null(SoundColor.Parse(string.Empty));
        }

        [Fact]
        public void RoundTrip()
        {
            var color = new SoundColor(0x80, 0x12, 0x34, 0x56);
            Assert.Equal(color, SoundColor.Parse(color.ToHtml()));
        }
    }
}
