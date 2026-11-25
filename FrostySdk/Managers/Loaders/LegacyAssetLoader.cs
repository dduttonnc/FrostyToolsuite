using FrostySdk.Interfaces;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FrostySdk.Managers.Entries;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FrostySdk.Managers
{
    public partial class AssetManager
    {
        internal class LegacyAssetLoader : IAssetLoader
        {
            private const int PreloadMaxBundles = 256;

            private class PreloadedSuperBundle
            {
                public bool BaseTocFound;
                public DbObject BaseToc;
                public bool PatchTocFound;
                public DbObject PatchToc;
                public List<DbObject> BundleList;
                public DbObject[] Bundles;
            }

            // [DAI fix] the loading thread runs at the CPU priority option too
            public void Load(AssetManager parent, BinarySbDataHelper helper)
            {
                Thread thread = Thread.CurrentThread;
                ThreadPriority oldPriority = thread.Priority;
                thread.Priority = WorkerPool.Priority;
                try
                {
                    LoadSuperBundles(parent, helper);
                }
                finally
                {
                    thread.Priority = oldPriority;
                }
            }

            private void LoadSuperBundles(AssetManager parent, BinarySbDataHelper helper)
            {
                // [DAI fix] parsed ahead on other threads, added here in the original order
                List<string> superBundleNames = new List<string>(parent.m_fileSystem.SuperBundles);
                Task<PreloadedSuperBundle>[] preloads = new Task<PreloadedSuperBundle>[superBundleNames.Count];
                int window = Math.Max(2, WorkerPool.MaxThreads);

                for (int i = 0; i < Math.Min(window, superBundleNames.Count); i++)
                    preloads[i] = StartPreload(parent, superBundleNames[i]);

                BinaryPipeline binary = new BinaryPipeline(parent, superBundleNames, preloads, 2 * window);
                LoadingList loadingList = new LoadingList(parent, superBundleNames, preloads, binary, window);

                for (int sbIndex = 0; sbIndex < superBundleNames.Count; sbIndex++)
                {
                    string superBundleName = superBundleNames[sbIndex];
                    PreloadedSuperBundle preload = preloads[sbIndex].GetAwaiter().GetResult();

                    binary.Close(sbIndex - 1);
                    binary.Open(sbIndex, preload);
                    preloads[sbIndex] = null;

                    if (sbIndex + window < superBundleNames.Count)
                        preloads[sbIndex + window] = StartPreload(parent, superBundleNames[sbIndex + window]);

                    binary.Fill(sbIndex);

                    parent.m_superBundles.Add(new SuperBundleEntry() { Name = superBundleName });
                    bool patchFileExists = false;

                    // process base toc, bail out if it doesnt exist
                    if (!preload.BaseTocFound)
                        continue;
                    DbObject baseToc = parent.ProcessTocChunks(preload.BaseToc, string.Format("native_data/{0}.toc", superBundleName), helper, true);
                    bool isBinarySuperBundle = !baseToc.GetValue<bool>("cas");

                    // [DAI fix] show the count and progress, since several superbundles are in flight at once
                    parent.WriteToLog("Loading Data {0}/{1} (up to {2} at once): {3}", sbIndex + 1, superBundleNames.Count, WorkerPool.MaxThreads, superBundleName);
                    parent.WriteToLog("progress:{0}", (sbIndex + 1) * 100.0 / superBundleNames.Count);
                    loadingList.Update(sbIndex, "adding");

                    // process patch toc
                    DbObject patchToc = (preload.PatchTocFound)
                        ? parent.ProcessTocChunks(preload.PatchToc, string.Format("native_patch/{0}.toc", superBundleName), helper, false)
                        : null;

                    // default to base bundles first
                    DbObject baseBundleList = baseToc.GetValue<DbObject>("bundles");
                    DbObject patchBundleList = baseBundleList;

                    // no bundles, move on
                    if (baseBundleList.Count == 0)
                        continue;

                    if (patchToc != null)
                    {
                        // use patch bundle list instead
                        patchBundleList = patchToc.GetValue<DbObject>("bundles");
                        patchFileExists = true;
                    }

                    if (preload.Bundles != null)
                    {
                        AddBundles(parent, helper, preload.BundleList, 0, preload.Bundles);
                        continue;
                    }

                    if (isBinarySuperBundle)
                    {
                        LoadBinaryBundles(parent, helper, binary, loadingList, sbIndex);
                        continue;
                    }

                    // open superbundles for processing
                    NativeReader baseMf = new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read));
                    NativeReader patchMf = baseMf;

                    if (patchFileExists)
                        patchMf = new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read));

                    LoadCasBundles(parent, helper, patchBundleList, baseMf, patchMf);
                    baseMf.Dispose();
                    patchMf.Dispose();
                }

                binary.Dispose();
                loadingList.Clear();
            }

            // [DAI fix] lists the superbundles in flight on the splash screen, at most ten times a second
            private class LoadingList
            {
                private const int MaxLines = 32;

                private readonly ILoadingListLogger logger;
                private readonly List<string> superBundleNames;
                private readonly Task<PreloadedSuperBundle>[] preloads;
                private readonly BinaryPipeline binary;
                private readonly int window;
                private readonly Stopwatch timer = Stopwatch.StartNew();
                private long lastUpdate = -1000;

                public LoadingList(AssetManager parent, List<string> inSuperBundleNames, Task<PreloadedSuperBundle>[] inPreloads, BinaryPipeline inBinary, int inWindow)
                {
                    logger = parent.m_logger as ILoadingListLogger;
                    superBundleNames = inSuperBundleNames;
                    preloads = inPreloads;
                    binary = inBinary;
                    window = inWindow;
                }

                public void Update(int sbIndex, string currentState)
                {
                    if (logger == null || timer.ElapsedMilliseconds - lastUpdate < 100)
                        return;
                    lastUpdate = timer.ElapsedMilliseconds;

                    StringBuilder text = new StringBuilder();
                    int end = Math.Min(sbIndex + window, superBundleNames.Count);
                    for (int i = sbIndex; i < end; i++)
                    {
                        if (i - sbIndex == MaxLines)
                        {
                            text.AppendFormat("\n... and {0} more", end - i);
                            break;
                        }

                        string state = currentState;
                        if (i != sbIndex)
                        {
                            if (binary.IsStarted(i))
                                state = "unpacking";
                            else if (preloads[i] == null || !preloads[i].IsCompleted)
                                state = "reading";
                            else
                                state = "ready";
                        }

                        if (i != sbIndex)
                            text.Append('\n');
                        text.Append(superBundleNames[i]).Append("  (").Append(state).Append(')');
                    }

                    logger.LogLoadingList(text.ToString());
                }

                public void Clear() => logger?.LogLoadingList("");
            }

            private class BinaryBundleWork
            {
                public DbObject Sb;
                public PreparedBundleData Prepared;
            }

            private class BinarySuperBundle
            {
                public NativeReader BaseMf;
                public NativeReader PatchMf;
                public List<DbObject> Bundles;
                public Dictionary<string, BaseBundleInfo> BaseBundles;
                public Task<BinaryBundleWork>[] Work;
                public int Started;
            }

            // [DAI fix] runs across superbundle boundaries so cores don't idle at the end of each one
            private class BinaryPipeline : IDisposable
            {
                private readonly AssetManager parent;
                private readonly List<string> superBundleNames;
                private readonly Task<PreloadedSuperBundle>[] preloads;
                private readonly int maxInFlight;
                private readonly Dictionary<int, BinarySuperBundle> opened = new Dictionary<int, BinarySuperBundle>();
                private int feedIndex;
                private int inFlight;

                public BinaryPipeline(AssetManager inParent, List<string> inSuperBundleNames, Task<PreloadedSuperBundle>[] inPreloads, int inMaxInFlight)
                {
                    parent = inParent;
                    superBundleNames = inSuperBundleNames;
                    preloads = inPreloads;
                    maxInFlight = inMaxInFlight;
                }

                public void Open(int sbIndex, PreloadedSuperBundle preload)
                {
                    if (!opened.ContainsKey(sbIndex))
                        opened[sbIndex] = Create(sbIndex, preload);
                }

                public BinarySuperBundle Get(int sbIndex) => opened[sbIndex];

                public bool IsStarted(int sbIndex) => opened.TryGetValue(sbIndex, out BinarySuperBundle sb) && sb != null && sb.Started > 0;

                // only superbundles whose tocs were already read ahead are started, so this never waits
                public void Fill(int currentIndex)
                {
                    if (feedIndex < currentIndex)
                        feedIndex = currentIndex;

                    while (inFlight < maxInFlight && feedIndex < superBundleNames.Count)
                    {
                        if (!opened.TryGetValue(feedIndex, out BinarySuperBundle sb))
                        {
                            Task<PreloadedSuperBundle> preload = preloads[feedIndex];
                            if (preload == null || preload.Status != TaskStatus.RanToCompletion)
                                return;

                            sb = Create(feedIndex, preload.Result);
                            opened[feedIndex] = sb;
                        }

                        if (sb == null || sb.Started == sb.Bundles.Count)
                        {
                            feedIndex++;
                            continue;
                        }

                        StartNext(sb);
                    }
                }

                public BinaryBundleWork Take(BinarySuperBundle sb, int index)
                {
                    while (sb.Started <= index)
                        StartNext(sb);

                    BinaryBundleWork result = sb.Work[index].GetAwaiter().GetResult();
                    sb.Work[index] = null;
                    inFlight--;
                    return result;
                }

                public void Close(int sbIndex)
                {
                    if (!opened.TryGetValue(sbIndex, out BinarySuperBundle sb))
                        return;

                    opened.Remove(sbIndex);
                    if (sb != null)
                    {
                        if (sb.PatchMf != sb.BaseMf)
                            sb.PatchMf.Dispose();
                        sb.BaseMf.Dispose();
                    }
                }

                public void Dispose()
                {
                    foreach (int sbIndex in new List<int>(opened.Keys))
                        Close(sbIndex);
                }

                private void StartNext(BinarySuperBundle sb)
                {
                    sb.Work[sb.Started] = StartBinaryBundle(parent, sb.Bundles[sb.Started], sb.BaseBundles, sb.BaseMf, sb.PatchMf);
                    sb.Started++;
                    inFlight++;
                }

                // same choices as the loading thread makes: null for anything that isn't a binary superbundle with bundles
                private BinarySuperBundle Create(int sbIndex, PreloadedSuperBundle preload)
                {
                    if (!preload.BaseTocFound || preload.BaseToc == null || preload.BaseToc.GetValue<bool>("cas"))
                        return null;

                    DbObject baseBundleList = preload.BaseToc.GetValue<DbObject>("bundles");
                    if (baseBundleList == null || baseBundleList.Count == 0)
                        return null;

                    BinarySuperBundle sb = new BinarySuperBundle
                    {
                        Bundles = new List<DbObject>(),
                        BaseBundles = new Dictionary<string, BaseBundleInfo>()
                    };

                    foreach (DbObject bundle in baseBundleList)
                    {
                        BaseBundleInfo bi = new BaseBundleInfo
                        {
                            Name = bundle.GetValue<string>("id"),
                            Offset = bundle.GetValue<long>("offset"),
                            Size = bundle.GetValue<long>("size")
                        };
                        sb.BaseBundles.Add(bi.Name.ToLower(), bi);
                    }

                    bool patchFileExists = preload.PatchTocFound && preload.PatchToc != null;
                    DbObject bundleList = patchFileExists ? preload.PatchToc.GetValue<DbObject>("bundles") : baseBundleList;
                    foreach (DbObject bundle in bundleList)
                        sb.Bundles.Add(bundle);
                    sb.Work = new Task<BinaryBundleWork>[sb.Bundles.Count];

                    string superBundleName = superBundleNames[sbIndex];
                    sb.BaseMf = new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read));
                    sb.PatchMf = patchFileExists
                        ? new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read))
                        : sb.BaseMf;
                    return sb;
                }
            }

            private static void LoadBinaryBundles(AssetManager parent, BinarySbDataHelper helper, BinaryPipeline binary, LoadingList loadingList, int sbIndex)
            {
                BinarySuperBundle sb = binary.Get(sbIndex);

                for (int i = 0; i < sb.Bundles.Count; i++)
                {
                    BinaryBundleWork result = binary.Take(sb, i);
                    binary.Fill(sbIndex);
                    loadingList.Update(sbIndex, string.Format("adding bundle {0}/{1}", i + 1, sb.Bundles.Count));

                    string bundleName = sb.Bundles[i].GetValue<string>("id").ToLower();
                    parent.m_bundles.Add(new BundleEntry() { Name = bundleName, SuperBundleId = parent.m_superBundles.Count - 1 });
                    int bundleId = parent.m_bundles.Count - 1;

                    if (result.Prepared != null)
                        helper.AddPreparedBundleData(result.Prepared);

                    parent.ProcessBundleEbx(result.Sb, bundleId, helper);
                    parent.ProcessBundleRes(result.Sb, bundleId, helper);
                    parent.ProcessBundleChunks(result.Sb, bundleId, helper);
                }

                binary.Close(sbIndex);
            }

            // the superbundle files are read here, on the loading thread; the rest runs on the thread pool
            private static Task<BinaryBundleWork> StartBinaryBundle(AssetManager parent, DbObject bundle, Dictionary<string, BaseBundleInfo> baseBundles, NativeReader baseMf, NativeReader patchMf)
            {
                string bundleName = bundle.GetValue<string>("id").ToLower();
                long offset = bundle.GetValue<long>("offset");
                long size = bundle.GetValue<long>("size");
                bool isDeltaBundle = bundle.GetValue<bool>("delta");
                bool isBaseBundle = bundle.GetValue<bool>("base");

                Stream stream = (isBaseBundle)
                    ? baseMf.CreateViewStream(offset, size)
                    : patchMf.CreateViewStream(offset, size);

                if (!isDeltaBundle)
                {
                    return WorkerPool.Run(() =>
                    {
                        using (BinarySbReader reader = new LegacyBinarySbReader(stream, offset, parent.m_fileSystem.CreateDeobfuscator()))
                            return new BinaryBundleWork { Sb = reader.ReadDbObject() };
                    });
                }

                BaseBundleInfo bi = (baseBundles.ContainsKey(bundleName)) ? baseBundles[bundleName] : null;
                Stream baseStream = (bi != null) ? baseMf.CreateViewStream(bi.Offset, bi.Size) : null;
                Stream baseSbStream = (bi != null) ? baseMf.CreateViewStream(bi.Offset, bi.Size) : null;

                return WorkerPool.Run(() =>
                {
                    DbObject sb;
                    using (BinarySbReader reader = new LegacyBinarySbReader(baseStream, stream, parent.m_fileSystem.CreateDeobfuscator()))
                        sb = reader.ReadDbObject();

                    DbObject baseSb = null;
                    if (bi != null)
                    {
                        using (BinarySbReader reader = new LegacyBinarySbReader(baseSbStream, bi.Offset, parent.m_fileSystem.CreateDeobfuscator()))
                            baseSb = reader.ReadDbObject();
                    }

                    return new BinaryBundleWork { Sb = sb, Prepared = BinarySbDataHelper.PrepareBundleData(baseSb, sb) };
                });
            }

            private static Task<PreloadedSuperBundle> StartPreload(AssetManager parent, string superBundleName)
            {
                return WorkerPool.Run(() => Preload(parent, superBundleName));
            }

            // reads only; anything that changes the asset lists happens on the loading thread
            private static PreloadedSuperBundle Preload(AssetManager parent, string superBundleName)
            {
                PreloadedSuperBundle result = new PreloadedSuperBundle();

                string baseTocPath = parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.toc", superBundleName));
                if (baseTocPath == "")
                    return result;

                result.BaseTocFound = true;
                result.BaseToc = ReadToc(parent, baseTocPath);

                string patchTocPath = parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.toc", superBundleName));
                if (patchTocPath != "")
                {
                    result.PatchTocFound = true;
                    result.PatchToc = ReadToc(parent, patchTocPath);
                }

                // bundles of small cas superbundles are parsed here too; larger ones load in batches later
                DbObject baseBundleList = result.BaseToc?.GetValue<DbObject>("bundles");
                if (result.BaseToc == null || !result.BaseToc.GetValue<bool>("cas") || baseBundleList == null || baseBundleList.Count == 0)
                    return result;

                DbObject patchBundleList = (result.PatchToc != null) ? result.PatchToc.GetValue<DbObject>("bundles") : baseBundleList;
                if (patchBundleList == null || patchBundleList.Count > PreloadMaxBundles)
                    return result;

                List<DbObject> bundleList = new List<DbObject>();
                foreach (DbObject bundle in patchBundleList)
                    bundleList.Add(bundle);

                Stream[] streams = new Stream[bundleList.Count];
                using (NativeReader baseMf = new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read)))
                using (NativeReader patchMf = (result.PatchToc != null) ? new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read)) : null)
                {
                    for (int i = 0; i < bundleList.Count; i++)
                    {
                        DbObject bundle = bundleList[i];
                        NativeReader mf = (bundle.GetValue<bool>("base") || patchMf == null) ? baseMf : patchMf;
                        streams[i] = mf.CreateViewStream(bundle.GetValue<long>("offset"), bundle.GetValue<long>("size"));
                    }
                }

                DbObject[] bundles = new DbObject[streams.Length];
                for (int i = 0; i < streams.Length; i++)
                {
                    using (DbReader reader = new DbReader(streams[i], parent.m_fileSystem.CreateDeobfuscator()))
                        bundles[i] = reader.ReadDbObject();
                }

                result.BundleList = bundleList;
                result.Bundles = bundles;
                return result;
            }

            private static DbObject ReadToc(AssetManager parent, string path)
            {
                using (DbReader reader = new DbReader(new FileStream(path, FileMode.Open, FileAccess.Read), parent.m_fileSystem.CreateDeobfuscator()))
                    return reader.ReadDbObject();
            }

            // [DAI fix] bundles of large superbundles are parsed on several cores, then added one at a time in toc order
            private static void LoadCasBundles(AssetManager parent, BinarySbDataHelper helper, DbObject bundleList, NativeReader baseMf, NativeReader patchMf)
            {
                const int batchSize = 256;

                List<DbObject> tocBundles = new List<DbObject>();
                foreach (DbObject bundle in bundleList)
                    tocBundles.Add(bundle);

                Task<DbObject[]> parsing = null;
                int parsingStart = 0;

                for (int start = 0; start < tocBundles.Count; start += batchSize)
                {
                    Stream[] streams = new Stream[Math.Min(batchSize, tocBundles.Count - start)];
                    for (int i = 0; i < streams.Length; i++)
                    {
                        DbObject bundle = tocBundles[start + i];
                        NativeReader mf = bundle.GetValue<bool>("base") ? baseMf : patchMf;
                        streams[i] = mf.CreateViewStream(bundle.GetValue<long>("offset"), bundle.GetValue<long>("size"));
                    }

                    // the next batch parses while this thread adds the previous one
                    Task<DbObject[]> next = WorkerPool.Run(() => ParseBundles(parent, streams));
                    if (parsing != null)
                        AddBundles(parent, helper, tocBundles, parsingStart, parsing.GetAwaiter().GetResult());

                    parsing = next;
                    parsingStart = start;
                }

                if (parsing != null)
                    AddBundles(parent, helper, tocBundles, parsingStart, parsing.GetAwaiter().GetResult());
            }

            private static DbObject[] ParseBundles(AssetManager parent, Stream[] streams)
            {
                DbObject[] result = new DbObject[streams.Length];
                WorkerPool.For(streams.Length, i =>
                {
                    using (DbReader reader = new DbReader(streams[i], parent.m_fileSystem.CreateDeobfuscator()))
                        result[i] = reader.ReadDbObject();
                });
                return result;
            }

            private static void AddBundles(AssetManager parent, BinarySbDataHelper helper, List<DbObject> tocBundles, int start, DbObject[] sbs)
            {
                for (int i = 0; i < sbs.Length; i++)
                {
                    string bundleName = tocBundles[start + i].GetValue<string>("id").ToLower();

                    parent.m_bundles.Add(new BundleEntry() { Name = bundleName, SuperBundleId = parent.m_superBundles.Count - 1 });
                    int bundleId = parent.m_bundles.Count - 1;

                    parent.ProcessBundleEbx(sbs[i], bundleId, helper);
                    parent.ProcessBundleRes(sbs[i], bundleId, helper);
                    parent.ProcessBundleChunks(sbs[i], bundleId, helper);
                }
            }
        }
    }
}
