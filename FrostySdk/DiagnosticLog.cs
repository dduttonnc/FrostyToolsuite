using FrostySdk.Interfaces;
using System;
using System.IO;
using System.Linq;

namespace FrostySdk
{
    // [DAI fix] Options > Logging > Diagnostic Logging: extra lines in the log, and the whole log copied to a file in Logs
    public static class DiagnosticLog
    {
        public static bool Enabled { get; private set; }
        public static string FilePath { get; private set; }

        private static readonly object s_lock = new object();
        private static ILogger s_logger;
        private static StreamWriter s_file;

        public static void Start(ILogger logger, string folder, string existingText)
        {
            string error = null;
            lock (s_lock)
            {
                s_logger = logger;
                if (Enabled)
                    return;

                Enabled = true;
                try
                {
                    Directory.CreateDirectory(folder);
                    FilePath = Path.Combine(folder, $"Diagnostics_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                    s_file = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                    s_file.Write(existingText ?? "");
                }
                catch (Exception e)
                {
                    s_file = null;
                    FilePath = null;
                    error = e.Message;
                }
            }

            Log((error == null) ? "Diagnostic logging is on. The whole log is also saved to " + FilePath : "Diagnostic logging is on, but the log file could not be created: " + error);
        }

        // [DAI fix] Options > Logging > Automatic Log Cleanup; only Frosty's own diagnostics files
        public static int DeleteOldFiles(string folder, int days)
        {
            if (!Directory.Exists(folder))
                return 0;

            DateTime cutoff = DateTime.Now.AddDays(-days);
            int deleted = 0;
            try
            {
                foreach (FileInfo old in new DirectoryInfo(folder).GetFiles("Diagnostics_*.txt").Where(f => f.LastWriteTime < cutoff))
                {
                    try { old.Delete(); deleted++; } catch { }
                }
            }
            catch { }
            return deleted;
        }

        public static void Stop()
        {
            lock (s_lock)
            {
                Enabled = false;
                s_file?.Dispose();
                s_file = null;
                FilePath = null;
            }
        }

        public static void Log(string text)
        {
            // passed as an argument, since the logger formats its text and paths or ids can contain braces
            if (Enabled)
                s_logger?.Log("[Diagnostics] {0}", text);
        }

        // the file only, for long lists that would flood the Log tab
        public static void Detail(string text)
        {
            if (Enabled)
                WriteLine("[" + DateTime.Now.ToLongTimeString() + "]: [Diagnostics] " + text);
        }

        public static void WriteLine(string line)
        {
            if (!Enabled)
                return;

            lock (s_lock)
            {
                try { s_file?.WriteLine(line); } catch { }
            }
        }

        public static string DescribeGameVersion(long version)
        {
            int index = Array.IndexOf(GameVersions.DragonAgeTheVeilguardSteam, (int)version);
            if (index >= 0)
                return $"{version} (Veilguard Steam, patch {index + 1})";

            index = Array.IndexOf(GameVersions.DragonAgeTheVeilguardEA, (int)version);
            if (index >= 0)
                return $"{version} (Veilguard EA app/Epic, patch {index + 1})";

            return version.ToString();
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1L << 30)
                return $"{bytes / (double)(1L << 30):0.00} GB";
            if (bytes >= 1L << 20)
                return $"{bytes / (double)(1L << 20):0.0} MB";
            return $"{bytes:N0} bytes";
        }

        public static string DescribeFile(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists)
                    return path + " (not found)";

                string link = fi.Attributes.HasFlag(FileAttributes.ReparsePoint) ? ", link" : "";
                return $"{path} ({Size(fi.Length)}, modified {fi.LastWriteTime:yyyy-MM-dd HH:mm}{link})";
            }
            catch (Exception e)
            {
                return $"{path} (could not read: {e.Message})";
            }
        }
    }
}
