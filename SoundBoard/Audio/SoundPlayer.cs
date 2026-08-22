using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using SoundBoard.Model;

namespace SoundBoard.Audio
{
    /// <summary>
    /// Event data for <see cref="SoundPlayer.Stopped"/>.
    /// </summary>
    internal sealed class SoundStoppedEventArgs : EventArgs
    {
        /// <summary>
        /// True if the sound reached its end on its own (as opposed to being stopped, restarted or failing).
        /// </summary>
        public bool Finished { get; set; }

        /// <summary>
        /// The playback error NAudio reported, if any.
        /// </summary>
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Plays one <see cref="Sound"/> on one or more output devices at once, applying its loop and volume-offset settings.
    /// Knows nothing about buttons or windows.
    /// </summary>
    /// <remarks>
    /// One output (<see cref="DirectSoundOut"/>) plus one <see cref="AudioFileReader"/> is created per device, so the devices
    /// are fed independently and started together. <see cref="Stopped"/> is raised once per output, on whatever thread NAudio
    /// raises <see cref="IWavePlayer.PlaybackStopped"/> on (the thread that called <see cref="Start"/> when that thread has a
    /// synchronization context, i.e. the UI thread) — exactly as the per-player handlers it replaces were.
    /// </remarks>
    internal sealed class SoundPlayer : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private const float VolumeOffsetMultiplier = 2f;

        private readonly List<IWavePlayer> _players = new List<IWavePlayer>();
        private readonly Dictionary<IWavePlayer, AudioFileReader> _audioFileReaders = new Dictionary<IWavePlayer, AudioFileReader>();
        private readonly Dictionary<IWavePlayer, WaveStream> _waveProviders = new Dictionary<IWavePlayer, WaveStream>();
        private Stopwatch _stopWatch;

        /// <summary>
        /// Raised once per output when it stops, whether it finished, was stopped, was torn down by a new <see cref="Start"/>, or failed.
        /// </summary>
        public event EventHandler<SoundStoppedEventArgs> Stopped;

        /// <summary>
        /// True if at least one output is currently playing.
        /// </summary>
        public bool IsPlaying => _players.Any(p => p.PlaybackState == PlaybackState.Playing);

        /// <summary>
        /// True if at least one output has stopped. False when there are no outputs.
        /// </summary>
        public bool IsAnyOutputStopped => _players.Any(p => p.PlaybackState == PlaybackState.Stopped);

        /// <summary>
        /// Length of the sound being played, or null if nothing has been started.
        /// </summary>
        public TimeSpan? Duration => _audioFileReaders.Values.FirstOrDefault()?.TotalTime;

        /// <summary>
        /// Read position of the sound being played (in bytes), or null if nothing has been started.
        /// </summary>
        public long? Position => _audioFileReaders.Values.FirstOrDefault()?.Position;

        /// <summary>
        /// Wall-clock time played since <see cref="Start"/> or the last <see cref="RestartClock"/>, excluding time paused.
        /// </summary>
        public TimeSpan Elapsed => _stopWatch?.Elapsed ?? TimeSpan.Zero;

        /// <summary>
        /// Restarts <see cref="Elapsed"/> from zero (used when a looping sound wraps around).
        /// </summary>
        public void RestartClock() => _stopWatch = Stopwatch.StartNew();

        /// <summary>
        /// Starts playing <paramref name="sound"/> from the beginning on the given devices, tearing down any previous playback first.
        /// </summary>
        /// <param name="sound">The sound to play. Its <see cref="Sound.Path"/> must exist.</param>
        /// <param name="outputDevices">Device ids to play on. A device that is not present is replaced by the default device (once).</param>
        /// <exception cref="Exception">Any failure to open the file or a device propagates to the caller after the previous playback has been torn down.</exception>
        public void Start(Sound sound, IEnumerable<Guid> outputDevices)
        {
            if (sound is null) throw new ArgumentNullException(nameof(sound));
            if (outputDevices is null) throw new ArgumentNullException(nameof(outputDevices));

            TearDown();

            // One output per device, falling back to the default device for any that has gone missing
            bool addedDefaultDevice = false;
            List<Guid> devices = outputDevices.ToList();
            foreach (Guid device in devices)
            {
                if (Utilities.DoesOutAudioDeviceExist(device))
                {
                    _players.Add(new DirectSoundOut(device));
                }
                else if (!addedDefaultDevice)
                {
                    Logger.Info("Configured output device {0} not found; falling back to the default device", device);
                    _players.Add(new DirectSoundOut(Guid.Empty));
                    addedDefaultDevice = true;
                }
            }

            _players.ForEach(p => _audioFileReaders[p] = new AudioFileReader(sound.Path));

            // Unmute the selected device(s)
            devices.ForEach(d => Utilities.UnmuteDeviceAudio(d, unmuteDefaultIfGivenNotFound: true));

            _players.ForEach(p => p.PlaybackStopped += PlaybackStoppedHandler);

            _stopWatch = Stopwatch.StartNew();

            // Looping
            foreach (var kvp in _audioFileReaders.ToList())
            {
                _waveProviders[kvp.Key] = sound.Loop ? new LoopStream(kvp.Value) : (WaveStream)kvp.Value;
            }

            // Volume
            if (sound.VolumeOffset == 0)
            {
                _players.ForEach(p => p.Init(_waveProviders[p]));
            }
            else
            {
                float volume = sound.VolumeOffset < 0 ? 1f / (sound.VolumeOffset * VolumeOffsetMultiplier) : (sound.VolumeOffset * VolumeOffsetMultiplier);
                _players.ForEach(p => p.Init(new VolumeSampleProvider(_waveProviders[p].ToSampleProvider()) { Volume = volume }));
            }

            // Aaaaand play
            Parallel.ForEach(_players, p => p.Play());

            Logger.Debug("Started '{0}' ({1}) on {2} output(s) [{3}]; loop={4}, volumeOffset={5}", sound.Name, sound.Path, _players.Count, string.Join(",", devices), sound.Loop, sound.VolumeOffset);
        }

        /// <summary>
        /// Resumes after <see cref="Pause"/>.
        /// </summary>
        public void Resume()
        {
            Parallel.ForEach(_players, p => p.Play());
            _stopWatch?.Start();
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            Parallel.ForEach(_players, p => p.Pause());
            _stopWatch?.Stop();
        }

        /// <summary>
        /// Stops every output that is not already stopped. <see cref="Stopped"/> is raised for each.
        /// </summary>
        public void Stop()
        {
            Parallel.ForEach(_players.Where(p => p.PlaybackState != PlaybackState.Stopped), p => p.Stop());
        }

        /// <summary>
        /// Stops and disposes every output and closes the file readers. <see cref="Stopped"/> is raised synchronously for each
        /// output first (reporting whether it had finished), so listeners can settle their state before the next <see cref="Start"/>.
        /// </summary>
        public void TearDown()
        {
            // Iterate a copy: a Stopped listener may start another sound (next-sound chaining), possibly this very player
            foreach (IWavePlayer player in _players.ToList())
            {
                player.PlaybackStopped -= PlaybackStoppedHandler;
                RaiseStopped(player, null);
            }

            Stop();
            _players.ForEach(p => p.Dispose());
            _players.Clear();

            // Closing a stream that has already been torn down by NAudio can throw; there is nothing to clean up in that case.
            foreach (var kvp in _waveProviders.ToList())
            {
                try { kvp.Value.Close(); } catch (Exception ex) { Logger.Debug(ex, "Closing previous wave provider failed"); }
            }
            _waveProviders.Clear();

            foreach (var kvp in _audioFileReaders.ToList())
            {
                try { kvp.Value.Close(); } catch (Exception ex) { Logger.Debug(ex, "Closing previous audio file reader failed"); }
            }
            _audioFileReaders.Clear();
        }

        /// <inheritdoc/>
        public void Dispose() => TearDown();

        private void PlaybackStoppedHandler(object sender, StoppedEventArgs e)
        {
            if (sender is IWavePlayer player)
            {
                RaiseStopped(player, e.Exception);
            }
        }

        private void RaiseStopped(IWavePlayer player, Exception exception)
        {
            // Indicates that the sound finished playing on its own
            bool finished = false;

            if (_audioFileReaders.TryGetValue(player, out AudioFileReader audioFileReader) && audioFileReader != null)
            {
                finished = audioFileReader.Position >= audioFileReader.Length;
                audioFileReader.Position = 0;
            }

            Stopped?.Invoke(this, new SoundStoppedEventArgs { Finished = finished, Exception = exception });
        }
    }
}
