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
            public void Load(AssetManager parent, BinarySbDataHelper helper)
            {
                // all other games read from superbundles
                foreach (string superBundleName in parent.fs.SuperBundles)
                {
                    parent.superBundles.Add(new SuperBundleEntry() { Name = superBundleName });
                    bool patchFileExists = false;

                    // process base toc, bail out if it doesnt exist
                    DbObject baseToc = parent.ProcessTocChunks(string.Format("native_data/{0}.toc", superBundleName), helper, true);
                    if (baseToc == null)
                        continue;
                    bool isBinarySuperBundle = !baseToc.GetValue<bool>("cas");

                    parent.WriteToLog("Loading Data ({0})", superBundleName);

                    // process patch toc
                    DbObject patchToc = parent.ProcessTocChunks(string.Format("native_patch/{0}.toc", superBundleName), helper);

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
            }

            // [DAI fix] bundles are parsed on several cores, then added one at a time in toc order
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
