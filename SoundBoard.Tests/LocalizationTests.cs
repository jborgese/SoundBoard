using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace SoundBoard.Tests
{
    /// <summary>
    /// Guards the translations against the mistakes that only show up on a user's machine: a language offered in the
    /// menu with no resources behind it, a language built into the exe that the menu never offers, a string nobody
    /// translated, and a translation whose <c>{0}</c> placeholders no longer match the code that fills them in.
    /// </summary>
    /// <remarks>
    /// These read the compiled resources rather than the <c>.resx</c> files, so they also cover the build: a
    /// translation that never made it into a satellite assembly fails here.
    /// </remarks>
    public class LocalizationTests
    {
        private static readonly ResourceManager Resources = SoundBoard.Properties.Resources.ResourceManager;

        /// <summary>
        /// Matches <c>{0}</c> and <c>{0:X}</c> but not <c>{{</c>. Only the index matters: a translation may reorder
        /// the placeholders (and Spanish does), and may change the alignment or format after the colon, but it must
        /// use exactly the same set of arguments.
        /// </summary>
        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

        /// <summary>
        /// Every language the app offers, apart from "same as Windows" and English (which is the neutral resource set,
        /// and so has no satellite assembly of its own).
        /// </summary>
        public static IEnumerable<object[]> TranslatedLanguages =>
            Localization.Available
                .Where(language => language.Tag.Length > 0 && language.Tag != "en")
                .Select(language => new object[] { language.Tag });

        private static IDictionary<string, string> Strings(CultureInfo culture, bool tryParents)
        {
            ResourceSet set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: tryParents);

            if (set is null)
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in set)
            {
                if (entry.Value is string value)
                {
                    result[(string)entry.Key] = value;
                }
            }

            return result;
        }

        private static IDictionary<string, string> Neutral() => Strings(CultureInfo.InvariantCulture, tryParents: true);

        private static string[] Placeholders(string value) =>
            Placeholder.Matches(value).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(i => i).ToArray();

        [Fact]
        public void EveryOfferedLanguageIsDistinct()
        {
            string[] tags = Localization.Available.Select(language => language.Tag).ToArray();

            Assert.Equal(tags.Length, tags.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(string.Empty, tags);
            Assert.Contains("en", tags);
        }

        [Fact]
        public void OfferedLanguagesAreRealCultures()
        {
            foreach (string tag in Localization.Available.Select(language => language.Tag).Where(t => t.Length > 0))
            {
                // Throws CultureNotFoundException if the tag is a typo, which would otherwise only show up as a
                // menu entry that silently does nothing.
                CultureInfo.GetCultureInfo(tag);
            }
        }

        [Theory]
        [MemberData(nameof(TranslatedLanguages))]
        public void TranslationIsPresentInTheBuild(string tag)
        {
            // tryParents: false, so this is the satellite assembly's own strings and never the English fallback.
            IDictionary<string, string> translated = Strings(CultureInfo.GetCultureInfo(tag), tryParents: false);

            Assert.True(translated != null && translated.Count > 0,
                $"No resources were found for '{tag}'. Either Properties\\Resources.{tag}.resx is missing from " +
                "SoundBoard.csproj, or the satellite assembly was not built.");
        }

        [Fact]
        public void TheLanguagesBuiltAndTheLanguagesOfferedAreTheSameSet()
        {
            // The other checks in this class are driven by Localization.Available, so they can only ever see a
            // language that has been registered in TranslatedLanguageTags. That leaves the opposite mistake invisible:
            // a translation that is compiled and shipped but never registered produces no failing test, because it
            // produces no test at all. This one is driven by the build output instead, which makes it the only check
            // here that can fail for a language nobody declared.
            //
            // Stated as an invariant rather than a guard against one slip: the satellite assemblies are the truth,
            // TranslatedLanguageTags is the claim, and the two must agree. Both directions of disagreement are real
            // mistakes and both are silent without this - a tag added without its .resx, and a tag removed while its
            // .resx and <EmbeddedResource> entry stay behind, still shipping a language the app no longer offers.
            string satellite = typeof(Localization).Assembly.GetName().Name + ".resources.dll";
            string outputDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Satellites live in a directory named for their culture, but most of the culture directories here belong
            // to somebody else: xUnit, MahApps, AppHelpers.WPF and TaskScheduler between them ship fourteen, and only
            // one of those (es) holds ours. There is also a Fixtures directory, which is not a culture at all. So the
            // filter has to be on the satellite's file name; matching on "a directory exists" would report every one
            // of those as an unregistered language. Do not simplify this to *.resources.dll.
            string[] built = Directory.GetDirectories(outputDirectory)
                .Where(directory => File.Exists(Path.Combine(directory, satellite)))
                .Select(Path.GetFileName)
                .ToArray();

            string[] offered = Localization.Available
                .Select(language => language.Tag)
                .Where(tag => tag.Length > 0 && tag != "en")
                .ToArray();

            // The whole directory name is the culture, never a parsed piece of it: pt-BR, zh-Hans and zh-Hant are all
            // real culture directories in this very folder, so anything that assumed a two-letter tag would break on
            // the first regional translation. Compared case-insensitively because the case a directory name is given
            // need not match the case someone typed into TranslatedLanguageTags (Costura, for one, lowercases zh-CN
            // when it embeds it), and a casing difference here would be reported as a missing language.
            string[] unregistered = built.Except(offered, StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();
            string[] unbuilt = offered.Except(built, StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToArray();

            Assert.True(unregistered.Length == 0,
                $"Built into the exe but never offered: {string.Join(", ", unregistered)}. The .resx compiled into a " +
                "satellite assembly, but the tag is not in TranslatedLanguageTags in Localization.cs, so the Language " +
                "menu never shows it and nothing else in this class enumerates it. Add the tag, or delete the .resx " +
                "and its <EmbeddedResource> entry in SoundBoard.csproj.");

            Assert.True(unbuilt.Length == 0,
                $"Offered in the menu but not built: {string.Join(", ", unbuilt)}. TranslatedLanguageTags names it, but " +
                $"no {satellite} was produced for it - Properties\\Resources.<tag>.resx is probably missing from " +
                "SoundBoard.csproj.");
        }

        [Theory]
        [MemberData(nameof(TranslatedLanguages))]
        public void TranslationCoversEveryString(string tag)
        {
            IDictionary<string, string> neutral = Neutral();
            IDictionary<string, string> translated = Strings(CultureInfo.GetCultureInfo(tag), tryParents: false);

            string[] missing = neutral.Keys.Where(key => !translated.ContainsKey(key)).OrderBy(key => key).ToArray();

            Assert.True(missing.Length == 0,
                $"Properties\\Resources.{tag}.resx is missing: {string.Join(", ", missing)}");
        }

        [Theory]
        [MemberData(nameof(TranslatedLanguages))]
        public void TranslationHasNoStringsThatNoLongerExist(string tag)
        {
            IDictionary<string, string> neutral = Neutral();
            IDictionary<string, string> translated = Strings(CultureInfo.GetCultureInfo(tag), tryParents: false);

            string[] extra = translated.Keys.Where(key => !neutral.ContainsKey(key)).OrderBy(key => key).ToArray();

            Assert.True(extra.Length == 0,
                $"Properties\\Resources.{tag}.resx has strings the neutral resources no longer define: {string.Join(", ", extra)}");
        }

        [Theory]
        [MemberData(nameof(TranslatedLanguages))]
        public void TranslationUsesTheSamePlaceholders(string tag)
        {
            IDictionary<string, string> neutral = Neutral();
            IDictionary<string, string> translated = Strings(CultureInfo.GetCultureInfo(tag), tryParents: false);

            var mismatches = new List<string>();
            foreach (var pair in neutral.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!translated.TryGetValue(pair.Key, out string value)) continue;

                string[] expected = Placeholders(pair.Value);
                string[] actual = Placeholders(value);

                if (!expected.SequenceEqual(actual))
                {
                    mismatches.Add($"{pair.Key}: expected {{{string.Join(",", expected)}}} but found {{{string.Join(",", actual)}}}");
                }
            }

            Assert.True(mismatches.Count == 0,
                $"Placeholder mismatches in Properties\\Resources.{tag}.resx (string.Format would throw or drop an " +
                $"argument at runtime):{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
        }

        [Fact]
        public void NoStringLeansOnBeingConcatenatedWithAnother()
        {
            // A leading or trailing space in a resource almost always means the string is glued onto another one at
            // runtime, which is the one thing a translator cannot see and cannot reorder. Join complete sentences in
            // code instead (HotkeyDialog.OKButton_Click does).
            string[] offenders = Neutral()
                .Where(pair => pair.Value.Length > 0 && pair.Value.Trim().Length != pair.Value.Length)
                .Select(pair => pair.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            Assert.True(offenders.Length == 0,
                $"These resources start or end with whitespace: {string.Join(", ", offenders)}");
        }

        [Fact]
        public void SpanishResolvesThroughTheResourceManager()
        {
            CultureInfo spanish = CultureInfo.GetCultureInfo("es");

            Assert.Equal("Aceptar", Resources.GetString("OK", spanish));

            // A regional variant has no resources of its own and must fall back to its parent, not to English.
            Assert.Equal("Aceptar", Resources.GetString("OK", CultureInfo.GetCultureInfo("es-MX")));

            // A language with no translation at all falls back to the neutral (English) resources.
            Assert.Equal("OK", Resources.GetString("OK", CultureInfo.GetCultureInfo("fr-FR")));
        }

        [Fact]
        public void SpanishReordersPlaceholdersWithoutLosingThem()
        {
            // Spanish names the sound before the hotkey where English does the opposite. This is the case the
            // placeholder test above exists to protect, so pin it down with a real format call.
            string formatted = string.Format(
                Resources.GetString("LocalHotkeyInUse", CultureInfo.GetCultureInfo("es")), "Ctrl + A", "Airhorn");

            Assert.Contains("Ctrl + A", formatted);
            Assert.Contains("Airhorn", formatted);
            Assert.True(formatted.IndexOf("Airhorn", StringComparison.Ordinal) < formatted.IndexOf("Ctrl + A", StringComparison.Ordinal));
        }

        [Fact]
        public void SystemDefaultLanguageIsNamedInTheCurrentLanguage()
        {
            Assert.Equal(SoundBoard.Properties.Resources.LanguageSystemDefault, UiLanguage.SystemDefault.DisplayName);
        }

        [Fact]
        public void ATranslationIsNamedInItsOwnLanguage()
        {
            // What a speaker of the language will be looking for in the menu, capitalized the way Windows shows it.
            Assert.Equal("Español", Localization.Available.Single(language => language.Tag == "es").DisplayName);
            Assert.Equal("English", Localization.Available.Single(language => language.Tag == "en").DisplayName);
        }
    }
}
