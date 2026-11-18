using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.IO;
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

            public void Load(AssetManager parent, BinarySbDataHelper helper)
            {
                // [DAI fix] DAI has hundreds of small superbundles: read and parse several ahead on other
                // threads, then add them here one at a time in the original order
                List<string> superBundleNames = new List<string>(parent.fs.SuperBundles);
                Task<PreloadedSuperBundle>[] preloads = new Task<PreloadedSuperBundle>[superBundleNames.Count];
                int window = Math.Max(2, Environment.ProcessorCount);

                for (int i = 0; i < Math.Min(window, superBundleNames.Count); i++)
                    preloads[i] = StartPreload(parent, superBundleNames[i]);

                for (int sbIndex = 0; sbIndex < superBundleNames.Count; sbIndex++)
                {
                    string superBundleName = superBundleNames[sbIndex];
                    PreloadedSuperBundle preload = preloads[sbIndex].GetAwaiter().GetResult();
                    preloads[sbIndex] = null;

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
                        AddBundles(parent, helper, preload.BundleList, 0, preload.Bundles);
                        continue;
                    }

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

                    LoadBinaryBundles(parent, helper, patchBundleList, baseBundles, baseMf, patchMf);

                    baseMf.Dispose();
                    patchMf.Dispose();
                }
            }

            private class BinaryBundleWork
            {
                public DbObject Sb;
                public PreparedBundleData Prepared;
            }

            // [DAI fix] binary superbundles: about one bundle per core is unpacked and matched against the
            // base game at a time, then added here in toc order (each bundle used to take ~0.3 s on one core)
            private static void LoadBinaryBundles(AssetManager parent, BinarySbDataHelper helper, DbObject bundleList, Dictionary<string, BaseBundleInfo> baseBundles, NativeReader baseMf, NativeReader patchMf)
            {
                List<DbObject> tocBundles = new List<DbObject>();
                foreach (DbObject bundle in bundleList)
                    tocBundles.Add(bundle);

                int window = Math.Max(2, Environment.ProcessorCount);
                Task<BinaryBundleWork>[] work = new Task<BinaryBundleWork>[tocBundles.Count];
                int started = 0;

                for (int i = 0; i < tocBundles.Count; i++)
                {
                    for (; started < tocBundles.Count && started < i + window; started++)
                        work[started] = StartBinaryBundle(parent, tocBundles[started], baseBundles, baseMf, patchMf);

                    BinaryBundleWork result = work[i].GetAwaiter().GetResult();
                    work[i] = null;

                    string bundleName = tocBundles[i].GetValue<string>("id").ToLower();
                    parent.bundles.Add(new BundleEntry() { Name = bundleName, SuperBundleId = parent.superBundles.Count - 1 });
                    int bundleId = parent.bundles.Count - 1;

                    if (result.Prepared != null)
                        helper.AddPreparedBundleData(result.Prepared);

                    parent.ProcessBundleEbx(result.Sb, bundleId, helper);
                    parent.ProcessBundleRes(result.Sb, bundleId, helper);
                    parent.ProcessBundleChunks(result.Sb, bundleId, helper);
                }
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
                    return Task.Run(() =>
                    {
                        using (BinarySbReader reader = new BinarySbReader(stream, offset, parent.fs.CreateDeobfuscator()))
                            return new BinaryBundleWork { Sb = reader.ReadDbObject() };
                    });
                }

                BaseBundleInfo bi = (baseBundles.ContainsKey(bundleName)) ? baseBundles[bundleName] : null;
                Stream baseStream = (bi != null) ? baseMf.CreateViewStream(bi.Offset, bi.Size) : null;
                Stream baseSbStream = (bi != null) ? baseMf.CreateViewStream(bi.Offset, bi.Size) : null;

                return Task.Run(() =>
                {
                    DbObject sb;
                    using (BinarySbReader reader = new BinarySbReader(baseStream, stream, parent.fs.CreateDeobfuscator()))
                        sb = reader.ReadDbObject();

                    DbObject baseSb = null;
                    if (bi != null)
                    {
                        using (BinarySbReader reader = new BinarySbReader(baseSbStream, bi.Offset, parent.fs.CreateDeobfuscator()))
                            baseSb = reader.ReadDbObject();
                    }

                    return new BinaryBundleWork { Sb = sb, Prepared = BinarySbDataHelper.PrepareBundleData(baseSb, sb) };
                });
            }

            private static Task<PreloadedSuperBundle> StartPreload(AssetManager parent, string superBundleName)
            {
                return Task.Run(() => Preload(parent, superBundleName));
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
