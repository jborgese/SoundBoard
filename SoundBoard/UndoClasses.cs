using System.Collections.Generic;
using SoundBoard.Model;

namespace SoundBoard
{
    #region IUndoable interface

    /// <summary>
    /// Defines a class which can save and load its state via a snapshot object
    /// </summary>
    public interface IUndoable<T>
    {
        /// <summary>
        /// Save the current state of the object. The result is an independent copy that later changes cannot affect.
        /// </summary>
        T SaveState();

        /// <summary>
        /// Load a given object state. The snapshot is not consumed; it may be loaded again.
        /// </summary>
        void LoadState(T undoState);
    }

    #endregion

    #region UndoState classes

    /// <summary>
    /// Defines the undo save state for a removed tab page
    /// </summary>
    public class TabPageUndoState
    {
        /// <summary>
        /// Copy of the removed page, or null if the removed tab was not a sound page (the welcome page)
        /// </summary>
        public Page Page { get; set; }

        /// <summary>
        /// Index the tab had in the tab control
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Defines the undo save state for the whole configuration (settings and every page)
    /// </summary>
    public class ConfigUndoState
    {
        /// <summary>
        /// Copy of the configuration
        /// </summary>
        public SoundBoardConfig Config { get; set; }
    }

    /// <summary>
    /// Defines the undo save state for the sounds on a page
    /// </summary>
    public class TabPageSoundsUndoState
    {
        /// <summary>
        /// The tab whose sounds were captured. The restore targets this tab, not whichever tab happens to be selected.
        /// </summary>
        internal MyMetroTabItem Tab { get; set; }

        /// <summary>
        /// Copies of every cell on the page. Each copy carries the row and column it belongs to.
        /// </summary>
        public IReadOnlyList<Sound> Sounds { get; set; }
    }

    #endregion
}
