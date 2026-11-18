using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
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
                public long PreloadMs;
                public int Refs;
            }

            // TIMING BUILD ONLY
            private class LoadTiming
            {
                public string Name;
                public long PreloadMs;
                public long WaitMs;
                public long TocMs;
                public long RestMs;
                public int Bundles;
                public int TocChunks;
                public int Refs;
                public string Path = "none";
            }

            public void Load(AssetManager parent, BinarySbDataHelper helper)
            {
                // [DAI fix] DAI has hundreds of small superbundles: read and parse several ahead on other
                // threads, then add them here one at a time in the original order
                List<string> superBundleNames = new List<string>(parent.fs.SuperBundles);
                Task<PreloadedSuperBundle>[] preloads = new Task<PreloadedSuperBundle>[superBundleNames.Count];
                int window = Math.Max(2, Environment.ProcessorCount);

                for (int i = 0; i < Math.Min(window, superBundleNames.Count); i++)
                    preloads[i] = StartPreload(parent, superBundleNames[i]);

                Stopwatch total = Stopwatch.StartNew();
                List<LoadTiming> timings = new List<LoadTiming>();
                LoadTiming timing = null;
                long restStart = 0;
                int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);

                for (int sbIndex = 0; sbIndex < superBundleNames.Count; sbIndex++)
                {
                    if (timing != null)
                        timing.RestMs = total.ElapsedMilliseconds - restStart;

                    string superBundleName = superBundleNames[sbIndex];
                    long waitStart = total.ElapsedMilliseconds;
                    PreloadedSuperBundle preload = preloads[sbIndex].GetAwaiter().GetResult();
                    preloads[sbIndex] = null;

                    timing = new LoadTiming
                    {
                        Name = superBundleName,
                        PreloadMs = preload.PreloadMs,
                        WaitMs = total.ElapsedMilliseconds - waitStart,
                        Bundles = preload.BundleList?.Count ?? 0,
                        TocChunks = (preload.BaseToc?.GetValue<DbObject>("chunks")?.Count ?? 0) + (preload.PatchToc?.GetValue<DbObject>("chunks")?.Count ?? 0),
                        Refs = preload.Refs
                    };
                    timings.Add(timing);
                    long tocStart = total.ElapsedMilliseconds;
                    restStart = tocStart;

                    if (sbIndex + window < superBundleNames.Count)
                        preloads[sbIndex + window] = StartPreload(parent, superBundleNames[sbIndex + window]);

                    parent.superBundles.Add(new SuperBundleEntry() { Name = superBundleName });
                    bool patchFileExists = false;

                    // process base toc, bail out if it doesnt exist
                    if (!preload.BaseTocFound)
                        continue;
                    DbObject baseToc = parent.ProcessTocChunks(preload.BaseToc, string.Format("native_data/{0}.toc", superBundleName), helper, true);
                    bool isBinarySuperBundle = !baseToc.GetValue<bool>("cas");

                    parent.WriteToLog("Loading Data ({0})", superBundleName);

                    // process patch toc
                    DbObject patchToc = (preload.PatchTocFound)
                        ? parent.ProcessTocChunks(preload.PatchToc, string.Format("native_patch/{0}.toc", superBundleName), helper, false)
                        : null;

                    timing.TocMs = total.ElapsedMilliseconds - tocStart;
                    restStart = total.ElapsedMilliseconds;

                    // default to base bundles first
                    DbObject baseBundleList = baseToc.GetValue<DbObject>("bundles");
                    DbObject patchBundleList = baseBundleList;

                    // no bundles, move on
                    if (baseBundleList.Count == 0)
                        continue;

                    // create base bundle list if binary superbundle
                    Dictionary<string, BaseBundleInfo> baseBundles = new Dictionary<string, BaseBundleInfo>();
                    if (isBinarySuperBundle)
                    {
                        foreach (DbObject bundle in baseBundleList)
                        {
                            BaseBundleInfo bi = new BaseBundleInfo
                            {
                                Name = bundle.GetValue<string>("id"),
                                Offset = bundle.GetValue<long>("offset"),
                                Size = bundle.GetValue<long>("size")
                            };
                            baseBundles.Add(bi.Name.ToLower(), bi);
                        }
                    }

                    if (patchToc != null)
                    {
                        // use patch bundle list instead
                        patchBundleList = patchToc.GetValue<DbObject>("bundles");
                        patchFileExists = true;
                    }

                    if (preload.Bundles != null)
                    {
                        timing.Path = "preloaded";
                        AddBundles(parent, helper, preload.BundleList, 0, preload.Bundles);
                        continue;
                    }

                    timing.Path = isBinarySuperBundle ? "binary" : "batched";
                    timing.Bundles = patchBundleList.Count;

                    // open superbundles for processing
                    NativeReader baseMf = new NativeReader(new FileStream(parent.fs.ResolvePath(string.Format("native_data/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read));
                    NativeReader patchMf = baseMf;

                    if (patchFileExists)
                        patchMf = new NativeReader(new FileStream(parent.fs.ResolvePath(string.Format("native_patch/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read));

                    if (!isBinarySuperBundle)
                    {
                        LoadCasBundles(parent, helper, patchBundleList, baseMf, patchMf);
                        baseMf.Dispose();
                        patchMf.Dispose();
                        continue;
                    }

                    // iterate bundles in superbundle
                    foreach (DbObject bundle in patchBundleList)
                    {
                        string bundleName = bundle.GetValue<string>("id").ToLower();
                        long offset = bundle.GetValue<long>("offset");
                        long size = bundle.GetValue<long>("size");
                        bool isDeltaBundle = bundle.GetValue<bool>("delta");
                        bool isBaseBundle = bundle.GetValue<bool>("base");

                        // add new bundle entry
                        parent.bundles.Add(new BundleEntry() { Name = bundleName, SuperBundleId = parent.superBundles.Count - 1 });
                        int bundleId = parent.bundles.Count - 1;

                        // obtain view of appropriate stream
                        Stream stream = (isBaseBundle)
                            ? baseMf.CreateViewStream(offset, size)
                            : patchMf.CreateViewStream(offset, size);

                        DbObject sb = null;
                        if (isBinarySuperBundle)
                        {
                            if (isDeltaBundle)
                            {
                                // get base stream
                                BaseBundleInfo bi = (baseBundles.ContainsKey(bundleName)) ? baseBundles[bundleName] : null;
                                Stream baseStream = (bi != null) ? baseMf.CreateViewStream(bi.Offset, bi.Size) : null;

                                //if (bi == null)
                                //    Console.WriteLine("TEST");

                                // patched binary super bundle
                                using (BinarySbReader reader = new BinarySbReader(baseStream, stream, parent.fs.CreateDeobfuscator()))
                                    sb = reader.ReadDbObject();

                                DbObject baseSb = null;
                                if (bi != null)
                                {
                                    using (BinarySbReader reader = new BinarySbReader(baseMf.CreateViewStream(bi.Offset, bi.Size), bi.Offset, parent.fs.CreateDeobfuscator()))
                                        baseSb = reader.ReadDbObject();
                                }

                                // compare with base superbundle and process patched data
                                helper.FilterAndAddBundleData(baseSb, sb);
                            }
                            else
                            {
                                // binary super bundle
                                using (BinarySbReader reader = new BinarySbReader(stream, offset, parent.fs.CreateDeobfuscator()))
                                    sb = reader.ReadDbObject();
                            }
                        }
                        else
                        {
                            // normal superbundle
                            using (DbReader reader = new DbReader(stream, parent.fs.CreateDeobfuscator()))
                                sb = reader.ReadDbObject();
                        }

                        // process assets
                        parent.ProcessBundleEbx(sb, bundleId, helper);
                        parent.ProcessBundleRes(sb, bundleId, helper);
                        parent.ProcessBundleChunks(sb, bundleId, helper);
                    }

                    baseMf.Dispose();
                    patchMf.Dispose();

                    if (isBinarySuperBundle)
                        GC.Collect();
                }

                if (timing != null)
                    timing.RestMs = total.ElapsedMilliseconds - restStart;
                WriteTimings(parent, timings, total.ElapsedMilliseconds, GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2);
            }

            private static void WriteTimings(AssetManager parent, List<LoadTiming> timings, long totalMs, int gen0, int gen1, int gen2)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Loader total: " + totalMs + " ms, superbundles: " + timings.Count);
                    sb.AppendLine("Cores: " + Environment.ProcessorCount + ", server GC: " + GCSettings.IsServerGC + ", GCs gen0/1/2: " + gen0 + "/" + gen1 + "/" + gen2);
                    sb.AppendLine("Main thread waiting for read-ahead: " + timings.Sum(t => t.WaitMs) + " ms");
                    sb.AppendLine("Main thread toc chunks: " + timings.Sum(t => t.TocMs) + " ms");
                    sb.AppendLine("Main thread bundles: " + timings.Sum(t => t.RestMs) + " ms");
                    sb.AppendLine("Read-ahead work (all threads): " + timings.Sum(t => t.PreloadMs) + " ms");
                    sb.AppendLine("Bundles: " + timings.Sum(t => t.Bundles) + ", asset refs (read-ahead only): " + timings.Sum(t => (long)t.Refs) + ", toc chunks: " + timings.Sum(t => (long)t.TocChunks));
                    foreach (string path in new[] { "preloaded", "batched", "binary", "none" })
                        sb.AppendLine("  " + path + ": " + timings.Count(t => t.Path == path) + " superbundles, " + timings.Where(t => t.Path == path).Sum(t => t.WaitMs + t.TocMs + t.RestMs) + " ms main thread");
                    sb.AppendLine();
                    sb.AppendLine("Slowest on the main thread (wait / toc / bundles / read-ahead ms, path, bundles, refs, toc chunks):");
                    foreach (LoadTiming t in timings.OrderByDescending(t => t.WaitMs + t.TocMs + t.RestMs).Take(40))
                        sb.AppendLine(string.Format("  {0} / {1} / {2} / {3}  {4}  {5}  {6}  {7}  {8}", t.WaitMs, t.TocMs, t.RestMs, t.PreloadMs, t.Path, t.Bundles, t.Refs, t.TocChunks, t.Name));
                    sb.AppendLine();
                    sb.AppendLine("Slowest read-ahead:");
                    foreach (LoadTiming t in timings.OrderByDescending(t => t.PreloadMs).Take(20))
                        sb.AppendLine(string.Format("  {0} ms  {1}  {2}  {3}  {4}", t.PreloadMs, t.Path, t.Bundles, t.TocChunks, t.Name));

                    string file = parent.fs.CacheName + "_loadtimes.txt";
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                    File.WriteAllText(file, sb.ToString());
                }
                catch (Exception)
                {
                }
            }

            private static Task<PreloadedSuperBundle> StartPreload(AssetManager parent, string superBundleName)
            {
                return Task.Run(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    PreloadedSuperBundle result = Preload(parent, superBundleName);
                    result.PreloadMs = sw.ElapsedMilliseconds;
                    return result;
                });
            }

            // reads only; anything that changes the asset lists happens on the loading thread
            private static PreloadedSuperBundle Preload(AssetManager parent, string superBundleName)
            {
                PreloadedSuperBundle result = new PreloadedSuperBundle();

                string baseTocPath = parent.fs.ResolvePath(string.Format("native_data/{0}.toc", superBundleName));
                if (baseTocPath == "")
                    return result;

                result.BaseTocFound = true;
                result.BaseToc = ReadToc(parent, baseTocPath);

                string patchTocPath = parent.fs.ResolvePath(string.Format("native_patch/{0}.toc", superBundleName));
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
                using (NativeReader baseMf = new NativeReader(new FileStream(parent.fs.ResolvePath(string.Format("native_data/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read)))
                using (NativeReader patchMf = (result.PatchToc != null) ? new NativeReader(new FileStream(parent.fs.ResolvePath(string.Format("native_patch/{0}.sb", superBundleName)), FileMode.Open, FileAccess.Read)) : null)
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
                    using (DbReader reader = new DbReader(streams[i], parent.fs.CreateDeobfuscator()))
                        bundles[i] = reader.ReadDbObject();
                }

                result.BundleList = bundleList;
                result.Bundles = bundles;
                foreach (DbObject b in bundles)
                    result.Refs += (b.GetValue<DbObject>("ebx")?.Count ?? 0) + (b.GetValue<DbObject>("res")?.Count ?? 0) + (b.GetValue<DbObject>("chunks")?.Count ?? 0);
                return result;
            }

            private static DbObject ReadToc(AssetManager parent, string path)
            {
                using (DbReader reader = new DbReader(new FileStream(path, FileMode.Open, FileAccess.Read), parent.fs.CreateDeobfuscator()))
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
                    Task<DbObject[]> next = Task.Run(() => ParseBundles(parent, streams));
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
                Parallel.For(0, streams.Length, i =>
                {
                    using (DbReader reader = new DbReader(streams[i], parent.fs.CreateDeobfuscator()))
                        result[i] = reader.ReadDbObject();
                });
                return result;
            }

            private static void AddBundles(AssetManager parent, BinarySbDataHelper helper, List<DbObject> tocBundles, int start, DbObject[] sbs)
            {
                for (int i = 0; i < sbs.Length; i++)
                {
                    string bundleName = tocBundles[start + i].GetValue<string>("id").ToLower();

                    parent.bundles.Add(new BundleEntry() { Name = bundleName, SuperBundleId = parent.superBundles.Count - 1 });
                    int bundleId = parent.bundles.Count - 1;

                    parent.ProcessBundleEbx(sbs[i], bundleId, helper);
                    parent.ProcessBundleRes(sbs[i], bundleId, helper);
                    parent.ProcessBundleChunks(sbs[i], bundleId, helper);
                }
            }
        }
    }
}
