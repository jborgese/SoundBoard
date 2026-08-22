#region Usings

using System;
using System.IO;
using System.Text;
using NAudio.Wave;
using System.Windows;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Bluegrams.Application;
using Gma.System.MouseKeyHook;
using MahApps.Metro.SimpleChildWindow;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using Color = System.Windows.Media.Color;
using Timer = System.Timers.Timer;
using ContextMenu = System.Windows.Controls.ContextMenu;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using BondTech.HotKeyManagement.WPF._4;
using System.Windows.Media;
using NLog;
using SoundBoard.Audio;
using SoundBoard.Model;
using Logger = NLog.Logger;
using Page = SoundBoard.Model.Page;

#endregion

namespace SoundBoard
{ 
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : ISoundBoardHost, IUndoable<TabPageUndoState>
    {
        #region P/Invoke stuff

        enum MapType : uint
        {
            MAPVK_VK_TO_VSC = 0x0,
            MAPVK_VSC_TO_VK = 0x1,
            MAPVK_VK_TO_CHAR = 0x2,
            MAPVK_VSC_TO_VK_EX = 0x3,
        }

        [DllImport("user32.dll")]
        static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, MapType uMapType);

        static char GetCharFromKey(Key key)
        {
            char ch = ' ';

            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            byte[] keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            uint scanCode = MapVirtualKey((uint)virtualKey, MapType.MAPVK_VK_TO_VSC);
            StringBuilder stringBuilder = new StringBuilder(2);

            int result = ToUnicode((uint)virtualKey, scanCode, keyboardState, stringBuilder, stringBuilder.Capacity, 0);
            switch (result)
            {
                case -1:
                    break;
                case 0:
                    break;
                case 1:
                    {
                        ch = stringBuilder[0];
                        break;
                    }
                default:
                    {
                        ch = stringBuilder[0];
                        break;
                    }
            }
            return ch;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Set up our event handlers
            AddHandler(KeyDownEvent, KeyDownHandler, true);
            AddHandler(KeyUpEvent, KeyUpHandler, true);
            Closing += FormClosingHandler;

            // Set up timer to automatically save settings on an interval
            Timer timer = new Timer
            {
                Interval = TWO_MINUTES_IN_MILLISECONDS
            };
            timer.Elapsed += (_, __) => this.Invoke(SaveSettings);
            timer.Start();

            RightWindowCommandsOverlayBehavior = WindowCommandsOverlayBehavior.Never;

            LoadSettingsCompat();

            Task.Run(CleanupBackups);

            CreateTabContextMenus();

            CloseSnackbarButton.Content = ImageHelper.GetImage(ImageHelper.CloseButtonPath, 11, 11);

            // Subscribe to any mouse down. We want any interaction with the application to close the snackbar
            _globalMouseEvents = Hook.AppEvents();
            _globalMouseEvents.MouseDown += Global_MouseDown;

            _updateChecker = new MyUpdateChecker(BuildInfo.UpdateManifestUrl)
            {
                Owner = this,
                DownloadIdentifier = "portable"
            };

            HandleAudioPassthroughChange();

            Tabs.SelectionChanged += (_, __) =>
            {
                GetSoundButtons().Where(sb => sb.IsSelected).ToList().ForEach(sb => sb.IsSelected = false);
            };

            Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(e => SizeChanged += e, e => SizeChanged -= e)
                .Throttle(TimeSpan.FromSeconds(.5))
                .ObserveOn(this)
                .Subscribe(args =>
                {
                    GetSoundButtons().ToList().ForEach(sb => sb.CalculateTextMargin());
                });

            // We want to show our warning if the current process is elevated and UAC is enabled on the machine.
            // If we're elevated but UAC is not enabled, then drag-n-drop will probably work fine.
            if (UACHelper.UACHelper.IsElevated && UACHelper.UACHelper.IsUACEnable)
            {
                AdminBorder.Visibility = Visibility.Visible;
            }
            else
            {
                AdminBorder.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override async void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Hotkeys.Attach(new HotKeyManager(this));
            Hotkeys.HotkeyPressed += (_, name) =>
            {
                if (!IsHotkeyPickerOpen)
                {
                    // Resolve the press through the model, then to the button showing that sound
                    Sound sound = AllSounds().FirstOrDefault(s => HotkeyRegistry.IsRegistrationFor(name, s));
                    if (FindButton(sound) is SoundButton soundButton)
                    {
                        soundButton.ParentTab.Focus();
                        soundButton.StartSound();
                    }
                }
            };

            // There is a weird issue where after some changes to the executable (new version, new directory)
            // the first global hotkey registration fails. This only happens during the first launch after the change,
            // and even that time, all subsequent registrations work. Therefore, register a hotkey we don't care about first.
            Hotkeys.RegisterThrowaway();

            // Register the loaded sounds' hotkeys. (The buttons tried this when they were built, but the manager did not
            // exist yet, so nothing was registered.)
            List<Tuple<string, Hotkey>> badHotkeys = new List<Tuple<string, Hotkey>>();
            foreach (Sound sound in AllSounds().ToList())
            {
                if (sound.LocalHotkey != null)
                {
                    try
                    {
                        Hotkeys.RegisterLocal(sound);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to register local hotkey {0} for sound '{1}' on load", sound.LocalHotkey, sound.Name);
                        badHotkeys.Add(new Tuple<string, Hotkey>(sound.Name, sound.LocalHotkey));
                    }
                }

                if (sound.GlobalHotkey != null)
                {
                    try
                    {
                        Hotkeys.RegisterGlobal(sound);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, "Failed to register global hotkey {0} for sound '{1}' on load", sound.GlobalHotkey, sound.Name);
                        badHotkeys.Add(new Tuple<string, Hotkey>(sound.Name, sound.GlobalHotkey));
                    }
                }
            }

            Logger.Info("Hotkey registration on load complete: {0} failed", badHotkeys.Count);

            if (badHotkeys.Any())
            {
                await this.ShowMessageAsync(Properties.Resources.Error, string.Format(Properties.Resources.HotkeyRegistrationFailedOnLoad, string.Join(Environment.NewLine, badHotkeys.Select(tup => string.Format(Properties.Resources.HotkeyAndSoundName, tup.Item2, tup.Item1)))),
                    MessageDialogStyle.Affirmative, new MetroDialogSettings
                    {
                        AffirmativeButtonText = Properties.Resources.OK
                    });
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                GetSoundButtons().ToList().ForEach(sb => sb.IsSelected = false);
            }
        }

        /// <inheritdoc/>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            AdminBorder.BorderBrush = new SolidColorBrush(Colors.LightBlue);
            AdminText.Foreground = new SolidColorBrush(Colors.LightBlue);
        }

        /// <inheritdoc/>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            AdminBorder.BorderBrush = new SolidColorBrush(Colors.LightGray);
            AdminText.Foreground = new SolidColorBrush(Colors.LightGray);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Loads settings with compatibility for legacy config files
        /// </summary>
        private void LoadSettingsCompat()
        {
            ConfigStore.LoadWithLegacyMigration(LegacyConfigFilePath, ConfigFilePath, TempConfigFilePath, LoadSettings, SaveSettings);
        }

        private void LoadSettings()
        {
            LoadSettings(ConfigFilePath);
        }

        /// <summary>
        /// Load settings from the config file and populate the UI.
        /// </summary>
        private void LoadSettings(string configFilePath)
        {
            if (!File.Exists(configFilePath) && Tabs.Items.Count == 1)
            {
                Logger.Info("No config file at {0}; showing welcome page", configFilePath);

                // Populate content for "welcome"
                CreateHelpContent((MyMetroTabItem)Tabs.Items[0]);
                return;
            }

            Logger.Info("Loading config from {0}", configFilePath);

            // If we get here, we can remove the default tab.
            if (Tabs.Items.Count == 1)
            {
                Tabs.Items.RemoveAt(0);
            }

            try
            {
                // Parse the file into the model, then build the UI from it. The current settings are passed as defaults so that
                // a file which predates a setting (e.g. an imported pre-1.9 config) leaves the running value alone.
                var currentSettings = new BoardSettings
                {
                    AudioPassthroughLatency = GlobalSettings.AudioPassthroughLatency,
                    NewPageDefaultRows = GlobalSettings.NewPageDefaultRows,
                    NewPageDefaultColumns = GlobalSettings.NewPageDefaultColumns,
                    Language = GlobalSettings.Language,
                };

                SoundBoardConfig config = ConfigStore.Load(configFilePath, currentSettings);

                if (config.SchemaVersion != SoundBoardConfig.CurrentSchemaVersion)
                {
                    Logger.Info("Config schema version is {0} (current is {1})", config.SchemaVersion, SoundBoardConfig.CurrentSchemaVersion);
                }

                ApplyConfigToUi(config);

                Logger.Info("Loaded config: {0} tab(s), {1} sound(s), output device(s) [{2}], passthrough input [{3}], passthrough output(s) [{4}], latency {5}",
                    Tabs.Items.Count, GetSoundButtons().Count(sb => sb.HasValidSound),
                    string.Join(",", GlobalSettings.GetOutputDeviceGuids()), string.Join(",", GlobalSettings.GetInputDeviceGuids()),
                    string.Join(",", GlobalSettings.GetPassthroughOutputDeviceGuids()), GlobalSettings.AudioPassthroughLatency);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load config from {0}", configFilePath);

                // Immediately back up the config that actually failed to load, which is not necessarily ConfigFilePath
                // (we may have been asked to load a legacy config, an imported file, or an undo state).
                // Backing up is best effort: the file may not exist, may already be the backup file, or may be locked.
                // None of those should replace the error we're actually reporting.
                string backupFilePath = null;
                try
                {
                    if (File.Exists(configFilePath)
                        && string.Equals(Path.GetFullPath(configFilePath), Path.GetFullPath(TempConfigFilePath), StringComparison.OrdinalIgnoreCase) == false)
                    {
                        File.Copy(configFilePath, TempConfigFilePath, overwrite: true);
                        backupFilePath = TempConfigFilePath;
                        Logger.Info("Backed up failing config to {0}", backupFilePath);
                    }
                }
                catch (Exception backupEx)
                {
                    // Swallow. We couldn't back up the config, so we just won't tell the user that we did.
                    Logger.Warn(backupEx, "Could not back up failing config {0} to {1}", configFilePath, TempConfigFilePath);
                }

                // Do better error handling
                Dispatcher.Invoke(async () =>
                {
                    string errorMessage = backupFilePath is null
                        ? Properties.Resources.ConfigLoadErrorNoBackup
                        : string.Format(Properties.Resources.ConfigLoadError, backupFilePath);

                    var res = await this.ShowMessageAsync(Properties.Resources.Error,
                        string.Join(Environment.NewLine, errorMessage, string.Empty, ex.Message),
                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                        {
                            AffirmativeButtonText = Properties.Resources.CopyDetails,
                            NegativeButtonText = Properties.Resources.OK
                        });

                    if (res == MessageDialogResult.Affirmative)
                    {
                        Clipboard.SetText(string.Join(Environment.NewLine, backupFilePath ?? configFilePath, string.Empty, ex.ToString()));
                    }
                });
            }

            ShowHelpIfNoTabs();
        }

        /// <summary>
        /// If there are no tabs (after a load or an undo that restored an empty config), show the help screen
        /// </summary>
        private void ShowHelpIfNoTabs()
        {
            if (Tabs.Items.Count == 0)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(Help);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv?.Invoke();
            }
        }

        private void CreateTabContextMenus()
        {
            // Add context menu to each tab
            foreach (MetroTabItem tab in Tabs.Items)
            {
                if (tab.GetTabContextMenu() is null == false) continue;

                ContextMenu contextMenu = new ContextMenu();

                if (tab.Tag?.ToString() != WELCOME_PAGE_TAG)
                {
                    MenuItem renameMenuItem = new MenuItem {Header = Properties.Resources.Rename};
                    renameMenuItem.Click += RenameMenuItem_Click;
                    contextMenu.Items.Add(renameMenuItem);
                }

                MenuItem removeMenuItem = new MenuItem {Header = Properties.Resources.Remove};
                removeMenuItem.Click += RemoveMenuItem_Click;
                contextMenu.Items.Add(removeMenuItem);

                if (tab.Tag?.ToString() != WELCOME_PAGE_TAG)
                {
                    MenuItem clearAllSoundsMenuItem = new MenuItem { Header = Properties.Resources.ClearAllSounds };
                    clearAllSoundsMenuItem.Click += ClearAllSoundsMenuItem_Click;
                    contextMenu.Items.Add(clearAllSoundsMenuItem);

                    contextMenu.Items.Add(new Separator());

                    MenuItem changeButtonGrid = new MenuItem {Header = Properties.Resources.ChangeButtonGrid};
                    changeButtonGrid.Click += ChangeButtonGridMenuItem_Click;
                    contextMenu.Items.Add(changeButtonGrid);
                }

                // Handle showing the context menu manually (instead of assigning it to the tab's ContextMenu property)
                //  so that we can filter out bubbled events from child controls and only show it when the tab itself is clicked.
                // args.Source shows the real object from which the event originated.
                tab.MouseRightButtonUp += (_, args) =>
                {
                    if (args.Source is MetroTabItem metroTabItem)
                    {
                        metroTabItem.Focus();
                        contextMenu.IsOpen = true;
                    }
                };

                // Because we're managing the tab context menus manually (instead of assigning to the tab's ContextMenu property)
                // we also have to keep track of whether we've created a context menu for this tab yet.
                // This is stored on the tab itself so that it goes away when the tab does.
                tab.SetTabContextMenu(contextMenu);
            }
        }

        /// <summary>
        /// Populates the live settings (<see cref="GlobalSettings.Current"/>) and the tab control from a loaded config.
        /// The config's pages become the tabs' live pages; the config object itself is not retained.
        /// </summary>
        /// <remarks>
        /// Device settings are applied additively (they are never cleared first), which is how every previous release behaved
        /// when loading or importing a config on top of the current one.
        /// </remarks>
        /// <param name="config">The config to apply. Its pages become the tabs' live pages.</param>
        /// <param name="replaceSettings">
        /// True to make the live settings exactly match the config's (used when restoring an undo snapshot);
        /// false to merge device lists additively, as loading and importing have always done.
        /// </param>
        private void ApplyConfigToUi(SoundBoardConfig config, bool replaceSettings = false)
        {
            BoardSettings settings = config.Settings;

            if (replaceSettings)
            {
                GlobalSettings.Current.CopyFrom(settings);
            }
            else
            {
                GlobalSettings.Current.OutputDevices.UnionWith(settings.OutputDevices);
                GlobalSettings.Current.InputDevices.UnionWith(settings.InputDevices);
                GlobalSettings.Current.PassthroughOutputDevices.UnionWith(settings.PassthroughOutputDevices);
                GlobalSettings.Current.AudioPassthroughLatency = settings.AudioPassthroughLatency;
                GlobalSettings.Current.NewPageDefaultRows = settings.NewPageDefaultRows;
                GlobalSettings.Current.NewPageDefaultColumns = settings.NewPageDefaultColumns;
                GlobalSettings.Current.Language = settings.Language;
            }

            // Remove the existing tabs; their buttons are discarded, so stop them listening to sounds that may live on
            GetSoundButtons().ToList().ForEach(button => button.Detach());
            Tabs.Items.Clear();

            TabItem selectedTab = null;

            foreach (Page page in config.Pages)
            {
                MyMetroTabItem tab = new MyMetroTabItem { Page = page };
                Tabs.Items.Add(tab);

                if (page.IsFocused)
                {
                    selectedTab = tab;
                }

                CreatePageContent(tab);
            }

            if (selectedTab is null == false)
            {
                Tabs.SelectedItem = selectedTab;
            }

            CreateTabContextMenus();
        }

        /// <summary>
        /// The live pages, in tab order. Tabs that are not sound pages (the welcome page) are excluded.
        /// </summary>
        public IEnumerable<Page> Pages => Tabs.Items.OfType<MyMetroTabItem>().Select(tab => tab.Page).Where(page => page != null);

        /// <summary>
        /// Every sound on every live page, in page order then row-major. Includes empty cells.
        /// </summary>
        public IEnumerable<Sound> AllSounds() => Pages.SelectMany(page => page.Sounds);

        /// <summary>
        /// Finds the sound with the given <see cref="Sound.Id"/> on any page, or null.
        /// </summary>
        public Sound FindSound(string id) => string.IsNullOrEmpty(id) ? null : Pages.Select(page => page.FindSound(id)).FirstOrDefault(sound => sound != null);

        /// <summary>
        /// Finds the button displaying the given sound, or null.
        /// </summary>
        internal SoundButton FindButton(Sound sound) => sound is null ? null : GetSoundButtons().FirstOrDefault(button => ReferenceEquals(button.Sound, sound));

        /// <summary>
        /// Captures the current state (live settings and pages, in tab order) as a config ready to be written.
        /// The pages in the result are the live objects, not copies.
        /// </summary>
        private SoundBoardConfig BuildConfigFromUi()
        {
            var config = new SoundBoardConfig { Settings = GlobalSettings.Current };

            foreach (MyMetroTabItem tab in Tabs.Items.OfType<MyMetroTabItem>())
            {
                if (tab.Page is Page page)
                {
                    page.IsFocused = tab.IsSelectedItem();
                    config.Pages.Add(page);
                }
            }

            return config;
        }

        /// <summary>
        /// (Re)builds the tab's button grid from its <see cref="MyMetroTabItem.Page"/>, creating a default-sized empty page
        /// first if the tab has none.
        /// </summary>
        private void CreatePageContent(MyMetroTabItem tab)
        {
            if (tab.Page is null)
            {
                tab.Page = new Page(tab.HeaderText, GlobalSettings.NewPageDefaultRows, GlobalSettings.NewPageDefaultColumns);
            }

            Page page = tab.Page;
            Grid parentGrid = new Grid();

            // Add column definitions to the grid
            for (int i = 0; i < page.Columns; ++i)
            {
                ColumnDefinition col = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
                parentGrid.ColumnDefinitions.Add(col);
            }

            // Add row definitions to the grid
            for (int i = 0; i < page.Rows; ++i)
            {
                RowDefinition row = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
                parentGrid.RowDefinitions.Add(row);
            }

            // Add the buttons to the grid
            for (int rowIndex = 0; rowIndex < page.Rows; ++rowIndex)
            {
                for (int columnIndex = 0; columnIndex < page.Columns; ++columnIndex)
                {
                    // Sound button, bound to its model cell. This happens before the child buttons are created, so the
                    // child buttons pick up the sound's state through their own constructors, as they always have.
                    SoundButton soundButton = new SoundButton(this, parentTab: tab);
                    soundButton.AttachSound(page[rowIndex, columnIndex]);

                    Grid.SetColumn(soundButton, columnIndex);
                    Grid.SetRow(soundButton, rowIndex);
                    parentGrid.Children.Add(soundButton);

                    // Menu button
                    MenuButton menuButton = new MenuButton(soundButton);

                    Grid.SetColumn(menuButton, columnIndex);
                    Grid.SetRow(menuButton, rowIndex);
                    parentGrid.Children.Add(menuButton);
                    soundButton.ChildButtons.Add(menuButton);

                    // Play/pause button
                    PlayPauseButton playPauseButton = new PlayPauseButton(soundButton);

                    Grid.SetColumn(playPauseButton, columnIndex);
                    Grid.SetRow(playPauseButton, rowIndex);
                    parentGrid.Children.Add(playPauseButton);
                    soundButton.ChildButtons.Add(playPauseButton);

                    // Stop button
                    StopButton stopButton = new StopButton(soundButton);

                    Grid.SetColumn(stopButton, columnIndex);
                    Grid.SetRow(stopButton, rowIndex);
                    parentGrid.Children.Add(stopButton);
                    soundButton.ChildButtons.Add(stopButton);

                    // Loop icon
                    LoopIconButton loopIconButton = new LoopIconButton(soundButton);

                    Grid.SetColumn(loopIconButton, columnIndex);
                    Grid.SetRow(loopIconButton, rowIndex);
                    parentGrid.Children.Add(loopIconButton);
                    soundButton.ChildButtons.Add(loopIconButton);

                    // Volume offset icon
                    VolumeOffsetIconButton volumeOffsetIconButton = new VolumeOffsetIconButton(soundButton);

                    Grid.SetColumn(volumeOffsetIconButton, columnIndex);
                    Grid.SetRow(volumeOffsetIconButton, rowIndex);
                    parentGrid.Children.Add(volumeOffsetIconButton);
                    soundButton.ChildButtons.Add(volumeOffsetIconButton);

                    // Warning icon
                    SoundWarningIconButton soundWarningIconButton = new SoundWarningIconButton(soundButton);

                    Grid.SetColumn(soundWarningIconButton, columnIndex);
                    Grid.SetRow(soundWarningIconButton, rowIndex);
                    parentGrid.Children.Add(soundWarningIconButton);
                    soundButton.ChildButtons.Add(soundWarningIconButton);

                    // Hotkey indicator
                    HotkeyIndicatorButton hotkeyIndicatorButton = new HotkeyIndicatorButton(soundButton);

                    Grid.SetColumn(hotkeyIndicatorButton, columnIndex);
                    Grid.SetRow(hotkeyIndicatorButton, rowIndex);
                    parentGrid.Children.Add(hotkeyIndicatorButton);
                    soundButton.ChildButtons.Add(hotkeyIndicatorButton);

                    // StopAllSounds icon
                    StopAllSoundsIconButton stopAllSoundsIconButton = new StopAllSoundsIconButton(soundButton);

                    Grid.SetColumn(stopAllSoundsIconButton, columnIndex);
                    Grid.SetRow(stopAllSoundsIconButton, rowIndex);
                    parentGrid.Children.Add(stopAllSoundsIconButton);
                    soundButton.ChildButtons.Add(stopAllSoundsIconButton);

                    // NextSound icon
                    NextSoundIconButton nextSoundIconButton = new NextSoundIconButton(soundButton);

                    Grid.SetColumn(nextSoundIconButton, columnIndex);
                    Grid.SetRow(nextSoundIconButton, rowIndex);
                    parentGrid.Children.Add(nextSoundIconButton);
                    soundButton.ChildButtons.Add(nextSoundIconButton);

                    // Progress bar
                    SoundProgressBar progressBar = new SoundProgressBar();

                    Grid.SetColumn(progressBar, columnIndex);
                    Grid.SetRow(progressBar, rowIndex);
                    parentGrid.Children.Add(progressBar);

                    soundButton.SoundProgressBar = progressBar;
                }
            }

            tab.Content = parentGrid;

            OnAnySoundRenamed();
        }

        private void CreateHelpContent(MyMetroTabItem tab)
        {
            // The welcome page is not a sound page and must never be persisted
            tab.Page = null;
            tab.HeaderText = Properties.Resources.Welcome;

            tab.Tag = WELCOME_PAGE_TAG;

            StackPanel stackPanel = new StackPanel();

            TextBlock text = new TextBlock
            {
                Text = Properties.Resources.WelcomeToSoundBoard.ToUpper(CultureInfo.CurrentUICulture),
                Padding = new Thickness(5),
                FontSize = 25,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardDescription,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.HowDoesItWork.ToUpper(CultureInfo.CurrentUICulture),
                Padding = new Thickness(5),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardExplanation1,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardExplanation2,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardExplanation3,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardExplanation4,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            text = new TextBlock
            {
                Text = Properties.Resources.SoundBoardExplanation5,
                Padding = new Thickness(5),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(text);

            tab.Content = stackPanel;
        }

        private void SaveSettings()
        {
            SaveSettings(ConfigFilePath);
        }

        private void SaveSettings(string configFilePath)
        {
            try
            {
                SaveSettingsCore(configFilePath);
                Logger.Debug("Saved config to {0}", configFilePath);
            }
            catch (Exception ex)
            {
                // Callers (the autosave timer, window close, import/export) decide how to surface this; we just make sure it's recorded.
                Logger.Error(ex, "Failed to save config to {0}", configFilePath);
                throw;
            }
        }

        private void SaveSettingsCore(string configFilePath)
        {
            ConfigStore.Save(configFilePath, BuildConfigFromUi());
        }

        /// <summary>
        /// Returns all <see cref="SoundButton"/>s in the given <see cref="MainWindow"/>.
        /// If parameter <paramref name="metroTabItem"/> is passed, only <see cref="SoundButton"/>s which appear on the given <paramref name="metroTabItem"/> are returned.
        /// </summary>
        internal IEnumerable<SoundButton> GetSoundButtons(MetroTabItem metroTabItem = null)
        {
            foreach (MetroTabItem tab in Tabs.Items.OfType<MetroTabItem>())
            {
                if (metroTabItem is null || tab == metroTabItem)
                {
                    if (tab.Content is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is SoundButton button)
                            {
                                yield return button;
                            }
                        }
                    }
                }
            }
        }

        private async Task ShowAboutBox()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

            var res = await this.ShowMessageAsync(Properties.Resources.AboutSoundBoard,
                Properties.Resources.CreatedByMicahMorrison + Environment.NewLine +
                string.Format(Properties.Resources.VersionNumber, version),
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = Properties.Resources.CheckForUpdates, NegativeButtonText = Properties.Resources.OK });

            if (res == MessageDialogResult.Affirmative)
            {
                _updateChecker.CheckForUpdates(UpdateNotifyMode.Always);
            }
        }

        private void CleanupBackups() => ConfigStore.CleanupBackups(ConfigFilePath);

        #endregion

        #region Event handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _updateChecker.CheckForUpdates(UpdateNotifyMode.Auto);
        }

        private void RenameMenuItem_Click(object sender, EventArgs e)
        {
            // Tab will be focused (because of right-click handler), so just invoke "rename" button
            ButtonAutomationPeer peer = new ButtonAutomationPeer(Rename);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv?.Invoke();
        }

        private void RemoveMenuItem_Click(object sender, EventArgs e)
        {
            // Tab will be focused (because of right-click handler), so just invoke "remove" button
            ButtonAutomationPeer peer = new ButtonAutomationPeer(Remove);
            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            invokeProv?.Invoke();
        }

        private void ClearAllSoundsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem is MyMetroTabItem metroTabItem)
            {
                TabPageSoundsUndoState tabPageSoundsUndoState = (this as IUndoable<TabPageSoundsUndoState>).SaveState();

                // Set up our UndoAction
                SetUndoAction(() => { LoadState(tabPageSoundsUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.AllSoundsClearedFromTab;
                string truncatedTabName = Utilities.Truncate(metroTabItem.HeaderText, SnackbarMessageFont, (int)Width - 50, message);
                ShowUndoSnackbar(string.Format(message, truncatedTabName));

                foreach (SoundButton soundButton in GetSoundButtons(metroTabItem))
                {
                    soundButton.ClearButton();
                }
            }
        }

        private async void ChangeButtonGridMenuItem_Click(object sender, EventArgs e)
        {
            ButtonGridDialog buttonGridDialog = new ButtonGridDialog(SelectedTab.GetRows(),SelectedTab.GetColumns());
            await this.ShowChildWindowAsync(buttonGridDialog);

            if (buttonGridDialog.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                bool proceed = true;

                if (buttonGridDialog.RowCount * buttonGridDialog.ColumnCount > 300)
                {
                    var res = await this.ShowMessageAsync(Properties.Resources.Warning, Properties.Resources.LargeButtonCountWarning, MessageDialogStyle.AffirmativeAndNegative);
                    proceed = res == MessageDialogResult.Affirmative;
                }

                if (proceed)
                {
                    ChangeButtonGrid(buttonGridDialog.RowCount, buttonGridDialog.ColumnCount);
                }
            }
        }

        /// <summary>
        /// Change the button grid
        /// </summary>
        public void ChangeButtonGrid(int rowCount, int columnCount)
        {
            if (!(SelectedTab is MyMetroTabItem tab && tab.Page is Page page))
            {
                Logger.Warn("ChangeButtonGrid called with no sound page selected; ignoring");
                return;
            }

            using (new WaitCursor())
            {
                ConfigUndoState configUndoState = (this as IUndoable<ConfigUndoState>).SaveState();

                // Set up our UndoAction
                SetUndoAction(() => { LoadState(configUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.ButtonLayoutWasChanged;
                string truncatedMessage = Utilities.Truncate(message, SnackbarMessageFont, (int)Width - 50);
                ShowUndoSnackbar(truncatedMessage);

                // The buttons on this tab are about to be recreated, so stop them and release their hotkey registrations
                // (the new buttons register again when they attach to their cells)
                foreach (SoundButton soundButton in GetSoundButtons(tab))
                {
                    soundButton.Stop();
                    soundButton.UnregisterLocalHotkey();
                    soundButton.UnregisterGlobalHotkey();
                    soundButton.Detach();
                }

                // Resize the page and rebuild just this tab from it
                foreach (Sound dropped in page.Resize(rowCount, columnCount).Where(sound => !sound.IsEmpty))
                {
                    Logger.Warn("Dropping sound \"{0}\" at ({1}, {2}): outside the new {3}x{4} grid of page \"{5}\"", dropped.Name, dropped.Row, dropped.Column, rowCount, columnCount, page.Name);
                }

                CreatePageContent(tab);
                SaveSettings();
            }
        }

        private void RoutedKeyDownHandler(object sender, RoutedEventArgs args)
        {
            if (Utilities.AreAnyDialogsVisible() == false)
            { 
                if (args is KeyEventArgs e)
                {
                    Mouse.Capture(null);

                    if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        GetSoundButtons(SelectedTab).Where(sb => sb.HasValidSound).ToList().ForEach(sb => sb.IsSelected = true);
                        return;
                    }

                    char c = GetCharFromKey(e.Key);
                    if (char.IsLetter(c) || char.IsPunctuation(c) || char.IsNumber(c))
                    {
                        _searchString += c;
                    }
                    else if (e.Key == Key.Space)
                    {
                        _searchString += ' ';
                    }
                    else if (e.Key == Key.Back && _searchString.Length > 0)
                    {
                        _searchString = _searchString.Substring(0, _searchString.Length - 1);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        // If the search bar is open, close it
                        if (Search.IsOpen)
                        {
                            CloseSearch();
                        }
                        // If there are any selected sounds, unselect them
                        else if (GetSoundButtons().Any(sb => sb.IsSelected))
                        {
                            GetSoundButtons().ToList().ForEach(sb => sb.IsSelected = false);
                        }
                        // Otherwise, stop any playing sounds
                        else
                        {
                            ButtonAutomationPeer peer = new ButtonAutomationPeer(Silence);
                            IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            invokeProv?.Invoke();
                        }
                        return;
                    }
                    else if (e.Key == Key.Down)
                    {
                        // If the texbox is focused, we want to focus the first button.
                        // Then we'll let Windows handle the navigation and pressing

                        if (Query.IsFocused)
                        {
                            ResultsPanel.Children.OfType<SoundButton>().FirstOrDefault()?.Focus();
                        }

                        return;
                    }
                    else
                    {
                        return;
                    }

                    Query.Text = _searchString;
                    Query.CaretIndex = Query.Text.Length;

                    Search.IsOpen = true;

                    // Get rid of any previous buttons (do reverse iteration to prevent collection modified errors)
                    for (int i = ResultsPanel.Children.Count - 1; i >= 0; --i)
                    {
                        if (ResultsPanel.Children[i] is SoundButton soundButton)
                        {
                            ResultsPanel.Children.Remove(soundButton);
                        }
                    }

                    // Perform search
                    if (string.IsNullOrEmpty(_searchString) == false)
                    {
                        // Query the model, then resolve each hit to the button that shows it
                        string query = _searchString.ToLower();
                        Dictionary<Sound, SoundButton> buttonsBySound = GetSoundButtons().ToDictionary(sb => sb.Sound);

                        foreach (Sound sound in AllSounds())
                        {
                            if (sound.Name.ToLower().Contains(query) && buttonsBySound.TryGetValue(sound, out SoundButton soundButton))
                            {
                                // The search result displays the source button's sound directly
                                SoundButton button = new SoundButton(this, SoundButtonMode.Search, sourceTabAndButton: (soundButton.ParentTab, soundButton));

                                ResultsPanel.Children.Add(button);
                            }
                        }

                        // If we've added at least one button, focus the first one
                        if (ResultsPanel.Children.Count > 0)
                        {
                            Dispatcher.BeginInvoke(new Action(() => ResultsPanel.Children.OfType<SoundButton>().FirstOrDefault()?.Focus()), DispatcherPriority.ApplicationIdle);
                        }
                    }
                }
            }
        }

        private void RoutedKeyUpHandler(object sender, RoutedEventArgs args)
        {
        }

        private void FlyoutCloseHandler(object sender, RoutedEventArgs e)
        {
            _searchString = string.Empty;
        }

        private void silence_Click(object sender, EventArgs e)
        {
            Playback.StopAll();
        }

        private void help_Click(object sender, RoutedEventArgs e)
        {
            MyMetroTabItem tab = new MyMetroTabItem();
            CreateHelpContent(tab);
            Tabs.Items.Add(tab);
            tab.Focus();

            // Make sure the new tab has a context menu
            CreateTabContextMenus();
        }

        private async void about_Click(object sender, RoutedEventArgs e)
        {
            await ShowAboutBox();
        }

        private void overflow_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu overflowMenu = Overflow.ContextMenu;

            if (overflowMenu is null)
            {
                overflowMenu = new ContextMenu();

                MenuItem importConfig = new MenuItem {Header = Properties.Resources.ImportConfiguration};
                importConfig.Click += ImportConfig_Click;

                MenuItem exportConfig = new MenuItem {Header = Properties.Resources.ExportConfiguration};
                exportConfig.Click += ExportConfig_Click;

                MenuItem clearConfig = new MenuItem {Header = Properties.Resources.ClearConfiguration};
                clearConfig.SetSeparator(true);
                clearConfig.Click += ClearConfig_Click;

                _newPageDefaultMenu = new MenuItem
                {
                    Header = Properties.Resources.NewPageDefaultGrid,
                    ToolTip = Properties.Resources.NewPageDefaultGridToolTip
                };
                _newPageDefaultMenu.Click += NewPageDefault_Click;
                _newPageDefaultMenu.SetSeparator(true);

                _audioPassthroughMenu = new MenuItem { Header = Properties.Resources.AudioPassthrough };
                _audioPassthroughMenu.SubmenuOpened += AudioPassthroughMenuOpened;

                _outputDeviceMenu = new MenuItem {Header = Properties.Resources.SoundOutputDevice};
                _outputDeviceMenu.SubmenuOpened += OutputDeviceMenuOpened;
                _outputDeviceMenu.SetSeparator(true);

                MenuItem languageMenu = new MenuItem { Header = Properties.Resources.Language };
                languageMenu.SetSeparator(true);
                PopulateLanguageMenu(languageMenu);

                MenuItem openLogFolder = new MenuItem {Header = Properties.Resources.OpenLogFolder};
                openLogFolder.Click += OpenLogFolder_Click;

                // Add a placeholder menu item so that "Output device" will have a submenu
                // even before we have evaluated the audio devices to add to the menu
                MenuItem placeholder = new MenuItem();
                _outputDeviceMenu.Items.Add(placeholder);

                // Add a placeholder menu item so that "Audio Passthrough" will have a submenu
                // even before we have evaluated the audio devices to add to the menu
                placeholder = new MenuItem();
                _audioPassthroughMenu.Items.Add(placeholder);

                overflowMenu.Items.Add(importConfig);
                overflowMenu.Items.Add(exportConfig);
                overflowMenu.Items.Add(clearConfig);
                overflowMenu.Items.Add(_newPageDefaultMenu);
                overflowMenu.Items.Add(_audioPassthroughMenu);
                overflowMenu.Items.Add(_outputDeviceMenu);
                overflowMenu.Items.Add(languageMenu);
                overflowMenu.Items.Add(openLogFolder);

                overflowMenu.AddSeparators();

                Overflow.ContextMenu = overflowMenu;
            }

            overflowMenu.IsOpen = true;
        }

        private void addPage_Click(object sender, RoutedEventArgs e)
        {
            MyMetroTabItem tab = new MyMetroTabItem {HeaderText = Properties.Resources.NewPage};
            CreatePageContent(tab);
            Tabs.Items.Add(tab);
            tab.Focus();

            // Make sure the new tab has a context menu
            CreateTabContextMenus();
        }

        private async void renamePage_Click(object sender, RoutedEventArgs e)
        {
            RemoveHandler(KeyDownEvent, KeyDownHandler);

            if (Tabs.SelectedItem is MyMetroTabItem tab)
            {
                string result = await this.ShowInputAsync(Properties.Resources.Rename, Properties.Resources.WhatDoYouWantToCallIt,
                    new MetroDialogSettings {DefaultText = tab.HeaderText});

                if (string.IsNullOrEmpty(result) == false)
                {
                    tab.HeaderText = result;
                }
            }

            AddHandler(KeyDownEvent, KeyDownHandler, true);
        }

        private void removePage_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem is MyMetroTabItem metroTabItem)
            {
                TabPageUndoState tabPageUndoState = SaveState();

                // Set up our UndoAction
                SetUndoAction(() => { LoadState(tabPageUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.TabWasRemoved;
                string truncatedTabName = Utilities.Truncate(metroTabItem.HeaderText, SnackbarMessageFont, (int)Width - 50, message);
                ShowUndoSnackbar(string.Format(message, truncatedTabName));

                // Stop all sounds on this page
                foreach (SoundButton soundButton in GetSoundButtons(metroTabItem))
                {
                    soundButton.Stop();
                    soundButton.UnregisterLocalHotkey();
                    soundButton.UnregisterGlobalHotkey();
                    soundButton.Detach();
                }

                // Remove the page
                Tabs.Items.Remove(Tabs.SelectedItem);
            }
        }

        private void FormClosingHandler(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private async void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            // First, make sure our current settings are saved
            SaveSettings();

            // Prompt the user to browse for where the file should be saved.
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = $@"SoundBoardConfiguration-{ConfigStore.DateTimeStamp()}",
                Filter = ConfigFileFilter
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(ConfigFilePath, saveFileDialog.FileName, true);
                    Logger.Info("Exported config to {0}", saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to export config to {0}", saveFileDialog.FileName);
                    await this.ShowMessageAsync(Properties.Resources.Error,
                        Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
                }
            }
        }

        private async void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            // Prompt the user to browse for a config file
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = ConfigFileFilter
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Logger.Info("Importing config from {0}", openFileDialog.FileName);

                try
                {
                    // Stop all sounds
                    foreach (SoundButton soundButton in GetSoundButtons())
                    {
                        soundButton.Stop();
                        soundButton.UnregisterLocalHotkey();
                        soundButton.UnregisterGlobalHotkey();
                    }

                    ConfigUndoState configUndoState = (this as IUndoable<ConfigUndoState>).SaveState();

                    // Set up our UndoAction
                    SetUndoAction(() => { LoadState(configUndoState); });

                    // Create and show a snackbar
                    string message = Properties.Resources.ConfigurationWasImported;
                    string truncatedMessage = Utilities.Truncate(message, SnackbarMessageFont, (int) Width - 50);
                    ShowUndoSnackbar(truncatedMessage);

                    // Load settings with the given file
                    LoadSettings(openFileDialog.FileName);

                    // Immediately save the settings to overwrite our file
                    SaveSettings();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to import config from {0}", openFileDialog.FileName);
                    await this.ShowMessageAsync(Properties.Resources.Error,
                        Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
                }
            }
        }

        private async void ClearConfig_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info("Clearing config");

            try
            {
                // Stop all sounds
                foreach (SoundButton soundButton in GetSoundButtons())
                {
                    soundButton.Stop();
                    soundButton.UnregisterLocalHotkey();
                    soundButton.UnregisterGlobalHotkey();
                }

                ConfigUndoState configUndoState = (this as IUndoable<ConfigUndoState>).SaveState();

                // Set up our UndoAction
                SetUndoAction(() => { LoadState(configUndoState); });

                // Create and show a snackbar
                string message = Properties.Resources.ConfigurationWasCleared;
                string truncatedMessage = Utilities.Truncate(message, SnackbarMessageFont, (int)Width - 50);
                ShowUndoSnackbar(truncatedMessage);

                // Clear the config from the UI by removing all the tabs (detaching their buttons from the sounds), and persist that so the file matches
                GetSoundButtons().ToList().ForEach(button => button.Detach());
                Tabs.Items.Clear();
                SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clear config");
                await this.ShowMessageAsync(Properties.Resources.Error,
                    Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
            }
        }

        private async void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Flush();

                // Select the active log file if it exists, otherwise just open the folder
                if (File.Exists(Log.LogFilePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{Log.LogFilePath}\"");
                }
                else
                {
                    Directory.CreateDirectory(Log.LogDirectory);
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{Log.LogDirectory}\"");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open log folder {0}", Log.LogDirectory);
                await this.ShowMessageAsync(Properties.Resources.Error,
                    Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
            }
        }

        /// <summary>
        /// Fills the Language submenu: one item per language this build ships, plus "same as Windows", with a check
        /// next to whichever one is in effect. The list never changes while the app is running, so unlike the audio
        /// device menus this is built once rather than on every open.
        /// </summary>
        private void PopulateLanguageMenu(MenuItem languageMenu)
        {
            foreach (UiLanguage language in Localization.Available)
            {
                UiLanguage captured = language;

                MenuItem item = new MenuItem
                {
                    Header = language.DisplayName,
                    Tag = language.Tag,
                    Icon = language.Tag == Localization.Current.Tag ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null
                };

                item.Click += (_, __) => LanguageMenuItem_Click(languageMenu, captured);

                languageMenu.Items.Add(item);
            }
        }

        private async void LanguageMenuItem_Click(MenuItem languageMenu, UiLanguage language)
        {
            if (language.Tag == GlobalSettings.Language)
            {
                return;
            }

            Logger.Info("Language setting changed from '{0}' to '{1}'", GlobalSettings.Language, language.Tag);

            GlobalSettings.Language = language.Tag;

            // Persist straight away: if the user says yes to the restart below we are about to exit, and if they say
            // no this is still a global setting that should survive a crash, exactly like the output device is.
            // Awaited so that a failure is reported before the restart prompt rather than on top of it.
            await TrySaveSettingsAsync();

            // Move the check mark, so the menu tells the truth about what has been chosen even before the restart.
            foreach (MenuItem item in languageMenu.Items.OfType<MenuItem>())
            {
                item.Icon = Equals(item.Tag, language.Tag) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null;
            }

            // Everything the language touches (the window's own chrome, cached context menus, the welcome page, every
            // tooltip built with string.Format) is created once and kept, so re-reading the resources now would leave
            // the window half translated. Restarting is the honest way to apply it. See Localization.
            string message = language.Tag.Length == 0
                ? Properties.Resources.LanguageChangeRestartMessageSystemDefault
                : string.Format(Properties.Resources.LanguageChangeRestartMessage, language.DisplayName);

            var result = await this.ShowMessageAsync(Properties.Resources.Language, message,
                MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                {
                    AffirmativeButtonText = Properties.Resources.RestartNow,
                    NegativeButtonText = Properties.Resources.RestartLater
                });

            if (result == MessageDialogResult.Affirmative)
            {
                await RestartAsync();
            }
        }

        /// <summary>
        /// Saves the config, reporting a failure the way the other settings menus do rather than throwing out of an
        /// event handler.
        /// </summary>
        private async Task TrySaveSettingsAsync()
        {
            try
            {
                SaveSettings();
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync(Properties.Resources.Error,
                    Properties.Resources.ThereWasAProblem + Environment.NewLine + Environment.NewLine + ex.Message);
            }
        }

        /// <summary>
        /// Starts a second copy of this executable and closes this one.
        /// </summary>
        /// <remarks>
        /// This one closes through the normal <see cref="Window.Close"/> path, so the config is written and the global
        /// hotkeys are released exactly as on any other exit. The two processes do overlap briefly, and the new one
        /// may lose the race for a global hotkey; it says so on startup as it always does, and the fix is the same as
        /// it has always been -- start SoundBoard again.
        /// </remarks>
        private async Task RestartAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetEntryAssembly()?.Location ?? Process.GetCurrentProcess().MainModule?.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not restart to apply the language change");

                await this.ShowMessageAsync(Properties.Resources.Error, Properties.Resources.RestartFailed);
                return;
            }

            Close();
        }

        private async void NewPageDefault_Click(object sender, RoutedEventArgs e)
        {
            ButtonGridDialog buttonGridDialog = new ButtonGridDialog(GlobalSettings.NewPageDefaultRows, GlobalSettings.NewPageDefaultColumns, Properties.Resources.ChangeDefaultButtonGrid, validate: false);
            await this.ShowChildWindowAsync(buttonGridDialog);

            if (buttonGridDialog.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                GlobalSettings.NewPageDefaultColumns = buttonGridDialog.ColumnCount;
                GlobalSettings.NewPageDefaultRows = buttonGridDialog.RowCount;
            }
        }

        private void AudioPassthroughMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem audioPassthroughMenu)
            {
                // Clear the current items, whether they are the placeholder
                // or the previously evaluated audio devices.
                // We're marking them for removal instead of removing them immediately so that the 
                // menu doesn't resize and decide to close because our mouse is no longer over it.
                // Instead we'll add all the new items, then remove the old ones at the very end.
                // Use Control instead of MenuItem to capture the Separator.
                List<Control> itemsToRemove = audioPassthroughMenu.Items.OfType<Control>().ToList();

                #region Output

                // Create a menu item for each output device
                using (MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator())
                {
                    // Note: We're going in reverse order to preserve the separator and "Close" item at the bottom

                    // Now add the rest
                    foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Reverse())
                    {
                        // If we can't determine the device's guid, we can't select it. (We must not fall back to
                        // Guid.Empty, which means "the default device.") Show it, but disabled, and don't wire up a
                        // handler at all -- being disabled already keeps it out of the input route, but not relying on
                        // that means flipping IsEnabled later can't silently resurrect the default-device aliasing.
                        bool hasGuid = device.TryGetGuid(out Guid deviceGuid);

                        MenuItem menuItem = new MenuItem
                        {
                            Header = string.Format(Properties.Resources.SingleSpecifier, device.FriendlyName),
                            Icon = hasGuid && GlobalSettings.GetPassthroughOutputDeviceGuids().Contains(deviceGuid) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                            StaysOpenOnClick = true,
                            IsEnabled = hasGuid
                        };
                        if (hasGuid)
                        {
                            menuItem.PreviewMouseUp += (_, args) => HandlePassthroughOutputDeviceSelection(deviceGuid, args.ChangedButton);
                        }
                        else
                        {
                            Logger.Warn("Could not parse a guid from audio device ID '{0}' ({1}); it cannot be selected", device.ID, device.FriendlyName);
                        }
                        audioPassthroughMenu.Items.Insert(0, menuItem);
                    }

                    // First, add the default device
                    var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var defaultDeviceMenuItem = new MenuItem
                    {
                        Header = string.Format(Properties.Resources.DefaultDevice, defaultDevice.FriendlyName),
                        Icon = GlobalSettings.GetPassthroughOutputDeviceGuids().Contains(Guid.Empty) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                        StaysOpenOnClick = true
                    };
                    defaultDeviceMenuItem.PreviewMouseUp += (_, args) => HandlePassthroughOutputDeviceSelection(Guid.Empty, args.ChangedButton);
                    audioPassthroughMenu.Items.Insert(0, defaultDeviceMenuItem);
                }

                // Add an item for output heading
                audioPassthroughMenu.Items.Insert(0, new MenuItem { Header = Properties.Resources.OutputDevice, IsEnabled = false });

                audioPassthroughMenu.Items.Insert(0, new Separator());

                #endregion

                #region Input

                // Create a menu item for each output device
                using (MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator())
                {
                    // Note: We're going in reverse order to preserve the separator and "Close" item at the bottom

                    // Now add the rest
                    foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Reverse())
                    {
                        // See the note on the output devices above.
                        bool hasGuid = device.TryGetGuid(out Guid deviceGuid);

                        MenuItem menuItem = new MenuItem
                        {
                            Header = string.Format(Properties.Resources.SingleSpecifier, device.FriendlyName),
                            Icon = hasGuid && GlobalSettings.GetInputDeviceGuids().Contains(deviceGuid) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                            StaysOpenOnClick = true,
                            IsEnabled = hasGuid
                        };
                        if (hasGuid)
                        {
                            menuItem.PreviewMouseUp += (_, args) => HandlePassthroughInputDeviceSelection(deviceGuid);
                        }
                        else
                        {
                            Logger.Warn("Could not parse a guid from audio device ID '{0}' ({1}); it cannot be selected", device.ID, device.FriendlyName);
                        }
                        audioPassthroughMenu.Items.Insert(0, menuItem);
                    }
                }

                // Add an item for input heading
                audioPassthroughMenu.Items.Insert(0, new MenuItem { Header = Properties.Resources.InputDevice, IsEnabled = false });

                #endregion

                // Add close
                MenuItem closeDeviceMenuMenuItem = new MenuItem
                {
                    Header = Properties.Resources.Close
                };
                audioPassthroughMenu.Items.Add(new Separator());
                audioPassthroughMenu.Items.Add(closeDeviceMenuMenuItem);

                // Finally, remove the items marked for removal
                foreach (Control control in itemsToRemove)
                {
                    audioPassthroughMenu.Items.Remove(control);
                }
            }
        }

        private void OutputDeviceMenuOpened(object sender, RoutedEventArgs e)
        {
            // Re-evaluate the audio devices every time this sub-menu is opened
            if (sender is MenuItem outputDeviceMenuItem)
            {
                // Clear the current items, whether they are the placeholder
                // or the previously evaluated audio devices.
                // We're marking them for removal instead of removing them immediately so that the 
                // menu doesn't resize and decide to close because our mouse is no longer over it.
                // Instead we'll add all the new items, then remove the old ones at the very end.
                // Use Control istead of MenuItem to capture the Separator.
                List<Control> itemsToRemove = outputDeviceMenuItem.Items.OfType<Control>().ToList();

                // Create a menu item for each output device
                using (MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator())
                {
                    // Note: We're going in reverse order to preserve the separator and "Close" item at the bottom

                    // Now add the rest
                    foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Reverse())
                    {
                        // See the note on the passthrough output devices above.
                        bool hasGuid = device.TryGetGuid(out Guid deviceGuid);

                        MenuItem menuItem = new MenuItem
                        {
                            Header = string.Format(Properties.Resources.SingleSpecifier, device.FriendlyName),
                            Icon = hasGuid && GlobalSettings.GetOutputDeviceGuids().Contains(deviceGuid) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                            StaysOpenOnClick = true,
                            IsEnabled = hasGuid
                        };
                        if (hasGuid)
                        {
                            menuItem.PreviewMouseUp += (_, args) => HandleOutputDeviceSelection(deviceGuid, args.ChangedButton);
                        }
                        else
                        {
                            Logger.Warn("Could not parse a guid from audio device ID '{0}' ({1}); it cannot be selected", device.ID, device.FriendlyName);
                        }
                        outputDeviceMenuItem.Items.Insert(0, menuItem);
                    }

                    // First, add the default device
                    var defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    var defaultDeviceMenuItem = new MenuItem
                    {
                        Header = string.Format(Properties.Resources.DefaultDevice, defaultDevice.FriendlyName),
                        Icon = GlobalSettings.GetOutputDeviceGuids().Contains(Guid.Empty) ? ImageHelper.GetImage(ImageHelper.CheckIconPath) : null,
                        StaysOpenOnClick = true
                    };
                    defaultDeviceMenuItem.PreviewMouseUp += (_, args) => HandleOutputDeviceSelection(Guid.Empty, args.ChangedButton);
                    outputDeviceMenuItem.Items.Insert(0, defaultDeviceMenuItem);

                    MenuItem closeDeviceMenuMenuItem = new MenuItem
                    {
                        Header = Properties.Resources.Close
                    };
                    outputDeviceMenuItem.Items.Add(new Separator());
                    outputDeviceMenuItem.Items.Add(closeDeviceMenuMenuItem);

                    // If, after adding all audio devices, none of them are selected, then select the default
                    if (outputDeviceMenuItem.Items.OfType<MenuItem>().All(item => item.Icon is null))
                    {
                        defaultDeviceMenuItem.Icon = ImageHelper.GetImage(ImageHelper.CheckIconPath);
                    }
                }

                // Finally, remove the items marked for removal
                foreach (Control control in itemsToRemove)
                {
                    outputDeviceMenuItem.Items.Remove(control);
                }

                // We only have close and separator, which looks funny, so remove the separator
                if (outputDeviceMenuItem.Items.Count == 2
                    && outputDeviceMenuItem.Items.OfType<Separator>().FirstOrDefault() is Separator separator)
                {
                    outputDeviceMenuItem.Items.Remove(separator);
                }
            }
        }

        // This behavior is different from the output devices in that we can only select one.
        // However, all of the pieces are in place to allow multiple selection if needed.
        private void HandlePassthroughInputDeviceSelection(Guid deviceGuid)
        {
            if (GlobalSettings.GetInputDeviceGuids().Contains(deviceGuid))
            {
                // Toggle it off
                GlobalSettings.RemoveInputDeviceGuid(deviceGuid);
            }
            else
            {
                // Toggle it on and remove others
                GlobalSettings.RemoveAllInputDeviceGuids();
                GlobalSettings.AddInputDeviceGuid(deviceGuid);
            }

            Logger.Info("Passthrough input device toggled {0}; now [{1}]", deviceGuid, string.Join(",", GlobalSettings.GetInputDeviceGuids()));

            // Refresh the menu
            AudioPassthroughMenuOpened(_audioPassthroughMenu, new RoutedEventArgs());

            HandleAudioPassthroughChange();
        }

        // This behavior is different from both in put selection (which only allows one) and output selection (which requires at least one)
        private void HandlePassthroughOutputDeviceSelection(Guid guid, MouseButton mouseButton)
        {
            if (mouseButton == MouseButton.Right)
            {
                // This is a toggle. Do not clear the list, and add or removing depending on existence.
                if (GlobalSettings.GetPassthroughOutputDeviceGuids().Contains(guid))
                {
                    if (GlobalSettings.GetPassthroughOutputDeviceGuids().All(g => g == guid))
                    {
                        // This is the only device in the list, so we can't really toggle it. Do nothing.
                    }
                    else
                    {
                        // This is in the list, and it's being toggled off, so remove it.
                        GlobalSettings.RemovePassthroughOutputDeviceGuid(guid);
                    }
                }
                else
                {
                    // This is not the list, and it's being togged on, so add it.
                    GlobalSettings.AddPassthroughOutputDeviceGuid(guid);
                }
            }
            else
            {
                // A single click will toggle this one
                if (GlobalSettings.GetPassthroughOutputDeviceGuids().Any() && GlobalSettings.GetPassthroughOutputDeviceGuids().All(g => g == guid))
                {
                    // Toggle it off
                    GlobalSettings.RemovePassthroughOutputDeviceGuid(guid);
                }
                else
                {
                    // Toggle it on and remove others
                    GlobalSettings.RemoveAllPassthroughOutputDeviceGuids();
                    GlobalSettings.AddPassthroughOutputDeviceGuid(guid);
                }
            }

            Logger.Info("Passthrough output device {0} ({1}-click); now [{2}]", guid, mouseButton, string.Join(",", GlobalSettings.GetPassthroughOutputDeviceGuids()));

            // Refresh the menu
            AudioPassthroughMenuOpened(_audioPassthroughMenu, new RoutedEventArgs());

            HandleAudioPassthroughChange();
        }

        private void HandleAudioPassthroughChange()
        {
            // Clear any existing chaining
            CleanUpAudioPassthrough();

            if (GlobalSettings.GetInputDeviceGuids().Any())
            {
                Guid inputDeviceId = GlobalSettings.GetInputDeviceGuids().First();

                Logger.Info("Starting audio passthrough from {0} to [{1}] with latency {2}ms",
                    inputDeviceId, string.Join(",", GlobalSettings.GetPassthroughOutputDeviceGuids()), GlobalSettings.AudioPassthroughLatency);

                foreach (var outputDeviceId in GlobalSettings.GetPassthroughOutputDeviceGuids())
                {
                    try
                    {
                        // Create the input
                        MMDevice inputDevice = Utilities.GetDevice(inputDeviceId, DataFlow.Capture);
                        WasapiCapture inputCapture = new WasapiCapture(inputDevice);
                        inputCapture.RecordingStopped += HandlePassthroughRecordingStopped;
                        _inputCaptures.Add(inputCapture);

                        // Create the buffer
                        BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(inputDevice.AudioClient.MixFormat)
                        {
                            DiscardOnBufferOverflow = true
                        };
                        _bufferedWaveProviders.Add(bufferedWaveProvider);

                        inputCapture.DataAvailable += (_, args) =>
                        {
                            bufferedWaveProvider.AddSamples(args.Buffer, 0, args.BytesRecorded);
                        };

                        // Create the outputs
                        WasapiOut output = new WasapiOut(Utilities.GetDevice(outputDeviceId, DataFlow.Render), AudioClientShareMode.Shared, true, GlobalSettings.AudioPassthroughLatency);
                        output.PlaybackStopped += HandlePassthroughPlaybackStopped;
                        _outputCaptures.Add(output);

                        output.Init(bufferedWaveProvider);
                        output.Play();

                        inputCapture.StartRecording();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Audio passthrough failed for input {0} -> output {1}", inputDeviceId, outputDeviceId);

                        HandlePassthroughRecordingStopped(this, new StoppedEventArgs());
                        HandlePassthroughPlaybackStopped(this, new StoppedEventArgs());

                        Dispatcher.Invoke(async () =>
                        {
                            // Try to get the friendly input/output device names.
                            // GetDevice returns null if the device is gone, so these fail (by design) for a missing device.
                            string inputDeviceName = Properties.Resources.UNKNOWN;
                            string outputDeviceName = Properties.Resources.UNKNOWN;
                            try
                            {
                                inputDeviceName = Utilities.GetDevice(inputDeviceId, DataFlow.Capture).FriendlyName;
                            }
                            catch (Exception nameEx)
                            {
                                Logger.Debug(nameEx, "Could not resolve friendly name for input device {0}", inputDeviceId);
                            }
                            try
                            {
                                outputDeviceName = Utilities.GetDevice(outputDeviceId, DataFlow.Render).FriendlyName;
                            }
                            catch (Exception nameEx)
                            {
                                Logger.Debug(nameEx, "Could not resolve friendly name for output device {0}", outputDeviceId);
                            }

                            string error = string.Format(Properties.Resources.AudioPassthroughError, inputDeviceName, outputDeviceName);

                            if (ex is COMException comException)
                            {
                                if (comException.ErrorCode == -2004287478)
                                {
                                    // This is a specific error we know about which means the output device is being held exclusively.
                                    error += $"{Environment.NewLine}{Environment.NewLine}{Properties.Resources.AudioPassthroughOutputExclusiveError}";
                                }

                                error += $"{Environment.NewLine}{Environment.NewLine}{string.Format(Properties.Resources.ComErrorCode, comException.ErrorCode)}";
                            }

                            string fullError = $"{error}{Environment.NewLine}{Environment.NewLine}{ex}";

                            var res = await this.ShowMessageAsync(Properties.Resources.Error, error,
                                MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                                {
                                    AffirmativeButtonText = Properties.Resources.CopyDetails,
                                    NegativeButtonText = Properties.Resources.OK
                                });

                            if (res == MessageDialogResult.Affirmative)
                            {
                                Clipboard.SetText(fullError);
                            }
                        });

                        return;
                    }
                }
            }
        }

        private void HandlePassthroughRecordingStopped(object sender, StoppedEventArgs args)
        {
            // Handle the input device being disabled/disconnected/etc.
            Logger.Warn(args.Exception, "Passthrough recording stopped; clearing input device [{0}]", string.Join(",", GlobalSettings.GetInputDeviceGuids()));

            GlobalSettings.RemoveAllInputDeviceGuids();
            CleanUpAudioPassthrough();
        }

        private void HandlePassthroughPlaybackStopped(object sender, StoppedEventArgs args)
        {
            // This fires even for normal stoppages, so we only want to clean things up if there was an exception
            if (args.Exception != null)
            {
                // Handle the output device being disabled/disconnected/etc.
                Logger.Warn(args.Exception, "Passthrough playback stopped with error; clearing output devices [{0}]", string.Join(",", GlobalSettings.GetPassthroughOutputDeviceGuids()));

                GlobalSettings.RemoveAllPassthroughOutputDeviceGuids();
                CleanUpAudioPassthrough();
            }
        }

        private void CleanUpAudioPassthrough()
        {
            try
            {
                _inputCaptures.ForEach(ic =>
                {
                    ic.RecordingStopped -= HandlePassthroughRecordingStopped;
                    ic.StopRecording();
                    ic.Dispose();
                });
            }
            finally
            {
                _inputCaptures.Clear();
            }

            try
            {
                _outputCaptures.ForEach(oc =>
                {
                    oc.Stop();
                    oc.Dispose();
                });
            }
            finally
            {
                _outputCaptures.Clear();
            }

            try
            {
                _bufferedWaveProviders.ForEach(bwp => bwp.ClearBuffer());
            }
            finally
            {
                _bufferedWaveProviders.Clear();
            }
        }

        private readonly List<WasapiCapture> _inputCaptures = new List<WasapiCapture>();
        private readonly List<WasapiOut> _outputCaptures = new List<WasapiOut>();
        private readonly List<BufferedWaveProvider> _bufferedWaveProviders = new List<BufferedWaveProvider>();

        private void HandleOutputDeviceSelection(Guid deviceGuid, MouseButton mouseButton)
        {
            if (mouseButton == MouseButton.Right)
            {
                // This is a toggle. Do not clear the list, and add or removing depending on existence.
                if (GlobalSettings.GetOutputDeviceGuids().Contains(deviceGuid))
                {
                    if (GlobalSettings.GetOutputDeviceGuids().All(g => g == deviceGuid))
                    {
                        // This is the only device in the list, so we can't really toggle it. Do nothing.
                    }
                    else
                    {
                        // This is in the list, and it's being toggled off, so remove it.
                        GlobalSettings.RemoveOutputDeviceGuid(deviceGuid);
                    }
                }
                else
                {
                    // This is not the list, and it's being togged on, so add it.
                    GlobalSettings.AddOutputDeviceGuid(deviceGuid);
                }
            }
            else
            {
                // Not a toggle, just a selection
                GlobalSettings.RemoveAllOutputDeviceGuids();
                GlobalSettings.AddOutputDeviceGuid(deviceGuid);
            }

            Logger.Info("Output device {0} ({1}-click); now [{2}]", deviceGuid, mouseButton, string.Join(",", GlobalSettings.GetOutputDeviceGuids()));

            // Refresh the menu
            OutputDeviceMenuOpened(_outputDeviceMenu, new RoutedEventArgs());
        }

        private void CloseSnackbarButton_Click(object sender, RoutedEventArgs e)
        {
            Snackbar.IsOpen = false;
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // Invoke the assigned undo action
            _undoAction?.Invoke();

            // Always close the snackbar when undoing
            Snackbar.IsOpen = false;
        }

        private void Global_MouseDown(object sender, EventArgs e)
        {
            // Always close the snackbar on any user interaction (unless they're interacting with the snackbar itself)
            if (Snackbar.IsMouseOver == false)
            {
                Snackbar.IsOpen = false;
            }
        }

        private async void AboutBoxCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            await ShowAboutBox();
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        protected override void OnClosed(EventArgs e)
        {
            // Do cleanup
            _globalMouseEvents.MouseDown -= Global_MouseDown;
            _globalMouseEvents.Dispose();

            CleanUpAudioPassthrough();

            base.OnClosed(e);
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Event handler for KeyDown event
        /// </summary>
        public RoutedEventHandler KeyDownHandler => RoutedKeyDownHandler;

        // Members that mention internal types are implemented explicitly, since this class is public.

        /// <inheritdoc />
        IEnumerable<MyMetroTabItem> ISoundBoardHost.SoundTabs => Tabs.Items.OfType<MyMetroTabItem>().Where(tab => tab.Page != null);

        /// <inheritdoc />
        SoundButton ISoundBoardHost.FindButton(Sound sound) => FindButton(sound);

        /// <inheritdoc />
        IEnumerable<SoundButton> ISoundBoardHost.GetSoundButtons(MetroTabItem tab) => GetSoundButtons(tab);

        /// <inheritdoc />
        PlaybackCoordinator ISoundBoardHost.Playback => Playback;

        /// <inheritdoc />
        HotkeyRegistry ISoundBoardHost.Hotkeys => Hotkeys;

        /// <inheritdoc />
        void ISoundBoardHost.OnAnySoundStarted(SoundButton soundButton) => OnAnySoundStarted(soundButton);

        /// <inheritdoc />
        void ISoundBoardHost.OnAnySoundStopped(SoundButton soundButton) => OnAnySoundStopped(soundButton);

        /// <inheritdoc />
        void ISoundBoardHost.OnSoundFinished(SoundButton soundButton) => OnSoundFinished(soundButton);

        /// <inheritdoc />
        void ISoundBoardHost.OnAnySoundRenamed() => OnAnySoundRenamed();

        /// <inheritdoc />
        double ISoundBoardHost.WindowWidth => Width;

        /// <inheritdoc />
        public void SuspendTypeToSearch() => RemoveHandler(KeyDownEvent, KeyDownHandler);

        /// <inheritdoc />
        public void ResumeTypeToSearch() => AddHandler(KeyDownEvent, KeyDownHandler, true);

        /// <inheritdoc />
        void ISoundBoardHost.ShowUndoSnackbar(string message) => ShowUndoSnackbar(message);

        // The dialog helpers are MahApps extension methods on MetroWindow; the explicit implementations forward to them
        // (an implicit instance method with the same name would hide the extension for every `this.Show...` call in this class).

        /// <inheritdoc />
        Task<MessageDialogResult> ISoundBoardHost.ShowMessageAsync(string title, string message, MessageDialogStyle style, MetroDialogSettings settings)
            => DialogManager.ShowMessageAsync(this, title, message, style, settings);

        /// <inheritdoc />
        Task<string> ISoundBoardHost.ShowInputAsync(string title, string message, MetroDialogSettings settings)
            => DialogManager.ShowInputAsync(this, title, message, settings);

        /// <inheritdoc />
        Task ISoundBoardHost.ShowChildWindowAsync(ChildWindow childWindow)
            => ChildWindowManager.ShowChildWindowAsync(this, childWindow);

        /// <summary>
        /// Event handler for KeyUp event
        /// </summary>
        public RoutedEventHandler KeyUpHandler => RoutedKeyUpHandler;

        /// <summary>
        /// Tracks every sound player that has been started, so they can all be silenced at once
        /// </summary>
        internal PlaybackCoordinator Playback { get; } = new PlaybackCoordinator();

        /// <summary>
        /// Returns the <see cref="Font"/> of the <see cref="SnackbarMessage"/>.
        /// </summary>
        public Font SnackbarMessageFont => new Font(SnackbarMessage.FontFamily.ToString(), (float) SnackbarMessage.FontSize);

        /// <summary>
        /// Registers sounds' hotkeys with the system and reports presses. Attached to the window in <see cref="OnSourceInitialized"/>.
        /// </summary>
        internal HotkeyRegistry Hotkeys { get; } = new HotkeyRegistry();

        /// <summary>
        /// Whether or not any instance of the hotkey picker dialog is open
        /// </summary>
        public bool IsHotkeyPickerOpen { get; set; }

        /// <summary>
        /// The currently selected tab
        /// </summary>
        public MetroTabItem SelectedTab => Tabs.SelectedItem as MetroTabItem;

        #endregion

        #region Public methods

        /// <summary>
        /// Closes the search pane and clears any query
        /// </summary>
        public void CloseSearch()
        {
            _searchString = string.Empty; // Don't wait for it to close to clear the query
            Search.IsOpen = false;
        }

        /// <summary>
        /// Defines an <see cref="Action"/> to call if <see cref="ShowUndoSnackbar(string, int)"/> is called
        /// and the user chooses to perform the undo.
        /// </summary>
        /// <param name="action"></param>
        public void SetUndoAction(Action action)
        {
            _undoAction = action;
        }

        /// <summary>
        /// Shows a snackbar that allows the user to undo an action.
        /// </summary>
        /// <param name="message">Message to show the user on the snackbar</param>
        /// <param name="timeout">Time in ms until the snackbar is closed automatically. Defaults to 5 seconds.</param>
        public void ShowUndoSnackbar(string message = "", int timeout = 5000)
        {
            SnackbarMessage.Text = message;
            Snackbar.AutoCloseInterval = timeout;
            Snackbar.IsOpen = true;
        }

        /// <summary>
        /// Invoked when any sound starts
        /// </summary>
        internal void OnAnySoundStarted(SoundButton soundButton)
        {
            soundButton.ParentTab.IndicateSoundPlaying();
        }

        /// <summary>
        /// Invoked when any sound stop
        /// </summary>
        internal void OnAnySoundStopped(SoundButton soundButton)
        {
            if (!IsAnySoundPlayingOnTab(soundButton.ParentTab))
            {
                soundButton.ParentTab.RemoveSoundPlaying();
            }
        }

        /// <summary>
        /// Invoked when a sound reaches its end on its own. Starts the sound it chains to, if any (and if that sound still
        /// exists and has a file), switching to its tab first.
        /// </summary>
        internal void OnSoundFinished(SoundButton soundButton)
        {
            Sound next = FindSound(soundButton.NextSound);
            if (next is null || next.IsEmpty)
            {
                return;
            }

            if (FindButton(next) is SoundButton nextSoundButton)
            {
                Logger.Debug("'{0}' finished; chaining to '{1}'", soundButton.SoundName, next.Name);

                // If the next sound isn't on the current tab, focus that tab.
                if (nextSoundButton.ParentTab != SelectedTab)
                {
                    nextSoundButton.ParentTab.Focus();
                }

                nextSoundButton.StartSound();
            }
        }

        internal bool IsAnySoundPlayingOnTab(MyMetroTabItem myMetroTabItem) => myMetroTabItem.IsAnySoundPlaying;

        internal void OnAnySoundRenamed()
        {
            GetSoundButtons().SelectMany(sb => sb.ChildButtons.OfType<NextSoundIconButton>()).ToList().ForEach(ns => ns.Update());
        }

        #endregion

        #region Private fields

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IKeyboardMouseEvents _globalMouseEvents;
        private string _searchString = string.Empty;
        private Action _undoAction;
        private readonly WpfUpdateChecker _updateChecker;
        private MenuItem _newPageDefaultMenu;
        private MenuItem _audioPassthroughMenu;
        private MenuItem _outputDeviceMenu;

        #endregion

        #region Private properties

        private static string ConfigFilePath => ConfigStore.ConfigFilePath;

        private static string TempConfigFilePath => ConfigStore.TempConfigFilePath;

        private static string LegacyConfigFilePath => ConfigStore.LegacyConfigFilePath;

        /// <summary>
        /// Filter for the import/export file dialogs. The <c>|</c>-separated shape is Win32's, so only the description
        /// is localized; the pattern is repeated because Windows shows the first half and matches on the second.
        /// </summary>
        private static string ConfigFileFilter =>
            $@"{string.Format(Properties.Resources.FileFilterDescription, Properties.Resources.ConfigurationFiles, ConfigFilePattern)}|{ConfigFilePattern}";

        #endregion

        #region Consts

        private const int TWO_MINUTES_IN_MILLISECONDS = 120000;

        private const string ConfigFilePattern = @"*.config";

        private const string WELCOME_PAGE_TAG = nameof(WELCOME_PAGE_TAG);

        #endregion

        #region IUndoable members

        /// <inheritdoc />
        public TabPageUndoState SaveState()
        {
            return new TabPageUndoState
            {
                Page = (SelectedTab as MyMetroTabItem)?.Page?.DeepClone(),
                Index = Tabs.SelectedIndex
            };
        }

        /// <inheritdoc />
        public void LoadState(TabPageUndoState undoState)
        {
            MyMetroTabItem tab = new MyMetroTabItem();

            if (undoState.Page is Page page)
            {
                // Work from a copy so the snapshot stays intact. Building the content registers the sounds' hotkeys.
                tab.Page = page.DeepClone();
                CreatePageContent(tab);
            }
            else
            {
                CreateHelpContent(tab);
            }

            int index = Math.Max(0, Math.Min(undoState.Index, Tabs.Items.Count));
            Tabs.Items.Insert(index, tab);
            Tabs.SelectedIndex = index;

            CreateTabContextMenus();
        }

        /// <inheritdoc />
        ConfigUndoState IUndoable<ConfigUndoState>.SaveState()
        {
            return new ConfigUndoState { Config = BuildConfigFromUi().DeepClone() };
        }

        /// <inheritdoc />
        public void LoadState(ConfigUndoState undoState)
        {
            foreach (SoundButton soundButton in GetSoundButtons())
            {
                soundButton.Stop();
                soundButton.UnregisterLocalHotkey();
                soundButton.UnregisterGlobalHotkey();
            }

            // Work from a copy so the snapshot stays intact, and restore the settings exactly (not additively):
            // undoing an import or clear should put the devices back the way they were.
            BoardSettings before = GlobalSettings.Current.DeepClone();
            ApplyConfigToUi(undoState.Config.DeepClone(), replaceSettings: true);
            BoardSettings after = GlobalSettings.Current;

            // If the passthrough devices changed, rebuild the passthrough chain to match. (Not unconditionally: tearing
            // it down and recreating it interrupts live passthrough, so undoing a grid change must not do that.)
            if (!before.InputDevices.SetEquals(after.InputDevices)
                || !before.PassthroughOutputDevices.SetEquals(after.PassthroughOutputDevices)
                || before.AudioPassthroughLatency != after.AudioPassthroughLatency)
            {
                HandleAudioPassthroughChange();
            }

            // An undo that restores a board with no pages (e.g. undoing an import done on a fresh install) shows the welcome page, as a load does
            ShowHelpIfNoTabs();

            SaveSettings();
        }

        /// <inheritdoc />
        TabPageSoundsUndoState IUndoable<TabPageSoundsUndoState>.SaveState()
        {
            MyMetroTabItem tab = SelectedTab as MyMetroTabItem;

            return new TabPageSoundsUndoState
            {
                Tab = tab,
                Sounds = tab?.Page?.Sounds.Select(sound => sound.DeepClone()).ToList() ?? new List<Sound>()
            };
        }

        /// <inheritdoc />
        public void LoadState(TabPageSoundsUndoState undoState)
        {
            // Restore onto the tab the sounds came from, and only if it is still here (the user may have switched tabs, or removed it, meanwhile)
            if (!(undoState.Tab is MyMetroTabItem tab && Tabs.Items.Contains(tab) && tab.Page is Page page))
            {
                Logger.Warn("Cannot undo: the tab the sounds were cleared from is no longer open");
                return;
            }

            // Restore by position, so this still works if the grid was resized in between (cells that no longer exist are skipped).
            // Cells that are empty in both the snapshot and the page are left alone so their ids are not churned.
            foreach (Sound sound in undoState.Sounds)
            {
                if (page.IsInRange(sound.Row, sound.Column) && !(sound.IsEmpty && page[sound.Row, sound.Column].IsEmpty))
                {
                    FindButton(page[sound.Row, sound.Column])?.LoadState(sound);
                }
            }
        }

        #endregion
    }
}
