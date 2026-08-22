using System.Collections.Generic;
using System.Linq;

namespace SoundBoard.Audio
{
    /// <summary>
    /// Tracks every <see cref="SoundPlayer"/> that has been started so that they can all be stopped at once
    /// ("silence", and the stop-all-sounds option on a sound).
    /// </summary>
    internal sealed class PlaybackCoordinator
    {
        private readonly HashSet<SoundPlayer> _players = new HashSet<SoundPlayer>();

        /// <summary>
        /// Registers a player. Registering twice is harmless.
        /// </summary>
        public void Register(SoundPlayer player) => _players.Add(player);

        /// <summary>
        /// Forgets a player (it is not stopped).
        /// </summary>
        public void Unregister(SoundPlayer player) => _players.Remove(player);

        /// <summary>
        /// Stops every registered player.
        /// </summary>
        public void StopAll()
        {
            foreach (SoundPlayer player in _players.ToList())
            {
                player.Stop();
            }
        }
    }
}
