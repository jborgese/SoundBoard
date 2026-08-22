#region Usings

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.Windows.Input;
using Dsafa.WpfColorPicker;
using MahApps.Metro.SimpleChildWindow;
using NAudio.Wave.SampleProviders;
using Timer = System.Timers.Timer;
using ControlPaint = System.Windows.Forms.ControlPaint;
using System.Windows.Media.Animation;
using Humanizer;
using NLog;
using SoundBoard.Audio;
using SoundBoard.Model;

#endregion

namespace SoundBoard
{
    #region MenuButtonBase class

    /// <summary>
    /// Defines a button that can be placed on a sound button to offer additional functionality
    /// </summary>
    internal abstract class MenuButtonBase : Button
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        protected MenuButtonBase()
        {
            FontSize = 13;
            Width = 35;
            Height = 35;
            Margin = new Thickness(0, 15, 15, 15);
            Padding = new Thickness(0.5, 0, 0, 1.5);

            Style = (Style) FindResource(@"MahApps.Styles.Button.Circle");
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        protected MenuButtonBase(SoundButton parentButton) : this()
        {
            ParentButton = parentButton;

            // Default mode is light, unless the parent button specifies otherwise
            Mode = ColorMode.Light;
            if (ParentButton?.SoundButtonStyle?.IsLightColor == false)
            {
                Mode = ColorMode.Dark;
            }

            SetUpStyle();
        }

        #endregion

        #region Protected methods

        /// <summary>
        /// Set the mode of the button
        /// </summary>
        public virtual void SetMode(ColorMode mode = ColorMode.Dark)
        {
            Mode = mode;
            SetUpStyle();
        }

        /// <summary>
        /// Sets up the WPF button style
        /// </summary>
        protected virtual void SetUpStyle()
        {
            Style style = new Style(GetType(), (Style)FindResource(@"MahApps.Styles.Button.Circle"));

            if (Mode == ColorMode.Dark)
            {
                // If we're in dark mode, our button borders should be white.
                style.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(Colors.White)));
            }

            // Apply the style
            Style = style;
        }

        #endregion

        #region Properties

        protected readonly SoundButton ParentButton;

        public ColorMode Mode;

        #endregion

        #region Mode enum

        public enum ColorMode
        {
            /// <summary>
            /// Light mode means dark buttons
            /// </summary>
            Light,

            /// <summary>
            /// Dark mode means light buttons
            /// </summary>
            Dark
        }

        #endregion
    }

    #endregion

    #region HideableMenuButtonBase class

    /// <summary>
    /// Defines a hideable button that can be placed on a sound button to offer additional functionality
    /// </summary>
    internal abstract class HideableMenuButtonBase : MenuButtonBase
    {
        #region Constructor

        protected HideableMenuButtonBase(SoundButton parentButton) : base(parentButton)
        {
            Padding = new Thickness(Padding.Left, Padding.Top + 2, Padding.Right, Padding.Bottom);
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Show the button
        /// </summary>
        public virtual void Show()
        {
            Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hide the button
        /// </summary>
        public virtual void Hide()
        {
            Visibility = Visibility.Hidden;
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Whether or not this button should participate in automatic showing/hiding in relation to sounds playing/stopping
        /// </summary>
        public bool ShowHideAutomatically { get; set; } = true;

        #endregion
    }

    #endregion

    #region MenuButton class

    /// <summary>
    /// Defines a menu button that is placed on a sound button to offer additional functionality
    /// </summary>
    internal sealed class MenuButton : MenuButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        public MenuButton(SoundButton parentButton) : base(parentButton)
        {
            Padding = new Thickness(Padding.Left, Padding.Top + 2, Padding.Right, Padding.Bottom);

            Content = ImageHelper.GetImage(ImageHelper.MenuButtonPath, 15, 15, Mode == ColorMode.Dark);

            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Right;
        }

        #endregion

        #region Overrides

        protected override void OnClick()
        {
            base.OnClick();

            if (ParentButton.ContextMenu is null == false)
            {
                ParentButton.ContextMenu.IsOpen = true;
            }
        }

        public override void SetMode(ColorMode mode = ColorMode.Dark)
        {
            base.SetMode(mode);

            Content = ImageHelper.GetImage(ImageHelper.MenuButtonPath, 15, 15, Mode == ColorMode.Dark);
        }

        #endregion
    }

    #endregion

    #region PlayPauseButton class

    /// <summary>
    /// Defines a menu button that is placed on a sound button to offer play/pause functionality
    /// </summary>
    internal sealed class PlayPauseButton : HideableMenuButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        public PlayPauseButton(SoundButton parentButton) : base(parentButton)
        {
            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Center;
            Margin = new Thickness(0, Margin.Top, Width, Margin.Bottom);

            Visibility = Visibility.Hidden; // Hidden by default
        }

        #endregion

        #region Overrides

        protected override void OnClick()
        {
            base.OnClick();

            if (_playing)
            {
                ParentButton.Pause();
                _playing = false;
                ParentButton.Host.OnAnySoundStopped(ParentButton);
                Content = ImageHelper.GetImage(ImageHelper.PlayButtonPath, 11, 11, Mode == ColorMode.Dark);
            }
            else
            {
                ParentButton.Play();
                _playing = true;
                ParentButton.Host.OnAnySoundStarted(ParentButton);
                Content = ImageHelper.GetImage(ImageHelper.PauseButtonPath, 11, 11, Mode == ColorMode.Dark);
            }
        }

        public override void SetMode(ColorMode mode = ColorMode.Dark)
        {
            base.SetMode(mode);

            if (_playing)
            {
                Content = ImageHelper.GetImage(ImageHelper.PauseButtonPath, 11, 11, mode == ColorMode.Dark);
            }
            else
            {
                Content = ImageHelper.GetImage(ImageHelper.PlayButtonPath, 11, 11, mode == ColorMode.Dark);
            }
        }

        #endregion

        #region Public methods

        public override void Show()
        {
            base.Show();

            Content = ImageHelper.GetImage(ImageHelper.PauseButtonPath, 11, 11, Mode == ColorMode.Dark);
            _playing = true;
        }

        #endregion

        #region Private fields

        private bool _playing = false;

        #endregion
    }

    #endregion

    #region StopButton class

    /// <summary>
    /// Defines a menu button that is placed on a sound button to offer individual silencing (stopping) functionality
    /// </summary>
    internal sealed class StopButton : HideableMenuButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        public StopButton(SoundButton parentButton) : base(parentButton)
        {
            Content = ImageHelper.GetImage(ImageHelper.StopButtonPath, 11, 11, Mode == ColorMode.Dark);

            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Center;
            Margin = new Thickness(Width, Margin.Top, 0, Margin.Bottom);

            Visibility = Visibility.Hidden; // Hidden by default
        }

        #endregion

        #region Overrides

        protected override void OnClick()
        {
            base.OnClick();

            ParentButton.Stop();
        }

        public override void SetMode(ColorMode mode = ColorMode.Dark)
        {
            base.SetMode(mode);

            Content = ImageHelper.GetImage(ImageHelper.StopButtonPath, 11, 11, mode == ColorMode.Dark);
        }

        #endregion
    }

    #endregion

    #region IconButtonBase class

    /// <summary>
    /// Defines an icon "button" which can be used to display an icon
    /// </summary>
    internal abstract class IconButtonBase : HideableMenuButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        protected IconButtonBase(SoundButton parentButton) : base(parentButton)
        {
            ShowHideAutomatically = false;

            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Bottom;

            BorderThickness = new Thickness(0);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Override and handle the left mouse button down.
        /// This essentially makes the button unclickable (without disabling it),
        ///  and prevents the click animation from running
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        #endregion
    }

    #endregion

    #region LoopIconButton class

    internal sealed class LoopIconButton : IconButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        public LoopIconButton(SoundButton parentButton) : base(parentButton)
        {
            Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom + 25);
            ToolTip = Properties.Resources.SoundSetToLoop;

            if (ParentButton.Loop)
            {
                Show();
            }
            else
            {
                Hide();
            }

            SetUpStyle();
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        protected override void SetUpStyle()
        {
            Content = ImageHelper.GetImage(ImageHelper.LoopIconPath, 13, 13, Mode == ColorMode.Dark);
        }

        #endregion
    }

    #endregion

    #region VolumeOffsetIconButton class

    internal sealed class VolumeOffsetIconButton : IconButtonBase
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="parentButton"></param>
        public VolumeOffsetIconButton(SoundButton parentButton) : base(parentButton)
        {
            Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom + 45);
            FontWeight = FontWeights.SemiBold;

            SetUpStyle();
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        protected override void SetUpStyle()
        {
            Update();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Updates the volume offset icon so that it reflects the current value, or hides if there is no offset
        /// </summary>
        public void Update()
        {
            if (ParentButton.VolumeOffset == 0)
            {
                Hide();
            }
            else
            {
                Show();

                Foreground = Mode == ColorMode.Dark
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);

                string volumeOffset = ParentButton.VolumeOffset.ToString(@"+#;-#;0");
                Content = volumeOffset;
                ToolTip = string.Format(Properties.Resources.VolumeOfSoundIsOffset, volumeOffset);
            }
        }

        #endregion
    }

    #endregion

    #region SoundWarningIconButton class

    internal sealed class SoundWarningIconButton : IconButtonBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public SoundWarningIconButton(SoundButton parentButton) : base(parentButton)
        {
            VerticalAlignment = VerticalAlignment.Top;
            FontWeight = FontWeights.SemiBold;
            ToolTipService.SetShowDuration(this, (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
            
            SetUpStyle();
        }

        #region Overrides

        /// <inheritdoc />
        protected override void SetUpStyle()
        {
            Content = ImageHelper.GetImage(ImageHelper.WarningIconPath, 16, 16, Mode == ColorMode.Dark);
            Update();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Updates the warning depending on whether or not an audio track is detected in the current file
        /// </summary>
        public void Update()
        {
            if (string.IsNullOrEmpty(ParentButton.SoundPath))
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                if (!File.Exists(ParentButton.SoundPath))
                {
                    ToolTip = string.Format(Properties.Resources.FileNotFoundWarning, ParentButton.SoundPath);
                    Visibility = Visibility.Visible;
                }
                else
                {
                    try
                    {
                        // Try to instantiate a new reader for this audio file.
                        using (new AudioFileReader(ParentButton.SoundPath))
                        {
                            // If we get here, it's a good sound! Hide the warning.
                            Visibility = Visibility.Collapsed;
                        }

                        // Don't do anything else. Let it get disposed immediately.
                    }
                    catch (Exception ex)
                    {
                        // AudioFileReader will throw an exception if the file doesn't contain audio.
                        // It also throws for missing codecs and corrupt files, which is why we log the actual reason.
                        Logger.Warn(ex, "No audio track (or unreadable file) for '{0}'", ParentButton.SoundPath);
                        ToolTip = string.Format(Properties.Resources.NoAudioTrackWarning, Path.GetFileName(ParentButton.SoundPath));
                        Visibility = Visibility.Visible;
                    }
                }
            }
        }

        #endregion
    }

    internal sealed class HotkeyIndicatorButton : IconButtonBase
    {
        public HotkeyIndicatorButton(SoundButton parentButton) : base(parentButton)
        {
            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Left;
            Padding = new Thickness(Padding.Left + 20, Padding.Top, Padding.Right, Padding.Bottom);

            SetUpStyle();
        }

        protected override void SetUpStyle()
        {
            Content = ImageHelper.GetImage(ImageHelper.KeyboardIconPath, 16, 16, Mode == ColorMode.Dark);
            Update();
        }

        public void Update()
        {
            if (ParentButton.LocalHotkey != null || ParentButton.GlobalHotkey != null)
            {
                Visibility = Visibility.Visible;
                ToolTip = string.Format(Properties.Resources.HotkeyIndicatorToolTip, ParentButton.LocalHotkey?.ToString() ?? Properties.Resources.None, ParentButton.GlobalHotkey?.ToString() ?? Properties.Resources.None);
            }
            else
            {
                Visibility = Visibility.Collapsed;
                ToolTip = default;
            }
        }
    }

    #endregion

    #region StopAllSoundsIconButton class

    internal sealed class StopAllSoundsIconButton : IconButtonBase
    {
        public StopAllSoundsIconButton(SoundButton parentButton) : base(parentButton)
        {
            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Left;
            Padding = new Thickness(Padding.Left + 20, Padding.Top, Padding.Right, Padding.Bottom);
            Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom + 20);
            ToolTip = Properties.Resources.StopAllSoundsIcon;

            SetUpStyle();
        }

        protected override void SetUpStyle()
        {
            Content = ImageHelper.GetImage(ImageHelper.XIconPath, 16, 16, Mode == ColorMode.Dark);
            Update();
        }

        public void Update()
        {
            Visibility = ParentButton.StopAllSounds ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region NextSoundIconButton class

    internal sealed class NextSoundIconButton : IconButtonBase
    {
        public NextSoundIconButton(SoundButton parentButton) : base(parentButton)
        {
            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Left;
            Padding = new Thickness(Padding.Left + 20, Padding.Top, Padding.Right, Padding.Bottom);
            Margin = new Thickness(Margin.Left, Margin.Top, Margin.Right, Margin.Bottom + 40);

            SetUpStyle();
        }

        protected override void SetUpStyle()
        {
            Content = ImageHelper.GetImage(ImageHelper.RightIconPath, 16, 16, Mode == ColorMode.Dark);
            Update();
        }

        public void Update()
        {
            Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(ParentButton.NextSound))
            {
                SoundButton soundButton = ParentButton.Host.FindButton(ParentButton.Host.FindSound(ParentButton.NextSound));
                if (soundButton?.HasValidSound == true)
                {
                    Visibility = Visibility.Visible;

                    ToolTip = soundButton.ParentTab != ParentButton.ParentTab
                        ? string.Format(Properties.Resources.NextSoundTab, soundButton.ParentTab.HeaderText, soundButton.SoundName)
                        : string.Format(Properties.Resources.NextSoundName, soundButton.SoundName);
                }
            }
        }
    }

    #endregion

    #region SoundProgressBar class

    /// <summary>
    /// Defines a ProgressBar control to visually indicate the progress of a playing sound
    /// </summary>
    internal sealed class SoundProgressBar : MetroProgressBar
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public SoundProgressBar()
        {
            Margin = new Thickness(10);
            VerticalAlignment = VerticalAlignment.Bottom;

            // Hide by default
            Visibility = Visibility.Hidden;
        }

        #endregion
    }

    #endregion

    #region SoundButton class

    /// <summary>
    /// Defines a Button which plays a Sound
    /// </summary>
    internal sealed class SoundButton : Button, IUndoable<Sound>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public SoundButton(ISoundBoardHost host,
                           SoundButtonMode soundButtonMode = SoundButtonMode.Normal,
                           MyMetroTabItem parentTab = null,
                           (MetroTabItem SourceTab, SoundButton SourceButton) sourceTabAndButton = default)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Mode = soundButtonMode;
            ParentTab = parentTab;
            SourceTabAndButton = sourceTabAndButton;

            _player = new SoundPlayer();
            _player.Stopped += PlayerStoppedHandler;

            if (soundButtonMode == SoundButtonMode.Normal)
            {
                // A placeholder until the page content builder attaches the real cell (see AttachSound)
                ListenTo(new Sound());
                SetDefaultText();
            }
            else
            {
                // A search result shows (and shares) the source button's sound, so it updates live with the real button.
                // Results are thrown away whenever the search text changes, so stop listening once removed from the tree
                // (otherwise the long-lived sound would keep every past result alive). Page buttons must not do this:
                // they are unloaded whenever their tab is deselected and have to keep tracking their sound meanwhile.
                ListenTo(sourceTabAndButton.SourceButton?.Sound ?? new Sound());
                UpdateContent();
                Unloaded += (_, __) => Detach();
                Loaded += (_, __) => { ListenTo(Sound); UpdateContent(); }; // in case the flyout is hidden and shown again
            }

            FontSize = 20;
            Margin = new Thickness(10);

            // Search results share the source cell's Sound, so they must never be a drop target: a drop would rewrite
            // the real sound without the real button knowing.
            AllowDrop = soundButtonMode == SoundButtonMode.Normal;

            SetUpStyle();
            SetUpContextMenu();
        }

        #endregion

        #region Event handlers

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (ContextMenu?.Items.Contains(_loopMenuItem) == true)
            {
                if (IsSelected)
                {
                    bool anyNotLooped = Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).Any(sb => !sb.Loop);
                    _loopMenuItem.Icon = !anyNotLooped ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
                }
                else
                {
                    _loopMenuItem.Icon = Loop ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
                }
            }

            _loopMenuItem.IsEnabled = !IsPlaying;

            if (ContextMenu?.Items.Contains(_stopAllSoundsMenuItem) == true)
            {
                if (IsSelected)
                {
                    bool anyNotStopped = Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).Any(sb => !sb.StopAllSounds);
                    _stopAllSoundsMenuItem.Icon = !anyNotStopped ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
                }
                else
                {
                    _stopAllSoundsMenuItem.Icon = StopAllSounds ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
                }
            }

            // Verify that NextSound is valid. Only the real button may repair its sound; a search result shares it.
            if (Mode == SoundButtonMode.Normal && !string.IsNullOrEmpty(NextSound) && Host.FindSound(NextSound) is null)
            {
                NextSound = default;
            }

            if (ContextMenu?.Items.Contains(_nextSoundMenuItem) == true)
            {
                _nextSoundMenuItem.Icon = string.IsNullOrEmpty(NextSound) ? null : ImageHelper.GetImage(ImageHelper.CheckIconPath);
            }

            // Make everything visible
            ContextMenu?.Items.OfType<Control>().ToList().ForEach(i => i.Visibility = Visibility.Visible);

            // If there is a multi-selection in progress and this is one of the selected buttons,
            // hide things that are not multi applicable.
            if (IsSelected)
            {
                _chooseSoundMenuItem.Visibility = Visibility.Collapsed;
                _renameMenuItem.Visibility = Visibility.Collapsed;
                _viewSourceMenuItem.Visibility = Visibility.Collapsed;
                _hotkeysMenuItem.Visibility = Visibility.Collapsed;
                _nextSoundMenuItem.Visibility = Visibility.Collapsed;

                ContextMenu?.Items.OfType<Separator>().ToList().ForEach(s => s.Visibility = Visibility.Collapsed);
            }
        }

        private async void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Stop handling keypresses in the main window
            Host.SuspendTypeToSearch();

            string result = await Host.ShowInputAsync(Properties.Resources.Rename,
                Properties.Resources.WhatDoYouWantToCallIt,
                new MetroDialogSettings {DefaultText = SoundName});

            if (!string.IsNullOrEmpty(result))
            {
                SoundName = result;
            }

            // Rehandle keypresses in main window
            Host.ResumeTypeToSearch();
        }

        private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                TabPageSoundsUndoState tabPageSoundsUndoState = ((IUndoable<TabPageSoundsUndoState>)Host).SaveState();

                // Set up our UndoAction
                Host.SetUndoAction(() => { Host.LoadState(tabPageSoundsUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.MultipleSoundsClearedFromTab;
                string truncatedTabName = Utilities.Truncate(ParentTab.HeaderText, Host.SnackbarMessageFont, (int)Width - 50, message);
                Host.ShowUndoSnackbar(string.Format(message, truncatedTabName));

                Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.ClearButton());
            }
            else
            {
                Sound soundUndoState = SaveState();

                // Set up our UndoAction
                Host.SetUndoAction(() => { LoadState(soundUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.SoundWasCleared;
                string truncatedSoundName = Utilities.Truncate(SoundName, Host.SnackbarMessageFont, (int)Host.WindowWidth - 50, message);
                Host.ShowUndoSnackbar(string.Format(message, truncatedSoundName));

                ClearButton();
            }
        }

        private void ChooseSoundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BrowseForSound();
        }

        private void PlayerStoppedHandler(object sender, SoundStoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                // NAudio reports playback failures (device removed, decoder error mid-stream) here rather than throwing.
                // Nothing is shown to the user; the sound just stops.
                Logger.Warn(e.Exception, "Playback of '{0}' ({1}) stopped with an error", SoundName, SoundPath);
            }

            HandleSoundStopped(e.Finished);
        }

        private void GoToSoundMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // First, close the search
            Host.CloseSearch();

            // Now find the button for the given sound

            if (SourceTabAndButton.SourceTab is MetroTabItem metroTabItem && 
                SourceTabAndButton.SourceButton is SoundButton soundButton)
            {
                // Focus the parent tab
                metroTabItem.Focus();

                // Highlight the sound button
                soundButton.Highlight();
            }
        }

        private void ViewSourceMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            _viewSourceMenuItem.Items.Clear();

            MenuItem soundPathMenuItem = new MenuItem();
            soundPathMenuItem.Click += SoundPathMenuItem_Click;

            // Create a textblock to hold the sound path so that we can control truncation
            TextBlock headerTextBlock = new TextBlock
            {
                Text = SoundPath,
                TextWrapping = TextWrapping.Wrap
            };

            soundPathMenuItem.Header = headerTextBlock;

            soundPathMenuItem.IsVisibleChanged += (_, args) =>
            {
                if (args.NewValue as bool? == true) // The menu item is becoming visible
                {
                    if (VisualTreeHelper.GetParent(soundPathMenuItem) is StackPanel stackPanel &&
                        VisualTreeHelper.GetParent(stackPanel) is ItemsPresenter itemsPresenter &&
                        VisualTreeHelper.GetParent(itemsPresenter) is ScrollContentPresenter scrollContentPresenter)
                    {
                        // We've navigated up the visual tree to find the parent with the ACTUAL width
                        void ScrollContentPresenterLoaded(object __, EventArgs ___)
                        {
                            // When it loads, assign the ACTUAL width to the text block so we get proper truncation
                            headerTextBlock.Width = scrollContentPresenter.ActualWidth - 50;
                            scrollContentPresenter.Loaded -= ScrollContentPresenterLoaded;
                        }
                        scrollContentPresenter.Loaded += ScrollContentPresenterLoaded;
                    }
                }
            };

            // Add it to our submenu
            _viewSourceMenuItem.Items.Add(soundPathMenuItem);
        }

        private void SoundPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Open explorer with the current path selected
            Process.Start("explorer.exe", $"/select, \"{SoundPath}\"");
        }

        private void SetColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var palette = Host.GetSoundButtons().Where(sb => sb.Color != null).Select(sb => sb.Color.Value) // Existing colors
                .Concat(ColorPickerDialog.DefaultPalette) // The default palette
                .Distinct(); // Remove dupes

            ColorPickerDialog colorPickerDialog = new ColorPickerDialog(Color ?? Colors.White, palette)
            {
                Width = 584,
                Height = 491,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                ShowTransparencyPicker = false
            };

            if (colorPickerDialog.ShowDialog() == true)
            {
                if (IsSelected)
                {
                    Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.Color = colorPickerDialog.Color);
                }
                else
                {
                    Color = colorPickerDialog.Color;
                }
            }
        }

        private void AdjustVolumeMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            _adjustVolumeMenuItem.Items.Clear();

            // See if this is a multi-selection and if so, whether all selected sounds have the same volume
            int? volume = null;
            bool multiSelectSameVolume = true;
            foreach (var sb in Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected))
            {
                if (volume == null)
                {
                    volume = sb.VolumeOffset;
                }
                else if (volume != sb.VolumeOffset)
                {
                    multiSelectSameVolume = false;
                    break;
                }
            }

            for (int i = -5; i <= 5; ++i)
            {
                string header = i.ToString(@"+#;-#;0");

                MenuItem volumeAdjustmentMenuItem = new MenuItem {Header = header};

                if (i == VolumeOffset && multiSelectSameVolume)
                {
                    volumeAdjustmentMenuItem.Icon = ImageHelper.GetImage(ImageHelper.CheckIconPath);
                }

                if (IsPlaying)
                {
                    volumeAdjustmentMenuItem.IsEnabled = false;
                }

                int offset = i; // Copy i so we're not accessing modified closure
                volumeAdjustmentMenuItem.Click += (_, __) =>
                {
                    if (IsSelected)
                    {
                        Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.VolumeOffset = offset);
                    }
                    else
                    {
                        VolumeOffset = offset;
                    }
                };

                _adjustVolumeMenuItem.Items.Add(volumeAdjustmentMenuItem);
            }
        }

        private void NextSoundMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            _nextSoundMenuItem.Items.Clear();

            List<MyMetroTabItem> tabs = Host.SoundTabs.ToList();
            foreach (MyMetroTabItem metroTabItem in tabs)
            {
                MenuItem tabMenuItem = new MenuItem { Header = metroTabItem.HeaderText.Truncate(50), IsEnabled = false };

                IEnumerable<SoundButton> soundButtons = Host.GetSoundButtons(metroTabItem).Where(sb => sb.HasValidSound).ToList();
                if (soundButtons.Any())
                {
                    _nextSoundMenuItem.Items.Add(tabMenuItem);

                    foreach (SoundButton soundButton in soundButtons)
                    {
                        MenuItem soundButtonMenuItem = new MenuItem
                        {
                            Header = soundButton.SoundName.Truncate(50),
                            ToolTip = soundButton.SoundName,
                            StaysOpenOnClick = true,
                            Tag = soundButton.Id,
                            Icon = NextSound == soundButton.Id ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                            IsEnabled = soundButton != this
                        };

                        soundButtonMenuItem.Click += NextSoundItem_Clicked;

                        _nextSoundMenuItem.Items.Add(soundButtonMenuItem);

                        if (soundButton == soundButtons.Last())
                        {
                            _nextSoundMenuItem.Items.Add(new Separator());
                        }
                    }
                }
            }

            if (_nextSoundMenuItem.Items.OfType<object>().LastOrDefault() is Separator separator)
            {
                _nextSoundMenuItem.Items.Remove(separator);
            }
        }

        private void NextSoundItem_Clicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && Guid.TryParse(menuItem.Tag?.ToString(), out Guid guid))
            {
                string id = guid.ToString();
                NextSound = NextSound == id ? default : id;

                // Recalculate everything
                foreach (MenuItem otherMenuItem in _nextSoundMenuItem.Items.OfType<MenuItem>())
                {
                    if (Guid.TryParse(otherMenuItem.Tag?.ToString(), out Guid otherGuid))
                    {
                        string otherId = otherGuid.ToString();
                        otherMenuItem.Icon = NextSound == otherId ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
                    }
                }

                // Recalculate the main menu item
                _nextSoundMenuItem.Icon = string.IsNullOrEmpty(NextSound) ? null : ImageHelper.GetImage(ImageHelper.CheckIconPath);
            }
        }

        private void LoopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                bool anyNotLooped = Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).Any(sb => !sb.Loop);

                Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.Loop = anyNotLooped);
            }
            else
            {
                Loop = !Loop;
            }
        }

        private void StopAllSoundsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                bool anyNotStopped = Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).Any(sb => !sb.StopAllSounds);

                Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.StopAllSounds = anyNotStopped);
            }
            else
            {
                StopAllSounds = !StopAllSounds;
            }
        }

        private async void HotkeysMenuItemClick(object sender, RoutedEventArgs e)
        {
            Host.IsHotkeyPickerOpen = true;
            HotkeyDialog hotkeyDialog = new HotkeyDialog(this)
            {
                LocalHotkey = LocalHotkey,
                GlobalHotkey = GlobalHotkey
            };
            await Host.ShowChildWindowAsync(hotkeyDialog);
            Host.IsHotkeyPickerOpen = false;
        }

        #endregion

        #region Overrides

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (Mode == SoundButtonMode.Normal && HasValidSound)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    IsSelected = !IsSelected;

                    LastSelected = this;
                }
                else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    IsSelected = true;

                    // We also want to select everything between any prior selected button and this one.

                    // Make sure LastSelected is still in the collection
                    if (LastSelected != null && LastSelected != this && LastSelected.IsSelected)
                    {
                        var buttons = Host.GetSoundButtons(ParentTab).ToList();
                        if (buttons.Contains(LastSelected))
                        {
                            int indexOfThis = buttons.IndexOf(this);
                            int indexOfLastSelected = buttons.IndexOf(LastSelected);
                            for (int i = Math.Min(indexOfThis, indexOfLastSelected); i < Math.Max(indexOfThis, indexOfLastSelected); ++i)
                            {
                                if (buttons[i].HasValidSound)
                                {
                                    buttons[i].IsSelected = true;
                                }
                            }
                        }
                    }

                    LastSelected = this;
                }
            }
        }

        /// <inheritdoc />
        protected override void OnClick()
        {
            base.OnClick();

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Don't play the sound. This will be handled by OnPreviewMouseDown.
            }
            else
            {
                if (string.IsNullOrEmpty(SoundPath))
                {
                    // If this button doesn't have a sound yet, browse for it now. (A search result shares its source
                    // cell's sound, so browsing from it would assign the file behind the real button's back.)
                    if (Mode == SoundButtonMode.Normal)
                    {
                        BrowseForSound();
                    }
                }
                else
                {
                    if (Mode == SoundButtonMode.Normal)
                    {
                        if (IsSelected)
                        {
                            Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.StartSound());
                        }
                        else
                        {
                            StartSound();
                        }
                    }
                    else if (Mode == SoundButtonMode.Search &&
                             SourceTabAndButton.SourceButton is SoundButton sourceButton)
                    {
                        sourceButton.StartSound();
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.ChangedButton == MouseButton.Left)
            {
                _mouseDownPosition = Mouse.GetPosition(this);
            }
        }

        /// <inheritdoc />
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.ChangedButton == MouseButton.Left)
            {
                _mouseDownPosition = null;
            }
        }

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_mouseDownPosition is null == false && 
                Utilities.PointsArePastThreshold((Point)_mouseDownPosition, Mouse.GetPosition(this)) &&
                Mode != SoundButtonMode.Search)
            {
                Host.GetSoundButtons(ParentTab).Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.IsSelected = false);
                
                _mouseDownPosition = Mouse.GetPosition(this);
                DragDrop.DoDragDrop(this, new SoundDragData(this), DragDropEffects.Link);
            }
        }

        /// <inheritdoc />
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseDownPosition = null;
        }

        /// <inheritdoc />
        protected override void OnDragEnter(DragEventArgs e)
        {
            SoundDragData soundDragData = e.Data.GetData(typeof(SoundDragData)) as SoundDragData;

            if (soundDragData is null == false && soundDragData.Source != this)
            {
                e.Effects = DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <inheritdoc />
        protected override void OnDragOver(DragEventArgs e)
        {
            SoundDragData soundDragData = e.Data.GetData(typeof(SoundDragData)) as SoundDragData;

            if (soundDragData is null == false && soundDragData.Source != this)
            {
                e.Effects = DragDropEffects.Link;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Link;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <inheritdoc />
        protected override void OnDrop(DragEventArgs e)
        {
            if (Mode != SoundButtonMode.Normal)
            {
                // See the constructor: search results are not drop targets
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Get the dropped file(s)
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadFiles(files);
            }
            else
            {
                SoundDragData soundDragData = e.Data.GetData(typeof(SoundDragData)) as SoundDragData;
                if (soundDragData is null == false && soundDragData.Source != this)
                {
                    // Set up some placeholders for our source and destination (so we don't lose anything)
                    SoundButton sourceButton = soundDragData.Source;
                    Sound sourceButtonState = sourceButton.SaveState();

                    SoundButton destinationButton = this;
                    Sound destinationButtonState = destinationButton.SaveState();

                    // Make sure neither of the buttons is currently playing anything
                    sourceButton.Stop();
                    destinationButton.Stop();

                    // Release both buttons' hotkeys before either takes the other's Id, so that neither registration
                    // is briefly claimed twice (registrations are keyed on Id)
                    sourceButton.UnregisterLocalHotkey();
                    sourceButton.UnregisterGlobalHotkey();
                    destinationButton.UnregisterLocalHotkey();
                    destinationButton.UnregisterGlobalHotkey();

                    // Do the swap!
                    sourceButton.LoadStateAlreadyUnregistered(destinationButtonState);
                    destinationButton.LoadStateAlreadyUnregistered(sourceButtonState);
                }
            }

            e.Handled = true;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            // For debugging
            return $"{ParentTab?.HeaderText} - {SoundName}";
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Start playing the sound associated with this button
        /// </summary>
        public async void StartSound()
        {
            // Every time the sound is started, update the warning status
            ChildButtons.OfType<SoundWarningIconButton>().FirstOrDefault()?.Update();

            try
            {
                if (!File.Exists(SoundPath))
                {
                    var res = await Host.ShowMessageAsync(Properties.Resources.Error, string.Format(Properties.Resources.FileDoesNotExist, SoundPath),
                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                        {
                            AffirmativeButtonText = Properties.Resources.Browse,
                            NegativeButtonText = Properties.Resources.OK
                        });

                    if (res == MessageDialogResult.Affirmative)
                    {
                        string originalSoundFileName = Path.GetFileName(SoundPath);
                        if (BrowseForSound(initialFileName: originalSoundFileName))
                        {
                            // We selected a sound, check if it's an exact match
                            if (Path.GetFileName(SoundPath) == originalSoundFileName && Path.GetDirectoryName(SoundPath) is string newDirectory)
                            {
                                Dictionary<SoundButton, string> potentialMatches = new Dictionary<SoundButton, string>();

                                // This is a relinking, so check if there are any other missing sounds that can be relinked from this new directory.
                                foreach (SoundButton soundButton in Host.GetSoundButtons())
                                {
                                    originalSoundFileName = Path.GetFileName(soundButton.SoundPath);
                                    string potentialNewSoundPath = Path.Combine(newDirectory, originalSoundFileName);

                                    if (!File.Exists(soundButton.SoundPath) // The sound link is missing
                                        && File.Exists(potentialNewSoundPath)) // The broken sound file is found in this new directory
                                    {
                                        potentialMatches[soundButton] = potentialNewSoundPath;
                                    }
                                }

                                // If we found any matches, tell the user and let them decide
                                if (potentialMatches.Any())
                                {
                                    res = await Host.ShowMessageAsync(Properties.Resources.FixLinksHeader, string.Format(Properties.Resources.FixLinksMessage, potentialMatches.Count),
                                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                                        {
                                            AffirmativeButtonText = Properties.Resources.Yes,
                                            NegativeButtonText = Properties.Resources.No
                                        });

                                    if (res == MessageDialogResult.Affirmative)
                                    {
                                        using (new WaitCursor())
                                        {
                                            foreach (var kvp in potentialMatches)
                                            {
                                                kvp.Key.SetFile(kvp.Value);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return;
                }

                // Stop any previous playback of this button (the player raises Stopped for it, which settles our visuals)
                _player.TearDown();
                Host.Playback.Unregister(_player);

                if (StopAllSounds)
                {
                    Host.Playback.StopAll();
                }

                // Register before starting so that a sound which begins playing can always be silenced, even if Start fails part-way
                Host.Playback.Register(_player);

                // Aaaaand play
                _player.Start(Sound, GlobalSettings.GetOutputDeviceGuids());

                // Show the additional buttons (only now: if Start threw, nothing is playing and they must stay hidden)
                foreach (HideableMenuButtonBase hideableButton in ChildButtons
                    .OfType<HideableMenuButtonBase>()
                    .Where(hideableButton => hideableButton.ShowHideAutomatically))
                {
                    hideableButton.Show();
                }

                CalculateTextMargin();

                Host.OnAnySoundStarted(this);

                // Begin updating progress bar
                _progressBarCancellationToken?.Cancel();
                _progressBarCancellationToken?.Dispose();
                _progressBarCancellationToken = new CancellationTokenSource();
                await UpdateProgressTask(UpdateProgressAction, TimeSpan.FromMilliseconds(5), _progressBarCancellationToken.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start sound '{0}' ({1}) on device(s) [{2}]", SoundName, SoundPath, string.Join(",", GlobalSettings.GetOutputDeviceGuids()));
                await Host.ShowMessageAsync(Properties.Resources.Error,
                    Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
            }
        }

        /// <summary>
        /// Prompt the user to browse for and choose a sound for this button
        /// </summary>
        public bool BrowseForSound(string initialFileName = "")
        {
            // Show file dialog
            OpenFileDialog dialog = new OpenFileDialog
            {
                // Set file type filters
                FileName = initialFileName,
                Filter = $@"{Properties.Resources.AudioVideoFiles}|{Utilities.SupportedAudioFileTypes}|All files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                LoadFiles(dialog.FileNames);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Binds this freshly constructed button to a model cell and shows it. Used when a page's buttons are (re)built
        /// from its <see cref="Model.Page"/>; any hotkeys on the sound are registered, as they are when loading button state.
        /// </summary>
        /// <remarks>
        /// Construct-time only: it does not release registrations for a previously attached sound, so rebinding a live
        /// button would orphan them.
        /// </remarks>
        public void AttachSound(Sound sound)
        {
            if (!Sound.IsEmpty)
            {
                throw new InvalidOperationException("AttachSound may only be called on a button that has not been given a sound yet.");
            }

            ListenTo(sound ?? throw new ArgumentNullException(nameof(sound)));

            RefreshFromSound();

            if (!Sound.IsEmpty)
            {
                // Swallow registration failures here for the same reason as in LoadState: this runs during config load,
                // which cannot show a dialog. The hotkey stays assigned (and saved) so the next launch reports it properly.
                try
                {
                    ReregisterLocalHotkey();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to register local hotkey {0} for sound '{1}' while attaching", LocalHotkey, SoundName);
                }

                try
                {
                    ReregisterGlobalHotkey();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to register global hotkey {0} for sound '{1}' while attaching", GlobalHotkey, SoundName);
                }
            }
        }

        /// <summary>
        /// Removes the file associated with this button
        /// </summary>
        public void ClearButton()
        {
            Stop();
            Color = null;
            SetFile(string.Empty);
        }

        /// <summary>
        /// Set the sound file associated with this button
        /// </summary>
        /// <param name="soundPath"></param>
        /// <param name="soundName"></param>
        public void SetFile(string soundPath, string soundName = "")
        {
            if (string.IsNullOrEmpty(soundPath))
            {
                SetDefaultText();
                return;
            }

            // The visuals (content, style, context menu, warning icon) follow from the property changes
            SoundPath = soundPath;

            SoundName = string.IsNullOrEmpty(soundName)
                ? Path.GetFileNameWithoutExtension(soundPath).Replace(@"_", "")
                : soundName.Replace(@"_", "");

            // Re-check the file even if the path did not change (no event then): the user may have dropped the same file
            // again after fixing it, and expects a stale warning to clear
            ChildButtons.OfType<SoundWarningIconButton>().FirstOrDefault()?.Update();
        }

        /// <summary>
        /// Resumes the sound
        /// </summary>
        public void Play() => _player.Resume();

        /// <summary>
        /// Pauses the sound
        /// </summary>
        public void Pause() => _player.Pause();

        /// <summary>
        /// Stops the sound
        /// </summary>
        public void Stop() => _player.Stop();

        /// <summary>
        /// Temporarily highlights the button to draw the user's attention to it
        /// </summary>
        public async void Highlight()
        {
            // Change our highlight color to dark gray and
            Resources[@"HighlightColor"] = new SolidColorBrush(Colors.DarkGray);
            Style = (Style) FindResource(@"MyHighlightedSquareButtonStyle");

            // Leave the button highlighted for a second so that the user clearly sees it
            await Task.Delay(ONE_SECOND);

            // ----- Do a homebrew animation using a timer ----- //

            Timer animationTimer = new Timer { Interval = ANIMATION_TIMER_INTERVAL };

            // Find our starting value for R (since it's gray, it will be the same for G and B).
            byte val = ((SolidColorBrush) Resources[@"HighlightColor"]).Color.R;

            // Hook up our timer. Use a local function so that we can unsubscribe by name
            animationTimer.Elapsed += timer_Elapsed;

            void timer_Elapsed(object sender, EventArgs e)
            {
                // Unsubscribe right away so that we don't get double hits
                animationTimer.Elapsed -= timer_Elapsed;

                // Check if we've reached our destination color. If so, stop the timer
                if (val >= byte.MaxValue)
                {
                    animationTimer.Stop();
                    animationTimer.Dispose();
                    return;
                }
                
                // Update the color (on the main thread)
                this.Invoke(() =>
                {
                    val = (byte)Math.Min(byte.MaxValue, val + 2); // Make sure we don't go over our target
                    SolidColorBrush adjustedColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(byte.MaxValue, val, val, val));
                    Resources[@"HighlightColor"] = adjustedColor;
                });

                // Subscribe again
                animationTimer.Elapsed += timer_Elapsed;
            }

            // Start our timer
            animationTimer.Start();

            // When our animation is completely done, reset our style
            animationTimer.Disposed += (_, __) =>
            {
                // Remember to update the style on the main thread
                this.Invoke(SetUpStyle);
            };
        }

        /// <summary>
        /// Returns the row
        /// </summary>
        /// <returns></returns>
        public int GetRow()
        {
            return Grid.GetRow(this);
        }

        /// <summary>
        /// Returns the column
        /// </summary>
        /// <returns></returns>
        public int GetColumn()
        {
            return Grid.GetColumn(this);
        }

        /// <summary>
        /// Releases the local hotkey registration made for this button's current sound id, if any. Never throws.
        /// </summary>
        public void UnregisterLocalHotkey() => Host.Hotkeys.UnregisterLocal(Sound);

        /// <summary>
        /// Releases the global hotkey registration made for this button's current sound id, if any. Never throws.
        /// </summary>
        public void UnregisterGlobalHotkey() => Host.Hotkeys.UnregisterGlobal(Sound);

        /// <summary>
        /// Registers this button's local hotkey, if it has one. Always wrap this in a try/catch.
        /// </summary>
        public void ReregisterLocalHotkey() => Host.Hotkeys.RegisterLocal(Sound);

        /// <summary>
        /// Registers this button's global hotkey, if it has one. Always wrap this in a try/catch.
        /// </summary>
        public void ReregisterGlobalHotkey() => Host.Hotkeys.RegisterGlobal(Sound);

        public void CalculateTextMargin()
        {
            if (_viewboxPanel != null && _textBlock != null)
            {
                if (AreTransportControlsVisible // We only shrink when playing
                    && _viewboxPanel.ActualHeight - _textBlock.ActualHeight < 50 // There's not enough room to comfortable display everything
                    && _targetViewboxMarginBottom < 30) // We haven't done this yet
                {
                    _textMarginStoryboard.Stop();
                    _textMarginStoryboard.Children.Clear();

                    ThicknessAnimation animation = new ThicknessAnimation
                    {
                        From = new Thickness(30, 0, 30, _viewboxPanel.Margin.Bottom),
                        To = new Thickness(30, 0, 30, 30),
                        Duration = TimeSpan.FromSeconds(.1)
                    };

                    _targetViewboxMarginBottom = 30;

                    Storyboard.SetTarget(animation, _viewboxPanel);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(MarginProperty));
                    _textMarginStoryboard.Children.Add(animation);
                    _textMarginStoryboard.Begin();
                }
                else if (!AreTransportControlsVisible // Always reset when not playing
                         || (_targetViewboxMarginBottom > 0 && ((_viewboxPanel.ActualHeight + _targetViewboxMarginBottom) - _textBlock.ActualHeight) >= 50)) // The bottom margin is set, but the height that it would be without it set is sufficient
                {
                    _textMarginStoryboard.Stop();
                    _textMarginStoryboard.Children.Clear();

                    ThicknessAnimation animation = new ThicknessAnimation
                    {
                        From = new Thickness(30, 0, 30, _viewboxPanel.Margin.Bottom),
                        To = new Thickness(30, 0, 30, 0),
                        Duration = TimeSpan.FromSeconds(.1)
                    };

                    _targetViewboxMarginBottom = 0;

                    Storyboard.SetTarget(animation, _viewboxPanel);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(MarginProperty));
                    _textMarginStoryboard.Children.Add(animation);
                    _textMarginStoryboard.Begin();
                }
            }
        }

        #endregion

        #region Private methods

        private void SetDefaultText()
        {
            // Clear any hotkeys. This has to happen before the model is cleared, because registrations are keyed on Id
            // and Clear() issues a new one.
            UnregisterLocalHotkey();
            UnregisterGlobalHotkey();

            Sound.Clear();

            RefreshFromSound();
            IsSelected = false;
        }

        /// <summary>
        /// Pushes every value in <see cref="Sound"/> through to the visuals (content text, style, icon buttons, context menu).
        /// Used when a sound is first attached; individual changes afterwards arrive through <see cref="Sound_PropertyChanged"/>.
        /// </summary>
        private void RefreshFromSound()
        {
            UpdateContent();

            ChildButtons.OfType<SoundWarningIconButton>().FirstOrDefault()?.Update();
            ChildButtons.OfType<VolumeOffsetIconButton>().FirstOrDefault()?.Update();
            ChildButtons.OfType<StopAllSoundsIconButton>().FirstOrDefault()?.Update();
            ChildButtons.OfType<HotkeyIndicatorButton>().FirstOrDefault()?.Update();
            ChildButtons.OfType<NextSoundIconButton>().FirstOrDefault()?.Update();
            UpdateLoopIcon();

            Host.OnAnySoundRenamed();

            SetUpStyle();
            SetUpContextMenu();
        }

        /// <summary>
        /// Starts listening to <paramref name="sound"/>, after stopping listening to the previous one.
        /// </summary>
        private void ListenTo(Sound sound)
        {
            if (Sound != null)
            {
                Sound.PropertyChanged -= Sound_PropertyChanged;
            }

            Sound = sound;

            if (Sound != null)
            {
                Sound.PropertyChanged += Sound_PropertyChanged;
            }
        }

        /// <summary>
        /// Stops listening to the sound. Call when the button is discarded (its page was rebuilt or removed), so that the
        /// sound — which may live on — no longer keeps this button alive or updates it.
        /// </summary>
        public void Detach()
        {
            if (Sound != null)
            {
                Sound.PropertyChanged -= Sound_PropertyChanged;
            }
        }

        /// <summary>
        /// Reacts to a change in the sound, whoever made it (this button's own setters, an undo, a drag-swap, or — for a
        /// search result — the real button it mirrors).
        /// </summary>
        private void Sound_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Model.Sound.Path):
                    ChildButtons.OfType<SoundWarningIconButton>().FirstOrDefault()?.Update();
                    UpdateContent();
                    SetUpStyle();
                    SetUpContextMenu();
                    break;

                case nameof(Model.Sound.Name):
                    UpdateContent();
                    Host.OnAnySoundRenamed();
                    break;

                case nameof(Model.Sound.Color):
                    SetUpStyle();
                    break;

                case nameof(Model.Sound.VolumeOffset):
                    ChildButtons.OfType<VolumeOffsetIconButton>().FirstOrDefault()?.Update();
                    break;

                case nameof(Model.Sound.Loop):
                    UpdateLoopIcon();
                    break;

                case nameof(Model.Sound.StopAllSounds):
                    ChildButtons.OfType<StopAllSoundsIconButton>().FirstOrDefault()?.Update();
                    break;

                case nameof(Model.Sound.LocalHotkey):
                case nameof(Model.Sound.GlobalHotkey):
                    ChildButtons.OfType<HotkeyIndicatorButton>().FirstOrDefault()?.Update();
                    break;

                case nameof(Model.Sound.NextSoundId):
                    ChildButtons.OfType<NextSoundIconButton>().FirstOrDefault()?.Update();
                    break;
            }
        }

        private void UpdateContent() => SetContent(Sound.IsEmpty ? Properties.Resources.DragASoundHere : SoundName);

        private void UpdateLoopIcon()
        {
            if (Loop)
            {
                ChildButtons.OfType<LoopIconButton>().FirstOrDefault()?.Show();
            }
            else
            {
                ChildButtons.OfType<LoopIconButton>().FirstOrDefault()?.Hide();
            }
        }

        /// <summary>
        /// Returns false as long as there is still processing to perform.
        /// Returns true when progress no longer needs to be updated.
        /// </summary>
        private bool UpdateProgressAction()
        {
            bool result = false;

            if (_player.Duration is TimeSpan duration)
            {
                double maxSeconds = duration.TotalMilliseconds;
                double curSeconds = _player.Elapsed.TotalMilliseconds;

                SoundProgressBar.Visibility = Visibility.Visible;
                SoundProgressBar.Maximum = maxSeconds;
                SoundProgressBar.Value = curSeconds;

                // Hide the progress bar if the sound is done or has been stopped
                if (curSeconds > maxSeconds || _player.Position == 0)
                {
                    if (Loop && !_player.IsAnyOutputStopped)
                    {
                        _player.RestartClock();
                    }
                    else
                    {
                        SoundProgressBar.Visibility = Visibility.Hidden;
                        result = true;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Repeatedly invokes <paramref name="action"/> every <paramref name="interval"/> until it reports that
        /// there is no more progress to update (i.e., it returns <see langword="true"/>) or until <paramref name="token"/> is canceled.
        /// </summary>
        private async Task UpdateProgressTask(Func<bool> action, TimeSpan interval, CancellationToken token)
        {
            bool result = false;

            while (token.IsCancellationRequested == false && result == false)
            {
                result = action();
                await Task.Delay(interval);
            }
        }

        /// <summary>
        /// Sets up the <see cref="System.Windows.Controls.ContextMenu"/> for the current button.
        /// Should be called initially (i.e., in the constructor)
        /// and any time the button's state changes (i.e., when a sound is added/changed)
        /// </summary>
        private void SetUpContextMenu()
        {
            // If we don't have a context menu yet, create one and assign it
            if (ContextMenu is null)
            {
                ContextMenu = new ContextMenu();
            }

            // ----- Initialize our menu items ----- //

            // If the "Choose sound" menu item is null, create it and hook up its handler
            if (_chooseSoundMenuItem is null)
            {
                _chooseSoundMenuItem = new MenuItem {Header = Properties.Resources.ChooseSound};
                _chooseSoundMenuItem.SetSeparator(true);
                _chooseSoundMenuItem.Click += ChooseSoundMenuItem_Click;
            }

            // If the "Rename" menu item is null, create it and hook up its handler
            if (_renameMenuItem is null)
            {
                _renameMenuItem = new MenuItem {Header = Properties.Resources.Rename};
                _renameMenuItem.Click += RenameMenuItem_Click;
            }

            // If the "Clear" menu item is null, create it and hook up its handler
            if (_clearMenuItem is null)
            {
                _clearMenuItem = new MenuItem {Header = Properties.Resources.Clear};
                _clearMenuItem.SetSeparator(true);
                _clearMenuItem.Click += ClearMenuItem_Click;
            }

            if (_setColorMenuItem is null)
            {
                _setColorMenuItem = new MenuItem {Header = Properties.Resources.SetColor};
                _setColorMenuItem.Click += SetColorMenuItem_Click;
            }

            if (_loopMenuItem is null)
            {
                _loopMenuItem = new MenuItem {Header = Properties.Resources.Loop};
                _loopMenuItem.Click += LoopMenuItem_Click;
            }

            if (_stopAllSoundsMenuItem is null)
            {
                _stopAllSoundsMenuItem = new MenuItem { Header = Properties.Resources.StopAllSounds, ToolTip = Properties.Resources.StopAllSoundsToolTip };
                _stopAllSoundsMenuItem.Click += StopAllSoundsMenuItem_Click;
            }

            if (_adjustVolumeMenuItem is null)
            {
                _adjustVolumeMenuItem = new MenuItem {Header = Properties.Resources.AdjustVolume};

                // Add a dummy item so that this item becomes a parent with a sub-menu
                // The real items will be populated every time at run-time in the SubmenuOpened handler
                _adjustVolumeMenuItem.Items.Add(new MenuItem());

                _adjustVolumeMenuItem.SubmenuOpened += AdjustVolumeMenuItem_SubmenuOpened;
            }

            if (_hotkeysMenuItem is null)
            {
                _hotkeysMenuItem = new MenuItem { Header = Properties.Resources.SetHotkeys };
                _hotkeysMenuItem.Click += HotkeysMenuItemClick;
            }

            if (_nextSoundMenuItem is null)
            {
                _nextSoundMenuItem = new MenuItem { Header = Properties.Resources.NextSound };
                _nextSoundMenuItem.Items.Add(new MenuItem()); // Placeholder for submenu
                _nextSoundMenuItem.SetSeparator(true);
                _nextSoundMenuItem.SubmenuOpened += NextSoundMenuItem_SubmenuOpened;
            }

            // If the "Source" menu item is null, create it and hook up its handler
            if (_viewSourceMenuItem is null)
            {
                _viewSourceMenuItem = new MenuItem {Header = Properties.Resources.Source};
                _viewSourceMenuItem.Items.Add(new MenuItem()); // Add a dummy menu item so this item always has a submenu
                _viewSourceMenuItem.SubmenuOpened += ViewSourceMenuItem_SubmenuOpened;
            }

            // If the "Go to sound" menu item is null, create it and hook up its handler
            if (_goToSoundMenuItem is null)
            {
                _goToSoundMenuItem = new MenuItem { Header = Properties.Resources.GoToSound };
                _goToSoundMenuItem.SetSeparator(true);
                _goToSoundMenuItem.Click += GoToSoundMenuItem_Click;
            }

            // ----- Add our menu items to our context menu, depending on our current state ----- //

            if (Mode == SoundButtonMode.Normal)
            {
                // Add our menu items for our Normal mode

                if (ContextMenu.Items.Contains(_chooseSoundMenuItem) == false)
                {
                    ContextMenu.Items.Add(_chooseSoundMenuItem);
                }

                if (HasValidSound)
                {
                    if (ContextMenu.Items.Contains(_renameMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_renameMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_clearMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_clearMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_setColorMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_setColorMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_loopMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_loopMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_stopAllSoundsMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_stopAllSoundsMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_adjustVolumeMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_adjustVolumeMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_hotkeysMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_hotkeysMenuItem);
                    }

                    if (ContextMenu.Items.Contains(_nextSoundMenuItem) == false)
                    {
                        ContextMenu.Items.Add(_nextSoundMenuItem);
                    }
                }
            }
            else if (Mode == SoundButtonMode.Search)
            {
                // Add our  menu items for our Search mode

                if (ContextMenu.Items.Contains(_goToSoundMenuItem) == false)
                {
                    ContextMenu.Items.Add(_goToSoundMenuItem);
                }
            }

            // Add our menu items for either mode
            if (HasValidSound)
            {
                if (ContextMenu.Items.Contains(_viewSourceMenuItem) == false)
                {
                    ContextMenu.Items.Add(_viewSourceMenuItem);
                }
            }
            else
            {
                // Remove some menu items that should not be in the menu if we no longer have a valid sound
                if (ContextMenu.Items.Contains(_renameMenuItem))
                {
                    ContextMenu.Items.Remove(_renameMenuItem);
                }

                if (ContextMenu.Items.Contains(_clearMenuItem))
                {
                    ContextMenu.Items.Remove(_clearMenuItem);
                }

                if (ContextMenu.Items.Contains(_setColorMenuItem))
                {
                    ContextMenu.Items.Remove(_setColorMenuItem);
                }

                if (ContextMenu.Items.Contains(_loopMenuItem))
                {
                    ContextMenu.Items.Remove(_loopMenuItem);
                }

                if (ContextMenu.Items.Contains(_stopAllSoundsMenuItem))
                {
                    ContextMenu.Items.Remove(_stopAllSoundsMenuItem);
                }

                if (ContextMenu.Items.Contains(_adjustVolumeMenuItem))
                {
                    ContextMenu.Items.Remove(_adjustVolumeMenuItem);
                }

                if (ContextMenu.Items.Contains(_viewSourceMenuItem))
                {
                    ContextMenu.Items.Remove(_viewSourceMenuItem);
                }

                if (ContextMenu.Items.Contains(_hotkeysMenuItem))
                {
                    ContextMenu.Items.Remove(_hotkeysMenuItem);
                }

                if (ContextMenu.Items.Contains(_nextSoundMenuItem))
                {
                    ContextMenu.Items.Remove(_nextSoundMenuItem);
                }
            }

            ContextMenu.AddSeparators();

            ContextMenu.Opened -= ContextMenu_Opened; // Unassign before re-assigning so we don't get double assignment
            ContextMenu.Opened += ContextMenu_Opened;
        }

        /// <summary>
        /// Creates and sets a <see cref="Style"/> from the current <see cref="Color"/>.
        /// </summary>
        private void SetUpStyle()
        {
            SoundButtonStyle soundButtonStyle = SoundButtonStyle;

            // Create a new style based on the square button style
            Style style = new Style(GetType(), (Style)FindResource(@"MySquareButtonStyle"));

            // Add the background color
            if (soundButtonStyle.BackgroundColor is Color backgroundColor)
            {
                style.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(backgroundColor)));
            }

            // Add the foreground color
            if (soundButtonStyle.ForegroundColor is Color foregroundColor)
            {
                style.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(foregroundColor)));

                // Add the background hover color
                if (soundButtonStyle.BackgroundHoverColor is Color backgroundHoverColor)
                {
                    Trigger trigger = new Trigger { Property = IsMouseOverProperty, Value = true };
                    trigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(backgroundHoverColor)));
                    trigger.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(foregroundColor)));
                    style.Triggers.Add(trigger);
                }
            }

            // Add clicked colors
            if (soundButtonStyle.BackgroundClickColor is Color backgroundClickColor &&
                soundButtonStyle.ForegroundClickColor is Color foregroundClickColor)
            {
                Trigger trigger = new Trigger { Property = IsPressedProperty, Value = true };
                trigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(backgroundClickColor)));
                trigger.Setters.Add(new Setter(ForegroundProperty, new SolidColorBrush(foregroundClickColor)));
                style.Triggers.Add(trigger);
            }

            // Add focused colors
            if (Mode == SoundButtonMode.Search)
            {
                Trigger focusTrigger = new Trigger { Property = IsFocusedProperty, Value = true };
                focusTrigger.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(5)));
                focusTrigger.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush(Colors.SlateGray)));
                style.Triggers.Add(focusTrigger);
            }

            // Don't show the ugly dotted line around focused elements
            FocusVisualStyle = null;

            // Assign the style!
            Style = style;

            // Restyle the child buttons
            if (soundButtonStyle.IsLightColor is bool isLightColor && isLightColor == false)
            {
                foreach (MenuButtonBase menuButtonBase in ChildButtons)
                {
                    menuButtonBase.SetMode(MenuButtonBase.ColorMode.Dark);
                }
            }
            else
            {
                foreach (MenuButtonBase menuButtonBase in ChildButtons)
                {
                    menuButtonBase.SetMode(MenuButtonBase.ColorMode.Light);
                }
            }
        }

        /// <summary>
        /// Settles the visuals after one of this button's outputs stopped, and chains to the next sound if it finished on its own.
        /// </summary>
        private void HandleSoundStopped(bool finished)
        {
            _progressBarCancellationToken?.Cancel();

            // Hide the additional buttons
            foreach (HideableMenuButtonBase hideableButton in ChildButtons
                .OfType<HideableMenuButtonBase>()
                .Where(hideableButton => hideableButton.ShowHideAutomatically))
            {
                hideableButton.Hide();
            }

            CalculateTextMargin();

            Host.OnAnySoundStopped(this);

            if (finished)
            {
                Host.OnSoundFinished(this);
            }
        }

        private void SetContent(string text)
        {
            if (Mode == SoundButtonMode.Normal)
            {
                _textBlock = new TextBlock
                {
                    Text = text,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

                _viewboxPanel = new ViewboxPanel
                {
                    Margin = new Thickness(30, 0, 30, 0)
                };
                _viewboxPanel.Children.Add(_textBlock);

                Content = _viewboxPanel;
            }
            else if (Mode == SoundButtonMode.Search)
            {
                // Just do straight scaling with no wrapping
                TextBlock textBlock = new TextBlock
                {
                    Text = text,
                    TextAlignment = TextAlignment.Center
                };

                Viewbox viewbox = new Viewbox
                {
                    StretchDirection = StretchDirection.DownOnly
                };
                viewbox.Child = textBlock;

                Content = viewbox;
            }
        }

        private void LoadFiles(params string[] files)
        {
            List<string> multiFileDrop = new List<string>();

            if (files?.Length > 1)
            {
                multiFileDrop.AddRange(files);
            }
            else if (!string.IsNullOrEmpty(files?[0]) && Directory.Exists(files[0]))
            {
                multiFileDrop.AddRange(Directory.GetFiles(files[0]));
            }

            if (multiFileDrop.Any())
            {
                // This is a multi-file drop!

                // Since this is a big operation, make it undoable
                ConfigUndoState configUndoState = ((IUndoable<ConfigUndoState>)Host).SaveState();
                Host.SetUndoAction(() => { Host.LoadState(configUndoState); });

                // Set our grid size to exactly match the number
                int rows = ParentTab.GetRows();
                int columns = ParentTab.GetColumns();
                bool? lastOperation = false; // False means added column, true means added first row, null means added second row
                while (rows * columns < multiFileDrop.Count)
                {
                    if (lastOperation == false)
                    {
                        ++rows;
                        lastOperation = true;
                    }
                    else if (lastOperation == true)
                    {
                        ++rows;
                        lastOperation = null;
                    }
                    else if (lastOperation == null)
                    {
                        ++columns;
                        lastOperation = false;
                    }
                }

                // Get starting index before potentially changing grid, since that recreates all buttons
                var startingIndex = Host.GetSoundButtons(ParentTab).ToList().IndexOf(this);

                if (rows != ParentTab.GetRows() || columns != ParentTab.GetColumns())
                {
                    Host.ChangeButtonGrid(rows, columns);
                }

                // Start populating the buttons
                var buttons = Host.GetSoundButtons(Host.SelectedTab).ToList();
                buttons = buttons.GetRange(startingIndex, buttons.Count - startingIndex).Concat(buttons.GetRange(0, startingIndex)).ToList();
                for (int i = 0; i < multiFileDrop.Count; ++i)
                {
                    buttons[i].Stop();
                    buttons[i].SetFile(multiFileDrop[i]);
                }

                // Finally, make it undoable
                string message = Properties.Resources.MultipleSoundsAdded;
                string truncatedMessage = Utilities.Truncate(message, Host.SnackbarMessageFont, (int)Width - 50);
                Host.ShowUndoSnackbar(truncatedMessage);
            }
            else
            {
                // Only care about the first file
                string file = files?[0];

                if (string.IsNullOrEmpty(file) == false)
                {
                    // Stop any current playback
                    Stop();

                    // Set it
                    SetFile(file);
                }
            }
        }

        #endregion

        #region Private properties

        internal bool HasValidSound => string.IsNullOrEmpty(SoundPath) == false;

        internal SoundButtonStyle SoundButtonStyle
        {
            get
            {
                SoundButtonStyle soundButtonStyle = new SoundButtonStyle();
                soundButtonStyle.BackgroundColor = Color;

                if (HasValidSound == false)
                {
                    // Not a valid sound yet, use a "placeholder" color
                    soundButtonStyle.ForegroundColor = System.Windows.Media.Color.FromRgb(168, 168, 168);
                }
                else
                {
                    // Valid sound; calculate our other colors based on our background color
                    if (soundButtonStyle.BackgroundColor is Color backgroundColor)
                    {
                        bool lightColor = backgroundColor.ToSystemDrawingColor().GetBrightness() > 0.5;
                        soundButtonStyle.ForegroundColor = lightColor ? Colors.Black : Colors.White;

                        if (backgroundColor.IsWhite() || backgroundColor.IsBlack())
                        {
                            // If the background is completely black or white, use the hover color from the built in square button style
                            Style defaultStyle = (Style) FindResource(@"MahApps.Styles.Button.Square");
                            Trigger mouseOverTrigger = defaultStyle.Triggers.OfType<Trigger>().FirstOrDefault(trigger =>
                                trigger.Property == IsMouseOverProperty && trigger.Value as bool? == true);
                            Setter backgroundPropertySetter = mouseOverTrigger?.Setters.OfType<Setter>()
                                .FirstOrDefault(setter => setter.Property == BackgroundProperty);

                            soundButtonStyle.BackgroundHoverColor = backgroundPropertySetter?.Value as Color?;
                        }
                        else
                        {
                            // For normal background colors, pick a hover color that is slightly lighter or slightly darker
                            soundButtonStyle.BackgroundHoverColor = lightColor
                                ? ControlPaint.Light(backgroundColor.ToSystemDrawingColor()).ToSystemWindowsMediaColor()
                                : ControlPaint.Dark(backgroundColor.ToSystemDrawingColor(), 0.1f).ToSystemWindowsMediaColor();
                        }

                        soundButtonStyle.BackgroundClickColor = Colors.Black;
                        soundButtonStyle.ForegroundClickColor = Colors.White;
                        soundButtonStyle.IsLightColor = lightColor;
                    }
                }

                return soundButtonStyle;
            }
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Defines the mode used by the button
        /// </summary>
        public SoundButtonMode Mode { get; }

        /// <summary>
        /// Defines the progress bar used to show the progress of this sound
        /// </summary>
        public SoundProgressBar SoundProgressBar { get; set; } = new SoundProgressBar();

        /// <summary>
        /// The window hosting this button: model/view lookups, playback, hotkeys, undo and dialogs.
        /// </summary>
        public ISoundBoardHost Host { get; }

        /// <summary>
        /// The model cell this button displays and edits. Every data property on this class reads and writes through it.
        /// In <see cref="SoundButtonMode.Normal"/> this is the cell of <see cref="ParentTab"/>'s page at this button's grid
        /// position; in <see cref="SoundButtonMode.Search"/> it is the source button's sound (shared, not copied).
        /// </summary>
        public Sound Sound { get; private set; }

        // The data properties below are plain views of the Sound. The visuals react to changes through
        // Sound.PropertyChanged (see Sound_PropertyChanged), whoever made the change.

        /// <summary>
        /// Defines the path of the underlying sound file
        /// </summary>
        public string SoundPath
        {
            get => Sound.Path;
            private set => Sound.Path = value;
        }

        /// <summary>
        /// Defines the name of the sound file as displayed on the button
        /// </summary>
        public string SoundName
        {
            get => Sound.Name;
            private set => Sound.Name = value;
        }

        /// <summary>
        /// Defines the background color of the button
        /// </summary>
        public Color? Color
        {
            get => Sound.Color?.ToMediaColor();
            private set => Sound.Color = value?.ToSoundColor();
        }

        public int VolumeOffset
        {
            get => Sound.VolumeOffset;
            private set => Sound.VolumeOffset = value;
        }

        public bool Loop
        {
            get => Sound.Loop;
            private set => Sound.Loop = value;
        }

        public bool StopAllSounds
        {
            get => Sound.StopAllSounds;
            set => Sound.StopAllSounds = value;
        }

        public string Id
        {
            get => Sound.Id;
            set => Sound.Id = value;
        }

        public Hotkey LocalHotkey
        {
            get => Sound.LocalHotkey;
            set => Sound.LocalHotkey = value;
        }

        public Hotkey GlobalHotkey
        {
            get => Sound.GlobalHotkey;
            set => Sound.GlobalHotkey = value;
        }

        /// <summary>
        /// Contains a list of child buttons
        /// </summary>
        public ICollection<MenuButtonBase> ChildButtons { get; } = new List<MenuButtonBase>();

        /// <summary>
        /// When in <see cref="SoundButtonMode.Search"/>, this property specifies the underlying <see cref="MetroTabItem"/> and <see cref="SoundButton"/>
        /// that this search result originated from.
        /// </summary>
        public (MetroTabItem SourceTab, SoundButton SourceButton) SourceTabAndButton { get; }

        /// <summary>
        /// Specifies the <see cref="MetroTabItem"/> on which this sound lives. Will be null when in <see cref="SoundButtonMode.Search"/>.
        /// </summary>
        public MyMetroTabItem ParentTab { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;

                if (_isSelected)
                {
                    BorderThickness = new Thickness(5);
                    BorderBrush = new SolidColorBrush(Colors.SlateGray);
                }
                else
                {
                    BorderThickness = new Thickness(2);
                    BorderBrush = new SolidColorBrush(Colors.Black);
                }
            }
        }
        private bool _isSelected;

        public bool IsPlaying => _player.IsPlaying;

        public bool AreTransportControlsVisible => ChildButtons.OfType<HideableMenuButtonBase>().Where(b => b.ShowHideAutomatically).Any(b => b.Visibility == Visibility.Visible);

        public string NextSound
        {
            get => Sound.NextSoundId;
            set => Sound.NextSoundId = value;
        }

        #endregion

        #region Public static properties

        public static SoundButton LastSelected { get; set; }

        #endregion

        #region Private fields

        /// <summary>
        /// The audio engine for this button. Created in the constructor; lives as long as the button.
        /// </summary>
        private readonly SoundPlayer _player;

        private MenuItem _chooseSoundMenuItem;
        private MenuItem _renameMenuItem;
        private MenuItem _clearMenuItem;
        private MenuItem _viewSourceMenuItem;
        private MenuItem _goToSoundMenuItem;
        private MenuItem _setColorMenuItem;
        private MenuItem _adjustVolumeMenuItem;
        private MenuItem _loopMenuItem;
        private MenuItem _stopAllSoundsMenuItem;
        private MenuItem _hotkeysMenuItem;
        private MenuItem _nextSoundMenuItem;

        private Point? _mouseDownPosition;

        private CancellationTokenSource _progressBarCancellationToken;

        // Related to text resizing
        private ViewboxPanel _viewboxPanel;
        private int _targetViewboxMarginBottom;
        private TextBlock _textBlock;
        private readonly Storyboard _textMarginStoryboard = new Storyboard();

        #endregion

        #region Private consts

        private const int ONE_SECOND = 1000; // 1 s in ms

        private const int ANIMATION_TIMER_INTERVAL = 10; // 10 ms


        #endregion

        #region IUndoable members

        /// <summary>
        /// Returns an independent copy of this button's sound.
        /// </summary>
        public Sound SaveState()
        {
            return Sound.DeepClone();
        }

        /// <summary>
        /// Makes this button hold a copy of <paramref name="undoState"/> (keeping its own grid position), registering any
        /// hotkeys it carries. An empty state clears the button.
        /// </summary>
        public void LoadState(Sound undoState)
        {
            // Registrations are keyed on Id, which is about to change
            UnregisterLocalHotkey();
            UnregisterGlobalHotkey();

            LoadStateCore(undoState);
        }

        /// <summary>
        /// Like <see cref="LoadState"/> but without first releasing this button's hotkey registrations — for callers that
        /// have already released them. Needed when two buttons exchange sounds: the second button's current Id is the
        /// first button's new Id, so unregistering by current Id would remove the registration the first one just made.
        /// </summary>
        internal void LoadStateAlreadyUnregistered(Sound undoState) => LoadStateCore(undoState);

        private void LoadStateCore(Sound undoState)
        {
            if (undoState is null) throw new ArgumentNullException(nameof(undoState));

            if (undoState.IsEmpty)
            {
                Sound.Clear();
                RefreshFromSound();
                IsSelected = false;
                return;
            }

            Sound.CopyDataFrom(undoState);
            RefreshFromSound();

            // Swallow registration failures here: this runs during drag/drop swaps, undo, and config load, none of
            // which can show a dialog. The hotkey stays assigned (and saved) so the next launch reports it properly.
            try
            {
                ReregisterLocalHotkey();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to register local hotkey {0} for sound '{1}' while loading button state", LocalHotkey, SoundName);
            }

            try
            {
                ReregisterGlobalHotkey();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to register global hotkey {0} for sound '{1}' while loading button state", GlobalHotkey, SoundName);
            }
        }

        #endregion
    }

    #endregion

    #region SoundButtonMode enum

    /// <summary>
    /// Defines the mode used by an instance of <see cref="SoundButton"/>
    /// </summary>
    internal enum SoundButtonMode
    {
        /// <summary>
        /// The button is used in a normal context
        /// </summary>
        Normal,

        /// <summary>
        /// The button is used as a search result
        /// </summary>
        Search
    }

    #endregion

    #region SoundButtonStyle class

    /// <summary>
    /// Defines a Style applied to a <see cref="SoundButton"/>.
    /// </summary>
    internal class SoundButtonStyle
    {
        /// <summary>
        /// Defines the background color of the SoundButton
        /// </summary>
        public Color? BackgroundColor { get; set; }

        /// <summary>
        /// Defines the foreground color of the SoundButton
        /// </summary>
        public Color? ForegroundColor { get; set; }

        /// <summary>
        /// Defines the background color of the SoundButton when the mouse is over it
        /// </summary>
        public Color? BackgroundHoverColor { get; set; }

        /// <summary>
        /// Defines the background color of the SoundButton when it is clicked
        /// </summary>
        public Color? BackgroundClickColor { get; set; }

        /// <summary>
        /// Defines the foreground color of the SoundButton when it is clicked
        /// </summary>
        public Color? ForegroundClickColor { get; set; }

        /// <summary>
        /// Whether the main color palette of this style is light (e.g., requiring dark foreground)
        /// </summary>
        public bool? IsLightColor { get; set; }
    }

    #endregion

    #region SoundDragData class

    internal class SoundDragData
    {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public SoundDragData(SoundButton source = null)
        {
            Source = source;
        }

        #endregion

        #region Public properties

        /// <summary>
        /// The control from which the drag data originated
        /// </summary>
        public SoundButton Source { get; }

        #endregion
    }

    #endregion
}
