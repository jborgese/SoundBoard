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

        /// <summary>
        /// Builds the <see cref="SoundButtonUndoState"/> that <see cref="SoundButton.LoadState"/> expects from a model sound.
        /// </summary>
        public static SoundButtonUndoState ToUndoState(this Sound sound) => new SoundButtonUndoState
        {
            SoundPath = sound.Path,
            SoundName = sound.Name,
            Color = sound.Color?.ToMediaColor(),
            VolumeOffset = sound.VolumeOffset,
            Loop = sound.Loop,
            StopAllSounds = sound.StopAllSounds,
            NextSound = sound.NextSoundId,
            Id = sound.Id,
            LocalHotkey = sound.LocalHotkey,
            GlobalHotkey = sound.GlobalHotkey,
        };

        /// <summary>
        /// Captures a button's data as a model sound at the given grid position.
        /// </summary>
        public static Sound ToSound(this SoundButton button) => new Sound
        {
            Id = button.Id,
            Name = button.SoundName ?? string.Empty,
            Path = button.SoundPath ?? string.Empty,
            Color = button.Color?.ToSoundColor(),
            VolumeOffset = button.VolumeOffset,
            Loop = button.Loop,
            StopAllSounds = button.StopAllSounds,
            NextSoundId = button.NextSound,
            LocalHotkey = button.LocalHotkey,
            GlobalHotkey = button.GlobalHotkey,
            Row = button.GetRow(),
            Column = button.GetColumn(),
        };
    }
}
