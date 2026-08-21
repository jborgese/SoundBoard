using System;
using System.Collections.Generic;
using SoundBoard.Model;

namespace SoundBoard
{
    /// <summary>
    /// Static facade over the application's live <see cref="BoardSettings"/> (<see cref="Current"/>).
    /// </summary>
    /// <remarks>
    /// The settings themselves live in the model. This class only exists so that the many existing call sites keep
    /// compiling while the UI is migrated; new code should use <see cref="Current"/> directly.
    /// </remarks>
    public static class GlobalSettings
    {
        /// <summary>
        /// The settings in effect for this process. The instance never changes; its contents do.
        /// </summary>
        public static BoardSettings Current { get; } = new BoardSettings();

        #region Output device

        /// <summary>
        /// Add an output device to the current list
        /// </summary>
        public static void AddOutputDeviceGuid(Guid guid) => Current.OutputDevices.Add(guid);

        /// <summary>
        /// Remove an output device from the current list
        /// </summary>
        public static void RemoveOutputDeviceGuid(Guid guid) => Current.OutputDevices.Remove(guid);

        /// <summary>
        /// Removes all current output devices
        /// </summary>
        public static void RemoveAllOutputDeviceGuids() => Current.OutputDevices.Clear();

        /// <summary>
        /// Get the current list of output devices, or the default device (<see cref="Guid.Empty"/>) when none are selected
        /// </summary>
        public static List<Guid> GetOutputDeviceGuids() => Current.GetOutputDeviceGuidsOrDefault();

        #endregion

        #region Input device

        /// <summary>
        /// Add an input device to the current list
        /// </summary>
        public static void AddInputDeviceGuid(Guid guid) => Current.InputDevices.Add(guid);

        /// <summary>
        /// Remove an input device from the current list
        /// </summary>
        public static void RemoveInputDeviceGuid(Guid guid) => Current.InputDevices.Remove(guid);

        /// <summary>
        /// Removes all current input devices
        /// </summary>
        public static void RemoveAllInputDeviceGuids() => Current.InputDevices.Clear();

        /// <summary>
        /// Get the current list of input devices
        /// </summary>
        public static List<Guid> GetInputDeviceGuids() => new List<Guid>(Current.InputDevices);

        #endregion

        #region Passthrough output device

        /// <summary>
        /// Add a passthrough output device to the current list
        /// </summary>
        public static void AddPassthroughOutputDeviceGuid(Guid guid) => Current.PassthroughOutputDevices.Add(guid);

        /// <summary>
        /// Remove a passthrough output device from the current list
        /// </summary>
        public static void RemovePassthroughOutputDeviceGuid(Guid guid) => Current.PassthroughOutputDevices.Remove(guid);

        /// <summary>
        /// Removes all current passthrough output devices
        /// </summary>
        public static void RemoveAllPassthroughOutputDeviceGuids() => Current.PassthroughOutputDevices.Clear();

        /// <summary>
        /// Get the current list of passthrough output devices
        /// </summary>
        public static List<Guid> GetPassthroughOutputDeviceGuids() => new List<Guid>(Current.PassthroughOutputDevices);

        #endregion

        /// <summary>
        /// The number of button columns to use by default for new pages
        /// </summary>
        public static int NewPageDefaultColumns
        {
            get => Current.NewPageDefaultColumns;
            set => Current.NewPageDefaultColumns = value;
        }

        /// <summary>
        /// The number of button rows to use by default for new pages
        /// </summary>
        public static int NewPageDefaultRows
        {
            get => Current.NewPageDefaultRows;
            set => Current.NewPageDefaultRows = value;
        }

        /// <summary>
        /// The latency to use when chaining input to outputs
        /// </summary>
        public static int AudioPassthroughLatency
        {
            get => Current.AudioPassthroughLatency;
            set => Current.AudioPassthroughLatency = value;
        }
    }
}
