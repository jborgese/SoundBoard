using System;
using System.Collections.Generic;
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

            return _soundButton.Host.AllSounds().FirstOrDefault(sound =>
                !ReferenceEquals(sound, _soundButton.Sound)
                && (sound.LocalHotkey?.ToString() == text || sound.GlobalHotkey?.ToString() == text));
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            ClearWarning();

            // Up to three of these can be wrong at once. Each is a whole sentence of its own in the resources and they
            // are joined here, rather than concatenated into one resource, so that no translation has to carry a
            // trailing space or make assumptions about what follows it.
            List<string> warnings = new List<string>();

            // Both hotkeys are set, and are identical
            if (LocalHotkey != null && GlobalHotkey != null &&
                LocalHotkey.ToString() == GlobalHotkey.ToString())
            {
                warnings.Add(Properties.Resources.IdenticalHotkeyWarning);
            }

            // See if the local hotkey is used anywhere else
            if (LocalHotkey != null)
            {
                if (FindOtherSoundUsing(LocalHotkey) is Sound other)
                {
                    warnings.Add(string.Format(Properties.Resources.LocalHotkeyInUse, LocalHotkey, other.Name));
                }
            }

            if (GlobalHotkey != null)
            {
                if (FindOtherSoundUsing(GlobalHotkey) is Sound other)
                {
                    warnings.Add(string.Format(Properties.Resources.GlobalHotkeyInuse, GlobalHotkey, other.Name));
                }
            }

            if (warnings.Count > 0)
            {
                ShowWarning(string.Join(@" ", warnings));
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
                ShowWarning(string.Format(Properties.Resources.HotkeyRegistrationFailed, LocalHotkey));
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
                ShowWarning(string.Format(Properties.Resources.HotkeyRegistrationFailed, GlobalHotkey));
            }

            if (WarningLabel.Visibility == Visibility.Visible)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ClearWarning()
        {
            WarningLabel.Visibility = Visibility.Hidden;
            WarningLabel.Text = string.Empty;
        }

        private void ShowWarning(string text)
        {
            WarningLabel.Text = text;
            WarningLabel.Visibility = Visibility.Visible;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private readonly SoundButton _soundButton;
    }
}
