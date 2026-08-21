using System.Collections.Generic;
using System.Linq;

namespace SoundBoard.Model
{
    /// <summary>
    /// The root of the soundboard data: global settings plus an ordered list of pages.
    /// This is exactly what <c>soundboard.config</c> stores.
    /// </summary>
    public class SoundBoardConfig
    {
        /// <summary>
        /// Schema version of files that predate the <c>schemaVersion</c> attribute.
        /// </summary>
        public const int LegacySchemaVersion = 1;

        /// <summary>
        /// Schema version written by this build. Bump this (and teach <see cref="ConfigSerializer"/> the difference)
        /// whenever the file format changes in a way a reader needs to detect.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Schema version the config was read with, or <see cref="CurrentSchemaVersion"/> for a new config.
        /// Informational only; the serializer always writes <see cref="CurrentSchemaVersion"/>.
        /// </summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>
        /// Global settings.
        /// </summary>
        public BoardSettings Settings { get; set; } = new BoardSettings();

        /// <summary>
        /// Pages in tab order. Only sound pages — the welcome page is a UI concept and is never persisted.
        /// </summary>
        public List<Page> Pages { get; } = new List<Page>();

        /// <summary>
        /// Every sound on every page, in page order then row-major. Includes empty cells.
        /// </summary>
        public IEnumerable<Sound> AllSounds() => Pages.SelectMany(p => p.Sounds);

        /// <summary>
        /// Finds a sound by <see cref="Sound.Id"/>, or null if there is no such sound (e.g. a dangling <see cref="Sound.NextSoundId"/>).
        /// </summary>
        public Sound FindSound(string id) => string.IsNullOrEmpty(id) ? null : AllSounds().FirstOrDefault(s => s.Id == id);

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public SoundBoardConfig DeepClone()
        {
            var clone = new SoundBoardConfig
            {
                SchemaVersion = SchemaVersion,
                Settings = Settings.DeepClone(),
            };

            clone.Pages.AddRange(Pages.Select(p => p.DeepClone()));

            return clone;
        }
    }
}
