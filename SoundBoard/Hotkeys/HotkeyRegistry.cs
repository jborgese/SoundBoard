using System;
using System.Linq;
using System.Windows.Input;
using BondTech.HotKeyManagement.WPF._4;
using NLog;
using SoundBoard.Model;

namespace SoundBoard
{
    /// <summary>
    /// Registers sounds' hotkeys with the operating system (via <see cref="HotKeyManager"/>) and reports presses by sound id.
    /// </summary>
    /// <remarks>
    /// Every registration is named <c>Utilities.SanitizeId(sound.Id)</c>, so a sound's registrations can always be found from
    /// its current id and a press can be mapped back to the sound. Registrations do not follow a sound automatically: callers
    /// register when a sound gains a hotkey or is (re)attached to a button, and unregister before its id changes or it goes away.
    /// The manager does not exist until the window has a handle; until <see cref="Attach"/> is called every operation is a no-op.
    /// </remarks>
    internal sealed class HotkeyRegistry
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private HotKeyManager _manager;

        /// <summary>
        /// Raised when a registered hotkey (local or global) is pressed, with the <see cref="Sound.Id"/> the registration belongs to.
        /// </summary>
        public event EventHandler<string> HotkeyPressed;

        /// <summary>
        /// True once <see cref="Attach"/> has been called.
        /// </summary>
        public bool IsAttached => _manager != null;

        /// <summary>
        /// Connects the registry to the window's hotkey manager.
        /// </summary>
        public void Attach(HotKeyManager manager)
        {
            if (_manager != null) throw new InvalidOperationException("Already attached");

            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _manager.GlobalHotKeyPressed += (_, args) => Raise(args.HotKey.Name);
            _manager.LocalHotKeyPressed += (_, args) => Raise(args.HotKey.Name);
        }

        /// <summary>
        /// Works around a quirk where the first global registration after the executable changes fails: registers and
        /// immediately removes a hotkey nobody wants. Never throws.
        /// </summary>
        public void RegisterThrowaway()
        {
            if (_manager is null) return;

            GlobalHotKey ignore = new GlobalHotKey(Utilities.SanitizeId(Guid.NewGuid().ToString()), ModifierKeys.None, Keys.HangulMode);

            try
            {
                _manager.AddGlobalHotKey(ignore);
            }
            catch (Exception ex)
            {
                // Expected on the first launch after the executable changes. Only a problem if it happens every time.
                Logger.Warn(ex, "Throwaway global hotkey registration failed (expected once after the exe changes)");
            }

            // Unregister -- this will work for all but the bad scenario
            try
            {
                _manager.RemoveGlobalHotKey(ignore);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Throwaway global hotkey unregistration failed (expected if registration failed)");
            }
        }

        /// <summary>
        /// Registers the sound's local hotkey, if it has one.
        /// </summary>
        /// <exception cref="Exception">The key has no equivalent in the hotkey manager, or the manager refused it (e.g. already registered).</exception>
        public void RegisterLocal(Sound sound)
        {
            if (sound?.LocalHotkey is null || _manager is null) return;

            _manager.AddLocalHotKey(new LocalHotKey(Name(sound), sound.LocalHotkey.Modifiers, Map(sound.LocalHotkey), RaiseLocalEvent.OnKeyUp, true));
        }

        /// <summary>
        /// Registers the sound's global hotkey, if it has one.
        /// </summary>
        /// <exception cref="Exception">The key has no equivalent in the hotkey manager, or the manager refused it (e.g. already registered by another application).</exception>
        public void RegisterGlobal(Sound sound)
        {
            if (sound?.GlobalHotkey is null || _manager is null) return;

            _manager.AddGlobalHotKey(new GlobalHotKey(Name(sound), sound.GlobalHotkey.Modifiers, Map(sound.GlobalHotkey), true));
        }

        /// <summary>
        /// Removes the local registration made for the sound's current id, if any. Never throws.
        /// </summary>
        public void UnregisterLocal(Sound sound)
        {
            if (sound is null || _manager is null) return;

            string name = Name(sound);
            if (_manager.EnumerateLocalHotKeys.OfType<LocalHotKey>().FirstOrDefault(h => h.Name == name) is LocalHotKey existing)
            {
                try
                {
                    _manager.RemoveLocalHotKey(existing);
                }
                catch (Exception ex)
                {
                    // The hotkey manager may already have dropped it; either way there's nothing more to do.
                    Logger.Warn(ex, "Failed to unregister local hotkey {0} for sound '{1}'", sound.LocalHotkey, sound.Name);
                }
            }
        }

        /// <summary>
        /// Removes the global registration made for the sound's current id, if any. Never throws.
        /// </summary>
        public void UnregisterGlobal(Sound sound)
        {
            if (sound is null || _manager is null) return;

            string name = Name(sound);
            if (_manager.EnumerateGlobalHotKeys.OfType<GlobalHotKey>().FirstOrDefault(h => h.Name == name) is GlobalHotKey existing)
            {
                try
                {
                    _manager.RemoveGlobalHotKey(existing);
                }
                catch (Exception ex)
                {
                    // A global hotkey that fails to unregister stays registered with Windows until the process exits.
                    Logger.Warn(ex, "Failed to unregister global hotkey {0} for sound '{1}'", sound.GlobalHotkey, sound.Name);
                }
            }
        }

        /// <summary>
        /// Removes both registrations for the sound's current id. Never throws.
        /// </summary>
        public void Unregister(Sound sound)
        {
            UnregisterLocal(sound);
            UnregisterGlobal(sound);
        }

        /// <summary>
        /// True if <paramref name="name"/> is the registration name of <paramref name="sound"/>.
        /// </summary>
        public static bool IsRegistrationFor(string name, Sound sound) => sound != null && Name(sound) == name;

        private static string Name(Sound sound) => Utilities.SanitizeId(sound.Id);

        private static Keys Map(Hotkey hotkey)
        {
            Keys mapped = Utilities.MapKey(hotkey.Key);

            if (mapped == default)
            {
                throw new Exception($"Key '{hotkey.Key}' has no equivalent in the hotkey manager");
            }

            return mapped;
        }

        private void Raise(string name) => HotkeyPressed?.Invoke(this, name);
    }
}
