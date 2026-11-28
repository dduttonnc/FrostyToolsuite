using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Mod;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Frosty.ModSupport
{
    public partial class FrostyModExecutor
    {
        private class ArchiveInfo
        {
            public byte[] Data;
            public int RefCount;
        }
        private class ModBundleInfo
        {
            public class ModBundleAction
            {
                public HashSet<string> Ebx = new HashSet<string>();
                public HashSet<string> Res = new HashSet<string>();
                public HashSet<Guid> Chunks = new HashSet<Guid>();

                public void AddEbx(string name)
                {
                    lock (Ebx)
                    {
                        Ebx.Add(name);
                    }
                }
                public void AddRes(string name)
                {
                    lock (Res)
                    {
                        Res.Add(name);
                    }
                }
                public void AddChunk(Guid guid)
                {
                    lock (Chunks)
                    {
                        Chunks.Add(guid);
                    }
                }
            }
            public int Name;
            public ModBundleAction Add = new ModBundleAction();
            public ModBundleAction Remove = new ModBundleAction();
            public ModBundleAction Modify = new ModBundleAction();
        }
        private class CasFileEntry
        {
            public ManifestFileInfo FileInfo;
            public ChunkAssetEntry Entry;
        }
        private class CasDataEntry
        {
            public string Catalog { get; }
            public bool HasEntries => dataRefs.Count != 0;

            private HashSet<Sha1> dataRefs = new HashSet<Sha1>();
            private Dictionary<Sha1, List<CasFileEntry>> fileInfos = new Dictionary<Sha1, List<CasFileEntry>>();

            public CasDataEntry(string inCatalog, params Sha1[] sha1)
            {
                Catalog = inCatalog;
                if (sha1.Length != 0)
                    dataRefs.UnionWith(sha1);
            }

            public void Add(Sha1 sha1, ChunkAssetEntry entry = null, ManifestFileInfo file = null)
            {
                if (!dataRefs.Contains(sha1))
                {
                    dataRefs.Add(sha1);
                    fileInfos.Add(sha1, new List<CasFileEntry>());
                }

                if (entry != null || file != null)
                    fileInfos[sha1].Add(new CasFileEntry() { Entry = entry, FileInfo = file });
            }

            public bool Contains(Sha1 sha1) => dataRefs.Contains(sha1);

            public IEnumerable<Sha1> EnumerateDataRefs()
            {
                foreach (Sha1 sha1 in dataRefs)
                    yield return sha1;
            }

            public IEnumerable<CasFileEntry> EnumerateFileInfos(Sha1 sha1)
            {
                if (!dataRefs.Contains(sha1))
                    yield break;
                if (!fileInfos.ContainsKey(sha1))
                    yield break;
                foreach (CasFileEntry fi in fileInfos[sha1])
                    yield return fi;
            }
        }
        private class CasDataInfo
        {
            private Dictionary<string, CasDataEntry> entries = new Dictionary<string, CasDataEntry>();
            public void Add(string catalog, Sha1 sha1, ChunkAssetEntry entry = null, ManifestFileInfo file = null)
            {
                if (!entries.ContainsKey(catalog))
                {
                    entries.Add(catalog, new CasDataEntry(catalog));
                }
                entries[catalog].Add(sha1, entry, file);
            }

            public IEnumerable<CasDataEntry> EnumerateEntries()
            {
                foreach (CasDataEntry entry in entries.Values)
                    yield return entry;
            }

            public int CountEntries()
            {
                return entries.Count;
            }
        }

        [JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
        private class ModInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Category { get; set; }
            public string Link { get; set; }
            public string FileName { get; set; }
            public string Hash { get; set; }


            public override bool Equals(object obj)
            {
                if (obj is ModInfo other)
                {
                    return Equals(other);
                }

                return false;
            }

            public bool Equals(ModInfo other)
            {
                return Name == other.Name && Version == other.Version && Category == other.Category && FileName == other.FileName;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Name != null ? Name.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Category != null ? Category.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private struct SymLinkStruct
        {
            public readonly string dest;
            public readonly string src;
            public readonly bool isFolder;

            public SymLinkStruct(string inDst, string inSrc, bool inFolder)
            {
                dest = inDst;
                src = inSrc;
                isFolder = inFolder;
            }
        };

        private FileSystemManager m_fs;
        private ResourceManager m_rm;
        private AssetManager m_am;
        private ILogger m_logger;

        private List<string> m_addedSuperBundles = new List<string>();

        private ConcurrentDictionary<int, ModBundleInfo> m_modifiedSuperBundles = new ConcurrentDictionary<int, ModBundleInfo>();
        private ConcurrentDictionary<int, ModBundleInfo> m_modifiedBundles = new ConcurrentDictionary<int, ModBundleInfo>();
        private ConcurrentDictionary<int, HashSet<string>> m_addedBundles = new ConcurrentDictionary<int, HashSet<string>>();

        private ConcurrentDictionary<string, EbxAssetEntry> m_modifiedEbx = new ConcurrentDictionary<string, EbxAssetEntry>();
        private ConcurrentDictionary<string, ResAssetEntry> m_modifiedRes = new ConcurrentDictionary<string, ResAssetEntry>();
        private ConcurrentDictionary<Guid, ChunkAssetEntry> m_modifiedChunks = new ConcurrentDictionary<Guid, ChunkAssetEntry>();
        private ConcurrentDictionary<string, DbObject> m_modifiedFs = new ConcurrentDictionary<string, DbObject>();

        private ConcurrentDictionary<Sha1, ArchiveInfo> m_archiveData = new ConcurrentDictionary<Sha1, ArchiveInfo>();
        private int m_numArchiveEntries = 0;
        private int m_numTasks;

        private CasDataInfo m_casData = new CasDataInfo();
        private static int s_chunksBundleHash = Fnv1.HashString("chunks"); // should not be used, only here to ignore old mods adding assets to it
        private Dictionary<int, Dictionary<int, Dictionary<uint, CatResourceEntry>>> m_resources = new Dictionary<int, Dictionary<int, Dictionary<uint, CatResourceEntry>>>();
        private string m_modDirName = "ModData";
        private string m_patchPath = "Patch";
        private bool m_hasPatchFolder = true;
        private bool m_hasUpdateFolder = true;
        private bool m_useAltSymLink = Config.Get<bool>("UseAltSymLink", false);

        public ILogger Logger { get => m_logger; set => m_logger = value; }

        private void PrintLog(string modPackName)
        {
            // [DAI fix] dated file in Logs (was log.txt next to the exe), so Automatic Log Cleanup can delete old ones
            string path;
            FileStream stream;
            try
            {
                string folder = DiagnosticLog.LogsFolder;
                Directory.CreateDirectory(folder);
                path = Path.Combine(folder, $"ModDataBuild_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning("ModData Build Log could not be written: {0}", ex.Message);
                return;
            }

            using (NativeWriter writer = new NativeWriter(stream))
            {
                try
                {
                    writer.WriteLine($"{ProfilesLibrary.DisplayName}, pack '{modPackName}', {DateTime.Now}");
                    writer.WriteLine(string.Empty);

                    if (m_addedSuperBundles.Count > 0)
                    {
                        writer.WriteLine("Added SuperBundles:");
                        foreach (string sb in m_addedSuperBundles)
                        {
                            writer.WriteLine($"  - {sb}");
                        }
                        writer.WriteLine(string.Empty);
                    }

                    if (m_modifiedSuperBundles.Count > 0)
                    {
                        writer.WriteLine("Modified SuperBundles:");
                        foreach (var kv in m_modifiedSuperBundles)
                        {
                            writer.WriteLine($"  {m_am.GetSuperBundle(kv.Key).Name}:");

                            List<Guid> sortedChunks = new List<Guid>(kv.Value.Modify.Chunks);
                            sortedChunks.Sort();

                            if (kv.Value.Modify.Chunks.Count > 0)
                            {
                                writer.WriteLine($"    Modified Chunks:");
                                foreach (Guid chunkId in sortedChunks)
                                {
                                    writer.WriteLine($"    - {chunkId}");
                                }
                            }

                            sortedChunks = new List<Guid>(kv.Value.Add.Chunks);
                            sortedChunks.Sort();

                            if (kv.Value.Add.Chunks.Count > 0)
                            {
                                writer.WriteLine($"    Added Chunks:");
                                foreach (Guid chunkId in sortedChunks)
                                {
                                    writer.WriteLine($"    - {chunkId}");
                                }
                            }
                        }
                        writer.WriteLine(string.Empty);
                    }

                    if (m_modifiedBundles.Count > 0)
                    {
                        Dictionary<int, string> bundleHashMap = new Dictionary<int, string>();
                        foreach (BundleEntry be in m_am.EnumerateBundles())
                        {
                            if (!bundleHashMap.ContainsKey(HashBundle(be)))
                                bundleHashMap.Add(HashBundle(be), be.Name);
                        }
                        writer.WriteLine("Modified Bundles:");

                        List<int> bundles = m_modifiedBundles.Keys.ToList();
                        bundles.Sort();

                        foreach (int hash in bundles)
                        {
                            var kv = m_modifiedBundles[hash];
                            writer.WriteLine($"  {(bundleHashMap.TryGetValue(hash, out string bundleName) ? bundleName : "chunks")}:");

                            if (kv.Modify.Ebx.Count > 0)
                            {
                                writer.WriteLine($"    Modified Ebx:");
                                List<string> sorted = new List<string>(kv.Modify.Ebx);
                                sorted.Sort();
                                foreach (string name in sorted)
                                {
                                    writer.WriteLine($"    - {name}");
                                }
                            }
                            if (kv.Add.Ebx.Count > 0)
                            {
                                writer.WriteLine($"    Added Ebx:");
                                List<string> sorted = new List<string>(kv.Add.Ebx);
                                sorted.Sort();
                                foreach (string name in sorted)
                                {
                                    writer.WriteLine($"    - {name}");
                                }
                            }

                            if (kv.Modify.Res.Count > 0)
                            {
                                writer.WriteLine($"    Modified Res:");
                                List<string> sorted = new List<string>(kv.Modify.Res);
                                sorted.Sort();
                                foreach (string name in sorted)
                                {
                                    writer.WriteLine($"    - {name}");
                                }
                            }
                            if (kv.Add.Res.Count > 0)
                            {
                                writer.WriteLine($"    Added Res:");
                                List<string> sorted = new List<string>(kv.Add.Res);
                                sorted.Sort();
                                foreach (string name in sorted)
                                {
                                    writer.WriteLine($"    - {name}");
                                }
                            }

                            if (kv.Modify.Chunks.Count > 0)
                            {
                                writer.WriteLine($"    Modified Chunks:");
                                List<Guid> sorted = new List<Guid>(kv.Modify.Chunks);
                                sorted.Sort();
                                foreach (Guid chunkId in sorted)
                                {
                                    writer.WriteLine($"    - {chunkId}");
                                }
                            }
                            if (kv.Add.Chunks.Count > 0)
                            {
                                writer.WriteLine($"    Added Chunks:");
                                List<Guid> sorted = new List<Guid>(kv.Add.Chunks);
                                sorted.Sort();
                                foreach (Guid chunkId in sorted)
                                {
                                    writer.WriteLine($"    - {chunkId}");
                                }
                            }
                        }
                        writer.WriteLine(string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine(string.Empty);
                    writer.WriteLine($"Failed: {ex.Message}");
                }
            }
            App.Logger.Log("ModData Build Log saved to {0}", path);
        }

        // [DAI fix] the Detailed progress window: finished steps with their times, the current step and each running task
        private class ProgressDetails
        {
            private const int MaxLines = 32;

            private readonly ILoadingListLogger logger;
            private readonly List<string> doneSteps = new List<string>();
            private readonly Stopwatch stepWatch = new Stopwatch();
            private readonly Stopwatch timer = Stopwatch.StartNew();
            private long lastUpdate = -1000;
            private string step;

            public ProgressDetails(ILogger inLogger)
            {
                if (Frosty.Core.Windows.ModManagerOptionsData.IsDetailedProgressView())
                    logger = inLogger as ILoadingListLogger;
            }

            public bool Due => logger != null && timer.ElapsedMilliseconds - lastUpdate >= 100;

            public void Begin(string name)
            {
                step = name;
                stepWatch.Restart();
                Show(null);
            }

            public void Done(string name, double seconds)
            {
                doneSteps.Add($"{name}  ({seconds:0.0}s)");
                step = null;
                Show(null);
            }

            public void Show(string state, IEnumerable<string> tasks = null)
            {
                if (logger == null)
                    return;
                lastUpdate = timer.ElapsedMilliseconds;

                List<string> lines = new List<string>(doneSteps);
                if (step != null)
                {
                    lines.Add((state != null) ? $"{step}  ({state}, {stepWatch.Elapsed.TotalSeconds:0.0}s)" : $"{step}  ({stepWatch.Elapsed.TotalSeconds:0.0}s)");

                    List<string> running = tasks?.Where(t => t != null).ToList() ?? new List<string>();
                    for (int i = 0; i < running.Count; i++)
                    {
                        if (i == MaxLines)
                        {
                            lines.Add($"    ... and {running.Count - i} more");
                            break;
                        }
                        lines.Add("    " + running[i]);
                    }
                }

                logger.LogLoadingList(string.Join("\n", lines));
            }
        }

        private int m_lastProgress = -1;

        // [DAI fix] only tell the window when the bar moves by 0.1%, not once per file
        private void ReportProgress(int current, int total)
        {
            if (total > 0)
            {
                int permille = (int)(current * 1000L / total);
                if (Interlocked.Exchange(ref m_lastProgress, permille) != permille)
                    Logger.Log("progress:" + current / (float)total * 100d);
            }
        }

        private Dictionary<int, Dictionary<uint, CatResourceEntry>> LoadCatalog(string filename, out int catFileHash)
        {
            catFileHash = 0;
            string fullPath = m_fs.ResolvePath(filename);
            if (!File.Exists(fullPath))
                return null;

            catFileHash = Fnv1.HashString(fullPath.ToLower());
            Dictionary<int, Dictionary<uint, CatResourceEntry>> resources = new Dictionary<int, Dictionary<uint, CatResourceEntry>>();
            using (CatReader reader = new CatReader(new FileStream(fullPath, FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
            {
                for (int i = 0; i < reader.ResourceCount; i++)
                {
                    CatResourceEntry entry = reader.ReadResourceEntry();
                    if (!resources.ContainsKey(entry.ArchiveIndex))
                        resources.Add(entry.ArchiveIndex, new Dictionary<uint, CatResourceEntry>());
                    resources[entry.ArchiveIndex].Add(entry.Offset, entry);
                }
            }

            return resources;
        }

        private int HashBundle(BundleEntry bentry)
        {
            int hash = Fnv1.HashString(bentry.Name.ToLower());

            if (bentry.Name.Length == 8 && int.TryParse(bentry.Name, System.Globalization.NumberStyles.HexNumber, null, out int tmp))
                hash = tmp;

            return hash;
        }

        public class NullEntryException : Exception
        {
            public NullEntryException() : base() { }
        }

        // [DAI fix] archive ref counts are updated from several threads
        private readonly object archiveDataLock = new object();

        private void AddArchiveRef(Sha1 sha1, byte[] data)
        {
            lock (archiveDataLock)
            {
                if (!m_archiveData.TryAdd(sha1, new ArchiveInfo() { Data = data, RefCount = 1 }))
                    m_archiveData[sha1].RefCount++;
            }
        }

        private bool ReleaseArchiveRef(Sha1 sha1)
        {
            lock (archiveDataLock)
            {
                if (!m_archiveData.TryGetValue(sha1, out ArchiveInfo info))
                    return false;

                info.RefCount--;
                if (info.RefCount <= 0)
                    m_archiveData.TryRemove(sha1, out _);
                return true;
            }
        }

        // [DAI fix] DAI uses the 1.0.6.4 "chunks" pseudo-bundle; the superbundle route marked replaced chunks as deltas
        private static bool IsDai => ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition);

        private void AddChunkSuperBundles(ChunkAssetEntry chunkEntry, HashSet<int> bundles, List<int> superBundles)
        {
            if (IsDai)
                bundles.Add(s_chunksBundleHash);
            else
                superBundles.AddRange(chunkEntry.SuperBundles);
        }

        private void AddToChunksBundle(Guid id, bool isAdded)
        {
            m_modifiedBundles.TryAdd(s_chunksBundleHash, new ModBundleInfo() { Name = s_chunksBundleHash });
            ModBundleInfo chunksBundle = m_modifiedBundles[s_chunksBundleHash];
            if (isAdded)
                chunksBundle.Add.AddChunk(id);
            else
                chunksBundle.Modify.AddChunk(id);
        }

        private void ProcessModResources(IResourceContainer fmod)
        {
            // Bundle whitelist may not contain the chunk bundle. This adds it to prevent issues
            if (App.WhitelistedBundles.Count != 0)
            {
                App.WhitelistedBundles.Add(s_chunksBundleHash);
            }

            WorkerPool.ForEach(fmod.Resources.ToList(), resource =>
            {
                // pull existing bundles from asset manager
                HashSet<int> bundles = new HashSet<int>();

                // get superbundles which have modified chunks
                List<int> superBundles = new List<int>();

                // get superbundles which have added chunks
                List<int> addedSuperBundles = new List<int>();

                if (resource.Type == ModResourceType.Bundle)
                {
                    BundleEntry bEntry = new BundleEntry();
                    resource.FillAssetEntry(bEntry);

                    m_addedBundles.TryAdd(bEntry.SuperBundleId, new HashSet<string>());

                    HashSet<string> addedBundlesSet = m_addedBundles[bEntry.SuperBundleId];
                    lock (addedBundlesSet)
                    {
                        addedBundlesSet.Add(bEntry.Name);
                    }

                }
                else if (resource.Type == ModResourceType.Ebx)
                {
                    if (resource.IsModified || !m_modifiedEbx.ContainsKey(resource.Name))
                    {
                        if (resource.HasHandler)
                        {
                            HandlerExtraData extraData;
                            byte[] data = fmod.GetResourceData(resource);

                            if (m_modifiedEbx.TryGetValue(resource.Name, out EbxAssetEntry entry))
                            {
                                extraData = (HandlerExtraData)entry.ExtraData;
                            }
                            else
                            {
                                entry = new EbxAssetEntry();
                                extraData = new HandlerExtraData();

                                resource.FillAssetEntry(entry);
                                // the rest of the chunk will be populated via the handler

                                ICustomActionHandler handler = App.PluginManager.GetCustomHandler((uint)resource.Handler);
                                if (handler != null)
                                    extraData.Handler = handler;

                                // add in existing bundles
                                var ebxEntry = m_am.GetEbxEntry(resource.Name);
                                foreach (int bid in ebxEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }

                                entry.ExtraData = extraData;
                                m_modifiedEbx.TryAdd(resource.Name, entry);
                            }

                            // merge new and old data together
                            if (extraData != null)
                                extraData.Data = extraData.Handler.Load(extraData.Data, data);
                        }
                        else
                        {
                            if (m_modifiedEbx.TryGetValue(resource.Name, out EbxAssetEntry existingEntry))
                            {
                                if (existingEntry.ExtraData != null)
                                    goto label_add_bundles;
                                if (existingEntry.Sha1 == resource.Sha1)
                                    goto label_add_bundles;

                                // [DAI fix] thread-safe
                                if (!ReleaseArchiveRef(existingEntry.Sha1))
                                {
                                    return;
                                }

                                m_modifiedEbx.TryRemove(resource.Name, out _);
                                m_numArchiveEntries--;
                            }

                            EbxAssetEntry entry = new EbxAssetEntry();
                            resource.FillAssetEntry(entry);

                            byte[] data = fmod.GetResourceData(resource);
                            var ebxEntry = m_am.GetEbxEntry(resource.Name);

                            if (data == null)
                            {
                                try
                                {
                                    data = NativeReader.ReadInStream(m_am.GetRawStream(ebxEntry));
                                }
                                catch (System.NullReferenceException)
                                {
                                    throw new NullEntryException();
                                }

                                entry.Sha1 = ebxEntry.Sha1;
                                entry.OriginalSize = ebxEntry.OriginalSize;
                            }
                            else if (ebxEntry != null)
                            {
                                // add in existing bundles
                                foreach (int bid in ebxEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }
                            }

                            entry.Size = data.Length;

                            m_modifiedEbx.TryAdd(entry.Name, entry);
                            AddArchiveRef(entry.Sha1, data); // [DAI fix] thread-safe
                            m_numArchiveEntries++;
                        }
                    }
                }
                else if (resource.Type == ModResourceType.Res)
                {
                    if (resource.IsModified || !m_modifiedRes.ContainsKey(resource.Name))
                    {
                        if (resource.HasHandler)
                        {
                            HandlerExtraData extraData;

                            byte[] data = fmod.GetResourceData(resource);

                            if (m_modifiedRes.TryGetValue(resource.Name, out ResAssetEntry entry))
                            {
                                extraData = (HandlerExtraData)entry.ExtraData;
                            }
                            else
                            {
                                entry = new ResAssetEntry();
                                extraData = new HandlerExtraData();

                                resource.FillAssetEntry(entry);
                                // the rest of the chunk will be populated via the handler

                                ICustomActionHandler handler = App.PluginManager.GetCustomHandler((ResourceType)entry.ResType);
                                if (handler != null)
                                    extraData.Handler = handler;

                                // add in existing bundles
                                var resEntry = m_am.GetResEntry(resource.Name);
                                foreach (int bid in resEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }

                                entry.ExtraData = extraData;
                                m_modifiedRes.TryAdd(resource.Name, entry);
                            }

                            // merge new and old data together
                            if (extraData != null)
                                extraData.Data = extraData.Handler.Load(extraData.Data, data);
                        }
                        else
                        {
                            if (m_modifiedRes.TryGetValue(resource.Name, out ResAssetEntry existingEntry))
                            {
                                if (existingEntry.ExtraData != null)
                                    goto label_add_bundles;
                                if (existingEntry.Sha1 == resource.Sha1)
                                    goto label_add_bundles;

                                // [DAI fix] thread-safe
                                if (!ReleaseArchiveRef(existingEntry.Sha1))
                                {
                                    return;
                                }

                                m_modifiedRes.TryRemove(resource.Name, out _);
                                m_numArchiveEntries--;
                            }

                            ResAssetEntry entry = new ResAssetEntry();
                            resource.FillAssetEntry(entry);

                            byte[] data = fmod.GetResourceData(resource);
                            var resEntry = m_am.GetResEntry(resource.Name);

                            if (data == null)
                            {
                                try
                                {
                                    data = NativeReader.ReadInStream(m_am.GetRawStream(resEntry));
                                }
                                catch (System.NullReferenceException)
                                {
                                    throw new NullEntryException();
                                }

                                entry.Sha1 = resEntry.Sha1;
                                entry.OriginalSize = resEntry.OriginalSize;
                                entry.ResMeta = resEntry.ResMeta;
                                entry.ResRid = resEntry.ResRid;
                                entry.ResType = resEntry.ResType;
                            }
                            else if (resEntry != null)
                            {
                                // add in existing bundles
                                foreach (int bid in resEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }
                            }

                            entry.Size = data.Length;

                            m_modifiedRes.TryAdd(entry.Name, entry);
                            AddArchiveRef(entry.Sha1, data); // [DAI fix] thread-safe
                            m_numArchiveEntries++;
                        }
                    }
                }
                else if (resource.Type == ModResourceType.Chunk)
                {
                    Guid guid = new Guid(resource.Name);
                    if (resource.IsModified || !m_modifiedChunks.ContainsKey(guid))
                    {
                        if (resource.HasHandler)
                        {
                            HandlerExtraData extraData;
                            byte[] data = fmod.GetResourceData(resource);

                            if (m_modifiedChunks.TryGetValue(guid, out ChunkAssetEntry entry))
                            {
                                extraData = (HandlerExtraData)entry.ExtraData;
                            }
                            else
                            {
                                entry = new ChunkAssetEntry();
                                extraData = new HandlerExtraData();

                                entry.Id = guid;
                                // the rest of the chunk will be populated via the handler

                                if ((uint)resource.Handler == 0xBD9BFB65)
                                {
                                    // hack to ensure handler for legacy assets is set properly
                                    extraData.Handler = new Frosty.Core.Handlers.LegacyCustomActionHandler();
                                }
                                else
                                {
                                    ICustomActionHandler handler = App.PluginManager.GetCustomHandler((uint)resource.Handler);
                                    if (handler != null)
                                        extraData.Handler = handler;
                                }

                                // add in existing bundles
                                var chunkEntry = m_am.GetChunkEntry(guid);
                                foreach (int bid in chunkEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }

                                // add in existing superbundles
                                AddChunkSuperBundles(chunkEntry, bundles, superBundles);

                                entry.ExtraData = extraData;
                                m_modifiedChunks.TryAdd(guid, entry);
                            }

                            // merge new and old data together
                            extraData.Data = extraData.Handler.Load(extraData.Data, data);
                        }
                        else
                        {
                            if (m_modifiedChunks.TryGetValue(guid, out ChunkAssetEntry existingEntry))
                            {
                                if (existingEntry.ExtraData != null)
                                    goto label_add_bundles;
                                if (existingEntry.Sha1 == resource.Sha1)
                                    goto label_add_bundles;

                                // [DAI fix] thread-safe
                                if (!ReleaseArchiveRef(existingEntry.Sha1))
                                {
                                    return;
                                }

                                m_modifiedChunks.TryRemove(guid, out _);
                                m_numArchiveEntries--;
                            }

                            ChunkAssetEntry entry = new ChunkAssetEntry();
                            resource.FillAssetEntry(entry);

                            byte[] data = fmod.GetResourceData(resource);
                            var chunkEntry = m_am.GetChunkEntry(guid);

                            if (data == null)
                            {
                                try
                                {
                                    data = NativeReader.ReadInStream(m_am.GetRawStream(chunkEntry));
                                }
                                catch (System.NullReferenceException)
                                {
                                    throw new NullEntryException();
                                }

                                entry.Sha1 = (chunkEntry.Sha1 == Sha1.Zero) ? Utils.GenerateSha1(data) : chunkEntry.Sha1;
                                entry.OriginalSize = chunkEntry.OriginalSize;
                                entry.LogicalSize = chunkEntry.LogicalSize;
                                entry.LogicalOffset = chunkEntry.LogicalOffset;
                                entry.RangeStart = chunkEntry.RangeStart;
                                entry.RangeEnd = chunkEntry.RangeEnd;

                                if (chunkEntry.LogicalOffset != 0 && chunkEntry.RangeStart == 0)
                                {
                                    // need to calculate range start, since manifest bundle layouts don't store it directly
                                    // however it is used to store chunk portions in bundles
                                    // @todo: Move to mod export

                                    using (NativeReader reader = new NativeReader(new MemoryStream(data)))
                                    {
                                        long uncompressedSize = entry.LogicalOffset + entry.LogicalSize;
                                        long uncompressedBundledSize = (entry.LogicalOffset & 0xFFFF) | entry.LogicalSize;
                                        long logicalOffset = uncompressedSize - uncompressedBundledSize;
                                        uint size = 0;

                                        while (true)
                                        {
                                            int decompressedSize = reader.ReadInt(Endian.Big);
                                            ushort compressionType = reader.ReadUShort();
                                            int bufferSize = reader.ReadUShort(Endian.Big);

                                            int flags = ((compressionType & 0xFF00) >> 8);

                                            if ((flags & 0x0F) != 0)
                                                bufferSize = ((flags & 0x0F) << 0x10) + bufferSize;
                                            if ((decompressedSize & 0xFF000000) != 0)
                                                decompressedSize &= 0x00FFFFFF;

                                            logicalOffset -= decompressedSize;
                                            if (logicalOffset < 0)
                                                break;

                                            compressionType = (ushort)(compressionType & 0x7F);
                                            if (compressionType == 0x00)
                                                bufferSize = decompressedSize;

                                            size += (uint)(bufferSize + 8);
                                            reader.Position += bufferSize;
                                        }

                                        entry.RangeStart = size;
                                        entry.RangeEnd = (uint)data.Length;
                                    }
                                }

                                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5))
                                {
                                    if (m_fs.GetManifestChunk(chunkEntry.Id) != null)
                                    {
                                        entry.TocChunkSpecialHack = true;
                                        if (chunkEntry.Bundles.Count == 0)
                                        {
                                            resource.ClearAddedBundles();
                                        }
                                    }
                                }
                            }
                            else if (chunkEntry != null)
                            {
                                // add in existing bundles
                                foreach (int bid in chunkEntry.Bundles)
                                {
                                    bundles.Add(HashBundle(m_am.GetBundleEntry(bid)));
                                }

                                // add in existing superbundles
                                AddChunkSuperBundles(chunkEntry, bundles, superBundles);
                            }
                            else if (!resource.IsAdded && !IsDai)
                            {
                                // old mods had their IsAdded flag overridden so we have to manually set the flag and add it to chunks superbundles
                                resource.IsAdded = true;
                                foreach (var superBundle in m_am.EnumerateSuperBundles())
                                {
                                    // StandardModExecutor.cs had hack with "chunks" and FifaModExecutor.cs had hack with globals.toc 
                                    if (superBundle.Name.Contains("chunks") || superBundle.Name.Equals("Win32/globals", StringComparison.OrdinalIgnoreCase) || superBundle.Name.Equals("<none>"))
                                    {
                                        addedSuperBundles.Add(m_am.GetSuperBundleId(superBundle));
                                    }
                                }
                            }

                            // add in added superbundles
                            addedSuperBundles.AddRange(entry.AddedSuperBundles);

                            entry.Size = data.Length;

                            m_modifiedChunks.TryAdd(guid, entry);
                            AddArchiveRef(entry.Sha1, data); // [DAI fix] thread-safe
                            m_numArchiveEntries++;
                        }
                    }
                    else
                    {
                        if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5))
                        {
                            var chunkEntry = m_am.GetChunkEntry(guid);
                            var entry = m_modifiedChunks[guid];

                            if (m_fs.GetManifestChunk(chunkEntry.Id) != null)
                            {
                                entry.TocChunkSpecialHack = true;
                                if (chunkEntry.Bundles.Count == 0)
                                    resource.ClearAddedBundles();
                            }
                        }
                    }
                }
                else if (resource.Type == ModResourceType.FsFile)
                {
                    DbObject fileStub;
                    using (DbReader reader = new DbReader(new MemoryStream(fmod.GetResourceData(resource)), null))
                    {
                        fileStub = reader.ReadDbObject();
                    }

                    if (!m_modifiedFs.ContainsKey(resource.Name))
                    {
                        m_modifiedFs.TryAdd(resource.Name, null);
                    }

                    m_modifiedFs[resource.Name] = fileStub;
                }

                // modified bundle actions (these are pulled from the asset manager during applying)
                foreach (int bundleHash in bundles)
                {
                    // Skips bundle if the whitelist has more than one element and doesn't contain the bundle hash
                    if (App.WhitelistedBundles.Count != 0 && !App.WhitelistedBundles.Contains(bundleHash))
                    {
                        continue;
                    }

                    m_modifiedBundles.TryAdd(bundleHash, new ModBundleInfo() { Name = bundleHash });

                    ModBundleInfo modBundle = m_modifiedBundles[bundleHash];
                    switch (resource.Type)
                    {
                        case ModResourceType.Ebx: modBundle.Modify.AddEbx(resource.Name); break;
                        case ModResourceType.Res: modBundle.Modify.AddRes(resource.Name); break;
                        case ModResourceType.Chunk: modBundle.Modify.AddChunk(new Guid(resource.Name)); break;
                    }
                }

                label_add_bundles:
                // add bundle actions (these are stored in the mod)
                foreach (int bundleHash in resource.AddedBundles)
                {
                    // Skips bundle if the whitelist has more than one element and doesn't contain the bundle hash
                    // [DAI fix] DAI keeps the mod's own "chunks" actions, as 1.0.6.4 did
                    if ((App.WhitelistedBundles.Count != 0 && !App.WhitelistedBundles.Contains(bundleHash)) || (bundleHash == s_chunksBundleHash && !IsDai))
                    {
                        continue;
                    }

                    m_modifiedBundles.TryAdd(bundleHash, new ModBundleInfo() { Name = bundleHash });

                    ModBundleInfo modBundle = m_modifiedBundles[bundleHash];
                    switch (resource.Type)
                    {
                        case ModResourceType.Ebx: modBundle.Add.AddEbx(resource.Name); break;
                        case ModResourceType.Res: modBundle.Add.AddRes(resource.Name); break;
                        case ModResourceType.Chunk: modBundle.Add.AddChunk(new Guid(resource.Name)); break;
                    }
                }

                // modified superbundle actions (these are pulled from the asset manager during applying)
                foreach (int superBundleId in superBundles)
                {
                    m_modifiedSuperBundles.TryAdd(superBundleId, new ModBundleInfo() { Name = superBundleId });

                    ModBundleInfo modBundle = m_modifiedSuperBundles[superBundleId];

                    modBundle.Modify.AddChunk(new Guid(resource.Name));
                }

                // add superbundle actions (these are stored in the mod)
                if (IsDai && addedSuperBundles.Count > 0 && resource.Type == ModResourceType.Chunk)
                {
                    AddToChunksBundle(new Guid(resource.Name), true);
                    addedSuperBundles.Clear();
                }

                foreach (int superBundleId in addedSuperBundles)
                {
                    m_modifiedSuperBundles.TryAdd(superBundleId, new ModBundleInfo() { Name = superBundleId });

                    ModBundleInfo modBundle = m_modifiedSuperBundles[superBundleId];

                    modBundle.Add.AddChunk(new Guid(resource.Name));
                }
            });
        }

        private void ProcessLegacyModResources(string modPath)
        {
            DbObject mod = null;
            using (DbReader reader = new DbReader(new FileStream(modPath, FileMode.Open, FileAccess.Read), null))
                mod = reader.ReadDbObject();

            string magic = mod.GetValue<string>("magic");
            int ver = int.Parse(magic.Replace("FBMODV", ""));

            // obtain bundles to modify
            DbObject resourceList = mod.GetValue<DbObject>("resources");
            foreach (DbObject action in mod.GetValue<DbObject>("actions"))
            {
                int bundle = Fnv1.HashString(action.GetValue<string>("bundle").ToLower());
                string actionType = action.GetValue<string>("type");
                int resourceId = action.GetValue<int>("resourceId");

                if (bundle == s_chunksBundleHash && !IsDai)
                {
                    continue;
                }

                if (!m_modifiedBundles.ContainsKey(bundle))
                    m_modifiedBundles.TryAdd(bundle, new ModBundleInfo() { Name = bundle });

                ModBundleInfo modBundle = m_modifiedBundles[bundle];
                DbObject resource = resourceList[resourceId] as DbObject;

                string resName = resource.GetValue<string>("name");
                string resType = resource.GetValue<string>("type");

                if (actionType == "modify")
                {
                    switch (resType)
                    {
                        case "ebx": modBundle.Modify.Ebx.Add(resName); break;
                        case "res": modBundle.Modify.Res.Add(resName); break;
                        case "chunk": modBundle.Modify.Chunks.Add(new Guid(resName)); break;
                    }
                }
                else if (actionType == "add")
                {
                    switch (resType)
                    {
                        case "ebx": modBundle.Add.Ebx.Add(resName); break;
                        case "res": modBundle.Add.Res.Add(resName); break;
                        case "chunk": modBundle.Add.Chunks.Add(new Guid(resName)); break;
                    }
                }
                else if (actionType == "remove")
                {
                    switch (resType)
                    {
                        case "ebx": modBundle.Remove.Ebx.Add(resName); break;
                        case "res": modBundle.Remove.Res.Add(resName); break;
                        case "chunk": modBundle.Remove.Chunks.Add(new Guid(resName)); break;
                    }
                }
            }

            // [MEA fix] old mods store whole texture chunks; split them at the header's first mip like the importer does
            Dictionary<Guid, LegacyTextureLayout> textureLayouts = ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda)
                ? ReadLegacyTextureLayouts(modPath, resourceList)
                : new Dictionary<Guid, LegacyTextureLayout>();

            List<DbObject> resources = new List<DbObject>();
            foreach (DbObject resource in resourceList)
                resources.Add(resource);
            LegacyResourceData[] resourceData = ReadLegacyResourceData(modPath, resources);

            // obtain resources to modify
            for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                DbObject resource = resources[resourceIndex];

                // [DAI fix] .daimod removal entries have no data, just a name for "remove" actions
                if (resource.GetValue<bool>("isRemoval", false))
                    continue;

                string resourceType = resource.GetValue<string>("type");
                if (resourceType == "superbundle")
                {
                    string name = resource.GetValue<string>("name");
                    m_addedSuperBundles.Add(name);
                }
                else if (resourceType == "bundle")
                {
                    string name = resource.GetValue<string>("name");
                    string superBundle = resource.GetValue<string>("sb");

                    int hash = Fnv1a.HashString(superBundle.ToLower());
                    if (!m_addedBundles.ContainsKey(hash))
                        m_addedBundles.TryAdd(hash, new HashSet<string>());

                    m_addedBundles[hash].Add(name);
                }
                else if (resourceType == "ebx")
                {
                    string name = resource.GetValue<string>("name");

                    if (m_modifiedEbx.ContainsKey(name))
                    {
                        EbxAssetEntry existingEntry = m_modifiedEbx[name];
                        if (existingEntry.Sha1 == resource.GetValue<Sha1>("sha1"))
                            continue;

                        if (!m_archiveData.ContainsKey(existingEntry.Sha1))
                        {
                            // [DAI fix] skip only this asset, as 1.0.6.3 did
                            continue;
                        }

                        m_archiveData[existingEntry.Sha1].RefCount--;
                        if (m_archiveData[existingEntry.Sha1].RefCount == 0)
                            m_archiveData.TryRemove(existingEntry.Sha1, out _);

                        m_modifiedEbx.TryRemove(name, out _);
                        m_numArchiveEntries--;
                    }

                    EbxAssetEntry entry = new EbxAssetEntry
                    {
                        Name = name,
                        OriginalSize = resource.GetValue<long>("uncompressedSize"),
                        Size = resource.GetValue<long>("compressedSize")
                    };

                    byte[] buffer = resourceData[resourceIndex].Buffer;
                    if (resource.HasValue("archiveIndex"))
                        entry.IsInline = resource.GetValue<bool>("shouldInline");

                    entry.Sha1 = resourceData[resourceIndex].GetSha1();

                    m_modifiedEbx.TryAdd(entry.Name, entry);
                    if (!m_archiveData.ContainsKey(entry.Sha1))
                        m_archiveData.TryAdd(entry.Sha1, new ArchiveInfo() { Data = buffer, RefCount = 1 });
                    else
                        m_archiveData[entry.Sha1].RefCount++;
                    m_numArchiveEntries++;
                }
                else if (resourceType == "res")
                {
                    string name = resource.GetValue<string>("name");

                    if (m_modifiedRes.ContainsKey(name))
                    {
                        ResAssetEntry existingEntry = m_modifiedRes[name];
                        if (existingEntry.Sha1 == resource.GetValue<Sha1>("sha1"))
                            continue;

                        if (!m_archiveData.ContainsKey(existingEntry.Sha1))
                        {
                            // [DAI fix] skip only this asset, as 1.0.6.3 did
                            continue;
                        }

                        m_archiveData[existingEntry.Sha1].RefCount--;
                        if (m_archiveData[existingEntry.Sha1].RefCount == 0)
                            m_archiveData.TryRemove(existingEntry.Sha1, out _);

                        m_modifiedRes.TryRemove(name, out _);
                        m_numArchiveEntries--;
                    }

                    ResAssetEntry entry = new ResAssetEntry
                    {
                        Name = name,
                        OriginalSize = resource.GetValue<long>("uncompressedSize"),
                        Size = resource.GetValue<long>("compressedSize"),
                        ResRid = (ulong)resource.GetValue<long>("resRid"),
                        ResType = (uint)resource.GetValue<int>("resType"),
                        ResMeta = resource.GetValue<byte[]>("resMeta")
                    };

                    byte[] buffer = resourceData[resourceIndex].Buffer;
                    if (resource.HasValue("archiveIndex"))
                        entry.IsInline = resource.GetValue<bool>("shouldInline");

                    entry.Sha1 = resourceData[resourceIndex].GetSha1();

                    m_modifiedRes.TryAdd(entry.Name, entry);
                    if (!m_archiveData.ContainsKey(entry.Sha1))
                        m_archiveData.TryAdd(entry.Sha1, new ArchiveInfo() { Data = buffer, RefCount = 1 });
                    else
                        m_archiveData[entry.Sha1].RefCount++;
                    m_numArchiveEntries++;
                }
                else if (resourceType == "chunk")
                {
                    Guid chunkId = new Guid(resource.GetValue<string>("name"));
                    if (m_modifiedChunks.ContainsKey(chunkId))
                    {
                        ChunkAssetEntry existingEntry = m_modifiedChunks[chunkId];
                        if (existingEntry.Sha1 == resource.GetValue<Sha1>("sha1"))
                            continue;

                        if (!m_archiveData.ContainsKey(existingEntry.Sha1))
                        {
                            // [DAI fix] skip only this asset, as 1.0.6.3 did
                            continue;
                        }

                        m_archiveData[existingEntry.Sha1].RefCount--;
                        if (m_archiveData[existingEntry.Sha1].RefCount == 0)
                            m_archiveData.TryRemove(existingEntry.Sha1, out _);

                        m_modifiedChunks.TryRemove(chunkId, out _);
                        m_numArchiveEntries--;
                    }

                    ChunkAssetEntry entry = new ChunkAssetEntry
                    {
                        Id = chunkId,
                        Size = resource.GetValue<long>("compressedSize"),
                        LogicalOffset = resource.GetValue<uint>("logicalOffset"),
                        LogicalSize = resource.GetValue<uint>("logicalSize"),
                        RangeStart = resource.GetValue<uint>("rangeStart"),
                        RangeEnd = resource.GetValue<uint>("rangeEnd"),
                        FirstMip = resource.GetValue<int>("firstMip", -1),
                        H32 = resource.GetValue<int>("h32", 0)
                    };

                    byte[] buffer = resourceData[resourceIndex].Buffer;
                    if (resource.HasValue("archiveIndex"))
                    {
                        // obtain data from archive
                        entry.IsInline = resource.GetValue<bool>("shouldInline", false);
                    }
                    else
                    {
                        if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5))
                        {
                            if (entry.LogicalOffset != 0)
                            {
                                // calculate range values from cas data
                                using (NativeReader reader = new NativeReader(new MemoryStream(buffer)))
                                {
                                    int totalSize = 0;
                                    while (totalSize != entry.LogicalOffset)
                                    {
                                        int uncompressedSize = reader.ReadInt(Endian.Big);
                                        ushort compressCode = reader.ReadUShort(Endian.Big);
                                        ushort blockSize = reader.ReadUShort(Endian.Big);

                                        totalSize += uncompressedSize;
                                        if (totalSize > entry.LogicalOffset)
                                        {
                                            reader.Position -= 8;
                                            break;
                                        }

                                        reader.Position += blockSize;
                                    }

                                    entry.RangeStart = (uint)reader.Position;
                                    entry.RangeEnd = (uint)buffer.Length;
                                }
                            }
                        }
                    }

                    if (entry.LogicalOffset == 0 && entry.RangeStart == 0 && textureLayouts.TryGetValue(entry.Id, out LegacyTextureLayout layout))
                        SplitLegacyTextureChunk(layout, buffer, entry);

                    entry.Sha1 = resourceData[resourceIndex].GetSha1();

                    m_modifiedChunks.TryAdd(entry.Id, entry);
                    if (!m_archiveData.ContainsKey(entry.Sha1))
                        m_archiveData.TryAdd(entry.Sha1, new ArchiveInfo() { Data = buffer, RefCount = 1 });
                    else
                        m_archiveData[entry.Sha1].RefCount++;
                    m_numArchiveEntries++;

                    ChunkAssetEntry originalEntry = m_am.GetChunkEntry(entry.Id);
                    if (IsDai)
                    {
                        // [DAI fix] 1.0.6.4 routing; other chunks come from the mod's own "chunks" actions
                        if (ver < 2)
                            AddToChunksBundle(entry.Id, false);
                        else if (ver == 3 && entry.FirstMip != -1)
                            AddToChunksBundle(entry.Id, true);
                    }
                    else if (originalEntry != null)
                    {
                        // pull superbundles from am
                        foreach (int sbId in originalEntry.SuperBundles)
                        {
                            m_modifiedSuperBundles.TryAdd(sbId, new ModBundleInfo() { Name = sbId });

                            ModBundleInfo chunksBundle = m_modifiedSuperBundles[sbId];
                            chunksBundle.Modify.Chunks.Add(entry.Id);
                        }
                    }
                    else
                    {
                        // need to add it to some superbundle
                        foreach (var superBundle in m_am.EnumerateSuperBundles())
                        {
                            // StandardModExecutor.cs had hack with "chunks" and FifaModExecutor.cs had hack with globals.toc 
                            if (superBundle.Name.Contains("chunks") || superBundle.Name.Equals("Win32/globals", StringComparison.OrdinalIgnoreCase) || superBundle.Name.Equals("<none>"))
                            {
                                int sbId = m_am.GetSuperBundleId(superBundle);
                                m_modifiedSuperBundles.TryAdd(sbId, new ModBundleInfo() { Name = sbId });

                                ModBundleInfo chunksBundle = m_modifiedSuperBundles[sbId];
                                chunksBundle.Add.Chunks.Add(entry.Id);
                            }
                        }
                    }

                    if (ver < 2)
                    {
                        // new code requires first mip to be set to modify range values, however
                        // old mods didnt modify this. So lets force it, hopefully not too many
                        // issues result from this.
                        entry.FirstMip = 0;
                    }

                    if (entry.FirstMip == -1 && entry.RangeEnd != 0)
                        entry.FirstMip = 0;
                }
            }
        }

        public int Run(FileSystemManager inFs, CancellationToken cancelToken, ILogger inLogger, string rootPath, string modPackName, string additionalArgs, params string[] modPaths)
        {
            m_modDirName = "ModData\\" + modPackName;
            cancelToken.ThrowIfCancellationRequested();

            App.Logger.Log("Launching");

            m_fs = inFs;
            Logger = inLogger;

            string modDataPath = m_fs.BasePath + m_modDirName + "\\";

            if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa17,
                ProfileVersion.DragonAgeInquisition,
                ProfileVersion.Battlefield4,
                ProfileVersion.NeedForSpeed,
                ProfileVersion.PlantsVsZombiesGardenWarfare2,
                ProfileVersion.NeedForSpeedRivals))
            {
                // old fb3 games use an update folder
                m_patchPath = "Update\\Patch\\Data";
            }
            else if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield5, ProfileVersion.NeedForSpeedUnbound, ProfileVersion.DragonAgeTheVeilguard, ProfileVersion.DeadSpace))
            {
                // bfv doesnt have a patch directory
                m_patchPath = "Data";
                m_hasPatchFolder = false;
            }

            // [DS fix] Dead Space has no Patch or Update folder: changed files go into ModData's Data folder
            if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa17, ProfileVersion.DragonAgeTheVeilguard, ProfileVersion.DeadSpace))
            {
                m_hasUpdateFolder = false;
            }

            DiagnosticLog.Log($"Launch: pack '{modPackName}', ModData {modDataPath}, changed game files go under {m_patchPath}, extra arguments '{additionalArgs}', {modPaths.Length} mods applied");

            if (ProfilesLibrary.IsLoaded(ProfileVersion.Madden20))
            {
                string lcuPath = Environment.ExpandEnvironmentVariables(@"%ProgramData%\Frostbite\Madden NFL 20");
                if (Directory.Exists(lcuPath))
                {
                    DirectoryInfo di = new DirectoryInfo(lcuPath);
                    FrostyMessageBox.Show("Frosty has detected the presence of a Live Content Update. This must be removed and the game taken offline for mods to take affect.\r\n\r\nThe location of this update is:\r\n\r\n" + di.FullName, "Frosty Editor");
                    return -1;
                }
            }

            // check for already running process
            if (!Config.Get<bool>("DisableLaunchProcessCheck", false) && !EndRunningGame())
                return -1;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            cancelToken.ThrowIfCancellationRequested();
            Logger.Log("Loading Mods");

            bool needsModding = false;
            if (!File.Exists(Path.Combine(modDataPath, m_patchPath, "mods.json")))
            {
                needsModding = true;
                DiagnosticLog.Log("ModData needs building: it has no mods.json yet");
            }
            else
            {
                List<ModInfo> oldModInfoList = JsonConvert.DeserializeObject<List<ModInfo>>(File.ReadAllText(Path.Combine(modDataPath, m_patchPath, "mods.json")));
                List<ModInfo> currentModInfoList = GenerateModInfoList(modPaths, rootPath);

                // check if the mod data needs recreating
                // ie. mod change or patch
                bool hashMatch = true;

                if (oldModInfoList.SequenceEqual(currentModInfoList))
                {
                    for (int i = 0; i < oldModInfoList.Count; i++)
                    {
                        if (oldModInfoList[i].Hash != currentModInfoList[i].Hash)
                        {
                            hashMatch = false;
                            break;
                        }
                    }
                }
                else
                {
                    hashMatch = false;
                }

                bool samePatch = IsSamePatch(modDataPath + m_patchPath);
                if (!samePatch || !hashMatch)
                {
                    needsModding = true;
                    DiagnosticLog.Log(samePatch ? "ModData needs rebuilding: the mod list or a mod file changed" : "ModData needs rebuilding: the game was updated since it was built, or ModData has no layout.toc");
                }
            }

            // [DAI fix] ModData from a Frosty version that applied this game's mods differently is rebuilt
            string dataVersionPath = Path.Combine(modDataPath, m_patchPath, "moddata_version.txt");
            if (!needsModding && (!File.Exists(dataVersionPath) || File.ReadAllText(dataVersionPath).Trim() != DataVersions.ModDataVersion.ToString()))
            {
                App.Logger.Log("ModData was created by an older version of Frosty, rebuilding");
                needsModding = true;
            }

            cancelToken.ThrowIfCancellationRequested();
            if (needsModding)
            {
                cancelToken.ThrowIfCancellationRequested();
                // [DAI fix] each step's time goes to the log, to see where applying mods spends its time
                Stopwatch stepWatch = Stopwatch.StartNew();
                ProgressDetails progress = new ProgressDetails(m_logger);
                Action<string> logStepTime = step =>
                {
                    App.Logger.Log($"{step} took {stepWatch.Elapsed.TotalSeconds:0.0}s");
                    progress.Done(step, stepWatch.Elapsed.TotalSeconds);
                    stepWatch.Restart();
                };

                progress.Begin("Loading the cache");
                Logger.Log("Initializing Resources");

                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons))
                {
                    foreach (string catalogName in m_fs.Catalogs)
                    {
                        Dictionary<int, Dictionary<uint, CatResourceEntry>> entries = LoadCatalog("native_data/" + catalogName + "/cas.cat", out int hash);
                        if (entries != null)
                            m_resources.Add(hash, entries);

                        entries = LoadCatalog("native_patch/" + catalogName + "/cas.cat", out hash);
                        if (entries != null)
                            m_resources.Add(hash, entries);
                    }
                }

                m_rm = new ResourceManager(m_fs);
                m_rm.SetLogger(m_logger);
                m_rm.Initialize();

                cancelToken.ThrowIfCancellationRequested();
                Logger.Log("Loading " + ProfilesLibrary.CacheName + ".cache");

                m_am = new AssetManager(m_fs, m_rm);
                m_am.SetLogger(m_logger);
                m_am.Initialize(additionalStartup: false);
                logStepTime("Loading the cache");

                cancelToken.ThrowIfCancellationRequested();
                progress.Begin("Loading mods");
                Logger.Log("Loading Mods");
                App.Logger.Log("Loading Mods");

                // Get Full Modlist
                List<FrostyMod> modList = new List<FrostyMod>();
                foreach (string path in modPaths)
                {
                    FileInfo fi = new FileInfo(Path.Combine(rootPath, path));

                    if (fi.Extension == ".fbmod")
                        modList.Add(new FrostyMod(fi.FullName, false));

                    else if (fi.Extension == ".fbcollection")
                    {
                        foreach (FrostyMod mod in new FrostyModCollection(fi.FullName).Mods)
                            modList.Add(mod);
                    }
                }

                if (DiagnosticLog.Enabled)
                    LogDiagnostics(() => LogModDiagnostics(modList));

                // Load Mod Resources
                int currentMod = 0;
                foreach (FrostyMod mod in modList)
                {
                    Logger.Log($"Loading Mods ({mod.ModDetails?.Title ?? mod.Filename.Replace(".fbmod", "")})");
                    progress.Show($"{currentMod + 1} of {modList.Count}", new[] { mod.ModDetails?.Title ?? mod.Filename.Replace(".fbmod", "") });
                    if (mod.NewFormat)
                    {
                        try
                        {
                            ProcessModResources(mod);
                        }
                        // [DAI fix] the worker pool rethrows the error unwrapped, so catch both forms
                        catch (Exception caught) when (caught is AggregateException || caught is NullEntryException)
                        {
                            AggregateException ae = caught as AggregateException ?? new AggregateException(caught);
                            string nullEntryText = "\"{0}\" is incompatible with the installed version of the game.\n\nPlease remove it from the 'Applied Mods' list and try again.";

                            if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeTheVeilguard))
                            {
                                if (GameVersions.DragonAgeTheVeilguardEA.Contains((int)m_fs.Head) && GameVersions.DragonAgeTheVeilguardSteam.Contains(mod.GameVersion))
                                {
                                    nullEntryText = nullEntryText.Replace("installed", "EA App");
                                }
                                else if (GameVersions.DragonAgeTheVeilguardSteam.Contains((int)m_fs.Head) && GameVersions.DragonAgeTheVeilguardEA.Contains(mod.GameVersion))
                                {
                                    nullEntryText = nullEntryText.Replace("installed", "Steam");
                                }
                            }

                            var ignoredExceptions = new List<Exception>();

                            foreach (var ex in ae.Flatten().InnerExceptions)
                            {
                                if (ex is NullEntryException)
                                {
                                    SystemSounds.Exclamation.Play();
                                    FrostyMessageBox.Show(string.Format(nullEntryText, mod.ModDetails.Title), "Failed to create ModData");
                                    throw new OperationCanceledException();
                                }
                                else
                                {
                                    ignoredExceptions.Add(ex);
                                }
                            }
                            if (ignoredExceptions.Count > 0)
                            {
                                throw new AggregateException(ignoredExceptions);
                            }
                        }
                    }
                    else
                    {
                        ProcessLegacyModResources(mod.Path);
                    }
                    ReportProgress(currentMod++, modList.Count);
                }

                logStepTime("Loading mods");
                DiagnosticLog.Log($"All mods together change {m_modifiedEbx.Count} ebx, {m_modifiedRes.Count} res and {m_modifiedChunks.Count} chunks in {m_modifiedBundles.Count} bundles; "
                    + $"they add {m_addedBundles.Values.Sum(b => b.Count)} bundles and {m_addedSuperBundles.Count} superbundles, and change chunks listed in {m_modifiedSuperBundles.Count} superbundle tocs");
                progress.Begin("Applying handlers");
                Logger.Log("Applying Handlers");
                App.Logger.Log("Applying Handlers");

                // apply handlers
                RuntimeResources runtimeResources = new RuntimeResources();

                List<AssetEntry> assetEntries = new List<AssetEntry>();
                assetEntries.AddRange(m_modifiedEbx.Values);
                assetEntries.AddRange(m_modifiedRes.Values);
                assetEntries.AddRange(m_modifiedChunks.Values);

                int currentResource = 0;
                WorkerPool.ForEach(assetEntries, entry =>
                {
                    if (entry.ExtraData is HandlerExtraData handlerExtaData)
                    {
                        handlerExtaData.Handler.Modify(entry, m_am, runtimeResources, handlerExtaData.Data, out byte[] data);

                        if (!m_archiveData.TryAdd(entry.Sha1, new ArchiveInfo() { Data = data, RefCount = 1 }))
                            m_archiveData[entry.Sha1].RefCount++;
                    }
                    ReportProgress(currentResource++, assetEntries.Count);
                });

                // process any new resources added during custom handler modification
                ProcessModResources(runtimeResources);

                // [DAI fix] texture mods that overlap, or old .daimod conversions without their remove steps, left extra chunks in bundles
                if (IsDai)
                    ReconcileTextureChunks();

#if FROSTY_DEVELOPER
                PrintLog(modPackName);
#elif !FROSTY_DEVELOPER
                if (Config.Get<bool>("PrintLog", false))
                {
                    PrintLog(modPackName);
                }
#endif

                cancelToken.ThrowIfCancellationRequested();
                logStepTime("Applying handlers");
                progress.Begin("Cleaning up ModData");
                Logger.Log("Cleaning Up ModData");
                App.Logger.Log("Cleaning Up ModData");

                List<SymLinkStruct> cmdArgs = new List<SymLinkStruct>();
                bool newInstallation = false;

                // [DS fix] ModData made with another folder layout can link straight into the game's folders: start it over so nothing in the game is changed
                if (PathGoesThroughLink(modDataPath, m_patchPath))
                {
                    App.Logger.Log("ModData was made with a different folder layout and is rebuilt from scratch");
                    DeleteWithoutFollowingLinks(new DirectoryInfo(modDataPath));
                }

                m_fs.ResetManifest();
                if (!DeleteSelectFiles(modDataPath + m_patchPath))
                {
                    if (!Directory.Exists(modDataPath))
                    {
                        newInstallation = true;
                        Logger.Log("Creating ModData");

                        // create mod path
                        Directory.CreateDirectory(modDataPath);

                        if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.StarWarsSquadrons))
                        {
                            if (!Directory.Exists(modDataPath + "Data"))
                                Directory.CreateDirectory(modDataPath + "Data");

                            if (m_useAltSymLink)
                            {
                                Utils.Directory.CreateSymbolicLink(modDataPath + "Data/Win32", m_fs.BasePath + "Data/Win32");
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(modDataPath + "Data/Win32", m_fs.BasePath + "Data/Win32", true));
                            }
                        }
                        else if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield5, ProfileVersion.NeedForSpeedUnbound, ProfileVersion.DragonAgeTheVeilguard, ProfileVersion.DeadSpace))
                        {
                            // bfv and unbound dont have a patch directory so we need to rebuild the data folder structure instead
                            if (!Directory.Exists(modDataPath + "Data"))
                                Directory.CreateDirectory(modDataPath + "Data");

                            foreach (string casFilename in Directory.EnumerateFiles(m_fs.BasePath + m_patchPath, "*.cas", SearchOption.AllDirectories))
                            {
                                FileInfo casFi = new FileInfo(casFilename);
                                string destPath = casFi.Directory.FullName.ToLower().Replace("\\" + m_patchPath.ToLower(), "\\" + m_modDirName.ToLower() + "\\" + m_patchPath.ToLower());
                                string tempPath = Path.Combine(destPath, casFi.Name);

                                if (!Directory.Exists(destPath))
                                    Directory.CreateDirectory(destPath);

                                if (m_useAltSymLink)
                                {
                                    Utils.File.CreateSymbolicLink(tempPath, casFi.FullName);
                                }
                                else
                                {
                                    cmdArgs.Add(new SymLinkStruct(tempPath, casFi.FullName, false));
                                }
                            }
                        }
                        else
                        {
                            // data path
                            if (m_useAltSymLink)
                            {
                                Utils.Directory.CreateSymbolicLink(modDataPath + "Data", m_fs.BasePath + "Data");
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(modDataPath + "Data", m_fs.BasePath + "Data", true));
                            }
                        }

                        if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                            ProfileVersion.Battlefield4,
                            ProfileVersion.NeedForSpeed,
                            ProfileVersion.PlantsVsZombiesGardenWarfare2,
                            ProfileVersion.NeedForSpeedRivals))
                        {
                            // create update dir if it does not exist
                            if (!Directory.Exists(modDataPath + "Update"))
                                Directory.CreateDirectory(modDataPath + "Update");

                            // update paths
                            foreach (string path in Directory.EnumerateDirectories(m_fs.BasePath + "Update"))
                            {
                                DirectoryInfo di = new DirectoryInfo(path);

                                // ignore the patch directory
                                if (di.Name.ToLower() != "patch")
                                {
                                    if (m_useAltSymLink)
                                    {
                                        Utils.Directory.CreateSymbolicLink(modDataPath + "Update/" + di.Name, di.FullName);
                                    }
                                    else
                                    {
                                        cmdArgs.Add(new SymLinkStruct(modDataPath + "Update/" + di.Name, di.FullName, true));
                                    }
                                }
                            }
                        }
                        else if (m_hasUpdateFolder)
                        {
                            // update path
                            if (m_useAltSymLink)
                            {
                                Utils.Directory.CreateSymbolicLink(modDataPath + "Update", m_fs.BasePath + "Update");
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(modDataPath + "Update", m_fs.BasePath + "Update", true));
                            }
                        }

                        if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19, ProfileVersion.Anthem,
                            ProfileVersion.Madden20, ProfileVersion.Fifa20,
                            ProfileVersion.NeedForSpeedHeat, ProfileVersion.PlantsVsZombiesBattleforNeighborville,
                            ProfileVersion.Fifa21, ProfileVersion.Madden22,
                            ProfileVersion.Fifa22, ProfileVersion.Madden23))
                        {
                            foreach (string casFilename in Directory.EnumerateFiles(m_fs.BasePath + m_patchPath, "*.cas", SearchOption.AllDirectories))
                            {
                                FileInfo casFi = new FileInfo(casFilename);
                                string destPath = casFi.Directory.FullName.ToLower().Replace("\\" + m_patchPath.ToLower(), "\\" + m_modDirName.ToLower() + "\\" + m_patchPath.ToLower());
                                string tempPath = Path.Combine(destPath, casFi.Name);

                                if (!Directory.Exists(destPath))
                                    Directory.CreateDirectory(destPath);

                                if (m_useAltSymLink)
                                {
                                    Utils.File.CreateSymbolicLink(tempPath, casFi.FullName);
                                }
                                else
                                {
                                    cmdArgs.Add(new SymLinkStruct(tempPath, casFi.FullName, false));
                                }
                            }
                        }
                    }
                }

                // if there is a gamedir/shader_cache folder, symlink it
                string baseShaderCachePath = Path.Combine(m_fs.BasePath, "shader_cache");
                string shaderCacheLinkPath = Path.Combine(modDataPath, "shader_cache");
                if (Directory.Exists(baseShaderCachePath))
                {
                    DirectoryInfo shaderCacheLink = new DirectoryInfo(shaderCacheLinkPath);
                    if (Config.Get<bool>("DisableShaderCacheSymLink", false))
                    {
                        if (shaderCacheLink.Exists)
                        {
                            try
                            {
                                shaderCacheLink.Delete();
                            }
                            catch
                            {
                                App.Logger.Log("Error deleting shader cache symbolic link");
                            }
                        }
                    }
                    else if (!shaderCacheLink.Exists)
                    {
                        if (m_useAltSymLink)
                        {
                            Utils.Directory.CreateSymbolicLink(shaderCacheLinkPath, baseShaderCachePath);
                        }
                        else
                        {
                            cmdArgs.Add(new SymLinkStruct(shaderCacheLinkPath, baseShaderCachePath, true));
                        }
                    }
                }

                // add cas files to link
                foreach (string catalog in m_fs.Catalogs)
                {
                    string path = m_fs.ResolvePath("native_patch/" + catalog + "/cas.cat");
                    if (!m_hasPatchFolder) //again, no patch directory. fun.
                    {
                        path = m_fs.ResolvePath("native_data/" + catalog + "/cas.cat");
                    }
                    if (!File.Exists(path))
                        continue;

                    FileInfo catInfo = new FileInfo(path);
                    string destPath = catInfo.Directory.FullName.Replace("\\" + m_patchPath.ToLower(), "\\" + m_modDirName.ToLower() + "\\" + m_patchPath.ToLower());

                    if (!Directory.Exists(destPath))
                        Directory.CreateDirectory(destPath);

                    foreach (FileInfo fi in catInfo.Directory.GetFiles())
                    {
                        string tempPath = Path.Combine(destPath, fi.Name);
                        if (fi.Extension == ".cas")
                        {
                            if (File.Exists(tempPath))
                                continue;

                            if (m_useAltSymLink)
                            {
                                Utils.File.CreateSymbolicLink(tempPath, fi.FullName);
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(tempPath, fi.FullName, false));
                            }
                        }
                        else if (fi.Extension == ".cat")
                            fi.CopyTo(tempPath, false);
                    }
                }

                DiagnosticLog.Log($"ModData folder {(newInstallation ? "created" : "reused")}; {cmdArgs.Count} links to the game's data to create{(m_useAltSymLink ? ", alternate link method on" : "")}");
                foreach (SymLinkStruct link in cmdArgs)
                    DiagnosticLog.Detail($"  link {link.dest} -> {link.src}");

                if (cmdArgs.Count > 0)
                {
                    string reason = "New patch detected.";
                    if (newInstallation)
                        reason = "New installation detected.";

                    FrostyMessageBox.Show(reason + "\r\n\r\nShortly you will be prompted for elevated privileges, this is required to create symbolic links between the original data and the new modified data. Please ensure that you accept this to avoid any issues.", "Frosty Toolsuite");
                    if (!RunSymbolicLinkProcess(cmdArgs))
                    {
                        FrostyMessageBox.Show("Frosty needs to generate symbolic links, please ensure that you accept this so you don't have to regenerate ModData.", "Frosty Editor");
                        if (!RunSymbolicLinkProcess(cmdArgs))
                        {
                            Directory.Delete(modDataPath, true);
                            FrostyMessageBox.Show("One ore more symbolic links could not be created, please restart tool as Administrator and ensure your storage drive is formatted to NTFS (not exFAT).", "Frosty Editor");
                            return -1;
                        }
                    }
                }

                logStepTime("Cleaning up ModData");
                List<string> slowSuperBundles = new List<string>();

                // modify tocs and sbs
                cancelToken.ThrowIfCancellationRequested();
                progress.Begin("Applying mods");
                Logger.Log("Applying Mods");
                App.Logger.Log("Applying Mods");

                cmdArgs.Clear();

                // nfs heat and pvzbfn
                if (ProfilesLibrary.IsLoaded(ProfileVersion.Anthem, ProfileVersion.NeedForSpeedHeat,
                    ProfileVersion.PlantsVsZombiesBattleforNeighborville, ProfileVersion.Fifa21,
                    ProfileVersion.Madden22, ProfileVersion.Fifa22,
                    ProfileVersion.Madden23, ProfileVersion.NeedForSpeedUnbound, ProfileVersion.DragonAgeTheVeilguard, ProfileVersion.DeadSpace))
                {
                    CasBundleAction.CasFiles.Clear();
                    foreach (string catalog in m_fs.Catalogs)
                    {
                        int casIndex = 1;
                        string path = m_fs.BasePath + m_patchPath + "\\" + catalog + "\\cas_" + casIndex.ToString("D2") + ".cas";

                        while (File.Exists(path))
                        {
                            casIndex++;
                            path = m_fs.BasePath + m_patchPath + "\\" + catalog + "\\cas_" + casIndex.ToString("D2") + ".cas";
                        }

                        // [DS fix] Dead Space cas numbering can have gaps: new cas files go after the highest one
                        if (ProfilesLibrary.IsLoaded(ProfileVersion.DeadSpace))
                            casIndex = Math.Max(casIndex, HighestCasIndex(m_fs.BasePath + m_patchPath + "\\" + catalog) + 1);

                        CasBundleAction.CasFiles.Add(catalog, casIndex);
                    }

                    List<CasBundleAction> actions = new List<CasBundleAction>();
                    ManualResetEvent doneEvent = new ManualResetEvent(false);

                    // @todo: Added bundles

                    int totalTasks = 0;
                    foreach (SuperBundleInfo superBundle in m_fs.EnumerateSuperBundleInfos())
                    {
                        if (m_fs.ResolvePath(superBundle.Name + ".toc") == "")
                            continue;

                        CasBundleAction action = new CasBundleAction(superBundle, doneEvent, this);
                        WorkerPool.Post(() => action.ThreadPoolCallback(null));
                        actions.Add(action);
                        m_numTasks++;
                        totalTasks++;
                    }

                    while (m_numTasks != 0)
                    {
                        // show progress
                        cancelToken.ThrowIfCancellationRequested();
                        ReportProgress(totalTasks - m_numTasks, totalTasks);
                        if (progress.Due)
                            progress.Show($"{totalTasks - m_numTasks} of {totalTasks} superbundles done", actions.Select(a => a.Describe()));
                        Thread.Sleep(1);
                    }

                    foreach (CasBundleAction completedAction in actions)
                    {
                        if (completedAction.HasErrored)
                        {
                            // if any of the threads caused an exception, throw it to the global handler
                            // as the game data is now in an inconsistent state
                            throw completedAction.Exception;
                        }

                        string srcPath = m_fs.ResolvePath($"native_patch/{completedAction.SuperBundleInfo.Name}.toc");
                        if (!m_hasPatchFolder)
                        {
                            srcPath = m_fs.ResolvePath($"native_data/{completedAction.SuperBundleInfo.Name}.toc");
                        }
                        FileInfo sbFi = new FileInfo(modDataPath + m_patchPath + "/" + completedAction.SuperBundleInfo.Name + ".toc");
                        if (!sbFi.Exists && srcPath != string.Empty)
                        {
                            Directory.CreateDirectory(sbFi.DirectoryName);

                            if (m_useAltSymLink)
                            {
                                Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                            }
                        }

                        foreach (string catalog in completedAction.SuperBundleInfo.SplitSuperBundles)
                        {
                            string name = completedAction.SuperBundleInfo.Name.Replace("win32", catalog);
                            srcPath = m_fs.ResolvePath($"native_patch/{name}.toc");
                            if (!m_hasPatchFolder)
                            {
                                srcPath = m_fs.ResolvePath($"native_data/{name}.toc");
                            }
                            sbFi = new FileInfo(modDataPath + m_patchPath + "/" + name + ".toc");
                            if (!sbFi.Exists && srcPath != string.Empty)
                            {
                                Directory.CreateDirectory(sbFi.DirectoryName);

                                if (m_useAltSymLink)
                                {
                                    Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                                }
                                else
                                {
                                    cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                                }
                            }
                        }

                        if (completedAction.HasSb)
                        {
                            srcPath = m_fs.ResolvePath($"native_patch/{completedAction.SuperBundleInfo.Name}.sb");
                            if (!m_hasPatchFolder)
                            {
                                srcPath = m_fs.ResolvePath($"native_data/{completedAction.SuperBundleInfo.Name}.sb");
                            }
                            sbFi = new FileInfo(modDataPath + m_patchPath + "/" + completedAction.SuperBundleInfo.Name + ".sb");
                            if (!sbFi.Exists && srcPath != string.Empty)
                            {
                                Directory.CreateDirectory(sbFi.DirectoryName);

                                if (m_useAltSymLink)
                                {
                                    Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                                }
                                else
                                {
                                    cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                                }
                            }

                            foreach (string catalog in completedAction.SuperBundleInfo.SplitSuperBundles)
                            {
                                string name = completedAction.SuperBundleInfo.Name.Replace("win32", catalog);
                                srcPath = m_fs.ResolvePath($"native_patch/{name}.sb");
                                if (!m_hasPatchFolder)
                                {
                                    srcPath = m_fs.ResolvePath($"native_data/{name}.sb");
                                }
                                sbFi = new FileInfo(modDataPath + m_patchPath + "/" + name + ".sb");
                                if (!sbFi.Exists && srcPath != string.Empty)
                                {
                                    Directory.CreateDirectory(sbFi.DirectoryName);

                                    if (m_useAltSymLink)
                                    {
                                        Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                                    }
                                    else
                                    {
                                        cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                                    }
                                }
                            }
                        }
                    }
                }

                // fifa19-20 and madden20
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19, ProfileVersion.Fifa20, ProfileVersion.Madden20))
                {
                    DbObject layout = null;
                    using (DbReader reader = new DbReader(new FileStream(m_fs.BasePath + m_patchPath + "/layout.toc", FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
                        layout = reader.ReadDbObject();

                    FifaBundleAction.CasFileCount = m_fs.CasFileCount;
                    List<FifaBundleAction> actions = new List<FifaBundleAction>();
                    ManualResetEvent doneEvent = new ManualResetEvent(false);

                    // @todo: Added bundles

                    int totalTasks = 0;
                    foreach (CatalogInfo ci in m_fs.EnumerateCatalogInfos())
                    {
                        FifaBundleAction action = new FifaBundleAction(ci, doneEvent, this, cancelToken);
                        WorkerPool.Post(() => action.ThreadPoolCallback(null));
                        actions.Add(action);
                        m_numTasks++;
                        totalTasks++;
                    }

                    while (m_numTasks != 0)
                    {
                        // show progress
                        cancelToken.ThrowIfCancellationRequested();
                        ReportProgress(totalTasks - m_numTasks, totalTasks);
                        Thread.Sleep(1);
                    }

                    foreach (FifaBundleAction completedAction in actions)
                    {
                        if (completedAction.HasErrored)
                        {
                            // if any of the threads caused an exception, throw it to the global handler
                            // as the game data is now in an inconsistent state
                            throw completedAction.Exception;
                        }

                        if (completedAction.CasFiles.Count > 0)
                        {
                            DbObject installManifest = layout.GetValue<DbObject>("installManifest");
                            foreach (DbObject installChunk in installManifest.GetValue<DbObject>("installChunks"))
                            {
                                if (completedAction.CatalogInfo.Name.Equals("win32/" + installChunk.GetValue<string>("name")))
                                {
                                    foreach (int idx in completedAction.CasFiles.Keys)
                                    {
                                        DbObject casFileEntry = DbObject.CreateObject();
                                        casFileEntry.SetValue("id", idx);
                                        casFileEntry.SetValue("path", completedAction.CasFiles[idx]);
                                        installChunk.GetValue<DbObject>("files").Add(casFileEntry);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // write out layout.toc with additional cas entries where required
                    using (DbWriter writer = new DbWriter(new FileStream(modDataPath + m_patchPath + "/layout.toc", FileMode.Create), true))
                        writer.Write(layout);
                }

                // swbf2, bfv, and sws
                else if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons))
                {
                    List<ManifestBundleAction> actions = new List<ManifestBundleAction>();
                    ManualResetEvent doneEvent = new ManualResetEvent(false);

                    if (m_addedBundles.Count != 0)
                    {
                        int hash = Fnv1a.HashString("<none>");
                        foreach (string bundleName in m_addedBundles[hash])
                            m_fs.AddManifestBundle(new ManifestBundleInfo() { hash = Fnv1.HashString(bundleName) });
                    }

                    Dictionary<string, List<ModBundleInfo>> tasks = new Dictionary<string, List<ModBundleInfo>>();
                    foreach (ModBundleInfo bundle in m_modifiedBundles.Values)
                    {
                        ManifestBundleInfo manifestBundle = m_fs.GetManifestBundle(bundle.Name);
                        string catalog = m_fs.GetCatalog(manifestBundle.files[0].file);

                        if (!tasks.ContainsKey(catalog))
                            tasks.Add(catalog, new List<ModBundleInfo>());

                        tasks[catalog].Add(bundle);
                    }

                    int totalTasks = 0;
                    foreach (List<ModBundleInfo> task in tasks.Values)
                    {
                        ManifestBundleAction action = new ManifestBundleAction(task, doneEvent, this, cancelToken);
                        WorkerPool.Post(() => action.ThreadPoolCallback(null));
                        actions.Add(action);
                        m_numTasks++;
                        totalTasks++;
                    }

                    while (m_numTasks != 0)
                    {
                        // show progress
                        cancelToken.ThrowIfCancellationRequested();
                        ReportProgress(totalTasks - m_numTasks, totalTasks);
                        Thread.Sleep(1);
                    }

                    foreach (ManifestBundleAction completedAction in actions)
                    {
                        if (completedAction.Exception != null)
                        {
                            // if any of the threads caused an exception, throw it to the global handler
                            // as the game data is now in an inconsistent state
                            throw completedAction.Exception;
                        }

                        if (completedAction.DataRefs.Count > 0)
                        {
                            // add refs to be added to cas (and manifest)
                            for (int i = 0; i < completedAction.DataRefs.Count; i++)
                                m_casData.Add(m_fs.GetCatalog(completedAction.FileInfos[i].FileInfo.file), completedAction.DataRefs[i], completedAction.FileInfos[i].Entry, completedAction.FileInfos[i].FileInfo);
                        }
                    }

                    // now process manifest chunk changes
                    if (m_modifiedSuperBundles.ContainsKey(0))
                    {
                        foreach (Guid id in m_modifiedSuperBundles[0].Modify.Chunks)
                        {
                            ChunkAssetEntry entry = m_modifiedChunks[id];
                            ManifestChunkInfo ci = m_fs.GetManifestChunk(entry.Id);

                            if (ci != null)
                            {
                                if (entry.TocChunkSpecialHack)
                                {
                                    // change to using the first catalog
                                    ci.file.file = new ManifestFileRef(0, false, 0);
                                }

                                m_casData.Add(m_fs.GetCatalog(ci.file.file), entry.Sha1, entry, ci.file);
                            }
                        }
                        foreach (Guid id in m_modifiedSuperBundles[0].Add.Chunks)
                        {
                            ChunkAssetEntry entry = m_modifiedChunks[id];

                            ManifestChunkInfo ci = new ManifestChunkInfo
                            {
                                guid = entry.Id,
                                file = new ManifestFileInfo { file = new ManifestFileRef(0, false, 0), isChunk = true }
                            };
                            m_fs.AddManifestChunk(ci);

                            m_casData.Add(m_fs.GetCatalog(ci.file.file), entry.Sha1, entry, ci.file);
                        }
                    }
                }

                // remaining games
                else
                {
                    // [DAI fix] superbundles, mod loading and handlers run on WorkerPool (CPU threads / priority options)
                    List<SuperBundleAction> actions = new List<SuperBundleAction>();
                    ManualResetEvent doneEvent = new ManualResetEvent(false);

                    foreach (string superBundle in m_fs.SuperBundles)
                    {
                        if (m_fs.ResolvePath(superBundle + ".toc") == "")
                            continue;

                        actions.Add(new SuperBundleAction(superBundle, doneEvent, this, m_modDirName + "/" + m_patchPath, cancelToken));
                    }

                    foreach (string superBundle in m_addedSuperBundles)
                        actions.Add(new SuperBundleAction(superBundle, doneEvent, this, m_modDirName + "/" + m_patchPath, cancelToken));

                    // [DAI fix] start the biggest superbundles first; results are still used in the original order
                    List<SuperBundleAction> startOrder = new List<SuperBundleAction>(actions);
                    if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition, ProfileVersion.MassEffectAndromeda))
                        startOrder = actions.OrderByDescending(a => GetSuperBundleSize(a.SuperBundle)).ToList();

                    int totalTasks = actions.Count;
                    m_numTasks += totalTasks;
                    foreach (SuperBundleAction action in startOrder)
                        WorkerPool.Post(() => action.ThreadPoolCallback(null));

                    while (m_numTasks != 0)
                    {
                        // show progress
                        cancelToken.ThrowIfCancellationRequested();
                        ReportProgress(totalTasks - m_numTasks, totalTasks);
                        if (progress.Due)
                            progress.Show($"{totalTasks - m_numTasks} of {totalTasks} superbundles done", startOrder.Select(a => a.Describe()));
                        Thread.Sleep(1);
                    }

                    foreach (SuperBundleAction completedAction in actions)
                    {
                        if (completedAction.HasErrored)
                        {
                            // if any of the threads caused an exception, throw it to the global handler
                            // as the game data is now in an inconsistent state
                            throw completedAction.Exception;
                        }

                        if (!completedAction.TocModified)
                        {
                            string srcPath = m_fs.ResolvePath(completedAction.SuperBundle + ".toc");
                            FileInfo sbFi = new FileInfo(modDataPath + m_patchPath + "/" + completedAction.SuperBundle + ".toc");

                            if (!Directory.Exists(sbFi.DirectoryName))
                                Directory.CreateDirectory(sbFi.DirectoryName);

                            if (m_useAltSymLink)
                            {
                                Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                            }
                        }

                        if (!completedAction.SbModified)
                        {
                            string srcPath = m_fs.ResolvePath(completedAction.SuperBundle + ".sb");
                            FileInfo sbFi = new FileInfo(modDataPath + m_patchPath + "/" + completedAction.SuperBundle + ".sb");

                            if (!Directory.Exists(sbFi.DirectoryName))
                                Directory.CreateDirectory(sbFi.DirectoryName);

                            if (m_useAltSymLink)
                            {
                                Utils.File.CreateSymbolicLink(sbFi.FullName, srcPath);
                            }
                            else
                            {
                                cmdArgs.Add(new SymLinkStruct(sbFi.FullName, srcPath, false));
                            }
                        }

                        if (completedAction.CasRefs.Count != 0)
                        {
                            string catalogPath = m_fs.GetCatalogFromSuperBundle(completedAction.SuperBundle);
                            for (int i = 0; i < completedAction.CasRefs.Count; i++)
                                m_casData.Add(catalogPath, completedAction.CasRefs[i]);
                        }
                    }

                    // [DAI fix] the slowest superbundles go to the log, to see what holds up this step
                    foreach (SuperBundleAction slowAction in actions.Where(a => a.Seconds >= 1.0).OrderByDescending(a => a.Seconds).Take(3))
                    {
                        string details = (slowAction.ReadSeconds + slowAction.CompressSeconds > 0)
                            ? $" (unpacking {slowAction.ReadSeconds:0.0}s, compressing {slowAction.CompressBytes / 1048576.0:0} MB took {slowAction.CompressSeconds:0.0}s)"
                            : "";
                        slowSuperBundles.Add($"  {slowAction.SuperBundle} took {slowAction.Seconds:0.0}s{details}");
                    }
                }

                cancelToken.ThrowIfCancellationRequested();
                if (cmdArgs.Count > 0)
                {
                    DiagnosticLog.Log($"{cmdArgs.Count} links to unchanged toc/sb files to create");
                    if (!RunSymbolicLinkProcess(cmdArgs))
                        DiagnosticLog.Log("Some of those links could not be created");
                }

                logStepTime("Applying mods");
                foreach (string line in slowSuperBundles)
                    App.Logger.Log(line);
                progress.Begin("Writing archive data");
                Logger.Log("Writing Archive Data");
                App.Logger.Log("Writing Archive Data");

                int totalEntries = m_casData.CountEntries();
                int currentEntry = 0;
                if (totalEntries > 0)
                {
                    ReportProgress(currentEntry, totalEntries);
                }

                // write out cas and modify cats
                foreach (CasDataEntry entry in m_casData.EnumerateEntries())
                {
                    if (!entry.HasEntries)
                        continue;

                    cancelToken.ThrowIfCancellationRequested();
                    if (!File.Exists(modDataPath + m_patchPath + "\\" + entry.Catalog + "\\cas.cat"))
                    {
                        if (!File.Exists(m_fs.BasePath + "data\\" + entry.Catalog + "\\cas.cat"))
                            continue;

                        using (NativeReader reader = new NativeReader(new FileStream(m_fs.BasePath + "data\\" + entry.Catalog + "\\cas.cat", FileMode.Open, FileAccess.Read)))
                        {
                            FileInfo fi = new FileInfo(modDataPath + m_patchPath + "\\" + entry.Catalog + "\\cas.cat");
                            if (!fi.Directory.Exists)
                                Directory.CreateDirectory(fi.Directory.FullName);

                            using (NativeWriter writer = new NativeWriter(new FileStream(modDataPath + m_patchPath + "\\" + entry.Catalog + "\\cas.cat", FileMode.Create)))
                            {
                                writer.Write(reader.ReadBytes(0x23C));
                                writer.Write(0x00);
                                writer.Write(0x00);
                                if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda,
                                    ProfileVersion.Fifa17,
                                    ProfileVersion.Fifa18,
                                    ProfileVersion.StarWarsBattlefrontII,
                                    ProfileVersion.NeedForSpeedPayback,
                                    ProfileVersion.Madden19,
                                    ProfileVersion.Battlefield5,
                                    ProfileVersion.StarWarsSquadrons))
                                {
                                    writer.Write(0x00);
                                    writer.Write(0x00);
                                    writer.Write(-1);
                                    writer.Write(-1);
                                }
                            }
                        }
                    }

                    if (progress.Due)
                        progress.Show($"{currentEntry} of {totalEntries}", new[] { entry.Catalog });
                    WriteArchiveData(modDataPath + m_patchPath + "\\" + entry.Catalog, entry);

                    ReportProgress(currentEntry++, totalEntries);
                }

                cancelToken.ThrowIfCancellationRequested();

                logStepTime("Writing archive data");
                progress.Begin("Writing manifest");
                Logger.Log("Writing Manifest");
                App.Logger.Log("Writing Manifest");

                // finally copy in the left over patch data
                if (m_modifiedFs.Count > 0)
                {
                    m_fs.WriteInitFs(m_fs.BasePath + m_patchPath + "/initfs_win32", modDataPath + m_patchPath + "/initfs_win32", m_modifiedFs);
                }
                else
                {
                    CopyFileIfRequired(Path.Combine(m_fs.BasePath, m_patchPath, "initfs_win32"), Path.Combine(modDataPath, m_patchPath, "initfs_win32"), false);
                }


                if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                    ProfileVersion.Battlefield4,
                    ProfileVersion.NeedForSpeed,
                    ProfileVersion.PlantsVsZombiesGardenWarfare2,
                    ProfileVersion.NeedForSpeedRivals))
                {
                    // modify layout.toc for any new superbundles added
                    DbObject layout = null;
                    using (DbReader reader = new DbReader(new FileStream(m_fs.BasePath + m_patchPath + "/layout.toc", FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
                        layout = reader.ReadDbObject();

                    foreach (string path in Directory.EnumerateFiles(modDataPath + m_patchPath, "*.sb", SearchOption.AllDirectories))
                    {
                        // remove path, and extension and replace \ with /
                        string sbName = path.Replace(modDataPath + m_patchPath + "\\", "").Replace("\\", "/").Replace(".sb", "");
                        foreach (DbObject entry in layout.GetValue<DbObject>("superBundles"))
                        {
                            if (entry.GetValue<string>("name").Equals(sbName, StringComparison.OrdinalIgnoreCase))
                            {
                                entry.RemoveValue("same");
                                entry.SetValue("delta", true);
                            }
                        }
                    }

                    using (DbWriter writer = new DbWriter(new FileStream(modDataPath + m_patchPath + "/layout.toc", FileMode.Create), true))
                        writer.Write(layout);
                }

                else if (!ProfilesLibrary.IsLoaded(ProfileVersion.Fifa19, ProfileVersion.Fifa20, ProfileVersion.Madden20))
                {
                    DbObject layout = null;
                    using (DbReader reader = new DbReader(new FileStream(m_fs.ResolvePath("layout.toc"), FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
                        layout = reader.ReadDbObject();

                    // write out new manifest
                    if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons))
                    {
                        DbObject manifest = layout.GetValue<DbObject>("manifest");
                        ManifestFileRef fileRef = (ManifestFileRef)manifest.GetValue<int>("file");

                        byte[] tmpBuf = m_fs.WriteManifest();
                        string catalog = m_fs.GetCatalog(fileRef);

                        // find the next available cas
                        int casIndex = 1;
                        while (File.Exists(modDataPath + m_patchPath + "/" + (string.Format("{0}\\cas_{1}.cas", catalog, casIndex.ToString("D2")))))
                            casIndex++;

                        Sha1 sha1 = Utils.GenerateSha1(tmpBuf);

                        m_archiveData.TryAdd(sha1, new ArchiveInfo() { Data = tmpBuf });
                        WriteArchiveData(modDataPath + m_patchPath + "/" + catalog, new CasDataEntry("", sha1));

                        manifest.SetValue("size", tmpBuf.Length);
                        manifest.SetValue("offset", 0);
                        manifest.SetValue("sha1", sha1);
                        if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield5)) //more patch directory shenanigans
                            manifest.SetValue("file", (int)new ManifestFileRef(fileRef.CatalogIndex, false, casIndex));
                        else
                            manifest.SetValue("file", (int)new ManifestFileRef(fileRef.CatalogIndex, true, casIndex));
                    }

                    // add any new superbundles
                    if (m_addedSuperBundles.Count > 0)
                    {
                        foreach (string superBundle in m_addedSuperBundles)
                        {
                            DbObject sbobj = new DbObject();
                            sbobj.SetValue("name", superBundle);
                            layout.GetValue<DbObject>("superBundles").Add(sbobj);

                            DbObject chunk = (DbObject)layout.GetValue<DbObject>("installManifest").GetValue<DbObject>("installChunks")[1];
                            chunk.GetValue<DbObject>("superbundles").Add(superBundle);
                        }
                    }

                    string layoutLocation = modDataPath + m_patchPath + "/layout.toc";
                    if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons))
                        layoutLocation = modDataPath + "Data/layout.toc";

                    using (DbWriter writer = new DbWriter(new FileStream(layoutLocation, FileMode.Create), true))
                        writer.Write(layout);
                }

                // fifa17, bf4, nfs, nfsr, gw2 (dai writes its own after this block)
                if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa17,
                    ProfileVersion.Battlefield4,
                    ProfileVersion.NeedForSpeed,
                    ProfileVersion.NeedForSpeedRivals,
                    ProfileVersion.PlantsVsZombiesGardenWarfare2))
                {
                    // copy additional files
                    CopyFileIfRequired(m_fs.BasePath + m_patchPath + "/../package.mft", modDataPath + m_patchPath + "/../package.mft");
                }

                // swbf2, bfv, sws
                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons))
                {
                    // copy from old data to new data
                    CopyFileIfRequired(m_fs.BasePath + "Data/chunkmanifest", modDataPath + "Data/chunkmanifest");
                    if (m_modifiedFs.Count > 0)
                    {
                        m_fs.WriteInitFs(m_fs.BasePath + "Data/initfs_Win32", modDataPath + "Data/initfs_Win32", m_modifiedFs);
                    }
                    else
                    {
                        CopyFileIfRequired(Path.Combine(m_fs.BasePath, "Data", "initfs_Win32"), Path.Combine(modDataPath, "Data", "initfs_Win32"), false);
                    }
                }

                // Process DEX Mods              
                string dexModPath = Path.Combine(modDataPath, m_patchPath, "DAVExtender");
                DirectoryInfo dexModDir = new DirectoryInfo(dexModPath);

                if (Directory.Exists(dexModPath))
                {
                    dexModDir.Delete(true);
                }

                foreach (FrostyMod mod in modList)
                {
                    if (mod.NewFormat && mod.ModDetails.HasDexResource)
                    {
                        Parallel.ForEach(mod.Resources, resource =>
                        {
                            if (resource.Type == ModResourceType.Embedded && resource.Name == "DexResource" && resource.Size != 0)
                            {
                                Directory.CreateDirectory(Path.Combine(dexModPath, mod.Filename));

                                byte[] dexResource = mod.GetResourceData(resource);

                                Stream data = new MemoryStream(dexResource);

                                ZipArchive archive = new ZipArchive(data);
                                archive.ExtractToDirectory(Path.Combine(dexModPath, mod.Filename));
                            }
                        });
                    }
                }

                // create the frosty mod list file
                File.WriteAllText(Path.Combine(modDataPath, m_patchPath, "mods.json"), JsonConvert.SerializeObject(GenerateModInfoList(modPaths, rootPath), Formatting.Indented));
                File.WriteAllText(dataVersionPath, DataVersions.ModDataVersion.ToString());
                if (DiagnosticLog.Enabled)
                    LogDiagnostics(() => LogModDataContents(modDataPath));

                // stopwatch
                watch.Stop();
                if (watch.Elapsed.Minutes > 0)
                {
                    App.Logger.Log($"Applied Mods in {watch.Elapsed.Minutes}m {watch.Elapsed.Seconds}s");
                }
                else
                {
                    App.Logger.Log($"Applied Mods in {watch.Elapsed.Seconds}s");
                }
            }
            else
            {
                App.Logger.Log("Launching with previously generated data.");
            }

            // [DAI fix] written on every launch, so a changed patch version choice needs no rebuild
            if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition))
                DaiPatchVersion.WriteModDataManifest(m_fs.BasePath, modDataPath, App.Logger);

            cancelToken.ThrowIfCancellationRequested();

            // DAI and NFS dont require bcrypt
            if (!ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                ProfileVersion.Battlefield4,
                ProfileVersion.NeedForSpeed,
                ProfileVersion.NeedForSpeedRivals))
            {
                // delete old useless bcrypt
                if (File.Exists(m_fs.BasePath + "bcrypt.dll"))
                    File.Delete(m_fs.BasePath + "bcrypt.dll");

                // copy over new CryptBase
                CopyFileIfRequired("ThirdParty/CryptBase.dll", m_fs.BasePath + "CryptBase.dll");
                if (DiagnosticLog.Enabled)
                    DiagnosticLog.Log("Frosty's CryptBase.dll in the game folder: " + DiagnosticLog.DescribeFile(m_fs.BasePath + "CryptBase.dll"));
            }
            CopyFileIfRequired(m_fs.BasePath + "user.cfg", modDataPath + "user.cfg");

            // FIFA games require a fifaconfig workaround
            if (ProfilesLibrary.IsLoaded(ProfileVersion.Fifa17,
                ProfileVersion.Fifa18,
                ProfileVersion.Fifa19,
                ProfileVersion.Fifa20))
            {
                FileInfo fi = new FileInfo(m_fs.BasePath + "FIFASetup\\fifaconfig_orig.exe");
                if (!fi.Exists)
                {
                    fi = new FileInfo(m_fs.BasePath + "FIFASetup\\fifaconfig.exe");
                    fi.MoveTo(fi.FullName.Replace(".exe", "_orig.exe"));
                }

                CopyFileIfRequired("thirdparty/fifaconfig.exe", m_fs.BasePath + "FIFASetup\\fifaconfig.exe");
            }

            // launch the game (redirecting to the modPath directory)
            Logger.Log("Launching Game");

            try
            {
                //KillEADesktop();
                //ModifyInstallerData($"-dataPath \"{modDataPath.Trim('\\')}\" {additionalArgs}");
                LaunchGame(m_fs.BasePath, m_modDirName, modDataPath, additionalArgs);
                //WaitForGame();
                //CleanUpInstalledData();
            }
            catch (Exception ex)
            {
                App.Logger.Log("Error Launching Game: " + ex);
            }

            App.Logger.Log("Done");

            GC.Collect();
            return 0;
        }

        // [DAI fix] a game left running (e.g. its window closed but the process stayed) can be ended from here
        private bool EndRunningGame()
        {
            string processName = ProfilesLibrary.ProfileName;
            List<int> ids = GetRunningGameIds(processName, null);
            if (ids.Count == 0)
                return true;

            string gameName = ProfilesLibrary.DisplayName;
            string idText = string.Join(", ", ids);
            DiagnosticLog.Log($"Launch: {processName}.exe is already running (process ID {idText})");

            if (!Config.Get<bool>("TerminateStuckGame", true))
            {
                if (FrostyMessageBox.Show($"{gameName} is already running (process ID {idText}), so it can't be launched again. This can happen when the game closes but doesn't fully shut down.\r\n\r\nDo you want Frosty to turn on Terminate Stuck Game and end {gameName} now? Anything not saved in that game will be lost. The option can be turned off again in Options.",
                    "Frosty Toolsuite", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes)
                {
                    DiagnosticLog.Log("Launch: cancelled, the game was left running (Terminate Stuck Game is off)");
                    return false;
                }

                Config.Add("TerminateStuckGame", true);
                Config.Save();
                DiagnosticLog.Log("Launch: Terminate Stuck Game turned on");
            }
            else if (FrostyMessageBox.Show($"{gameName} is still running (process ID {idText}), so it can't be launched again yet. This can happen when the game closes but doesn't fully shut down.\r\n\r\nDo you want Frosty to end it now and then launch? Anything not saved in that game will be lost.",
                "Frosty Toolsuite", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes)
            {
                DiagnosticLog.Log("Launch: cancelled, the game was left running");
                return false;
            }

            Logger.Log("Ending the running game");
            List<int> denied = new List<int>();
            foreach (int id in ids)
            {
                try
                {
                    using (Process process = Process.GetProcessById(id))
                        process.Kill();
                }
                catch (ArgumentException)
                {
                    // already exited
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    DiagnosticLog.Log($"Launch: could not end process {id}: {e.Message}");
                    denied.Add(id);
                }
                catch (Exception e)
                {
                    DiagnosticLog.Log($"Launch: could not end process {id}: {e.Message}");
                }
            }

            bool adminRefused = false;
            denied = GetRunningGameIds(processName, denied);
            if (denied.Count > 0)
            {
                DiagnosticLog.Log("Launch: asking for admin rights to end the game");
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo("taskkill.exe", "/F " + string.Join(" ", denied.Select(id => "/PID " + id)))
                    {
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (Process taskkill = Process.Start(info))
                        taskkill?.WaitForExit(15000);
                }
                catch (System.ComponentModel.Win32Exception e)
                {
                    // 1223: the Windows admin prompt was answered No
                    adminRefused = e.NativeErrorCode == 1223;
                    DiagnosticLog.Log($"Launch: ending the game as admin failed: {e.Message}");
                }
            }

            Logger.Log("Waiting for the game to close");
            Stopwatch wait = Stopwatch.StartNew();
            while (GetRunningGameIds(processName, ids).Count > 0 && !adminRefused && wait.Elapsed.TotalSeconds < 20)
                Thread.Sleep(250);

            List<int> left = GetRunningGameIds(processName, ids);
            if (left.Count == 0)
            {
                DiagnosticLog.Log($"Launch: the old game process closed after {wait.Elapsed.TotalSeconds:0.0}s");
                return true;
            }

            DiagnosticLog.Log($"Launch: the game is still running (process ID {string.Join(", ", left)}), launch cancelled");
            string reason = adminRefused ? "Windows needs admin rights to end it, and the admin prompt was declined." : "It didn't close when asked.";
            FrostyMessageBox.Show($"Frosty couldn't end {gameName} (process ID {string.Join(", ", left)}). {reason}\r\n\r\nTo end it yourself:\r\n1. Press Ctrl+Shift+Esc to open Task Manager. If that doesn't open it, press Ctrl+Alt+Del and choose Task Manager.\r\n2. If it opens as a small window, click More details.\r\n3. Go to the Details tab (on Windows 11 it's in the list on the left).\r\n4. Find {processName}.exe, right-click it and choose End task.\r\n5. If Windows asks whether you want to end it, click End process.\r\n6. Launch the game again from Frosty.\r\n\r\nIf it still won't close, restart your PC.",
                "Frosty Toolsuite");
            return false;
        }

        // ids of the game's processes still running; only those in 'among' when it is given
        private static List<int> GetRunningGameIds(string processName, List<int> among)
        {
            List<int> ids = new List<int>();
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                if (among == null || among.Contains(process.Id))
                    ids.Add(process.Id);
                process.Dispose();
            }
            return ids;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        // [DS fix] hard links to the exe and its DLLs (same drive, no extra space), made fresh each launch so a game update is picked up
        private static string PrepareExeInModData(string basePath, string modDataPath)
        {
            List<string> files = new List<string> { $"{ProfilesLibrary.ProfileName}.exe" };
            files.AddRange(Directory.EnumerateFiles(basePath, "*.dll").Select(Path.GetFileName));
            if (Directory.Exists(Path.Combine(basePath, "x64")))
                files.AddRange(Directory.EnumerateFiles(Path.Combine(basePath, "x64"), "*.dll").Select(f => Path.Combine("x64", Path.GetFileName(f))));

            // a folder link here (left by a manual test) would put the DLLs into the game's own x64 folder
            DirectoryInfo x64Folder = new DirectoryInfo(Path.Combine(modDataPath, "x64"));
            if (x64Folder.Exists && x64Folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
                x64Folder.Delete(false);

            foreach (string file in files)
            {
                string source = Path.Combine(basePath, file);
                string link = Path.Combine(modDataPath, file);
                Directory.CreateDirectory(Path.GetDirectoryName(link));

                FileInfo existing = new FileInfo(link);
                if (existing.Exists)
                {
                    if (existing.Attributes.HasFlag(FileAttributes.ReadOnly))
                        existing.Attributes &= ~FileAttributes.ReadOnly;
                    existing.Delete();
                }

                if (!CreateHardLink(link, source, IntPtr.Zero))
                {
                    DiagnosticLog.Log($"Could not link {file} into ModData (error {Marshal.GetLastWin32Error()}), copying it instead");
                    File.Copy(source, link, true);
                }
            }

            // stops the Steam API from restarting the game from its normal folder
            if (File.Exists(Path.Combine(basePath, "steam_api64.dll")))
                File.WriteAllText(Path.Combine(modDataPath, "steam_appid.txt"), DeadSpaceSteamAppId);

            return Path.Combine(modDataPath, $"{ProfilesLibrary.ProfileName}.exe");
        }

        private const string DeadSpaceSteamAppId = "1693980";

        private static int HighestCasIndex(string catalogPath)
        {
            int highest = 0;
            if (!Directory.Exists(catalogPath))
                return highest;

            foreach (string casPath in Directory.EnumerateFiles(catalogPath, "cas_*.cas"))
            {
                string number = Path.GetFileNameWithoutExtension(casPath).Substring(4);
                if (int.TryParse(number, out int index) && index > highest)
                    highest = index;
            }
            return highest;
        }

        public static void LaunchGame(string basePath, string modDirName, string modDataPath, string additionalArgs)
        {
            string gameExecutable = Path.Combine(basePath, $"{ProfilesLibrary.ProfileName}.exe");
            string dataPathArgument = $"-dataPath \"{modDataPath.Trim('\\')}\" {additionalArgs}";

            // [DS fix] Dead Space never splits its command line, so -dataPath is never seen; with no dataPath it uses the exe's own folder as the data root
            if (ProfilesLibrary.IsLoaded(ProfileVersion.DeadSpace))
            {
                string moddedExecutable = PrepareExeInModData(basePath, modDataPath);
                DiagnosticLog.Log($"Starting Dead Space from its ModData folder: \"{moddedExecutable}\" {additionalArgs}");
                ExecuteProcess(moddedExecutable, additionalArgs);
                return;
            }

            // [DAI fix] DAI and MEA Steam copies launch through Steam, as in 1.0.6.3
            bool steamCopy = (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition) || ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda))
                && File.Exists(Path.Combine(basePath, "steam_appid.txt"))
                && !Config.Get<bool>("DatapathFixEnabled", false);

            if (steamCopy || Config.Get<bool>("UseSteamProtocol", false))
            {
                string steamAppIdFilePath = Path.Combine(basePath, "steam_appid.txt");
                string steamAppId = null;

                if (File.Exists(steamAppIdFilePath))
                {
                    steamAppId = File.ReadLines(steamAppIdFilePath).FirstOrDefault();
                }

                if (string.IsNullOrEmpty(steamAppId) && ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeTheVeilguard))
                {
                    steamAppId = "1845910";
                }

                if (!string.IsNullOrEmpty(steamAppId))
                {
                    string arguments = $"-dataPath \"{modDirName.Replace('\\', '/')}\" {additionalArgs}".Trim();
                    string encodedArguments = Uri.EscapeDataString(arguments);

                    App.Logger.Log($"Launch: {arguments}");
                    App.Logger.Log($"Encoded: {encodedArguments}");
                    DiagnosticLog.Log($"Launching through Steam ({(steamCopy ? "Steam copy of DAI or MEA" : "Launch Game Using Steam Protocol option")}): steam://run/{steamAppId}//{encodedArguments}/");

                    Process.Start($"steam://run/{steamAppId}//{encodedArguments}/");
                }
                else
                {
                    // fallback
                    DiagnosticLog.Log($"No Steam app id found, starting the exe directly: \"{gameExecutable}\" {dataPathArgument}");
                    ExecuteProcess(gameExecutable, dataPathArgument);
                }
            }
            else
            {
                DiagnosticLog.Log($"Starting the exe directly: \"{gameExecutable}\" {dataPathArgument}");
                ExecuteProcess(gameExecutable, dataPathArgument);
            }
        }

        private static string DescribeProcessId(Process process)
        {
            try
            {
                return $" (process {process.Id})";
            }
            catch
            {
                return "";
            }
        }

        // diagnostics must never stop mods from being applied
        private static void LogDiagnostics(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                DiagnosticLog.Log("(diagnostics failed: " + e.Message + ")");
            }
        }

        // [DAI fix] Diagnostic Logging: what each applied mod contains and which game version it was made for
        private void LogModDiagnostics(List<FrostyMod> modList)
        {
            DiagnosticLog.Log($"Applying {modList.Count} mods in this order:");
            for (int i = 0; i < modList.Count; i++)
            {
                FrostyMod mod = modList[i];
                string contents = "old format (FBMOD, e.g. a converted .daimod)";
                if (mod.NewFormat)
                {
                    contents = string.Join(", ", mod.Resources.GroupBy(r => r.Type).Select(g => $"{g.Count()} {g.Key}"));
                    int handlers = mod.Resources.Count(r => r.HasHandler);
                    if (handlers > 0)
                        contents += $", {handlers} merged by handlers";
                }

                string version = mod.NewFormat ? $"made for game version {DiagnosticLog.DescribeGameVersion(mod.GameVersion)}{((mod.GameVersion == m_fs.Head) ? " (same as the game)" : " (game is " + DiagnosticLog.DescribeGameVersion(m_fs.Head) + ")")}" : "game version not stored";
                DiagnosticLog.Log($"  {i + 1}. '{mod.ModDetails?.Title ?? mod.Filename}' v{mod.ModDetails?.Version} by {mod.ModDetails?.Author}, {DiagnosticLog.DescribeFile(mod.Path)}, {version}; {contents}");
            }
        }

        private void LogModDataContents(string modDataPath)
        {
            int links = 0;
            long writtenBytes = 0;
            List<FileInfo> written = new List<FileInfo>();

            // linked folders (e.g. DAI's Data) point into the game, so they aren't walked
            Stack<DirectoryInfo> folders = new Stack<DirectoryInfo>();
            folders.Push(new DirectoryInfo(modDataPath));
            while (folders.Count > 0)
            {
                foreach (FileSystemInfo item in folders.Pop().EnumerateFileSystemInfos())
                {
                    if (item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        links++;
                    else if (item is DirectoryInfo folder)
                        folders.Push(folder);
                    else if (item is FileInfo fi)
                    {
                        written.Add(fi);
                        writtenBytes += fi.Length;
                    }
                }
            }

            DiagnosticLog.Log($"ModData now holds {written.Count} files Frosty wrote ({DiagnosticLog.Size(writtenBytes)}) and {links} links to unchanged game files; the list is in the diagnostic log file");
            foreach (FileInfo fi in written)
                DiagnosticLog.Detail($"  wrote {fi.FullName} ({DiagnosticLog.Size(fi.Length)})");
        }

        private void KillEADesktop()
        {
            foreach (Process process in Process.GetProcessesByName("EADesktop"))
            {
                try
                {
                    string processFilename = process?.MainModule?.ModuleName;
                    if (string.IsNullOrEmpty(processFilename))
                    {
                        continue;
                    }

                    FileInfo fi = new FileInfo(processFilename);
                    if (process.ProcessName.IndexOf("EADesktop.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }
                }
                catch
                {
                }
            }
        }

        private void WaitForGame()
        {
            DateTime time = DateTime.UtcNow;
            TimeSpan maxTime = new TimeSpan(0, 0, 30);

            while (true)
            {
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        string processFilename = process?.MainModule?.ModuleName;
                        if (string.IsNullOrEmpty(processFilename))
                        {
                            continue;
                        }

                        FileInfo fi = new FileInfo(processFilename);
                        if (process.ProcessName.IndexOf(ProfilesLibrary.ProfileName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
                if (DateTime.UtcNow > time + maxTime)
                {
                    break;
                }
            }
        }

        private void ModifyInstallerData(string parameters)
        {
            // clean up previous modification if it was not cleaned up already
            if (File.Exists(m_fs.BasePath + "__Installer/installerdata.xml.orig"))
            {
                if (File.Exists(m_fs.BasePath + "__Installer/installerdata.xml"))
                {
                    File.Delete(m_fs.BasePath + "__Installer/installerdata.xml");
                }
                File.Copy(m_fs.BasePath + "__Installer/installerdata.xml.orig", m_fs.BasePath + "__Installer/installerdata.xml");
            }
            else
            {
                if (File.Exists(m_fs.BasePath + "__Installer/installerdata.xml"))
                {
                    File.Copy(m_fs.BasePath + "__Installer/installerdata.xml", m_fs.BasePath + "__Installer/installerdata.xml.orig");
                }
            }

            System.Xml.Linq.XDocument doc = System.Xml.Linq.XDocument.Load(m_fs.BasePath + "__Installer/installerdata.xml");
            System.Xml.Linq.XElement parametersElem = doc.Root.Element("runtime")?.Elements("launcher").ToArray()[1].Element("parameters");
            parametersElem.SetValue(parameters);
            doc.Save(m_fs.BasePath + "__Installer/installerdata.xml");
        }

        private void CleanUpInstalledData()
        {
            if (File.Exists(m_fs.BasePath + "__Installer/installerdata.xml.orig") && File.Exists(m_fs.BasePath + "__Installer/installerdata.xml"))
            {
                File.Delete(m_fs.BasePath + "__Installer/installerdata.xml");
                File.Move(m_fs.BasePath + "__Installer/installerdata.xml.orig", m_fs.BasePath + "__Installer/installerdata.xml");
            }
        }

        private string GenerateModInfoHash(string filename)
        {
            FileInfo fi = new FileInfo(filename);

            return $"{fi.Length}{fi.LastWriteTimeUtc:ddMMyyyyHHmmss}";
        }

        private List<ModInfo> GenerateModInfoList(string[] modPaths, string rootPath)
        {
            List<ModInfo> modInfoList = new List<ModInfo>();

            foreach (string path in modPaths)
            {
                FileInfo fi = new FileInfo(Path.Combine(rootPath, path));
                FrostyMod fmod = new FrostyMod(fi.FullName, false);
                ModInfo modInfo;

                if (fmod.NewFormat)
                {
                    modInfo = new ModInfo
                    {
                        Name = fmod.ModDetails.Title,
                        Version = fmod.ModDetails.Version,
                        Category = fmod.ModDetails.Category,
                        Link = fmod.ModDetails.Link,
                        FileName = path,
                        Hash = GenerateModInfoHash(fi.FullName),
                    };
                }
                else
                {
                    FrostyModCollection fcollection = new FrostyModCollection(fi.FullName);
                    if (fcollection.IsValid)
                    {
                        modInfo = new ModInfo
                        {
                            Name = fcollection.ModDetails.Title,
                            Version = fcollection.ModDetails.Version,
                            Category = fcollection.ModDetails.Category,
                            Link = fcollection.ModDetails.Link,
                            FileName = path,
                            Hash = GenerateModInfoHash(fi.FullName),
                        };
                    }
                    else
                    {
                        DbObject mod = null;
                        using (DbReader reader = new DbReader(new FileStream(fi.FullName, FileMode.Open, FileAccess.Read), null))
                            mod = reader.ReadDbObject();

                        modInfo = new ModInfo
                        {
                            Name = mod.GetValue<string>("title"),
                            Version = mod.GetValue<string>("version"),
                            Category = mod.GetValue<string>("category"),
                            FileName = path
                        };
                    }

                }
                modInfoList.Add(modInfo);
            }
            return modInfoList;
        }

        private void WriteArchiveData(string catalog, CasDataEntry casDataEntry)
        {
            List<int> casEntries = new List<int>();
            int casIndex = 1;
            int totalSize = 0;

            // find the next available cas
            while (File.Exists(string.Format("{0}\\cas_{1}.cas", catalog, casIndex.ToString("D2"))))
                casIndex++;

            // write data to cas
            Stream currentCasStream = null;
            foreach (Sha1 sha1 in casDataEntry.EnumerateDataRefs())
            {
                ArchiveInfo info = m_archiveData[sha1];

                int casMaxBytes = 536870912;
                switch (Config.Get("MaxCasFileSize", "1GB"))
                {
                    case "1GB": casMaxBytes = 1073741824; break;
                    case "512MB": casMaxBytes = 536870912; break;
                    case "256MB": casMaxBytes = 268435456; break;
                }

                // if cas exceeds max size, create a new one (incrementing index)
                if (currentCasStream == null || ((totalSize + info.Data.Length) > casMaxBytes))
                {
                    if (currentCasStream != null)
                    {
                        currentCasStream.Dispose();
                        casIndex++;
                    }

                    FileInfo casFi = new FileInfo(string.Format("{0}\\cas_{1}.cas", catalog, casIndex.ToString("D2")));
                    Directory.CreateDirectory(casFi.DirectoryName);

                    currentCasStream = new FileStream(casFi.FullName, FileMode.Create, FileAccess.Write);
                    totalSize = 0;
                }

                if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition, ProfileVersion.Battlefield4, ProfileVersion.NeedForSpeed, ProfileVersion.NeedForSpeedRivals))
                {
                    byte[] tmpBuf = new byte[0x20];
                    using (NativeWriter tmpWriter = new NativeWriter(new MemoryStream(tmpBuf)))
                    {
                        tmpWriter.Write(0xF00FCEFA);
                        tmpWriter.Write(sha1);
                        tmpWriter.Write((long)info.Data.Length);
                    }
                    currentCasStream.Write(tmpBuf, 0, 0x20);
                    totalSize += 0x20;
                }

                foreach (CasFileEntry casEntry in casDataEntry.EnumerateFileInfos(sha1))
                {
                    if (casEntry.Entry != null && casEntry.Entry.RangeStart != 0 && !casEntry.FileInfo.isChunk)
                    {
                        casEntry.FileInfo.offset = (uint)(currentCasStream.Position + casEntry.Entry.RangeStart);
                        casEntry.FileInfo.size = (casEntry.Entry.RangeEnd - casEntry.Entry.RangeStart);
                    }
                    else
                    {
                        casEntry.FileInfo.offset = (uint)currentCasStream.Position;
                        casEntry.FileInfo.size = info.Data.Length;
                    }
                    if (ProfilesLibrary.IsLoaded(ProfileVersion.Battlefield5))
                    {
                        casEntry.FileInfo.file = new ManifestFileRef(casEntry.FileInfo.file.CatalogIndex, false, casIndex);
                    }
                    else
                    {
                        casEntry.FileInfo.file = new ManifestFileRef(casEntry.FileInfo.file.CatalogIndex, true, casIndex);
                    }
                }

                currentCasStream.Write(info.Data, 0, info.Data.Length);

                casEntries.Add(casIndex);
                totalSize += info.Data.Length;
            }
            currentCasStream.Dispose();

            FileInfo fi = new FileInfo(string.Format("{0}\\cas.cat", catalog));
            List<CatResourceEntry> entries = new List<CatResourceEntry>();
            List<CatPatchEntry> patchEntries = new List<CatPatchEntry>();
            List<CatResourceEntry> encEntries = new List<CatResourceEntry>();
            byte[] header = null;

            // read in original cat
            using (CatReader reader = new CatReader(new FileStream(fi.FullName, FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
            {
                for (int i = 0; i < reader.ResourceCount; i++)
                    entries.Add(reader.ReadResourceEntry());

                if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda,
                    ProfileVersion.Fifa17,
                    ProfileVersion.Fifa18,
                    ProfileVersion.NeedForSpeedPayback,
                    ProfileVersion.Madden19))
                {
                    for (int i = 0; i < reader.EncryptedCount; i++)
                        encEntries.Add(reader.ReadEncryptedEntry());
                }

                for (int i = 0; i < reader.PatchCount; i++)
                    patchEntries.Add(reader.ReadPatchEntry());

                reader.Position = 0;
                header = reader.ReadBytes(0x22C);
            }

            // write out new modified cat
            using (NativeWriter writer = new NativeWriter(new FileStream(fi.FullName, FileMode.Create)))
            {
                int numEntries = 0;
                int numPatchEntries = 0;

                MemoryStream ms = new MemoryStream();
                using (NativeWriter tmpWriter = new NativeWriter(ms))
                {
                    // unmodified entries
                    foreach (CatResourceEntry entry in entries)
                    {
                        //if (casDataEntry.Contains(entry.Sha1))
                        //    continue;

                        tmpWriter.Write(entry.Sha1);
                        tmpWriter.Write(entry.Offset);
                        tmpWriter.Write(entry.Size);
                        if (!ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                            ProfileVersion.Battlefield4,
                            ProfileVersion.NeedForSpeed,
                            ProfileVersion.NeedForSpeedRivals))
                        {
                            tmpWriter.Write(entry.LogicalOffset);
                        }
                        tmpWriter.Write(entry.ArchiveIndex);
                        numEntries++;
                    }

                    int offset = 0, index = 0, currentCasIndex = casEntries.Count > 0 ? casEntries[0] : 1;

                    // new entries
                    foreach (Sha1 sha1 in casDataEntry.EnumerateDataRefs())
                    {
                        if (currentCasIndex != casEntries[index])
                        {
                            offset = 0;
                            currentCasIndex = casEntries[index];
                        }

                        if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                            ProfileVersion.Battlefield4,
                            ProfileVersion.NeedForSpeed,
                            ProfileVersion.NeedForSpeedRivals))
                        {
                            offset += 0x20;
                        }

                        ArchiveInfo info = m_archiveData[sha1];

                        tmpWriter.Write(sha1);
                        tmpWriter.Write(offset);
                        tmpWriter.Write(info.Data.Length);
                        if (!ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                            ProfileVersion.Battlefield4,
                            ProfileVersion.NeedForSpeed,
                            ProfileVersion.NeedForSpeedRivals))
                        {
                            tmpWriter.Write(0x00);
                        }
                        tmpWriter.Write(casEntries[index++]);

                        offset += info.Data.Length;
                        numEntries++;
                    }

                    if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda,
                        ProfileVersion.Fifa17,
                        ProfileVersion.Fifa18,
                        ProfileVersion.NeedForSpeedPayback,
                        ProfileVersion.Madden19))
                    {
                        // encrypted entries
                        foreach (CatResourceEntry entry in encEntries)
                        {
                            tmpWriter.Write(entry.Sha1);
                            tmpWriter.Write(entry.Offset);
                            tmpWriter.Write(entry.EncryptedSize);
                            tmpWriter.Write(entry.LogicalOffset);
                            tmpWriter.Write(entry.ArchiveIndex);
                            tmpWriter.Write(entry.Unknown);
                            tmpWriter.WriteFixedSizedString(entry.KeyId, entry.KeyId.Length);
                            tmpWriter.Write(entry.UnknownData);
                        }
                    }

                    // unmodified patch entries
                    foreach (CatPatchEntry entry in patchEntries)
                    {
                        //if (casDataEntry.Contains(entry.Sha1))
                        //    continue;

                        tmpWriter.Write(entry.Sha1);
                        tmpWriter.Write(entry.BaseSha1);
                        tmpWriter.Write(entry.DeltaSha1);
                        numPatchEntries++;
                    }

                    // write it to file
                    if (!ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                        ProfileVersion.Battlefield4,
                        ProfileVersion.NeedForSpeed,
                        ProfileVersion.NeedForSpeedRivals))
                    {
                        writer.Write(header);
                    }
                    writer.WriteFixedSizedString("NyanNyanNyanNyan", 16);
                    if (!ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition,
                        ProfileVersion.Battlefield4,
                        ProfileVersion.NeedForSpeed,
                        ProfileVersion.NeedForSpeedRivals))
                    {
                        writer.Write(numEntries);
                        writer.Write(numPatchEntries);
                        if (ProfilesLibrary.IsLoaded(ProfileVersion.MassEffectAndromeda,
                            ProfileVersion.Fifa17,
                            ProfileVersion.Fifa18,
                            ProfileVersion.StarWarsBattlefrontII,
                            ProfileVersion.NeedForSpeedPayback,
                            ProfileVersion.Madden19,
                            ProfileVersion.Battlefield5,
                            ProfileVersion.StarWarsSquadrons))
                        {
                            writer.Write(encEntries.Count);
                            writer.Write(0x00);
                            writer.Write(-1);
                            writer.Write(-1);
                        }
                    }
                    writer.Write(ms.ToArray());
                }
            }
        }

        private bool DeleteSelectFiles(string modPath)
        {
            DirectoryInfo di = new DirectoryInfo(modPath);
            if (di.Exists)
            {
                RecursiveDeleteFiles(modPath);
                foreach (string catalog in m_fs.Catalogs)
                {
                    string basePatchCatalog = m_fs.ResolvePath("native_patch/" + catalog);
                    if (!m_hasPatchFolder)
                    {
                        basePatchCatalog = m_fs.ResolvePath("native_data/" + catalog);
                    }

                    string modDataCatalog = $"{modPath}/{catalog}";

                    // [DS fix] skip a catalog folder that is a link into the game
                    if (Directory.Exists(modDataCatalog) && !new DirectoryInfo(modDataCatalog).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        foreach (string filename in Directory.EnumerateFiles(modDataCatalog))
                        {
                            FileInfo fi = new FileInfo(filename);

                            // delete if cas does not exist in base patch OR is not a symbolic link
                            if (!File.Exists(basePatchCatalog + "/" + fi.Name) || (fi.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                File.Delete(fi.FullName);
                            }
                        }
                    }
                }

                return true;
            }
            return false;
        }

        private bool IsSamePatch(string modPath)
        {
            string baseLayoutPath = m_fs.ResolvePath("native_patch/layout.toc");
            if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons, ProfileVersion.DragonAgeTheVeilguard))
            {
                baseLayoutPath = m_fs.ResolvePath("native_data/layout.toc");
            }

            string modLayoutPath = modPath + "/layout.toc";
            if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII, ProfileVersion.Battlefield5, ProfileVersion.StarWarsSquadrons, ProfileVersion.DragonAgeTheVeilguard))
            {
                modLayoutPath = Directory.GetParent(modPath).FullName + "/Data/layout.toc";
            }

            if (!File.Exists(baseLayoutPath))
                return false;
            if (!File.Exists(modLayoutPath))
                return false;

            DbObject patchLayout = null;
            using (DbReader reader = new DbReader(new FileStream(baseLayoutPath, FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
                patchLayout = reader.ReadDbObject();

            DbObject modLayout = null;
            using (DbReader reader = new DbReader(new FileStream(modLayoutPath, FileMode.Open, FileAccess.Read), m_fs.CreateDeobfuscator()))
                modLayout = reader.ReadDbObject();

            int patchHead = patchLayout.GetValue<int>("head");
            int modHead = modLayout.GetValue<int>("head");

            string modPathParent = Directory.GetParent(modPath).FullName;

            if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII))
            {
                if (patchHead == 0xBDFB3 && modHead != patchHead)
                {
                    // SWBF2 new layout requires completely rebuilding ModData from scratch
                    Directory.Delete(modPathParent, true);
                    return false;
                }
            }

            if (modHead != patchHead)
            {
                Directory.Delete(modPathParent, true);
                return false;
            }

            return true;
        }

        private static bool PathGoesThroughLink(string modDataPath, string relativePath)
        {
            string path = modDataPath.TrimEnd('\\', '/');
            foreach (string part in relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                path = Path.Combine(path, part);
                DirectoryInfo di = new DirectoryInfo(path);
                if (!di.Exists)
                    return false;
                if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return true;
            }
            return false;
        }

        // links are removed, never what they point to
        private static void DeleteWithoutFollowingLinks(DirectoryInfo folder)
        {
            if (!folder.Exists)
                return;

            if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(folder.FullName, false);
                return;
            }

            foreach (FileSystemInfo item in folder.EnumerateFileSystemInfos())
            {
                bool isLink = item.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (item is DirectoryInfo subFolder)
                {
                    if (!isLink)
                        DeleteWithoutFollowingLinks(subFolder);
                    else
                        Directory.Delete(subFolder.FullName, false);
                }
                else
                {
                    item.Delete();
                }
            }

            Directory.Delete(folder.FullName, false);
        }

        private void RecursiveDeleteFiles(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);

            // [DS fix] never clean up inside a link to the game's own folders
            if (di.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return;

            DirectoryInfo[] paths = di.GetDirectories();
            FileInfo[] files = di.GetFiles();
            foreach (FileInfo fi in files)
            {
                // delete all cat/toc/sb files and the initfs_win32 and mods file
                if (fi.Extension == ".cat" || fi.Extension == ".toc" || fi.Extension == ".sb" || fi.Name.ToLower() == "mods.txt" || fi.Name.ToLower() == "mods.json")
                {
                    // dont delete layout.toc
                    if (fi.Name.ToLower() == "layout.toc")
                        continue;

                    fi.Delete();
                }
            }

            foreach (DirectoryInfo subDi in paths)
            {
                string tempPath = Path.Combine(path, subDi.Name);
                RecursiveDeleteFiles(tempPath);
            }
        }

        private bool RunSymbolicLinkProcess(List<SymLinkStruct> cmdArgs)
        {
            using (TextWriter writer = new StreamWriter(new FileStream(AppDomain.CurrentDomain.BaseDirectory + "\\run.bat", FileMode.Create)))
            {
                foreach (SymLinkStruct arg in cmdArgs)
                    writer.WriteLine("mklink" + ((arg.isFolder) ? "/D " : " ") + "\"" + arg.dest + "\" \"" + arg.src + "\"");
            }

            // create data and update symbolic links
            ExecuteProcess("cmd.exe", "/C \"" + AppDomain.CurrentDomain.BaseDirectory + "\\run.bat\"", true, true);

            // delete batch
            if (File.Exists("run.bat"))
            {
                File.Delete("run.bat");
            }

            // validate
            foreach (SymLinkStruct arg in cmdArgs)
            {
                if ((arg.isFolder && !Directory.Exists(arg.dest)) || (!arg.isFolder && !File.Exists(arg.dest)))
                    return false;
            }

            return true;
        }

        public static void ExecuteProcess(string processName, string args = "", bool waitForExit = false, bool asAdmin = false, Dictionary<string, string> env = null)
        {
            using (Process process = new Process())
            {
                FileInfo fi = new FileInfo(processName);

                process.StartInfo.FileName = processName;
                process.StartInfo.WorkingDirectory = fi.DirectoryName;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;

                if (env != null)
                {
                    foreach (string key in env.Keys)
                    {
                        process.StartInfo.EnvironmentVariables[key] = env[key];
                    }
                }

                if (asAdmin)
                {
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
                }

                try
                {
                    process.Start();
                    if (DiagnosticLog.Enabled)
                        DiagnosticLog.Log($"Started {fi.Name}{DescribeProcessId(process)}");

                    if (waitForExit)
                        process.WaitForExit();
                }
                catch (Exception e)
                {
                    DiagnosticLog.Log($"Could not start {processName}: {e.Message}");
                }
            }
        }

        private class TextureChunkInfo
        {
            public Guid ChunkId;
            public uint NameHash;
            public Guid OriginalChunkId;
            public HashSet<int> OriginalChunkBundles;
            public HashSet<int> ChunkBundles;
        }

        // the game keeps at most one chunk per texture in a bundle; keep the one the winning header points at
        private void ReconcileTextureChunks()
        {
            m_modifiedBundles.TryGetValue(s_chunksBundleHash, out ModBundleInfo chunksBundle);
            Dictionary<string, TextureChunkInfo> textures = new Dictionary<string, TextureChunkInfo>();
            HashSet<Guid> dropped = new HashSet<Guid>();
            HashSet<Guid> added = new HashSet<Guid>();

            foreach (ModBundleInfo bundle in m_modifiedBundles.Values)
            {
                if (bundle.Name == s_chunksBundleHash)
                    continue;

                foreach (string name in bundle.Modify.Res.Concat(bundle.Add.Res))
                {
                    if (!textures.TryGetValue(name, out TextureChunkInfo tex))
                    {
                        tex = ReadTextureChunkInfo(name);
                        textures.Add(name, tex);
                    }
                    if (tex == null)
                        continue;

                    bool hasChunk = tex.OriginalChunkBundles.Contains(bundle.Name) || tex.ChunkBundles.Contains(bundle.Name) || bundle.Add.Chunks.Contains(tex.ChunkId);
                    List<Guid> others = new List<Guid>();
                    foreach (Guid id in bundle.Add.Chunks)
                    {
                        if (id != tex.ChunkId && m_modifiedChunks.TryGetValue(id, out ChunkAssetEntry chunk) && (uint)chunk.H32 == tex.NameHash)
                            others.Add(id);
                    }

                    // a header-only bundle stays header-only, as in the game
                    if (!hasChunk && others.Count == 0)
                        continue;

                    if (tex.ChunkId == tex.OriginalChunkId)
                    {
                        bundle.Remove.Chunks.Remove(tex.ChunkId);
                    }
                    else if (m_modifiedChunks.ContainsKey(tex.ChunkId) || tex.ChunkBundles.Contains(bundle.Name))
                    {
                        if (tex.OriginalChunkId != Guid.Empty)
                            bundle.Remove.Chunks.Add(tex.OriginalChunkId);
                        if (!tex.ChunkBundles.Contains(bundle.Name) && bundle.Add.Chunks.Add(tex.ChunkId))
                            added.Add(tex.ChunkId);
                    }
                    else
                    {
                        continue;
                    }

                    foreach (Guid id in others)
                    {
                        bundle.Add.Chunks.Remove(id);
                        dropped.Add(id);
                    }
                }
            }

            // a dropped chunk no bundle uses any more doesn't need to go into chunks0 either
            if (chunksBundle != null)
            {
                foreach (ModBundleInfo bundle in m_modifiedBundles.Values)
                {
                    if (bundle.Name != s_chunksBundleHash)
                    {
                        dropped.ExceptWith(bundle.Add.Chunks);
                        dropped.ExceptWith(bundle.Modify.Chunks);
                    }
                }
                foreach (Guid id in dropped)
                    chunksBundle.Add.Chunks.Remove(id);
            }

            foreach (Guid id in added)
            {
                if (chunksBundle == null || (!chunksBundle.Add.Chunks.Contains(id) && !chunksBundle.Modify.Chunks.Contains(id)))
                    AddToChunksBundle(id, m_am.GetChunkEntry(id) == null);
            }
        }

        private TextureChunkInfo ReadTextureChunkInfo(string name)
        {
            if (!m_modifiedRes.TryGetValue(name, out ResAssetEntry entry) || entry.ResType != (uint)ResourceType.DxTexture || entry.ExtraData != null || !m_archiveData.TryGetValue(entry.Sha1, out ArchiveInfo data))
                return null;

            TextureChunkInfo tex = new TextureChunkInfo();
            try
            {
                byte[] header;
                using (CasReader reader = new CasReader(new MemoryStream(data.Data)))
                    header = reader.Read();
                if (header == null || header.Length < 112)
                    return null;
                tex.ChunkId = ReadDaiTextureChunkId(header);
                tex.NameHash = BitConverter.ToUInt32(header, 108);

                ResAssetEntry original = m_am.GetResEntry(name);
                if (original != null)
                {
                    using (NativeReader reader = new NativeReader(m_am.GetRes(original)))
                        tex.OriginalChunkId = ReadDaiTextureChunkId(reader.ReadToEnd());
                }
            }
            catch (Exception)
            {
                return null;
            }

            if (tex.ChunkId == Guid.Empty)
                return null;

            tex.OriginalChunkBundles = GetChunkBundleHashes(tex.OriginalChunkId);
            tex.ChunkBundles = (tex.ChunkId == tex.OriginalChunkId) ? tex.OriginalChunkBundles : GetChunkBundleHashes(tex.ChunkId);
            return tex;
        }

        private HashSet<int> GetChunkBundleHashes(Guid id)
        {
            HashSet<int> hashes = new HashSet<int>();
            ChunkAssetEntry chunk = (id == Guid.Empty) ? null : m_am.GetChunkEntry(id);
            if (chunk != null)
            {
                foreach (int bid in chunk.Bundles)
                    hashes.Add(HashBundle(m_am.GetBundleEntry(bid)));
            }
            return hashes;
        }

        // DAI texture header: chunk id at 28, name hash (the chunk meta's h32) at 108
        private static Guid ReadDaiTextureChunkId(byte[] header)
        {
            if (header == null || header.Length < 44)
                return Guid.Empty;

            byte[] guidBytes = new byte[16];
            Array.Copy(header, 28, guidBytes, 0, 16);
            return new Guid(guidBytes);
        }

        private class LegacyTextureLayout
        {
            public int MipCount;
            public int FirstMip;
            public uint[] MipSizes = new uint[15];
        }

        // keyed by the chunk each texture header points at
        private Dictionary<Guid, LegacyTextureLayout> ReadLegacyTextureLayouts(string modPath, DbObject resourceList)
        {
            Dictionary<Guid, LegacyTextureLayout> layouts = new Dictionary<Guid, LegacyTextureLayout>();

            foreach (DbObject resource in resourceList)
            {
                if (resource.GetValue<string>("type") != "res" || !resource.HasValue("archiveIndex") || (uint)resource.GetValue<int>("resType") != (uint)ResourceType.Texture)
                    continue;

                // header version 12 and up (MEA uses 13)
                byte[] resMeta = resource.GetValue<byte[]>("resMeta");
                if (resMeta == null || resMeta.Length < 4 || BitConverter.ToUInt32(resMeta, 0) < 12)
                    continue;

                byte[] header;
                try
                {
                    byte[] buffer = GetResourceData(modPath, resource.GetValue<int>("archiveIndex"), resource.GetValue<long>("archiveOffset"), (int)resource.GetValue<long>("compressedSize"));
                    using (CasReader reader = new CasReader(new MemoryStream(buffer)))
                        header = reader.Read();
                }
                catch (Exception)
                {
                    continue;
                }

                // 30 = mipCount, 31 = firstMip, 32 = chunkId, 48 = mipSizes[15]
                if (header == null || header.Length < 108)
                    continue;

                LegacyTextureLayout layout = new LegacyTextureLayout
                {
                    MipCount = header[30],
                    FirstMip = header[31]
                };
                for (int i = 0; i < 15; i++)
                    layout.MipSizes[i] = BitConverter.ToUInt32(header, 48 + i * 4);

                byte[] guidBytes = new byte[16];
                Array.Copy(header, 32, guidBytes, 0, 16);
                layouts[new Guid(guidBytes)] = layout;
            }

            return layouts;
        }

        // Same split as Utils.CompressTexture; leaves the entry alone if the data has no block boundary there.
        private static void SplitLegacyTextureChunk(LegacyTextureLayout layout, byte[] data, ChunkAssetEntry entry)
        {
            if (layout.MipCount <= 1 || layout.MipCount > 15 || layout.FirstMip <= 0 || layout.FirstMip >= layout.MipCount)
                return;

            long total = 0;
            for (int i = 0; i < layout.MipCount; i++)
                total += layout.MipSizes[i];
            if (total <= 0x10000 || total != entry.LogicalSize)
                return;

            // the bundled part keeps a minimum size of 0x10000, so it can start before firstMip
            long first = 0, second = total;
            for (int i = 0; i < layout.FirstMip && second > 0x10000; i++)
            {
                first += layout.MipSizes[i];
                second -= layout.MipSizes[i];
            }
            if (first == 0)
                return;

            long compressedPos = 0, uncompressedPos = 0, splitPos = -1;
            while (compressedPos + 8 <= data.Length)
            {
                if (uncompressedPos == first)
                    splitPos = compressedPos;

                int p = (int)compressedPos;
                int decompressedSize = ((data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3]) & 0x00FFFFFF;
                int compressionType = data[p + 4];
                int flags = data[p + 5];
                int bufferSize = (data[p + 6] << 8) | data[p + 7];

                if ((flags & 0x0F) != 0)
                    bufferSize += (flags & 0x0F) << 16;
                if ((compressionType & 0x7F) == 0x00)
                    bufferSize = decompressedSize;

                compressedPos += 8 + bufferSize;
                uncompressedPos += decompressedSize;
            }

            if (compressedPos != data.Length || uncompressedPos != total || splitPos < 0)
                return;

            entry.LogicalOffset = (uint)first;
            entry.LogicalSize = (uint)(total - first);
            entry.RangeStart = (uint)splitPos;
            entry.RangeEnd = (uint)data.Length;
        }

        private long GetSuperBundleSize(string superBundle)
        {
            string path = m_fs.ResolvePath(superBundle + ".sb");
            return (path != "" && File.Exists(path)) ? new FileInfo(path).Length : 0;
        }

        private class LegacyResourceData
        {
            public byte[] Buffer;
            public Sha1 Sha1;

            // a missing archive still fails at this asset, as before
            public Sha1 GetSha1() => (Buffer != null) ? Sha1 : Utils.GenerateSha1(Buffer);
        }

        // [DAI fix] read and hash on all threads; applying stays in mod order
        private LegacyResourceData[] ReadLegacyResourceData(string modPath, List<DbObject> resources)
        {
            LegacyResourceData[] result = new LegacyResourceData[resources.Count];
            int workers = Math.Max(1, Math.Min(WorkerPool.MaxThreads, resources.Count));

            WorkerPool.For(workers, worker =>
            {
                Dictionary<string, NativeReader> readers = new Dictionary<string, NativeReader>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    for (int i = worker; i < resources.Count; i += workers)
                    {
                        DbObject resource = resources[i];
                        string type = resource.GetValue<string>("type");
                        if (resource.GetValue<bool>("isRemoval", false) || (type != "ebx" && type != "res" && type != "chunk"))
                            continue;

                        bool fromArchive = resource.HasValue("archiveIndex");
                        string path = fromArchive
                            ? modPath.Replace(".fbmod", "_" + resource.GetValue<int>("archiveIndex").ToString("D2") + ".archive")
                            : m_fs.ResolvePath((ManifestFileRef)resource.GetValue<int>("file"));
                        long offset = fromArchive ? resource.GetValue<long>("archiveOffset") : resource.GetValue<int>("offset");

                        if (!readers.TryGetValue(path, out NativeReader reader))
                        {
                            reader = (fromArchive && !File.Exists(path)) ? null : new NativeReader(new FileStream(path, FileMode.Open, FileAccess.Read));
                            readers.Add(path, reader);
                        }

                        LegacyResourceData data = new LegacyResourceData();
                        if (reader != null)
                        {
                            reader.Position = offset;
                            data.Buffer = reader.ReadBytes((int)resource.GetValue<long>("compressedSize"));
                            data.Sha1 = Utils.GenerateSha1(data.Buffer);
                        }
                        result[i] = data;
                    }
                }
                finally
                {
                    foreach (NativeReader reader in readers.Values)
                        reader?.Dispose();
                }
            });

            return result;
        }

        private byte[] GetResourceData(string modFilename, int archiveIndex, long offset, int size)
        {
            string archiveFilename = modFilename.Replace(".fbmod", "_" + archiveIndex.ToString("D2") + ".archive");
            if (!File.Exists(archiveFilename))
                return null;

            using (NativeReader reader = new NativeReader(new FileStream(archiveFilename, FileMode.Open, FileAccess.Read)))
            {
                reader.Position = offset;
                return reader.ReadBytes(size);
            }
        }

        private void CopyFileIfRequired(string source, string dest, bool checkLength = true)
        {
            FileInfo baseFi = new FileInfo(source);
            FileInfo modFi = new FileInfo(dest);
            // copy file if base file exists and recently modified. if checkLength, also check if different file size
            if (baseFi.Exists && (baseFi.LastWriteTimeUtc > modFi.LastWriteTimeUtc || (checkLength && (baseFi.Length != modFi.Length))))
            {
                File.Copy(baseFi.FullName, modFi.FullName, true);
            }
        }
    }
}
