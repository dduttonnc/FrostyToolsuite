using FrostySdk.IO;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FrostySdk.Managers
{
    [Flags]
    internal enum Flags
    {
        None = 0,
        HasBaseBundles = 1, // base toc has bundles that the patch doesnt have
        HasBaseChunks = 2, // base toc has chunks that the patch doesnt have
        HasCompressedNames = 4 // bundle names are huffman encoded
    }

    public partial class AssetManager
    {
        internal class CasAssetLoader : IAssetLoader
        {
            // [DAI fix] bundles are parsed in batches on the worker pool, then added in their original order
            private const int ParseBatchSize = 512;

            // [DAI fix] same order as the old list, without a full search for every bundle name
            private class BundleList
            {
                private readonly List<BaseBundleInfo> items = new List<BaseBundleInfo>();
                private readonly Dictionary<string, int> indexByName = new Dictionary<string, int>();

                public void Remove(string name)
                {
                    if (indexByName.TryGetValue(name, out int idx))
                    {
                        items[idx] = null;
                        indexByName.Remove(name);
                    }
                }

                public void Add(BaseBundleInfo bi)
                {
                    Remove(bi.Name);
                    indexByName[bi.Name] = items.Count;
                    items.Add(bi);
                }

                public List<BaseBundleInfo> ToList() => items.FindAll(bi => bi != null);
            }

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
                // load superbundles
                foreach (SuperBundleInfo sbInfo in parent.m_fileSystem.EnumerateSuperBundleInfos())
                {
                    string sbName = sbInfo.Name;
                    parent.m_superBundles.Add(new SuperBundleEntry() { Name = sbName });
                    int sbIndex = parent.m_superBundles.Count - 1;

                    BundleList bundles = new BundleList();

                    // load default superbundle
                    parent.WriteToLog("Loading data ({0})", sbName);
                    parent.ReportProgress(parent.m_superBundles.Count, parent.m_fileSystem.SuperBundleCount);

                    // parse superbundle toc files from patch and data
                    bool isPatch = true;
                    string tocPath = parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.toc", sbName));
                    if (tocPath.Equals(string.Empty))
                    {
                        tocPath = parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.toc", sbName));
                        isPatch = false;
                    }

                    if (!tocPath.Equals(string.Empty))
                    {
                        using (NativeReader reader = new NativeReader(new FileStream(tocPath, FileMode.Open, FileAccess.Read), parent.m_fileSystem.CreateDeobfuscator()))
                            ReadToc(parent, sbName, reader, bundles, isPatch);
                    }

                    // load split superbundles from catalogs
                    foreach (string catalog in sbInfo.SplitSuperBundles)
                    {
                        string sbPath = sbName.Replace("win32", catalog);

                        parent.WriteToLog("Loading data ({0})", sbPath);

                        // parse superbundle toc files from patch and data
                        isPatch = true;
                        tocPath = parent.m_fileSystem.ResolvePath(string.Format("native_patch/{0}.toc", sbPath));
                        if (tocPath.Equals(string.Empty))
                        {
                            tocPath = parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.toc", sbPath));
                            isPatch = false;
                        }

                        if (!tocPath.Equals(string.Empty))
                        {
                            using (NativeReader reader = new NativeReader(new FileStream(tocPath, FileMode.Open, FileAccess.Read), parent.m_fileSystem.CreateDeobfuscator()))
                                ReadToc(parent, sbPath, reader, bundles, isPatch);
                        }
                    }

                    // parse superbundle sb files from patch and data
                    List<BaseBundleInfo> bundleList = bundles.ToList();
                    if (bundleList.Count > 0)
                        ReadSb(parent, helper, bundleList, sbIndex);
                }
            }

            private void ReadToc(AssetManager parent, string sbName, NativeReader reader, BundleList bundles, bool patch)
            {
                uint startPos = (uint)reader.Position;

                uint bundleHashMapOffset = reader.ReadUInt(Endian.Big) + startPos;
                uint bundleDataOffset = reader.ReadUInt(Endian.Big) + startPos;
                int bundlesCount = reader.ReadInt(Endian.Big);

                uint chunkHashMapOffset = reader.ReadUInt(Endian.Big) + startPos;
                uint chunkGuidOffset = reader.ReadUInt(Endian.Big) + startPos;
                int chunksCount = reader.ReadInt(Endian.Big);

                uint unknownOffset1 = reader.ReadUInt(Endian.Big) + startPos; // not used
                uint unknownOffset2 = reader.ReadUInt(Endian.Big) + startPos; // not used

                uint namesOffset = reader.ReadUInt(Endian.Big) + startPos;

                uint chunkDataOffset = reader.ReadUInt(Endian.Big) + startPos;
                int dataCount = reader.ReadInt(Endian.Big);

                Flags flags = (Flags)reader.ReadInt(Endian.Big);

                if (flags.HasFlag(Flags.HasBaseBundles) || flags.HasFlag(Flags.HasBaseChunks))
                {
                    string tocPath = parent.m_fileSystem.ResolvePath(string.Format("native_data/{0}.toc", sbName));
                    using (NativeReader baseReader = new NativeReader(new FileStream(tocPath, FileMode.Open, FileAccess.Read), parent.m_fileSystem.CreateDeobfuscator()))
                        ReadToc(parent, sbName, baseReader, bundles, false);
                }

                uint namesCount = 0;
                uint tableCount = 0;
                uint tableOffset = uint.MaxValue;
                HuffmanDecoder huffmanDecoder = null;

                if (flags.HasFlag(Flags.HasCompressedNames))
                {
                    huffmanDecoder = new HuffmanDecoder();
                    namesCount = reader.ReadUInt(Endian.Big);
                    tableCount = reader.ReadUInt(Endian.Big);
                    tableOffset = reader.ReadUInt(Endian.Big) + startPos;
                }

#if FROSTY_DEVELOPER
                Debug.Assert(unknownOffset1 == chunkDataOffset && unknownOffset2 == chunkDataOffset);
#endif

                if (bundlesCount != 0)
                {
                    if (flags.HasFlag(Flags.HasCompressedNames))
                    {
                        reader.Position = namesOffset;
                        huffmanDecoder.ReadEncodedData(reader, namesCount, Endian.Big);

                        reader.Position = tableOffset;
                        huffmanDecoder.ReadHuffmanTable(reader, tableCount, Endian.Big);
                    }

                    int[] bundleHashMap = new int[bundlesCount];
                    reader.Position = bundleHashMapOffset;
                    for (int i = 0; i < bundlesCount; i++)
                    {
                        bundleHashMap[i] = reader.ReadInt(Endian.Big);
                    }

                    reader.Position = bundleDataOffset;

                    for (int i = 0; i < bundlesCount; i++)
                    {
                        int nameOffset = reader.ReadInt(Endian.Big);
                        uint bundleSize = reader.ReadUInt(Endian.Big); // flag in first 2 bits: 0x40000000 inline sb
                        long bundleOffset = reader.ReadLong(Endian.Big);

                        string name = "";

                        if (flags.HasFlag(Flags.HasCompressedNames))
                        {
                            name = huffmanDecoder.ReadHuffmanEncodedString(nameOffset);
                        }
                        else
                        {
                            long curPos = reader.Position;
                            reader.Position = namesOffset + nameOffset;
                            name = reader.ReadNullTerminatedString();
                            reader.Position = curPos;
                        }

                        bundles.Remove(name);

                        BaseBundleInfo bi = new BaseBundleInfo()
                        {
                            Name = name,
                            SbName = sbName,
                            Offset = bundleOffset,
                            Size = bundleSize,
                            IsPatch = patch
                        };

                        if (bundleSize != uint.MaxValue && bundleOffset != -1)
                        {
                            bundles.Add(bi);
                        }
                    }
                    huffmanDecoder?.Dispose();
                }
                if (chunksCount != 0)
                {
                    reader.Position = chunkHashMapOffset;
                    int[] chunkHashMap = new int[chunksCount];
                    for (int i = 0; i < chunksCount; i++)
                    {
                        chunkHashMap[i] = reader.ReadInt(Endian.Big);
                    }

                    reader.Position = chunkGuidOffset;
                    Guid[] chunkGuids = new Guid[dataCount / 3];
                    for (int i = 0; i < chunksCount; i++)
                    {
                        byte[] b = reader.ReadBytes(16);

                        Array.Reverse(b);

                        Guid guid = new Guid(b);

                        // 0xFFFFFFFF remove chunk
                        int index = reader.ReadInt(Endian.Big);

                        if (index != -1)
                        {
                            // im guessing the unknown offsets are connected to this
                            byte flag = (byte)((index & 0xFF000000) >> 24);
#if FROSTY_DEVELOPER
                            Debug.Assert(flag == 1);
#endif
                            index = (index & 0xFFFFFF) / 3;


                            chunkGuids[index] = guid;
                        }
                        else
                        {
                            if (parent.m_chunkList.ContainsKey(guid))
                            {
                                parent.m_chunkList.Remove(guid);
                            }
                        }
                    }

                    reader.Position = chunkDataOffset;
                    for (int i = 0; i < (dataCount / 3); i++)
                    {
                        byte unk = reader.ReadByte();
                        bool isPatch = reader.ReadBoolean();
                        byte catalogIndex = reader.ReadByte();
                        byte casIndex = reader.ReadByte();
                        uint offset = reader.ReadUInt(Endian.Big);
                        uint size = reader.ReadUInt(Endian.Big);

                        ChunkAssetEntry chunk = new ChunkAssetEntry
                        {
                            Id = chunkGuids[i],
                            Size = size,
                            Location = AssetDataLocation.CasNonIndexed,
                            ExtraData = new AssetExtraData
                            {
                                CasPath = parent.m_fileSystem.GetFilePath(catalogIndex, casIndex, isPatch),
                                DataOffset = offset
                            },
                            SuperBundles = new List<int>() { parent.m_superBundles.Count - 1 }
                        };

                        if (parent.m_chunkList.ContainsKey(chunk.Id))
                        {
                            ChunkAssetEntry existing = parent.m_chunkList[chunk.Id];

                            // add superbundles and bundles so those dont get overridden
                            chunk.SuperBundles.AddRange(existing.SuperBundles);
                            chunk.Bundles.AddRange(existing.Bundles);

                            parent.m_chunkList.Remove(chunk.Id);
                        }

                        parent.m_chunkList.Add(chunk.Id, chunk);
                    }
                }
            }

            private void ReadSb(AssetManager parent, BinarySbDataHelper helper, List<BaseBundleInfo> bundles, int sbIndex)
            {
                string patchSbPath = "";
                string baseSbPath = "";
                NativeReader patchReader = null;
                NativeReader baseReader = null;

                byte[][] rawBundles = new byte[ParseBatchSize][];
                DbObject[] parsedBundles = new DbObject[ParseBatchSize];

                for (int batchStart = 0; batchStart < bundles.Count; batchStart += ParseBatchSize)
                {
                    int batchCount = Math.Min(ParseBatchSize, bundles.Count - batchStart);

                    // reading stays in order on this thread, the sb/toc files are read front to back
                    for (int i = 0; i < batchCount; i++)
                    {
                        BaseBundleInfo bi = bundles[batchStart + i];

                        byte flag;
                        if ((bi.Size & 0xC0000000) == 0x40000000)
                            flag = 1;
                        else if ((bi.Size & 0xC0000000) == 0x80000000)
                            throw new NotImplementedException("unkown flag");
                        else
                            flag = 0;

                        NativeReader reader;
                        if (bi.IsPatch)
                        {
                            if (patchReader == null || patchSbPath != bi.SbName)
                            {
                                patchSbPath = bi.SbName;
                                patchReader?.Dispose();
                                patchReader = OpenSbReader(parent, "native_patch/", patchSbPath, flag);
                            }
                            reader = patchReader;
                        }
                        else
                        {
                            if (baseReader == null || baseSbPath != bi.SbName)
                            {
                                baseSbPath = bi.SbName;
                                baseReader?.Dispose();
                                baseReader = OpenSbReader(parent, "native_data/", baseSbPath, flag);
                            }
                            reader = baseReader;
                        }

                        // TODO: make deobfuscator remove the obfs header
                        reader.Position = flag == 1 ? bi.Offset + 0x22C : bi.Offset;
                        rawBundles[i] = reader.ReadBytes((int)(bi.Size & ~0xC0000000));
                    }

                    WorkerPool.For(batchCount, i => parsedBundles[i] = ParseBundle(parent, rawBundles[i]));

                    for (int i = 0; i < batchCount; i++)
                    {
                        BaseBundleInfo bi = bundles[batchStart + i];
                        DbObject bundle = parsedBundles[i];
                        rawBundles[i] = null;
                        parsedBundles[i] = null;

                        // [DAI fix] removed an unused search of all loaded bundles for every bundle
                        parent.m_bundles.Add(new BundleEntry { Name = bi.Name, SuperBundleId = sbIndex });

                        // process assets
                        parent.ProcessBundleEbx(bundle, parent.m_bundles.Count - 1, helper);
                        parent.ProcessBundleRes(bundle, parent.m_bundles.Count - 1, helper);
                        parent.ProcessBundleChunks(bundle, parent.m_bundles.Count - 1, helper);
                    }
                }

                patchReader?.Dispose();
                baseReader?.Dispose();
            }

            private static NativeReader OpenSbReader(AssetManager parent, string root, string sbName, byte flag)
            {
                string ext = (flag == 1) ? ".toc" : ".sb";
                return new NativeReader(new FileStream(parent.m_fileSystem.ResolvePath(string.Format("{0}{1}{2}", root, sbName, ext)), FileMode.Open, FileAccess.Read), parent.m_fileSystem.CreateDeobfuscator());
            }

            private static DbObject ParseBundle(AssetManager parent, byte[] data)
            {
                DbObject bundle = null;
                using (BinarySbReader bundleReader = new BinarySbReader(new MemoryStream(data), parent.m_fileSystem.CreateDeobfuscator()))
                {
                    int bundleOffset = bundleReader.ReadInt(Endian.Big);
                    int bundleSize = bundleReader.ReadInt(Endian.Big);
                    uint locationOffset = bundleReader.ReadUInt(Endian.Big);
                    uint totalCount = bundleReader.ReadUInt(Endian.Big);
                    uint dataOffset = bundleReader.ReadUInt(Endian.Big);

                    uint unkOffset1 = bundleReader.ReadUInt(Endian.Big); // not used
                    uint unkOffset2 = bundleReader.ReadUInt(Endian.Big); // not used
                    int unkCount = bundleReader.ReadInt(Endian.Big); // count for unkOffset1 and 2 maybe

                    // bundles can be stored in this file or in a seperate cas file, then the first file info is for the bundle. Seems to be related to the flag for in which file the sb is stored
                    bool inlineBundle = !(bundleOffset == 0 && bundleSize == 0);
#if FROSTY_DEVELOPER
                    Debug.Assert(unkOffset2 == unkOffset1 && unkCount == 0);
#endif
                    bundleReader.Position = locationOffset;

                    bool[] flags = new bool[totalCount];
                    for (uint i = 0; i < totalCount; i++)
                        flags[i] = bundleReader.ReadBoolean();

                    byte unused = 0;
                    bool isPatch = false;
                    int catalogIndex = 0;
                    int casIndex = 0;
                    int offset = 0;
                    int size = 0;
                    int z = 0;

                    if (inlineBundle)
                    {
                        bundleReader.Position = bundleOffset;
                        bundle = bundleReader.ReadDbObject();
#if FROSTY_DEVELOPER
                        Debug.Assert(bundleReader.Position == bundleOffset + bundleSize);
                        Debug.Assert(bundleReader.TotalCount == totalCount);
#endif
                        bundleReader.Position = dataOffset;
                    }
                    else
                    {
                        bundleReader.Position = dataOffset;

                        if (flags[z++])
                        {
                            unused = bundleReader.ReadByte();
#if FROSTY_DEVELOPER
                            Debug.Assert(unused == 0);
#endif
                            isPatch = bundleReader.ReadBoolean();
                            catalogIndex = bundleReader.ReadByte();
                            casIndex = bundleReader.ReadByte();
                        }
                        offset = bundleReader.ReadInt(Endian.Big);
                        size = bundleReader.ReadInt(Endian.Big);

                        string path = parent.m_fileSystem.GetFilePath(catalogIndex, casIndex, isPatch);

                        using (Stream casStream = new FileStream(parent.m_fileSystem.ResolvePath(path), FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[size];
                            casStream.Position = offset;
                            casStream.Read(buffer, 0, size);

                            using (BinarySbReader casBundleReader = new BinarySbReader(new MemoryStream(buffer), parent.m_fileSystem.CreateDeobfuscator()))
                            {
                                bundle = casBundleReader.ReadDbObject();
#if FROSTY_DEVELOPER
                                Debug.Assert(casBundleReader.TotalCount == totalCount - 1);
#endif
                            }
                        }
                    }

                    for (int i = 0; i < bundle.GetValue<DbObject>("ebx").Count; i++)
                    {
                        if (flags[z++])
                        {
                            unused = bundleReader.ReadByte();
                            isPatch = bundleReader.ReadBoolean();
                            catalogIndex = bundleReader.ReadByte();
                            casIndex = bundleReader.ReadByte();
                        }

                        DbObject ebx = bundle.GetValue<DbObject>("ebx")[i] as DbObject;
                        offset = bundleReader.ReadInt(Endian.Big);
                        size = bundleReader.ReadInt(Endian.Big);

                        ebx.SetValue("catalog", catalogIndex);
                        ebx.SetValue("cas", casIndex);
                        ebx.SetValue("offset", offset);
                        ebx.SetValue("size", size);
                        if (isPatch)
                            ebx.SetValue("patch", true);
                    }

                    for (int i = 0; i < bundle.GetValue<DbObject>("res").Count; i++)
                    {
                        if (flags[z++])
                        {
                            unused = bundleReader.ReadByte();
                            isPatch = bundleReader.ReadBoolean();
                            catalogIndex = bundleReader.ReadByte();
                            casIndex = bundleReader.ReadByte();
                        }

                        DbObject res = bundle.GetValue<DbObject>("res")[i] as DbObject;
                        offset = bundleReader.ReadInt(Endian.Big);
                        size = bundleReader.ReadInt(Endian.Big);

                        res.SetValue("catalog", catalogIndex);
                        res.SetValue("cas", casIndex);
                        res.SetValue("offset", offset);
                        res.SetValue("size", size);
                        if (isPatch)
                            res.SetValue("patch", true);
                    }

                    for (int i = 0; i < bundle.GetValue<DbObject>("chunks").Count; i++)
                    {
                        if (flags[z++])
                        {
                            unused = bundleReader.ReadByte();
                            isPatch = bundleReader.ReadBoolean();
                            catalogIndex = bundleReader.ReadByte();
                            casIndex = bundleReader.ReadByte();
                        }

                        DbObject chunk = bundle.GetValue<DbObject>("chunks")[i] as DbObject;
                        offset = bundleReader.ReadInt(Endian.Big);
                        size = bundleReader.ReadInt(Endian.Big);

                        chunk.SetValue("catalog", catalogIndex);
                        chunk.SetValue("cas", casIndex);
                        chunk.SetValue("offset", offset);
                        chunk.SetValue("size", size);
                        if (isPatch)
                            chunk.SetValue("patch", true);
                    }
                }

                return bundle;
            }
        }
    }
}
