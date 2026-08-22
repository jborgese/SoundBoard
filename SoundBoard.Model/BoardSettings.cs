using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundBoard.Model
{
    /// <summary>
    /// Settings that apply to the whole soundboard rather than to one sound.
    /// </summary>
    public class BoardSettings : ObservableObject
    {
        private int _audioPassthroughLatency = DefaultAudioPassthroughLatency;
        private int _newPageDefaultRows = DefaultNewPageRows;
        private int _newPageDefaultColumns = DefaultNewPageColumns;
        private string _language = string.Empty;
        private string _theme = string.Empty;

        /// <summary>
        /// Default number of rows for a new page.
        /// </summary>
        public const int DefaultNewPageRows = 5;

        /// <summary>
        /// Default number of columns for a new page.
        /// </summary>
        public const int DefaultNewPageColumns = 2;

        /// <summary>
        /// Default audio passthrough latency in milliseconds.
        /// </summary>
        public const int DefaultAudioPassthroughLatency = 10;

        /// <summary>
        /// IDs of the audio output devices sounds are played on. Empty means "the default device"
        /// (see <see cref="GetOutputDeviceGuidsOrDefault"/>).
        /// </summary>
        public HashSet<Guid> OutputDevices { get; } = new HashSet<Guid>();

        /// <summary>
        /// IDs of the audio input devices captured for passthrough. The UI only ever selects one.
        /// </summary>
        public HashSet<Guid> InputDevices { get; } = new HashSet<Guid>();

        /// <summary>
        /// IDs of the audio output devices passthrough audio is sent to.
        /// </summary>
        public HashSet<Guid> PassthroughOutputDevices { get; } = new HashSet<Guid>();

        /// <summary>
        /// Latency in milliseconds used when chaining passthrough input to outputs.
        /// </summary>
        public int AudioPassthroughLatency
        {
            get => _audioPassthroughLatency;
            set => SetField(ref _audioPassthroughLatency, value);
        }

        /// <summary>
        /// Number of rows to use for new pages.
        /// </summary>
        public int NewPageDefaultRows
        {
            get => _newPageDefaultRows;
            set => SetField(ref _newPageDefaultRows, value);
        }

        /// <summary>
        /// Number of columns to use for new pages.
        /// </summary>
        public int NewPageDefaultColumns
        {
            get => _newPageDefaultColumns;
            set => SetField(ref _newPageDefaultColumns, value);
        }

        /// <summary>
        /// IETF language tag of the UI language the user picked (e.g. <c>"es"</c>), or the empty string to follow the
        /// operating system. Never null.
        /// </summary>
        /// <remarks>
        /// This is stored as a string rather than a <see cref="System.Globalization.CultureInfo"/> so that a config
        /// naming a culture this build does not ship (or that the machine does not have) round-trips untouched instead
        /// of being silently dropped. The UI resolves it at startup; see the app's <c>Localization</c> class.
        /// </remarks>
        public string Language
        {
            get => _language;
            set => SetField(ref _language, value ?? string.Empty);
        }

        /// <summary>
        /// Tag of the color theme the user picked (e.g. <c>"Dark"</c>), or the empty string for the default (light).
        /// Never null.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Language"/> this is not tied to a restart; see the app's <c>AppTheme</c> class.
        /// </remarks>
        public string Theme
        {
            get => _theme;
            set => SetField(ref _theme, value ?? string.Empty);
        }

        /// <summary>
        /// Copies the scalar settings and device lists of <paramref name="other"/> into this instance. Device lists are
        /// replaced, not merged.
        /// </summary>
        public void CopyFrom(BoardSettings other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));

            AudioPassthroughLatency = other.AudioPassthroughLatency;
            NewPageDefaultRows = other.NewPageDefaultRows;
            NewPageDefaultColumns = other.NewPageDefaultColumns;
            Language = other.Language;
            Theme = other.Theme;

            OutputDevices.Clear();
            OutputDevices.UnionWith(other.OutputDevices);
            InputDevices.Clear();
            InputDevices.UnionWith(other.InputDevices);
            PassthroughOutputDevices.Clear();
            PassthroughOutputDevices.UnionWith(other.PassthroughOutputDevices);
        }

        /// <summary>
        /// The output devices to play on: <see cref="OutputDevices"/>, or <c>[Guid.Empty]</c> (the default device) when none are set.
        /// This substitution is also what gets written to the config file.
        /// </summary>
        public List<Guid> GetOutputDeviceGuidsOrDefault() => OutputDevices.Any() ? OutputDevices.ToList() : new List<Guid> { Guid.Empty };

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public BoardSettings DeepClone()
        {
            var clone = new BoardSettings
            {
                AudioPassthroughLatency = AudioPassthroughLatency,
                NewPageDefaultRows = NewPageDefaultRows,
                NewPageDefaultColumns = NewPageDefaultColumns,
                Language = Language,
                Theme = Theme,
            };

            clone.OutputDevices.UnionWith(OutputDevices);
            clone.InputDevices.UnionWith(InputDevices);
            clone.PassthroughOutputDevices.UnionWith(PassthroughOutputDevices);

            return clone;
        }
    }
}
