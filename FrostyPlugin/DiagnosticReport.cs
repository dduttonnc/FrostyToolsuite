using FrostySdk;
using FrostySdk.Managers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace Frosty.Core
{
    // [DAI fix] what Diagnostic Logging adds about the PC, the game install and the launched game
    public static class DiagnosticReport
    {
        public static void LogProfile()
        {
            if (!DiagnosticLog.Enabled || !ProfilesLibrary.HasLoadedProfile || App.FileSystemManager == null)
                return;

            Run(LogSystem);
            Run(LogGame);
            Run(LogGameFolderFiles);
            Run(() => LogCacheFile("Cache"));
            Run(LogPlugins);
            Run(LogSettings);
            Run(LogModDataFolders);
        }

        public static void LogCacheFile(string label)
        {
            if (!DiagnosticLog.Enabled || !ProfilesLibrary.HasLoadedProfile)
                return;

            string path = "Caches/" + ProfilesLibrary.CacheName + ".cache";
            string builtFor = "";
            if (File.Exists(path))
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    // magic, version, profile hash, then the game version it was built from
                    if (reader.BaseStream.Length >= 20)
                    {
                        reader.BaseStream.Position = 8;
                        uint version = reader.ReadUInt32();
                        reader.ReadInt32();
                        builtFor = $", format {version}, built for game version {DiagnosticLog.DescribeGameVersion(reader.ReadUInt32())}";
                    }
                }
            }
            DiagnosticLog.Log($"{label}: {DiagnosticLog.DescribeFile(Path.GetFullPath(path))}{builtFor}");
        }

        public static void WatchGame()
        {
            if (!DiagnosticLog.Enabled || !ProfilesLibrary.HasLoadedProfile)
                return;

            string name = ProfilesLibrary.ProfileName;
            new Thread(() => Run(() => Watch(name))) { IsBackground = true, Name = "Diagnostics game watcher" }.Start();
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                DiagnosticLog.Log($"({action.Method.Name} failed: {e.Message})");
            }
        }

        private static void LogSystem()
        {
            DiagnosticLog.Log($"Frosty {App.Version}, running from {AppDomain.CurrentDomain.BaseDirectory} ({DescribeDrive(AppDomain.CurrentDomain.BaseDirectory)})");

            string windows = Environment.OSVersion.VersionString;
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (key != null)
                    windows = $"{key.GetValue("ProductName")} {key.GetValue("DisplayVersion") ?? key.GetValue("ReleaseId")} (build {key.GetValue("CurrentBuild")}.{key.GetValue("UBR")})";
            }

            string memory = "unknown";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementBaseObject obj in searcher.Get())
                    memory = DiagnosticLog.Size(Convert.ToInt64(obj["TotalPhysicalMemory"]));
            }

            bool admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            DiagnosticLog.Log($"Windows: {windows}, {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit, {Environment.ProcessorCount} logical processors, {memory} RAM, Frosty running as administrator: {(admin ? "yes" : "no")}");
        }

        private static void LogGame()
        {
            FileSystemManager fs = App.FileSystemManager;
            DiagnosticLog.Log($"Profile: {ProfilesLibrary.ProfileName} ({ProfilesLibrary.DisplayName}), cache revision {DataVersions.CacheRevision}, ModData version {DataVersions.ModDataVersion}");
            DiagnosticLog.Log($"Game folder: {fs.BasePath} ({DescribeDrive(fs.BasePath)})");

            string exe = Path.Combine(fs.BasePath, ProfilesLibrary.ProfileName + ".exe");
            string exeVersion = File.Exists(exe) ? ", file version " + FileVersionInfo.GetVersionInfo(exe).FileVersion : "";
            DiagnosticLog.Log($"Game exe: {DiagnosticLog.DescribeFile(exe)}{exeVersion}");

            string known = "";
            if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeTheVeilguard) && !GameVersions.DragonAgeTheVeilguardSteam.Contains((int)fs.Head) && !GameVersions.DragonAgeTheVeilguardEA.Contains((int)fs.Head))
                known = " - not a Veilguard version this Frosty knows";
            DiagnosticLog.Log($"Game version (from layout.toc): {DiagnosticLog.DescribeGameVersion(fs.Head)}{known}");

            uint sdkVersion = TypeLibrary.GetSdkVersion();
            DiagnosticLog.Log($"SDK: {DiagnosticLog.DescribeFile(Path.GetFullPath("Profiles/" + ProfilesLibrary.SDKFilename + ".dll"))}, made for game version {DiagnosticLog.DescribeGameVersion(sdkVersion)} ({(sdkVersion == fs.Head ? "matches" : "DOES NOT match")} the game)");

            if (ProfilesLibrary.RequiresKey)
                DiagnosticLog.Log($"Encryption key file: {DiagnosticLog.DescribeFile(Path.GetFullPath(ProfilesLibrary.CacheName + ".key"))}");

            List<string> sources = new List<string>();
            foreach (FileSystemSource source in ProfilesLibrary.Sources)
                sources.Add(source.Path + (Directory.Exists(fs.BasePath + source.Path) ? "" : " (missing)"));
            DiagnosticLog.Log($"Data folders: {string.Join(", ", sources)}; {fs.SuperBundleCount} superbundles, {fs.Catalogs.Count()} catalogs");

            string[] markers = { "steam_appid.txt", "steam_api64.dll", ".egstore", "EOSSDK-Win64-Shipping.dll", "__Installer", "CryptBase.dll", "bcrypt.dll", "d3d11.dll", "dxgi.dll", "dinput8.dll", "version.dll", "DAVE.asi", "DAVExtender", ProfilesLibrary.ProfileName + ".orig.exe", "DatapathFix.exe", "tmp" };
            List<string> found = markers.Where(m => File.Exists(Path.Combine(fs.BasePath, m)) || Directory.Exists(Path.Combine(fs.BasePath, m))).ToList();
            string steamAppId = Path.Combine(fs.BasePath, "steam_appid.txt");
            if (File.Exists(steamAppId))
                found[found.IndexOf("steam_appid.txt")] = $"steam_appid.txt ({File.ReadLines(steamAppId).FirstOrDefault()?.Trim()})";
            DiagnosticLog.Log($"Platform and loader files in the game folder: {(found.Count > 0 ? string.Join(", ", found) : "none")}");
        }

        private static void LogGameFolderFiles()
        {
            string basePath = App.FileSystemManager.BasePath;
            string[] extensions = { ".dll", ".asi", ".exe", ".ini", ".cfg", ".txt" };
            IEnumerable<string> files = new DirectoryInfo(basePath).GetFiles()
                .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()))
                .OrderBy(f => f.Name)
                .Select(f => $"{f.Name} ({DiagnosticLog.Size(f.Length)}, {f.LastWriteTime:yyyy-MM-dd})");
            DiagnosticLog.Log($"Game folder files: {string.Join(", ", files)}");
        }

        private static void LogPlugins()
        {
            DiagnosticLog.Log($"Plugins loaded for this game: {string.Join(", ", App.PluginManager.LoadedPlugins.Select(p => $"{p.Name} v{p.Version} ({Path.GetFileName(p.Assembly.Location)})"))}");
            DiagnosticLog.Log($"Launch actions: {string.Join(", ", App.PluginManager.ExecutionActions.Select(a => a.GetType().FullName).DefaultIfEmpty("none"))}");
        }

        private static void LogSettings()
        {
            string[] global = { "WorkerThreads", "WorkerPriority", "MaxCasFileSize", "UseSteamProtocol", "DatapathFixEnabled", "DisableLaunchProcessCheck",
                "DisableShaderCacheSymLink", "UseAltSymLink", "PrintLog", "UseCustomModsDirectory", "CustomModsDirectory", "ApplyModOrder", "ProgressView" };
            string[] game = { "CommandLineArgs", "PlatformLaunchingEnabled", "Platform", "ShouldRelaunchPlatform" };

            IEnumerable<string> values = global.Select(k => $"{k}={Config.Get<object>(k, null) ?? "(default)"}")
                .Concat(game.Select(k => $"{k}={Config.Get<object>(k, null, ConfigScope.Game) ?? "(default)"}"));
            DiagnosticLog.Log($"Settings ({Path.GetFullPath(App.GlobalSettingsPath + "/manager_config.json")}): {string.Join(", ", values)}");
        }

        private static void LogModDataFolders()
        {
            string modData = Path.Combine(App.FileSystemManager.BasePath, "ModData");
            if (!Directory.Exists(modData))
            {
                DiagnosticLog.Log($"ModData: {modData} doesn't exist yet");
                return;
            }

            foreach (DirectoryInfo pack in new DirectoryInfo(modData).GetDirectories())
            {
                // where FrostyModExecutor puts them: Update\Patch\Data (DAI and other old games), Data (Veilguard), Patch (MEA and the rest)
                string[] candidates = { Path.Combine(pack.FullName, "Update", "Patch", "Data"), Path.Combine(pack.FullName, "Data"), Path.Combine(pack.FullName, "Patch") };
                string dataPath = candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "mods.json"))) ?? candidates[0];
                string modsJson = Path.Combine(dataPath, "mods.json");
                string version = Path.Combine(dataPath, "moddata_version.txt");
                DiagnosticLog.Log($"ModData pack '{pack.Name}': {pack.FullName}, made {pack.LastWriteTime:yyyy-MM-dd HH:mm}, mods.json {(File.Exists(modsJson) ? "found" : "missing")}, moddata_version {(File.Exists(version) ? File.ReadAllText(version).Trim() : "missing")}");
            }
        }

        private static string DescribeDrive(string path)
        {
            try
            {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)));
                return $"drive {drive.Name} {drive.DriveFormat}, {DiagnosticLog.Size(drive.AvailableFreeSpace)} free";
            }
            catch (Exception e)
            {
                // e.g. a network (UNC) path, which DriveInfo doesn't accept
                return "drive unknown: " + e.Message;
            }
        }

        private const uint Synchronize = 0x00100000;
        private const uint QueryLimitedInformation = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool GetExitCodeProcess(IntPtr handle, out uint exitCode);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // follows the game, and any copy that relaunches it (Steam or the EA app can restart it)
        private static void Watch(string name)
        {
            TimeSpan wait = TimeSpan.FromSeconds(90);
            HashSet<int> seen = new HashSet<int>();
            int running = 0;
            long lastChange = Stopwatch.GetTimestamp();

            while (seen.Count < 10)
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        if (!seen.Add(process.Id))
                            continue;

                        int processId = process.Id;
                        IntPtr handle = OpenProcess(Synchronize | QueryLimitedInformation, false, processId);
                        int openError = Marshal.GetLastWin32Error();
                        DateTime started;
                        try { started = process.StartTime; } catch { started = DateTime.Now; }

                        Interlocked.Increment(ref running);
                        new Thread(() =>
                        {
                            Run(() => WatchProcess(name, processId, handle, openError, started));
                            Interlocked.Exchange(ref lastChange, Stopwatch.GetTimestamp());
                            Interlocked.Decrement(ref running);
                        }) { IsBackground = true, Name = "Diagnostics game watcher" }.Start();
                    }
                }

                double idleSeconds = (Stopwatch.GetTimestamp() - Interlocked.Read(ref lastChange)) / (double)Stopwatch.Frequency;
                if (Volatile.Read(ref running) == 0 && idleSeconds > wait.TotalSeconds)
                    break;
                Thread.Sleep(250);
            }

            DiagnosticLog.Log((seen.Count == 0) ? $"{name}.exe wasn't seen running within {wait.TotalSeconds:0}s of the launch" : $"No other {name}.exe started within {wait.TotalSeconds:0}s of the last one closing");
        }

        private static void WatchProcess(string name, int processId, IntPtr handle, int openError, DateTime started)
        {
            DiagnosticLog.Log($"Game process {processId} started at {started:HH:mm:ss}, command line: {GetCommandLine(processId) ?? "(couldn't be read)"}");

            string exit;
            if (handle != IntPtr.Zero)
            {
                WaitForSingleObject(handle, uint.MaxValue);
                exit = GetExitCodeProcess(handle, out uint code) ? DescribeExitCode(code) : "an unknown exit code";
                CloseHandle(handle);
            }
            else
            {
                while (IsRunning(processId))
                    Thread.Sleep(1000);
                exit = $"an unknown exit code (Windows error {openError} opening the process)";
            }

            DiagnosticLog.Log($"Game process {processId} exited after {(DateTime.Now - started).TotalSeconds:0}s with {exit}");
            if (!exit.StartsWith("exit code 0x00000000"))
                LogCrashEvents(name + ".exe", started.AddSeconds(-5));
        }

        private static bool IsRunning(int processId)
        {
            try
            {
                using (Process.GetProcessById(processId))
                    return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string GetCommandLine(int processId)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (ManagementBaseObject obj in searcher.Get())
                        return obj["CommandLine"] as string;
                }
            }
            catch
            {
            }
            return null;
        }

        private static string DescribeExitCode(uint code)
        {
            string meaning;
            switch (code)
            {
                case 0: meaning = "normal exit"; break;
                case 0xC0000005: meaning = "access violation, a crash"; break;
                case 0xC0000409: meaning = "stack buffer overrun or fail-fast, a crash"; break;
                case 0xC0000374: meaning = "heap corruption, a crash"; break;
                case 0xC000001D: meaning = "illegal instruction, a crash"; break;
                case 0xC0000135: meaning = "a DLL was not found"; break;
                case 0xC000007B: meaning = "a 32/64-bit DLL mismatch"; break;
                case 0xC0000142: meaning = "a DLL failed to initialize"; break;
                case 0x80000003: meaning = "breakpoint, a crash"; break;
                case 0xE06D7363: meaning = "unhandled C++ exception, a crash"; break;
                case 0x40010004: meaning = "closed by Windows or another program"; break;
                default: meaning = null; break;
            }
            return $"exit code 0x{code:X8}" + ((meaning != null) ? $" ({meaning})" : "");
        }

        // Windows Error Reporting names the module that crashed
        private static void LogCrashEvents(string exeName, DateTime since)
        {
            string[] sources = { "Application Error", "Application Hang", "Windows Error Reporting" };
            Stopwatch timer = Stopwatch.StartNew();
            bool found = false;

            // the report can take a few seconds to appear
            while (!found && timer.Elapsed < TimeSpan.FromSeconds(20))
            {
                Thread.Sleep(3000);
                using (EventLog log = new EventLog("Application"))
                {
                    EventLogEntryCollection entries = log.Entries;
                    for (int i = entries.Count - 1; i >= 0 && i >= entries.Count - 500; i--)
                    {
                        EventLogEntry entry = entries[i];
                        if (entry.TimeGenerated < since)
                            break;
                        if (!sources.Contains(entry.Source) || entry.Message.IndexOf(exeName, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string message = string.Join(" | ", entry.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
                        DiagnosticLog.Log($"Windows event log ({entry.Source}, {entry.TimeGenerated:HH:mm:ss}): {((message.Length > 1500) ? message.Substring(0, 1500) + "..." : message)}");
                        found = true;
                    }
                }
            }

            if (!found)
                DiagnosticLog.Log("No crash report for the game in the Windows event log");
        }
    }
}
