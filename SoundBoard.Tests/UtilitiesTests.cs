using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using WpfKey = System.Windows.Input.Key;
using HotKey = BondTech.HotKeyManagement.WPF._4.Keys;

namespace SoundBoard.Tests
{
    public class UtilitiesTruncateTests
    {
        // A fixed font so the measurements are deterministic on a given machine. The tests below never assert on exact
        // pixel widths, only on relationships between them, so they are robust to font substitution on the CI runner.
        private static readonly Font Font = new Font(FontFamily.GenericSansSerif, 12f);

        private static int Width(string text) => TextRenderer.MeasureText(text, Font).Width;

        [Fact]
        public void FitsWithinMaxWidth_IsReturnedUnchanged()
        {
            const string input = "Airhorn";

            Assert.Equal(input, Utilities.Truncate(input, Font, Width(input + "...") + 1));
        }

        [Fact]
        public void TooWide_IsTruncatedWithEllipsesAndFits()
        {
            const string input = "A fairly long sound button name that will not fit";
            int maxWidth = Width("A fairly long");

            string result = Utilities.Truncate(input, Font, maxWidth);

            Assert.EndsWith("...", result);
            Assert.StartsWith(result.Substring(0, result.Length - 3), input);
            Assert.True(result.Length < input.Length);
            Assert.True(Width(result) <= maxWidth, $"'{result}' ({Width(result)}px) does not fit in {maxWidth}px");
        }

        [Fact]
        public void Truncation_RemovesAsFewCharactersAsPossible()
        {
            const string input = "Crickets chirping at night";
            int maxWidth = Width("Crickets chirp...");

            string result = Utilities.Truncate(input, Font, maxWidth);

            // One more character would have overflowed
            string oneMore = input.Substring(0, result.Length - 3 + 1) + "...";
            Assert.True(Width(oneMore) > maxWidth);
        }

        [Fact]
        public void OffsetString_ReducesAvailableWidth()
        {
            const string input = "Crickets chirping at night";
            int maxWidth = Width(input + "...");

            // Without an offset the whole string fits...
            Assert.Equal(input, Utilities.Truncate(input, Font, maxWidth));

            // ...but an offset string eats into the budget
            string result = Utilities.Truncate(input, Font, maxWidth, "(2)");
            Assert.EndsWith("...", result);
            Assert.True(Width(result) + Width("(2)") <= maxWidth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-100)]
        public void ImpossiblySmallWidth_StopsAtEllipsesInsteadOfThrowing(int maxWidth)
        {
            Assert.Equal("...", Utilities.Truncate("Airhorn", Font, maxWidth));
        }

        [Fact]
        public void OffsetWiderThanMax_StopsAtEllipsesInsteadOfThrowing()
        {
            Assert.Equal("...", Utilities.Truncate("Airhorn", Font, Width("x"), "a very wide offset string"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EmptyOrNull_ReturnsEmpty(string input)
        {
            Assert.Equal(string.Empty, Utilities.Truncate(input, Font, 100));
            Assert.Equal(string.Empty, Utilities.Truncate(input, Font, 0));
        }
    }

    public class UtilitiesSanitizeIdTests
    {
        [Theory]
        [InlineData("11111111-1111-1111-1111-111111111111", "-1111-1111-1111-111111111111")]
        [InlineData("1a2b3c4d-0000-0000-0000-000000000000", "a2b3c4d-0000-0000-0000-000000000000")]
        [InlineData("a1b2c3d4-0000-0000-0000-000000000001", "a1b2c3d4-0000-0000-0000-000000000001")]
        [InlineData("abc", "abc")]
        [InlineData("123", "")]
        public void StripsLeadingDigitsOnly(string input, string expected)
        {
            Assert.Equal(expected, Utilities.SanitizeId(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void NullOrEmpty_PassesThrough(string input)
        {
            Assert.Equal(input, Utilities.SanitizeId(input));
        }

        [Fact]
        public void IsIdempotent()
        {
            string once = Utilities.SanitizeId("42abc-1");
            Assert.Equal(once, Utilities.SanitizeId(once));
        }

        [Fact]
        public void DistinctGuids_StayDistinct()
        {
            var ids = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid().ToString()).ToList();

            Assert.Equal(ids.Count, ids.Select(Utilities.SanitizeId).Distinct().Count());
        }
    }

    public class UtilitiesMapKeyTests
    {
        [Theory]
        [InlineData(WpfKey.A, HotKey.A)]
        [InlineData(WpfKey.Z, HotKey.Z)]
        [InlineData(WpfKey.F1, HotKey.F1)]
        [InlineData(WpfKey.F12, HotKey.F12)]
        [InlineData(WpfKey.D0, HotKey.D0)]
        [InlineData(WpfKey.NumPad5, HotKey.NumPad5)]
        [InlineData(WpfKey.Space, HotKey.Space)]
        [InlineData(WpfKey.Escape, HotKey.Escape)]
        [InlineData(WpfKey.Enter, HotKey.Enter)]
        [InlineData(WpfKey.Left, HotKey.Left)]
        [InlineData(WpfKey.Insert, HotKey.Insert)]
        [InlineData(WpfKey.PageDown, HotKey.PageDown)]
        [InlineData(WpfKey.None, HotKey.None)]
        public void MapsByName(WpfKey key, HotKey expected)
        {
            Assert.Equal(expected, Utilities.MapKey(key));
        }

        [Fact]
        public void MappedKeys_RoundTripByName()
        {
            foreach (WpfKey key in Enum.GetValues(typeof(WpfKey)).Cast<WpfKey>())
            {
                HotKey mapped = Utilities.MapKey(key);
                if (mapped != HotKey.None)
                {
                    // Parsed rather than compared as strings because both enums have aliases (e.g. Return/Enter)
                    Assert.Equal(key, (WpfKey)Enum.Parse(typeof(WpfKey), mapped.ToString(), true));
                }
            }
        }

        [Fact]
        public void KeysWithNoEquivalent_MapToNone()
        {
            // WPF has a few keys the hotkey manager does not know about; they must map to None (which the registry then rejects)
            // rather than to some unrelated key.
            var unmapped = Enum.GetValues(typeof(WpfKey)).Cast<WpfKey>()
                .Where(k => k != WpfKey.None && Utilities.MapKey(k) == HotKey.None)
                .ToList();

            Assert.All(unmapped, k => Assert.False(Enum.TryParse(k.ToString(), true, out HotKey _)));
            // Sanity check that the common keys are not among them
            Assert.DoesNotContain(WpfKey.A, unmapped);
            Assert.DoesNotContain(WpfKey.F5, unmapped);
        }
    }

    public class UtilitiesSupportedAudioFileTypesTests
    {
        [Fact]
        public void IsASemicolonSeparatedSortedListOfWildcards()
        {
            string[] parts = Utilities.SupportedAudioFileTypes.Split(new[] { "; " }, StringSplitOptions.None);

            Assert.NotEmpty(parts);
            Assert.All(parts, p => Assert.Matches(@"^\*\.[^\s;*]+$", p));
            Assert.Equal(parts.OrderBy(p => p).ToList(), parts.ToList());
            Assert.Equal(parts.Distinct().Count(), parts.Length);
        }

        [Theory]
        [InlineData("*.mp3")]
        [InlineData("*.wav")]
        [InlineData("*.ogg")]
        [InlineData("*.flac")]
        [InlineData("*.m4a")]
        [InlineData("*.mp4")]
        [InlineData("*.wma")]
        public void IncludesCommonAudioAndVideoTypes(string pattern)
        {
            Assert.Contains(pattern, Utilities.SupportedAudioFileTypes.Split(new[] { "; " }, StringSplitOptions.None));
        }

        [Theory]
        [InlineData("*.txt")]
        [InlineData("*.png")]
        [InlineData("*.exe")]
        public void ExcludesNonMediaTypes(string pattern)
        {
            Assert.DoesNotContain(pattern, Utilities.SupportedAudioFileTypes.Split(new[] { "; " }, StringSplitOptions.None));
        }

        [Fact]
        public void IsCached()
        {
            Assert.Same(Utilities.SupportedAudioFileTypes, Utilities.SupportedAudioFileTypes);
        }

        [Fact]
        public void IsUsableAsAnOpenFileDialogFilter()
        {
            // The value is dropped straight into OpenFileDialog.Filter, which uses '|' as its own separator
            Assert.DoesNotContain("|", Utilities.SupportedAudioFileTypes);
        }
    }
}
