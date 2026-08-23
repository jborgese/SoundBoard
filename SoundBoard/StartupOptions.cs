using System;
using NLog;

namespace SoundBoard
{
    /// <summary>
    /// The command-line switches SoundBoard understands at startup, and the parsing of them.
    /// </summary>
    /// <remarks>
    /// Hand-rolled rather than pulling in an argument parser: there are two switches, and the other one
    /// (<see cref="Update.UpdateApplier.ApplyUpdateSwitch"/>) is dealt with in <c>App</c> before this ever runs.
    /// </remarks>
    internal static class StartupOptions
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Names the configuration file to use instead of the one under <c>%AppData%</c>. Accepted either as
        /// <c>--config &lt;path&gt;</c> or as <c>--config=&lt;path&gt;</c>.
        /// </summary>
        public const string ConfigSwitch = @"--config";

        /// <summary>
        /// Applies the switches in <paramref name="args"/> to the running process.
        /// </summary>
        /// <returns>
        /// An error to show the user and exit on, or <see langword="null"/> when the command line was usable.
        /// </returns>
        /// <remarks>
        /// A bad <see cref="ConfigSwitch"/> is an error rather than something to shrug off and carry on from. Falling
        /// back to the default config would silently read and write the user's real board when they had explicitly
        /// asked for a different one, which is the accident the switch exists to prevent.
        /// </remarks>
        public static string Apply(string[] args)
        {
            if (args is null)
            {
                return null;
            }

            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];
                string path;

                if (arg == ConfigSwitch)
                {
                    // The value is the next argument, which has to actually be there and not be the next switch
                    if (i + 1 >= args.Length || args[i + 1].StartsWith(@"--", StringComparison.Ordinal))
                    {
                        return string.Format(Properties.Resources.ConfigSwitchNeedsAPath, ConfigSwitch);
                    }

                    path = args[++i];
                }
                else if (arg != null && arg.StartsWith(ConfigSwitch + @"=", StringComparison.Ordinal))
                {
                    path = arg.Substring(ConfigSwitch.Length + 1);

                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return string.Format(Properties.Resources.ConfigSwitchNeedsAPath, ConfigSwitch);
                    }
                }
                else
                {
                    // Unknown arguments are ignored rather than rejected: the app is also started by the shell with
                    // file arguments, and by the updater with switches of its own.
                    continue;
                }

                try
                {
                    ConfigStore.UseConfigFile(path);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is System.IO.PathTooLongException)
                {
                    Logger.Error(ex, "Unusable path given with {0}: {1}", ConfigSwitch, path);
                    return string.Format(Properties.Resources.ConfigSwitchPathIsNotUsable, ConfigSwitch, ex.Message);
                }

                Logger.Info("{0} given; using the configuration at {1}", ConfigSwitch, ConfigStore.ConfigFilePath);
            }

            return null;
        }
    }
}
