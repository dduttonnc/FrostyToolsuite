using FrostySdk.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Frosty.Core
{
    // [DAI fix] DAI Mod Manager's merged patch reports one version higher, and saves made with it won't load at the real version
    public static class DaiPatchVersion
    {
        public const string VersionKey = "DaiPatchVersion";
        public const string AskKey = "DaiPatchVersionAsk";

        // save header key whose value is the patch version as text
        private static readonly byte[] s_saveVersionKey = { 0x03, 0x46, 0xEA, 0xF1 };

        public static string SaveFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BioWare", "Dragon Age Inquisition", "Save");

        public static int GetGamePatchVersion(string gamePath)
        {
            string path = Path.Combine(gamePath, "Update", "Patch", "package.mft");
            return File.Exists(path) ? ReadManifestVersion(File.ReadAllLines(path)) : -1;
        }

        public static int GetChosenVersion(int gamePatch) => Math.Max(gamePatch, Config.Get<int>(VersionKey, 0, ConfigScope.Game));

        // patch version -> number of saves made at it
        public static SortedDictionary<int, int> ScanSaves()
        {
            SortedDictionary<int, int> counts = new SortedDictionary<int, int>();
            if (!Directory.Exists(SaveFolder))
                return counts;

            foreach (string file in Directory.EnumerateFiles(SaveFolder, "*.DAS"))
            {
                int version = ReadSaveVersion(file);
                if (version > 0)
                {
                    counts.TryGetValue(version, out int count);
                    counts[version] = count + 1;
                }
            }
            return counts;
        }

        public static void WriteModDataManifest(string gamePath, string modDataPath, ILogger logger)
        {
            string source = Path.Combine(gamePath, "Update", "Patch", "package.mft");
            string dest = Path.Combine(modDataPath, "Update", "Patch", "package.mft");
            if (!File.Exists(source))
                return;

            string[] lines = File.ReadAllLines(source);
            int gamePatch = ReadManifestVersion(lines);
            if (gamePatch < 0)
            {
                WriteIfDifferent(string.Join("\r\n", lines) + "\r\n", dest);
                return;
            }

            int chosen = GetChosenVersion(gamePatch);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("Version ", StringComparison.OrdinalIgnoreCase))
                    lines[i] = "Version " + chosen;
            }

            WriteIfDifferent(string.Join("\r\n", lines) + "\r\n", dest);
            if (chosen > gamePatch)
                logger.Log($"Reporting patch {chosen} to the game (real patch {gamePatch}) so saves made with DAI Mod Manager load");
        }

        private static void WriteIfDifferent(string contents, string dest)
        {
            if (File.Exists(dest) && File.ReadAllText(dest) == contents)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.WriteAllText(dest, contents);
        }

        private static int ReadManifestVersion(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                string[] parts = line.Trim().Split(new[] { ' ' }, 2);
                if (parts.Length == 2 && parts[0].Equals("Version", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1].Trim(), out int version))
                    return version;
            }
            return -1;
        }

        private static int ReadSaveVersion(string file)
        {
            byte[] header = new byte[8192];
            int read;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    read = stream.Read(header, 0, header.Length);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return -1;
            }

            for (int i = 0; i + 6 <= read; i++)
            {
                if (header[i] != s_saveVersionKey[0] || header[i + 1] != s_saveVersionKey[1] || header[i + 2] != s_saveVersionKey[2] || header[i + 3] != s_saveVersionKey[3])
                    continue;

                int length = (header[i + 4] << 8) | header[i + 5];
                if (length == 0 || length > 8 || i + 6 + length > read)
                    return -1;

                return int.TryParse(Encoding.ASCII.GetString(header, i + 6, length), out int version) ? version : -1;
            }
            return -1;
        }
    }
}
