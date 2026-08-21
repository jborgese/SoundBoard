using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundBoard.Model
{
    /// <summary>
    /// A tab of sounds laid out in a fixed grid.
    /// </summary>
    /// <remarks>
    /// Invariant: <see cref="Sounds"/> always holds exactly <see cref="Rows"/> × <see cref="Columns"/> entries in row-major order,
    /// one per cell, with each <see cref="Sound.Row"/>/<see cref="Sound.Column"/> matching its position. Empty cells are
    /// <see cref="Sound"/> instances with <see cref="Sound.IsEmpty"/> set. This ordering is what index-based consumers rely on.
    /// </remarks>
    public class Page : ObservableObject
    {
        private readonly List<Sound> _sounds = new List<Sound>();
        private string _name;
        private int _rows;
        private int _columns;
        private bool _isFocused;

        /// <summary>
        /// Creates a page with the given dimensions, filled with empty cells.
        /// </summary>
        public Page(string name, int rows, int columns)
        {
            if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
            if (columns < 0) throw new ArgumentOutOfRangeException(nameof(columns));

            Name = name ?? string.Empty;
            Rows = rows;
            Columns = columns;

            for (int row = 0; row < rows; ++row)
            {
                for (int column = 0; column < columns; ++column)
                {
                    _sounds.Add(new Sound { Row = row, Column = column });
                }
            }
        }

        /// <summary>
        /// Tab header text. Never null.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value ?? string.Empty);
        }

        /// <summary>
        /// Number of rows in the grid. Change via <see cref="Resize"/>.
        /// </summary>
        public int Rows
        {
            get => _rows;
            private set => SetField(ref _rows, value);
        }

        /// <summary>
        /// Number of columns in the grid. Change via <see cref="Resize"/>.
        /// </summary>
        public int Columns
        {
            get => _columns;
            private set => SetField(ref _columns, value);
        }

        /// <summary>
        /// Whether this page was the selected tab when the config was saved. Exactly one page should be focused, but the
        /// serializer tolerates zero or many.
        /// </summary>
        public bool IsFocused
        {
            get => _isFocused;
            set => SetField(ref _isFocused, value);
        }

        /// <summary>
        /// Every cell, row-major. See the class remarks for the invariant.
        /// </summary>
        public IReadOnlyList<Sound> Sounds => _sounds;

        /// <summary>
        /// The cell at the given position.
        /// </summary>
        public Sound this[int row, int column]
        {
            get
            {
                if (!IsInRange(row, column))
                {
                    throw new ArgumentOutOfRangeException($"({row}, {column}) is outside a {Rows}x{Columns} page.");
                }

                return _sounds[row * Columns + column];
            }
        }

        /// <summary>
        /// True if the position lies within the grid.
        /// </summary>
        public bool IsInRange(int row, int column) => row >= 0 && row < Rows && column >= 0 && column < Columns;

        /// <summary>
        /// Replaces the cell at <paramref name="sound"/>'s <see cref="Sound.Row"/>/<see cref="Sound.Column"/> with <paramref name="sound"/>.
        /// </summary>
        public void Set(Sound sound)
        {
            if (sound is null) throw new ArgumentNullException(nameof(sound));
            if (!IsInRange(sound.Row, sound.Column))
            {
                throw new ArgumentOutOfRangeException($"({sound.Row}, {sound.Column}) is outside a {Rows}x{Columns} page.");
            }

            _sounds[sound.Row * Columns + sound.Column] = sound;
            OnPropertyChanged(nameof(Sounds));
        }

        /// <summary>
        /// Changes the grid dimensions. Sounds that are still in range keep their position and identity; cells that are
        /// newly in range are empty; sounds that fall out of range are dropped and returned.
        /// </summary>
        public IReadOnlyList<Sound> Resize(int rows, int columns)
        {
            if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows));
            if (columns < 0) throw new ArgumentOutOfRangeException(nameof(columns));

            var kept = new Dictionary<(int, int), Sound>();
            var dropped = new List<Sound>();

            foreach (Sound sound in _sounds)
            {
                if (sound.Row < rows && sound.Column < columns)
                {
                    kept[(sound.Row, sound.Column)] = sound;
                }
                else
                {
                    dropped.Add(sound);
                }
            }

            _sounds.Clear();
            Rows = rows;
            Columns = columns;

            for (int row = 0; row < rows; ++row)
            {
                for (int column = 0; column < columns; ++column)
                {
                    _sounds.Add(kept.TryGetValue((row, column), out Sound sound) ? sound : new Sound { Row = row, Column = column });
                }
            }

            OnPropertyChanged(nameof(Sounds));

            return dropped;
        }

        /// <summary>
        /// Finds the cell holding a sound with the given <see cref="Sound.Id"/>, or null.
        /// </summary>
        public Sound FindSound(string id) => string.IsNullOrEmpty(id) ? null : _sounds.FirstOrDefault(s => s.Id == id);

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public Page DeepClone()
        {
            var clone = new Page(Name, Rows, Columns) { IsFocused = IsFocused };

            foreach (Sound sound in _sounds)
            {
                clone.Set(sound.DeepClone());
            }

            return clone;
        }

        /// <summary>
        /// Number of cells that have a sound assigned.
        /// </summary>
        public int SoundCount => _sounds.Count(s => !s.IsEmpty);
    }
}
