using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.SimpleChildWindow;
using SoundBoard.Audio;
using SoundBoard.Model;

namespace SoundBoard
{
    /// <summary>
    /// Everything a <see cref="SoundButton"/> (and the dialogs it opens) needs from the window that hosts it.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="MainWindow"/> and handed to each button at construction, replacing the old static
    /// <c>MainWindow.Instance</c> singleton. Grouped by concern; a future stage may split it along those lines.
    /// </remarks>
    internal interface ISoundBoardHost : IUndoable<ConfigUndoState>, IUndoable<TabPageSoundsUndoState>
    {
        #region Model and view lookups

        /// <summary>
        /// Every sound on every live page, in page order then row-major. Includes empty cells.
        /// </summary>
        IEnumerable<Sound> AllSounds();

        /// <summary>
        /// Finds the sound with the given id on any page, or null.
        /// </summary>
        Sound FindSound(string id);

        /// <summary>
        /// Finds the button displaying the given sound, or null.
        /// </summary>
        SoundButton FindButton(Sound sound);

        /// <summary>
        /// All sound buttons, or only those on the given tab.
        /// </summary>
        IEnumerable<SoundButton> GetSoundButtons(MetroTabItem tab = null);

        /// <summary>
        /// The sound-page tabs, in order.
        /// </summary>
        IEnumerable<MyMetroTabItem> SoundTabs { get; }

        /// <summary>
        /// The selected tab, or null.
        /// </summary>
        MetroTabItem SelectedTab { get; }

        /// <summary>
        /// Width of the window, used to fit snackbar text.
        /// </summary>
        double WindowWidth { get; }

        /// <summary>
        /// Resizes the selected page's button grid.
        /// </summary>
        void ChangeButtonGrid(int rowCount, int columnCount);

        /// <summary>
        /// Closes the type-to-search flyout.
        /// </summary>
        void CloseSearch();

        /// <summary>
        /// Stops key presses from being treated as type-to-search, e.g. while a text prompt is open.
        /// </summary>
        void SuspendTypeToSearch();

        /// <summary>
        /// Undoes <see cref="SuspendTypeToSearch"/>.
        /// </summary>
        void ResumeTypeToSearch();

        /// <summary>
        /// True while the hotkey picker is open, during which hotkey presses must not start sounds.
        /// </summary>
        bool IsHotkeyPickerOpen { get; set; }

        #endregion

        #region Playback and hotkeys

        /// <summary>
        /// Tracks every started sound player so they can all be silenced.
        /// </summary>
        PlaybackCoordinator Playback { get; }

        /// <summary>
        /// Registers sounds' hotkeys with the system.
        /// </summary>
        HotkeyRegistry Hotkeys { get; }

        /// <summary>
        /// True while at least one sound anywhere on the board is soloed, in which case every sound that is not soloed is
        /// silenced. Solo is live state and is never saved.
        /// </summary>
        bool IsAnySoundSoloed { get; }

        /// <summary>
        /// Invoked when a sound is soloed or unsoloed, so that every sound on the board can be silenced or let through again.
        /// </summary>
        void OnSoloChanged();

        /// <summary>
        /// Invoked when a sound starts.
        /// </summary>
        void OnAnySoundStarted(SoundButton soundButton);

        /// <summary>
        /// Invoked when a sound stops.
        /// </summary>
        void OnAnySoundStopped(SoundButton soundButton);

        /// <summary>
        /// Invoked when a sound finishes on its own (so that it may chain to its next sound).
        /// </summary>
        void OnSoundFinished(SoundButton soundButton);

        /// <summary>
        /// Invoked when any sound is renamed.
        /// </summary>
        void OnAnySoundRenamed();

        #endregion

        #region Undo

        /// <summary>
        /// Sets the action the undo snackbar's button performs.
        /// </summary>
        void SetUndoAction(Action action);

        /// <summary>
        /// Shows the undo snackbar with the given message.
        /// </summary>
        void ShowUndoSnackbar(string message);

        /// <summary>
        /// The font the snackbar message is drawn in, for fitting text.
        /// </summary>
        Font SnackbarMessageFont { get; }

        #endregion

        #region Dialogs

        /// <summary>
        /// Shows a message dialog over the window.
        /// </summary>
        Task<MessageDialogResult> ShowMessageAsync(string title, string message, MessageDialogStyle style = MessageDialogStyle.Affirmative, MetroDialogSettings settings = null);

        /// <summary>
        /// Shows a text prompt over the window. Returns null if cancelled.
        /// </summary>
        Task<string> ShowInputAsync(string title, string message, MetroDialogSettings settings = null);

        /// <summary>
        /// Shows a child window over the window and waits for it to close.
        /// </summary>
        Task ShowChildWindowAsync(ChildWindow childWindow);

        #endregion
    }
}
