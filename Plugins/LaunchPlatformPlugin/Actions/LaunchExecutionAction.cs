using Frosty.Core;
using Frosty.ModSupport;
using FrostySdk;
using FrostySdk.Interfaces;
using LaunchPlatformPlugin.Options;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LaunchPlatformPlugin.Actions
{
    public abstract class Platform
    {
        /// <summary>
        /// The installation path of the platform.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The name of the process when opened.
        /// </summary>
        public abstract string ProcessName { get; }

        /// <summary>
        /// The registry key for the platform, used to get the installation path.
        /// </summary>
        public abstract string RegistryKey { get; }

        /// <summary>
        /// The registry value for the platform, used to get the installation path.
        /// </summary>
        public abstract string RegistryValue { get; }

        /// <summary>
        /// Appended onto the registry path if it doesn't include the direct executable path.
        /// </summary>
        public abstract string AdditionalExecutablePath { get; }
    }

    public class OriginPlatform : Platform
    {
        public override string ProcessName => "Origin";
        public override string RegistryKey => "Electronic Arts\\EA Core";
        public override string RegistryValue => "EADM6InstallDir";
        public override string AdditionalExecutablePath => "\\Origin.exe";
    }
    public class EADesktopPlatform : Platform
    {
        public override string ProcessName => "EADesktop";
        public override string RegistryKey => "Electronic Arts\\EA Desktop";
        public override string RegistryValue => "DesktopAppPath";
        public override string AdditionalExecutablePath => "";
    }
    public class SteamPlatform : Platform
    {
        public override string ProcessName => "steam";
        public override string RegistryKey => "Valve\\Steam";
        public override string RegistryValue => "InstallPath";
        public override string AdditionalExecutablePath => "\\steam.exe";
    }
    public class EpicGamesPlatform : Platform
    {
        public override string ProcessName => "EpicGamesLauncher";
        public override string RegistryKey => "EpicGames\\Unreal Engine";
        public override string RegistryValue => "INSTALLDIR";
        public override string AdditionalExecutablePath => "\\Launcher\\Portal\\Binaries\\Win32\\EpicGamesLauncher.exe";
    }

    public class LaunchExecutionAction : ExecutionAction
    {

        private readonly Dictionary<LaunchPlatform, Platform> m_platforms = new Dictionary<LaunchPlatform, Platform>
        {
            { LaunchPlatform.Origin, new OriginPlatform() },
            { LaunchPlatform.EADesktop, new EADesktopPlatform() },
            { LaunchPlatform.Steam, new SteamPlatform() },
            { LaunchPlatform.EpicGamesLauncher, new EpicGamesPlatform() },
        };
        private Platform m_selectedPlatform;
        private bool m_shouldRelaunchPlatform = false;
        private int m_launchId;

        // the EA app or Steam can relaunch the game after its first process closes, once the EA app has started and signed in
        private const int RelaunchWaitSeconds = 90;
        private const int ClosedWaitSeconds = 10;
        private const int PlayedSeconds = 30;

        public override Action<ILogger, PluginManagerType, CancellationToken> PreLaunchAction => new Action<ILogger, PluginManagerType, CancellationToken>((ILogger logger, PluginManagerType type, CancellationToken cancelToken) =>
        {
            // only run platform launch system when PlatformLaunchingEnabled is true
            if (Config.Get("PlatformLaunchingEnabled", false, ConfigScope.Game))
            {
                if (IsDatapathFixEnabled())
                {
                    App.Logger.LogWarning("Launch Platform skipped: DatapathFix is enabled for this game, and the two can't be used together");
                    return;
                }

                Interlocked.Increment(ref m_launchId);

                var platform = (LaunchPlatform)Enum.Parse(typeof(LaunchPlatform), Config.Get("Platform", "Origin", ConfigScope.Game));
                m_selectedPlatform = m_platforms[platform];

                // get platform path
                m_selectedPlatform.Path = FindPlatformPath(m_selectedPlatform.RegistryKey, m_selectedPlatform.RegistryValue) + m_selectedPlatform.AdditionalExecutablePath;

                // check if platform is already launched
                logger.Log("Checking if platform is executing");
                Process process = GetProcessByName(m_platforms[platform].ProcessName);
                m_shouldRelaunchPlatform = Config.Get("ShouldRelaunchPlatform", false, ConfigScope.Game) || process != null;

                // kill all the other platform processes
                KillPlatformProcesses();

                logger.Log("Executing platform with custom enviroment");
                // origin doesn't need these modifications to launch with mods
                if (platform != LaunchPlatform.Origin)
                {
                    // run platfrom with the needed environmental variables
                    string modPath = $"ModData\\{App.SelectedPack}";
                    // [DAI fix] App.FileSystem is now App.FileSystemManager; the 1.0.6.3 DLL crashed on the old name
                    RunPlatformProcess(m_selectedPlatform.Path, App.FileSystemManager.BasePath + modPath);
                }
                else
                {
                    // run origin like normal
                    FrostyModExecutor.ExecuteProcess(m_selectedPlatform.Path, "");
                }

                logger.Log("Waiting for platform");
                try
                {
                    WaitForProcess(m_selectedPlatform.ProcessName, cancelToken, true, logger);
                }
                catch (OperationCanceledException)
                {
                }
            }
        });

        public override Action<ILogger, PluginManagerType, CancellationToken> PostLaunchAction => new Action<ILogger, PluginManagerType, CancellationToken>((ILogger logger, PluginManagerType type, CancellationToken cancelToken) =>
        {
            // only run platform launch system when PlatformLaunchingEnabled is true
            if (Config.Get("PlatformLaunchingEnabled", false, ConfigScope.Game) && !IsDatapathFixEnabled())
            {
                var platform = (LaunchPlatform)Enum.Parse(typeof(LaunchPlatform), Config.Get("Platform", "Origin", ConfigScope.Game));

                // origin doesn't need these modifications to launch with mods
                if (platform != LaunchPlatform.Origin)
                {
                    logger.Log("Waiting for game to launch");

                    // find game process to check when it closes
                    try
                    {
                        Process gameProcess = null;

                        // needed to weed out false launch by Steam
                        while (gameProcess == null || gameProcess.HasExited)
                        {
                            gameProcess = WaitForProcess(ProfilesLibrary.ProfileName, cancelToken, false, logger);
                        }

                        // [DAI fix] watching only this process closed Steam and the EA app as soon as it handed the launch over to them
                        int launchId = Volatile.Read(ref m_launchId);
                        string gameName = ProfilesLibrary.ProfileName;
                        new Thread(() =>
                        {
                            try
                            {
                                CloseLaunchersAfterGame(gameName, launchId);
                            }
                            catch (Exception e)
                            {
                                // an exception on this thread would close Frosty
                                App.Logger.LogWarning("Launch Platform couldn't close the launchers: {0}", e.Message);
                            }
                        }) { IsBackground = true, Name = "Launch Platform game watcher" }.Start();

                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        });

        // [DAI fix] DatapathFix relaunches the game through its stub exe; closing the launchers when the stub exits breaks that relaunch
        private static bool IsDatapathFixEnabled()
        {
            return Config.Get("DatapathFixEnabled", false)
                && App.PluginManager.ExecutionActions.Any(a => a.GetType().FullName == "DatapathFixPlugin.Actions.LaunchExecutionAction");
        }

        // Closes and relaunches platform process without the environmental variables, once no copy of the game is running
        private void CloseLaunchersAfterGame(string gameName, int launchId)
        {
            Stopwatch notRunning = Stopwatch.StartNew();
            Stopwatch running = new Stopwatch();
            bool played = false;

            while (notRunning.Elapsed.TotalSeconds < (played ? ClosedWaitSeconds : RelaunchWaitSeconds))
            {
                Thread.Sleep(1000);

                // a newer launch has taken over the launchers
                if (launchId != Volatile.Read(ref m_launchId))
                    return;

                if (IsGameRunning(gameName))
                {
                    notRunning.Restart();
                    running.Start();
                    played |= running.Elapsed.TotalSeconds >= PlayedSeconds;
                }
                else
                {
                    running.Reset();
                }
            }

            DiagnosticLog.Log($"Launch Platform: {gameName} closed, closing the launchers{(m_shouldRelaunchPlatform ? " and starting " + m_selectedPlatform.ProcessName + " again" : "")}");
            KillPlatformProcesses();
            if (m_shouldRelaunchPlatform)
            {
                FrostyModExecutor.ExecuteProcess(m_selectedPlatform.Path, "");
            }
        }

        private static bool IsGameRunning(string name)
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.ProcessName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return false;
        }

        private void RunPlatformProcess(string platformPath, string gamePath)
        {
            // add environmental variables
            Dictionary<string, string> env = new Dictionary<string, string>();
            env.Add("GAME_DATA_DIR", gamePath);

            // open platform process
            FrostyModExecutor.ExecuteProcess(platformPath, "", false, false, env);
        }

        private string FindPlatformPath(string key, string value)
        {
            using (RegistryKey lmKey = Registry.LocalMachine.OpenSubKey($"SOFTWARE\\WOW6432Node\\{key}"))
            {
                return lmKey != null ? (string)lmKey.GetValue(value) : null;
            }
        }

        private Process GetProcessByName(string name)
        {
            Process[] processes = Process.GetProcessesByName(name);
            if (processes.Length > 0)
            {
                return processes[0];
            }
            else
            {
                return null;
            }
        }

        private Process WaitForProcess(string name, CancellationToken cancelToken, bool exactMatch = false, ILogger logger = null)
        {
            Process foundProcess = null;
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        cancelToken.ThrowIfCancellationRequested();

                        if (process.ProcessName.StartsWith(name, StringComparison.OrdinalIgnoreCase) &&
                            (!exactMatch || process.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            foundProcess = process;
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (foundProcess != null)
                {
                    logger?.Log($"Waiting for {foundProcess.ProcessName}");
                    try
                    {
                        foundProcess.WaitForInputIdle();
                    }
                    catch (InvalidOperationException)
                    {
                        // not a graphic UI
                    }

                    return foundProcess;
                }
            }
        }

        private void KillPlatformProcess(LaunchPlatform platform)
        {
            Process platformProcess = GetProcessByName(m_platforms[platform].ProcessName);
            if (platformProcess != null)
            {
                platformProcess.Kill();
            }
        }

        private void KillPlatformProcesses()
        {
            KillPlatformProcess(LaunchPlatform.Origin);
            KillPlatformProcess(LaunchPlatform.Steam);
            KillPlatformProcess(LaunchPlatform.EpicGamesLauncher);
            KillPlatformProcess(LaunchPlatform.EADesktop);
        }
    }
}
