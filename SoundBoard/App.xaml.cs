using MahApps.Metro.Controls.Dialogs;
using NLog;
using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using SoundBoard.Update;

namespace SoundBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Initialize();
            Log.LogStartup();

            // Elevated update helper mode: replace our own executable and exit without showing any UI.
            // See UpdateApplier for why this exists and how it is constrained.
            if (e.Args.Length > 0 && e.Args[0] == UpdateApplier.ApplyUpdateSwitch)
            {
                int exitCode = UpdateApplier.RunApplyUpdateMode(e.Args);
                Log.LogShutdownAndFlush(exitCode);
                Environment.Exit(exitCode);
            }

            UpdateApplier.CleanupBackup();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Logger.Fatal(args.ExceptionObject as Exception, "Unhandled exception in AppDomain (IsTerminating={0}): {1}",
                    args.IsTerminating, args.ExceptionObject);
                Log.Flush();

                // If there's an unhandled exception, check if it's because the target framework version is not installed
                if (Utilities.IsRequiredNetFrameworkInstalled() == false)
                {
                    TargetFrameworkAttribute targetFrameworkAttribute = Assembly.GetExecutingAssembly()
                        .GetCustomAttribute(typeof(TargetFrameworkAttribute)) as TargetFrameworkAttribute;

                    Logger.Fatal("Required .NET Framework ({0}) is not installed; exiting", targetFrameworkAttribute?.FrameworkDisplayName);

                    MessageBox.Show(string.Format(SoundBoard.Properties.Resources.TargetFrameworkNotInstalled, targetFrameworkAttribute?.FrameworkDisplayName),
                        SoundBoard.Properties.Resources.NetFrameworkError, MessageBoxButton.OK, MessageBoxImage.Error);

                    // Exit gracefully
                    Log.LogShutdownAndFlush(0);
                    Environment.Exit(0);
                }
            };

            // Exceptions thrown from async code that nobody awaits (e.g. Task.Run without a continuation) end up here
            // rather than in either of the handlers above, so without this they would vanish entirely.
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Logger.Error(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            // Handle global exceptions
            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Error(args.Exception, "Unhandled exception on the dispatcher thread");

                Dispatcher.Invoke(async () =>
                {
                    var res = await (MainWindow as MahApps.Metro.Controls.MetroWindow).ShowMessageAsync(SoundBoard.Properties.Resources.Error,
                        string.Join(Environment.NewLine, SoundBoard.Properties.Resources.UnexpectedError, string.Empty, args.Exception.Message),
                        MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings
                        {
                            AffirmativeButtonText = SoundBoard.Properties.Resources.CopyDetails,
                            NegativeButtonText = SoundBoard.Properties.Resources.OK
                        });

                    if (res == MessageDialogResult.Affirmative)
                    {
                        Clipboard.SetText(args.Exception.ToString());
                    }
                });

                // Don't crash.
                args.Handled = true;
            };

            base.OnStartup(e);
        }

        /// <inheritdoc />
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Log.LogShutdownAndFlush(e.ApplicationExitCode);
        }
    }
}
