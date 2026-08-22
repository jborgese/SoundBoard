using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using BondTech.HotKeyManagement.WPF._4;
using NLog;
using SoundBoard.Model;
using Keys = BondTech.HotKeyManagement.WPF._4.Keys;

namespace SoundBoard
{
    /// <summary>
    /// Interaction logic for HotkeyDialog.xaml
    /// </summary>
    internal partial class HotkeyDialog
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        internal HotkeyDialog(SoundButton soundButton)
        {
            InitializeComponent();
            _soundButton = soundButton;
        }

        /// <summary>
        /// The result of the dialog
        /// </summary>
        public DialogResult DialogResult { get; private set; }

        public Hotkey LocalHotkey
        {
            get => LocalHotkeyControl.Hotkey;
            set => LocalHotkeyControl.Hotkey = value;
        }

        public Hotkey GlobalHotkey
        {
            get => GlobalHotkeyControl.Hotkey;
            set => GlobalHotkeyControl.Hotkey = value;
        }

        /// <summary>
        /// Finds a sound other than the one being edited that already uses <paramref name="hotkey"/> (as either its local or global hotkey), or null.
        /// </summary>
        private Sound FindOtherSoundUsing(Hotkey hotkey)
        {
            string text = hotkey.ToString();

            return MainWindow.Instance.AllSounds().FirstOrDefault(sound =>
                !ReferenceEquals(sound, _soundButton.Sound)
                && (sound.LocalHotkey?.ToString() == text || sound.GlobalHotkey?.ToString() == text));
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            WarningLabel.Visibility = Visibility.Hidden;
            WarningLabel.Text = string.Empty;

            // Both hotkeys are set, and are identical
            if (LocalHotkey != null && GlobalHotkey != null &&
                LocalHotkey.ToString() == GlobalHotkey.ToString())
            {
                WarningLabel.Text += Properties.Resources.IdenticalHotkeyWarning;
                WarningLabel.Visibility = Visibility.Visible;
            }

            // See if the local hotkey is used anywhere else
            if (LocalHotkey != null)
            {
                if (FindOtherSoundUsing(LocalHotkey) is Sound other)
                {
                    WarningLabel.Text += string.Format(Properties.Resources.LocalHotkeyInUse, LocalHotkey, other.Name);
                    WarningLabel.Visibility = Visibility.Visible;
                }
            }

            if (GlobalHotkey != null)
            {
                if (FindOtherSoundUsing(GlobalHotkey) is Sound other)
                {
                    WarningLabel.Text += string.Format(Properties.Resources.GlobalHotkeyInuse, GlobalHotkey, other.Name);
                    WarningLabel.Visibility = Visibility.Visible;
                }
            }

            if (WarningLabel.Visibility == Visibility.Visible)
            {
                return;
            }

            // Try to register
            try
            {
                _soundButton.LocalHotkey = null;

                // Start by clearing any registrations
                _soundButton.UnregisterLocalHotkey();

                if (LocalHotkey != null)
                {
                    // Assign and register
                    _soundButton.LocalHotkey = LocalHotkey;
                    _soundButton.ReregisterLocalHotkey();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to register local hotkey {0} for sound '{1}'", LocalHotkey, _soundButton.SoundName);
                _soundButton.LocalHotkey = null;
                WarningLabel.Text = string.Format(Properties.Resources.HotkeyRegistrationFailed, LocalHotkey);
                WarningLabel.Visibility = Visibility.Visible;
            }

            try
            {
                _soundButton.GlobalHotkey = null;

                // Start by clearing any registration
                _soundButton.UnregisterGlobalHotkey();

                if (GlobalHotkey != null)
                {
                    // Assign and register
                    _soundButton.GlobalHotkey = GlobalHotkey;
                    _soundButton.ReregisterGlobalHotkey();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to register global hotkey {0} for sound '{1}'", GlobalHotkey, _soundButton.SoundName);
                _soundButton.GlobalHotkey = null;
                WarningLabel.Text = string.Format(Properties.Resources.HotkeyRegistrationFailed, GlobalHotkey);
                WarningLabel.Visibility = Visibility.Visible;
            }

            if (WarningLabel.Visibility == Visibility.Visible)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private readonly SoundButton _soundButton;
    }
}
