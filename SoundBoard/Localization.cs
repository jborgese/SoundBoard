using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Markup;
using NLog;

namespace SoundBoard
{
    /// <summary>
    /// One language the UI can be shown in.
    /// </summary>
    internal class UiLanguage
    {
        /// <summary>
        /// The "follow the operating system" entry. Its <see cref="Tag"/> is the empty string, which is also what
        /// <see cref="Model.BoardSettings.Language"/> stores for it.
        /// </summary>
        public static readonly UiLanguage SystemDefault = new UiLanguage(string.Empty);

        public UiLanguage(string tag)
        {
            Tag = tag ?? string.Empty;
        }

        /// <summary>
        /// IETF language tag (e.g. <c>"es"</c>), or the empty string for <see cref="SystemDefault"/>.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// How the language names itself ("Espa&#241;ol"), which is what a speaker of it will be looking for in the menu.
        /// The system-default entry uses a localized name instead, since it does not name a language.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Tag.Length == 0) return Properties.Resources.LanguageSystemDefault;

                CultureInfo culture = CultureInfo.GetCultureInfo(Tag);
                string name = culture.NativeName;

                // Several languages, Spanish among them, do not capitalize their own name mid-sentence, so NativeName
                // comes back as "espa&#241;ol". As a menu entry it wants a capital, the way Windows' own language list
                // shows it. Uppercasing in the language's own culture keeps this correct for Turkish, and it is a
                // no-op in scripts that have no case at all.
                return name.Length == 0
                    ? Tag
                    : name.Substring(0, 1).ToUpper(culture) + name.Substring(1);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Tag.Length == 0 ? "(system)" : Tag;
    }

    /// <summary>
    /// The UI language: which ones this build ships, and applying one to the process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Strings live in <c>Properties\Resources.resx</c> (neutral, English) and one <c>Resources.&lt;tag&gt;.resx</c> per
    /// translation, which MSBuild compiles into satellite assemblies. Costura packs those into the single-file exe; see
    /// the <c>EmbedOwnSatelliteAssemblies</c> target in <c>SoundBoard.csproj</c> for why that needs help, and
    /// BUILDING.md "Localization" for how to add a language.
    /// </para>
    /// <para>
    /// The culture is applied once, in <see cref="App.OnStartup"/>, before any XAML is parsed. Changing the language
    /// therefore takes effect on the next start: too much of the UI (cached context menus, the welcome page, tooltips
    /// built with <see cref="string.Format(string, object[])"/>) is built once and kept, so re-applying live would
    /// leave the window half translated. <see cref="MainWindow"/> offers to restart instead.
    /// </para>
    /// </remarks>
    internal static class Localization
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Language tags this build ships a translation for, in menu order. The neutral resources are English, so
        /// "en" needs no satellite assembly and is not listed here.
        /// </summary>
        /// <remarks>
        /// Adding a tag here without adding the matching <c>Resources.&lt;tag&gt;.resx</c> would give that entry a menu
        /// item that silently shows English, so the two are kept in step by <c>LocalizationTests</c>.
        /// </remarks>
        private static readonly string[] TranslatedLanguageTags = { "es" };

        /// <summary>
        /// The language the UI is currently running in: system default, English, or one translation.
        /// Set by <see cref="Apply"/> and not changed again for the life of the process.
        /// </summary>
        public static UiLanguage Current { get; private set; } = UiLanguage.SystemDefault;

        /// <summary>
        /// Every language the menu offers: "system default", English (the neutral resources), then the translations.
        /// </summary>
        public static IReadOnlyList<UiLanguage> Available { get; } =
            new[] { UiLanguage.SystemDefault, new UiLanguage("en") }
                .Concat(TranslatedLanguageTags.Select(tag => new UiLanguage(tag)))
                .ToList();

        /// <summary>
        /// Applies <paramref name="languageTag"/> to this thread and to every thread started from here on.
        /// </summary>
        /// <param name="languageTag">
        /// An IETF language tag, or null/empty to follow the operating system. A tag this machine does not know is
        /// logged and ignored (the OS language is used) rather than failing the start; the setting itself is left
        /// alone so that moving the config to a machine that does know it starts working again.
        /// </param>
        public static void Apply(string languageTag)
        {
            Current = UiLanguage.SystemDefault;

            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
                    CultureInfo culture = CultureInfo.GetCultureInfo(languageTag);

                    Thread.CurrentThread.CurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;

                    Current = new UiLanguage(languageTag);
                }
                catch (CultureNotFoundException ex)
                {
                    Logger.Warn(ex, "Configured language '{0}' is not a culture this machine knows; using the OS language", languageTag);
                }
            }

            Logger.Info("UI language: {0} (configured '{1}', resolved culture {2})",
                Current, languageTag ?? string.Empty, CultureInfo.CurrentUICulture.Name);
        }

        /// <summary>
        /// Looks up a string by resource key. Returns the key itself if there is no such resource, which makes a typo
        /// in XAML show up on screen rather than as an empty control.
        /// </summary>
        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            try
            {
                return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not look up resource '{0}'", key);
                return key;
            }
        }
    }

    /// <summary>
    /// XAML markup extension that resolves a <c>Properties.Resources</c> key: <c>Content="{local:Loc Rename}"</c>.
    /// </summary>
    /// <remarks>
    /// Resolved once, when the XAML is loaded, which is all this app needs: the language is fixed for the lifetime of
    /// the process (see <see cref="Localization"/>). Going through <see cref="Localization.GetString"/> rather than
    /// <c>{x:Static}</c> also keeps the generated <c>Resources</c> class internal.
    /// </remarks>
    [MarkupExtensionReturnType(typeof(string))]
    internal class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        /// <summary>
        /// The resource key, e.g. <c>Rename</c>.
        /// </summary>
        [ConstructorArgument("key")]
        public string Key { get; set; }

        /// <inheritdoc/>
        public override object ProvideValue(IServiceProvider serviceProvider) => Localization.GetString(Key);
    }
}
