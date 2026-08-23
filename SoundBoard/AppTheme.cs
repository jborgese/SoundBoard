using System.Windows;
using ControlzEx.Theming;
using NLog;

namespace SoundBoard
{
    /// <summary>
    /// The color themes SoundBoard offers, and applying one to the running process.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Localization"/>, this can be changed at any time and takes effect immediately: every
    /// MahApps.Metro control already reads its colors through <c>DynamicResource</c> brushes, so swapping the
    /// underlying theme resource dictionary is all that is needed. No restart, and nothing else to rebuild.
    /// </remarks>
    internal static class AppTheme
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The tag <see cref="Model.BoardSettings.Theme"/> stores for the dark theme.
        /// </summary>
        public const string Dark = "Dark";

        /// <summary>
        /// The theme <see cref="Apply"/> last put in place: <see cref="Dark"/>, or the empty string for the light
        /// theme. Null before the first call, i.e. before the app has started up.
        /// </summary>
        public static string Current { get; private set; }

        /// <summary>
        /// Applies <paramref name="theme"/> to the running application: <see cref="Dark"/> for the dark theme, or
        /// anything else (including the empty string) for the default light theme. The accent color (Blue) is not
        /// user-configurable and stays the same either way.
        /// </summary>
        public static void Apply(string theme)
        {
            bool dark = theme == Dark;
            string mahAppsThemeName = dark ? "Dark.Blue" : "Light.Blue";

            ThemeManager.Current.ChangeTheme(Application.Current, mahAppsThemeName);
            Current = dark ? Dark : string.Empty;

            Logger.Info("UI theme: {0}", dark ? Dark : "Light");
        }
    }
}
