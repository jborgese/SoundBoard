using System.Windows.Media;
using SoundBoard.Model;

namespace SoundBoard
{
    /// <summary>
    /// Conversions between the UI-free model (<see cref="SoundBoard.Model"/>) and the WPF-facing types the UI still uses.
    /// </summary>
    internal static class ModelMapping
    {
        /// <summary>
        /// Converts a model color to a WPF color.
        /// </summary>
        public static Color ToMediaColor(this SoundColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

        /// <summary>
        /// Converts a WPF color to a model color.
        /// </summary>
        public static SoundColor ToSoundColor(this Color color) => new SoundColor(color.A, color.R, color.G, color.B);
    }
}
