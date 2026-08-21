using System;
using System.Drawing;

namespace SoundBoard.Model
{
    /// <summary>
    /// An ARGB color, independent of any UI framework.
    /// </summary>
    /// <remarks>
    /// The config file stores colors as <c>#AARRGGBB</c>, which is exactly what WPF's <c>System.Windows.Media.Color.ToString()</c>
    /// produces for sRGB colors. <see cref="ToHtml"/> reproduces that format byte-for-byte, and <see cref="Parse"/> uses the same
    /// <see cref="ColorTranslator.FromHtml"/> the app has always used to read it back.
    /// </remarks>
    public struct SoundColor : IEquatable<SoundColor>
    {
        /// <summary>Alpha</summary>
        public byte A { get; }

        /// <summary>Red</summary>
        public byte R { get; }

        /// <summary>Green</summary>
        public byte G { get; }

        /// <summary>Blue</summary>
        public byte B { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SoundColor(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>
        /// Formats the color as <c>#AARRGGBB</c> (uppercase hex), matching WPF's <c>Color.ToString()</c>.
        /// </summary>
        public string ToHtml() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <inheritdoc/>
        public override string ToString() => ToHtml();

        /// <summary>
        /// Parses a color using <see cref="ColorTranslator.FromHtml"/> semantics (accepts <c>#AARRGGBB</c>, <c>#RRGGBB</c>, <c>#RGB</c> and named colors).
        /// Returns <see langword="null"/> for a null or empty string. Throws for unparseable input, as the legacy reader did.
        /// </summary>
        public static SoundColor? Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            Color drawingColor = ColorTranslator.FromHtml(value);
            return new SoundColor(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        /// <inheritdoc/>
        public bool Equals(SoundColor other) => A == other.A && R == other.R && G == other.G && B == other.B;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SoundColor other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;

        /// <summary>Equality</summary>
        public static bool operator ==(SoundColor left, SoundColor right) => left.Equals(right);

        /// <summary>Inequality</summary>
        public static bool operator !=(SoundColor left, SoundColor right) => !left.Equals(right);
    }
}
