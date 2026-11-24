# Frosty Toolsuite: detailed changes

Like [PATCH_NOTES.md](../PATCH_NOTES.md), this file has one section per Frosty program, newest first,
because each has its own version numbers:

1. [Frosty by dduttonnc](#1-frosty-by-dduttonnc): v1.7.1, v1.7 and v1.0.6.4-dai.1, in full detail below.
2. [J-Lyt's Frosty](#2-j-lyts-frosty): v1.0.7 builds.
3. [Original Frosty Toolsuite](#3-original-frosty-toolsuite): v1.0.0 to v1.0.6.4.

# 1. Frosty by dduttonnc

Every code change in this section is marked with a `[DAI fix]` comment.

## 1.7.1

Shows as **1.7.1 - 2026.11.24.1**. Release tag: `2026.11.24.1`. DAI users delete ModData once; MEA users delete the MEA cache once.

Mass Effect: Andromeda changes are marked with `[MEA fix]`.

| Change | Files |
|---|---|
| Mass Effect: Andromeda is treated as supported, like DAI and Veilguard: picking it no longer shows the "not supported" message. Other games get an "untested with this version" note with Yes/No everywhere; the profile picker used to refuse them outright | `FrostyModManager/Windows/PrelaunchWindow2.xaml.cs`, `FrostyEditor/Windows/PrelaunchWindow2.xaml.cs`, `FrostyPlugin/Windows/FrostyProfileSelectWindow.xaml.cs` |
| `StandardAssetLoader` (MEA, and the other games that use it) read each superbundle's toc chunks before adding the superbundle, so every toc chunk was recorded under the previous superbundle's index. The mod executor then looked for modified chunks in the wrong toc, and `chunks0.toc` (where MEA streams full-resolution texture mips from) was never written: the game streamed the original mips under the modded texture header and hung on save loads and area transitions. The superbundle is now added first. The cache stores these indices, so delete the MEA cache once | `FrostySdk/Managers/Loaders/StandardAssetLoader.cs` |
| Old-format (FBMODV1) texture mods keep the whole texture chunk in one piece (`logicalOffset` 0), so Frosty wrote all mips into the bundle while the texture header says only the mips from `firstMip` on are there (e.g. Beard Styles, Nexus 506: 5.5 MB in `mastersharedbundle` where the game has 87 KB). Saves stopped loading and area transitions hung. For MEA, the legacy loader now reads each texture header in the mod and splits the matching chunk the same way as `Utils.CompressTexture` (`logicalOffset`, `logicalSize`, `rangeStart`, `rangeEnd`). When the mips after `firstMip` are smaller than 0x10000, the bundled part starts earlier, and `logicalOffset` is where it starts (as Frosty Editor writes it, e.g. Tempest Overhaul). The chunk is left as-is if the header doesn't match its size or there is no block boundary at that mip. | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI: the 1.0.7 base ignored a mod's `chunks` pseudo-bundle actions and took each chunk's superbundles from the asset manager instead. Replaced vanilla chunks then went through the toc modify path, which copies the base entry to the front of the patch `chunks0.toc` with `delta = true` (the game's own patch tocs never use it), and the game showed sparkles or pixelated textures (e.g. Cullen 4k Armor Textures, FBMODV2). New chunks flagged as added but without a superbundle (Creation Magic Expanded) went into no toc at all. DAI now routes chunks through the 1.0.6.4 `chunks` pseudo-bundle again: the mod's `chunks` actions are kept, old v1 mods modify and converted .daimod texture chunks are added, every toc gets the modify list and only the `chunks` superbundles get added chunks. Checked against ModData from the 1.0.6.4 build, which writes plain `{id, sha1}` entries | `FrostyModSupport/FrostyModExecutor.cs`, `FrostyModSupport/Actions/StandardModExecutor.cs` |

## 1.7: DAI and Veilguard in one build

One Mod Manager for both Dragon Age: Inquisition and Dragon Age: The Veilguard. Before this, the
DAI build and J-Lyt's Veilguard build shared `%LocalAppData%\Frosty`, so each listed the other
game's profile and failed on it.

Base: branch `DragonAge` of https://github.com/J-Lyt/FrostyToolsuite (68f741f, "v1.0.7 -
2026.08.21.0"), which is upstream's unreleased 1.0.7 engine plus Veilguard support. It blocked DAI
("Use 1.0.6.3"). The DAI fixes below (v1 to v8) were ported onto it; they are still described in
the sections further down, which refer to the 1.0.6.4 file names. Developed on branch
`dav/experimental`. Every DAI and Veilguard mod in a large load order was checked in game on
2026-11-21, before the Veilguard loading change (branch `dav/splash-ui`), which is still being tested.

Shows as **1.7 - 2026.11.21.1** (Mod Manager and Editor; FileVersion 1.7.0.0). Changes on top of
J-Lyt's branch:

| Change | Files |
|---|---|
| DAI can be picked again in the profile pickers | `FrostyModManager/Windows/PrelaunchWindow2.xaml.cs`, `FrostyEditor/Windows/PrelaunchWindow2.xaml.cs`, `FrostyPlugin/Windows/FrostyProfileSelectWindow.xaml.cs` |
| 1.0.6.4 DAI mod-apply fixes that 1.0.7 never got: binary superbundle test uses `!cas` (upstream dcd9e2a), patched toc chunks drop `base`, missing base toc guards, new bundles added to the toc (#249), plus v2/v5/v8 (`Standard.cs` became `StandardModExecutor.cs`) | `FrostyModSupport/Actions/StandardModExecutor.cs` |
| v8 cache build: parallel `LegacyAssetLoader`, prepared binary bundle data, zlib level 1 cache data, `WorkerPool` | `FrostySdk/Managers/Loaders/LegacyAssetLoader.cs`, `FrostySdk/Managers/AssetManager.cs`, `FrostySdk/Utils.cs`, `FrostySdk/WorkerPool.cs`, `FrostySdk/Interfaces/ILogger.cs`, `FrostySdk/FrostySdk.csproj` |
| Mod apply on the worker pool (DAI and Veilguard), thread-safe archive ref counts, `.daimod` removal entries skipped | `FrostyModSupport/FrostyModExecutor.cs` |
| v1-v7 Mod Manager changes, splash loading list, CPU Threads / CPU Priority options, server GC, version suffix | `FrostyModManager/Windows/MainWindow.xaml(.cs)`, `ConfigWindow.xaml.cs`, `FrostyPack.cs`, `SplashWindow.xaml(.cs)`, `App.xaml.cs`, `App.config`, `Properties/AssemblyInfo.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs` |
| Veilguard cache build: tocs are read and bundles parsed several superbundles ahead on the worker pool (CPU Threads option), then added in their original order; removed an unused search of all bundles for every bundle, and the per-name list search when reading tocs. The splash screen shows the same "Loading Data N/M (up to T at once)" line and list of superbundles in flight as DAI, with a bundle count for the one being added | `FrostySdk/Managers/Loaders/CasAssetLoader.cs` |
| All 33 game SDKs in `FrostySdk/Profiles` are copied into the build again, as upstream 1.0.7 did. J-Lyt's project only copied the Veilguard SDK, so DAI asked to regenerate its SDK, and that fails on 1.0.7 ("Unable to find the first type info offset"). Every SDK loads against this FrostySdk | `FrostySdk/FrostySdk.csproj` |
| A mod or collection file that fails to load at startup is logged ("Could not load ...") and skipped, instead of closing the Mod Manager with an AggregateException. Collections no longer show a message box from a worker thread | `FrostyModManager/Windows/MainWindow.xaml.cs` |
| The solution lists AnimationEditorPlugin, UnifyAssetsPlugin, DatapathFixPlugin and MeshVariationDbPlugin as SDK-style projects, which they are. J-Lyt had them as old-style C# projects, so Visual Studio 2022 could not parse them and left the plugin projects unloaded after its upgrade prompt. DatapathFix and DatapathFix(DEBUG) stay old-style C# projects, as they are: listed as SDK-style, Visual Studio could not see their `Release - Veilguard` / `Debug - Veilguard` configurations and warned about unknown configuration mappings. DatapathFix(DEBUG) builds its `Debug - Veilguard` configuration in every solution configuration: in `Release`, `Release - Alpha` and `Release - Beta` it built `Release`, which outputs `DatapathFix.exe`, so DatapathFixPlugin failed to embed `DatapathFix(DEBUG).exe` | `FrostyEditor/FrostyEditor.sln` |
| Old-format `.fbmod` files (from early Frosty Editor versions) load again. J-Lyt's Veilguard root-level check read their resource list, which old-format mods don't have, so they failed with "Object reference not set to an instance of an object". Load errors now log the full exception | `FrostyModManager/Windows/MainWindow.xaml.cs` |
| Version 1.7 (title bar, splash, About, log) and FileVersion 1.7.0.0; the Editor title uses the same version string as the Mod Manager. Credits list the fork chain (Wavebend, CosmicDreamsofCode, J-Lyt, dduttonnc) and links to this fork, the original Frosty and Frosty v2 | `FrostyModManager/Properties/AssemblyInfo.cs`, `FrostyEditor/Properties/AssemblyInfo.cs`, `FrostyEditor/App.xaml.cs`, `FrostyPlugin/App.cs`, `FrostyPlugin/CreditsFMM.txt` |
| Check for Updates uses this fork: it reads the latest release of `dduttonnc/FrostyToolsuite`, whose tag must be the build date (`2026.11.21.1`, a leading `v` is allowed), compares it with `App.BuildVersion`, shows `FrostyEditor/ChangeLog.txt` from `main` down to this build's entry, and Yes opens the release page in the browser (nothing is downloaded). No release yet, no connection or a bad tag is logged instead of showing an error box at every startup. New 1.7 entry at the top of `ChangeLog.txt` | `FrostyPlugin/Utils/UpdateCheckerUtils.cs`, `FrostyPlugin/Windows/UpdateCheckerWindow.xaml.cs`, `FrostyModManager/App.xaml.cs`, `FrostyEditor/App.xaml.cs`, `FrostyEditor/ChangeLog.txt` |

Differences from the 1.0.6.4 DAI build (all fine in game so far):

* The cache format is 1.0.7's (version 4), so the first launch rebuilds the DAI cache. Old 1.0.6.4
  caches are not reused.
* 1.0.7 tracks toc chunks per superbundle. For converted `.daimod` files an existing chunk is
  modified in the tocs that already hold it, and a new chunk is added to every `chunks` superbundle.
  1.0.6.4 added every texture chunk of a converted mod to the `chunks` tocs.

## 1.0.6.4-dai.1: DAI build

Base: branch `1.0.6.4` of https://github.com/CadeEvs/FrostyToolsuite (moved up from tag `v1.0.6.3`
on 2026-11-17; 1.0.6.4 is 1.0.6.3 plus 10 small upstream fixes, #327 to #344, and was tested in game).
Branch: `dai-fixes` on https://github.com/dduttonnc/FrostyToolsuite (v1 to v6 in one commit,
"My DAI build"; later versions in their own commits). Every change is marked in the code with a
`[DAI fix]` comment (21 in MainWindow.xaml.cs, 3 in MainWindow.xaml, 17 in ConfigWindow, 1 in
FrostyPack, 9 in the executor, 8 in Standard.cs, 5 in AssetManager.cs, 6 in LegacyAssetLoader.cs,
1 each in Utils.cs, WorkerPool.cs, ILogger.cs, OptionsWindow.xaml.cs, SplashWindow.xaml,
SplashWindow.xaml.cs, App.config and AssemblyInfo.cs, 2 in App.xaml.cs).

## Build version (1.0.6.4 builds)

This build shows as **1.0.6.4-dai.1** in the title bar, splash screen, About window and log. The
version is the upstream base plus a fork suffix (`-dai.1`, `-dai.2`, ... one per release).

* `FrostyModManager/Properties/AssemblyInfo.cs`: `AssemblyVersion` and `AssemblyFileVersion` stay
  4-part (now 1.0.6.4; upstream's 1.0.6.4 branch still says 1.0.6.3). The suffix lives only in
  `AssemblyInformationalVersion`. To release, bump that one string.
* `FrostyModManager/App.xaml.cs`: the displayed version reads the informational version, falling
  back to the 4-part one. The Developer build still adds " (Developer)".
* Mods aren't affected: the .fbmod format version and the game version written into converted
  mods are separate numbers. Exported packs record the displayed version in their manifest, which
  nothing reads.

| Version | Change | Summary |
|---|---|---|
| v1 | Fix Dragon Age: Inquisition .daimod import | Converter and config window rewrite, bundle removal fix |
| v2 | More DAI fixes from code review | Import crash, chunk meta, thread safety |
| v3 | Fix texture streaming ranges in converted .daimod files | Black/rainbow textures at a distance |
| v4 | Load game data for .daimod bundle checks | Add vs modify decided from the real game data |
| v5 | Leave empty texture ranges out | Infinite load screen with some texture mods |
| v6 | Clearer load order labels | "List"/"Priority" renamed to "Bottom wins"/"Top wins" |
| v7 | Reconfigure converted .daimod files | Right-click > Reconfigure... changes a mod's settings in place |
| v8 | Faster cache build and mod apply | Branches `perf/cache-build-and-apply` (cache rebuild ~15 to ~7 min), then on top `perf/parallel-load` (~45 s), `perf/splash-progress`, `perf/binary-overlap` and `perf/splash-files` (~32 s) |

## Pull requests to CadeEvs/FrostyToolsuite (base 1.0.6.4)

| PR | Contents |
|---|---|
| #350 | Load order labels (v6) |
| #351 | Bundle removal order + chunkMeta removal (v1 Standard.cs, v2) |
| #352 | Empty chunk range (v5) |
| #353 | Thread safety: archive ref counts (v2) |
| #354 | .daimod import: converter, config window, texture ranges, game data check (v1, v2, v3, v4); depends on #351 |
| #355 | Reconfigure for converted .daimod files (v7); depends on #354 |

All six were moved from 1.0.6.3 to 1.0.6.4 on 2026-11-17, at a contributor's request (1.0.6.3 gets no more
updates). They merge cleanly on 1.0.6.4, and #355 sits on top of #354. #353 lost its addedBundles part,
which 1.0.6.4 already has (#327).

---

## v1: Fix Dragon Age: Inquisition .daimod import

### `FrostyModManager/Windows/MainWindow.xaml.cs`: .daimod to .fbmod converter

The converter lives in `InstallMods`. New helper methods sit just above it:
`DaiXmlText`, `DaiHexToBytes`, `DaiDecompressedLength`, `DaiParseFirstMip`, `DaiReadMipValue`,
`DaiBuildBundleIdMap`, `DaiResolveBundleAction`.

1. **New assets were dropped.** Every ebx/res was imported as "modify", but Frosty's bundle writer
   only modifies entries that are already in a bundle, so anything a `.daimod` added was silently
   lost. Each bundle entry is now checked against the game data: already in that bundle means
   "modify", otherwise "add". If the bundle isn't in the game data, the `.daimod`'s own
   add/modify flag is used.
2. **Archive offsets could point at the wrong data.** Offsets were calculated in XML order while
   the archive was written in resource-id order. Both now use resource-id order.
3. **Off-by-one on texture/audio chunks.** The action that registers a chunk in the special
   `chunks` bundle referenced the *previous* resource (or -1). The last chunk in each run never got
   registered, and the action was applied to the wrong resource.
4. **First mip read with the wrong byte order.** The value is a little-endian int inside the chunk
   meta; it was parsed as one big-endian hex number (3 became 50331648). The parser now finds the
   `firstMip` field and reads it properly, with a 0-15 sanity check.
5. **Remove actions were ignored.** They now produce real "remove" bundle actions. These entries are
   marked `isRemoval` and carry no data.
6. **Stale size and hash after a configuration script changes data.** Both are now recalculated
   from the bytes actually imported. A script-changed texture chunk that has a streaming offset
   keeps its old size info, with a warning.
7. **Robustness.** XML comments, a missing `<bundles>` section, bad chunk ids or out-of-range
   resource ids now give a warning instead of crashing the import.
8. **Log summary.** Each import writes
   `[DAI] <mod>: N bundle modifications, N additions, N removals` to the Mod Manager log.

### `FrostyModManager/Windows/ConfigWindow.xaml.cs`: configurable .daimod window

1. **Closing with X skipped the script** and imported the mod unconfigured, with every variant
   enabled. Closing without Save now cancels that mod's import, with a warning.
2. **Script compile errors crashed the import.** They're now listed as import errors with line
   numbers. Framework assemblies are referenced by full path, so scripts compile regardless of the
   working directory.
3. **Script runtime errors crashed the Mod Manager.** They're now reported as import errors.
4. **Dropdowns passed the list position instead of the enum value.**
5. **A value still being typed when you press Save was lost.** It's now committed (`CommitAll`).
6. **Small fixes.** Unknown setting names return 0 instead of crashing. Bad resource indexes are
   ignored and logged. Duplicate setting ids no longer abort the import. Decimals accept `1,5` and
   `1.5`.
7. **Script logging.** `LogLn` output now goes to the Mod Manager log as `[DAI script] ...`.

### `FrostyModSupport/FrostyModExecutor.cs`

1. Removal-only entries from converted `.daimod` files carry no data, so they're skipped when mod
   data is loaded.

### `FrostyModSupport/Actions/Standard.cs`

1. **Removing two or more entries from one bundle removed the wrong ones.** The ebx, res and chunk
   lists were removed in ascending index order, so every removal shifted the indexes of the rest.
   All three now remove from the end first.

---

## v2: More DAI fixes from code review

### `FrostyModManager/Windows/MainWindow.xaml.cs`

1. **Import crash on some resource types.** A res whose type id is 0x80000000 or higher (several
   real Frostbite types are) crashed the import with an overflow error. Frosty stores the value as a
   signed int, and the converter read it back as unsigned. The parsed value is now compared
   directly.

### `FrostyModSupport/Actions/Standard.cs`

1. **Chunk meta out of step after a removal.** A bundle's `chunks` and `chunkMeta` lists are paired
   by position. Removing a chunk left its meta behind, so every later chunk got its neighbour's meta
   (wrong `firstMip`/`h32`). The matching meta entry is now removed too, but only when the two lists
   are the same length.

### `FrostyModSupport/FrostyModExecutor.cs` (affects all `.fbmod` files, not just DAI)

1. **Race on shared archive data.** Resources inside a mod are loaded on several threads at once,
   and the reference counts on shared data were updated without a lock. A lost update could free
   data still in use, causing a crash or a missing asset when applying. New helpers
   `AddArchiveRef` / `ReleaseArchiveRef` do this under a lock. They're used by the ebx, res and
   chunk paths.
2. **Race on added bundles.** Ported the upstream 1.0.6.4 fix (#327): the per-superbundle
   `HashSet` of added bundles is now locked when written. Since the move to 1.0.6.4 this is
   upstream's own code.

---

## v3: Fix texture streaming ranges in converted .daimod files

### `FrostyModManager/Windows/MainWindow.xaml.cs`

1. **Black or rainbow textures at a distance, correct up close.** Some `.daimod` files list a texture
   chunk as `firstMip 0`, with the always-loaded range covering the whole chunk, while the texture's
   own header says the always-loaded part starts at a smaller mip (e.g. 512×512 of a 2048×2048
   texture). The game then reads full-size mip data as if it were the small mip. DAI Mod Manager
   didn't hit this; Frosty writes the range exactly as listed.
   The converter now reads every texture header in the mod (`DaiReadTextureLayouts`) and, when a
   chunk's first mip disagrees with its header, recalculates `firstMip`, `logicalOffset`,
   `logicalSize`, `rangeStart` and `rangeEnd` (`DaiCalcTextureRange`). It uses the same layout as
   Frosty's own texture importer (`Utils.CompressTexture`), including its 0x10000 minimum range.
   It leaves the values alone if the texture has only one mip or the chunk isn't split into
   blocks at that mip. The `[DAI]` log line reports how many ranges were corrected.
   Confirmed in game with Cullen Face Retexture - No scar FIXED v1.1 (corrected by hand first, then
   the same values checked against the new code).

---

## v4: Load game data for .daimod bundle checks

### `FrostyModManager/Windows/MainWindow.xaml.cs`

1. **The add-vs-modify check never ran.** v1's fix #1 decides per bundle whether a `.daimod` entry
   is an addition or a modification by looking it up in Frosty's asset database. But the splash
   screen throws that database away once the Mod Manager has started, so every lookup failed and
   every entry fell back to the `.daimod`'s own flag, the old behaviour. The log showed it as
   "N decided from the .daimod's own add/modify flag". The converter now loads the asset database
   from Frosty's cache on demand (`DaiGetAssetManager`), only when a `.daimod` is imported, and
   frees it when the import finishes (`DaiReleaseAssetManager`). Importing a `.daimod` takes a
   little longer as a result.
2. **Diagnostic log line.** When a bundle still can't be found, the log shows the missing name, the
   closest name Frosty has, and how many bundles were loaded.

---

## v5: Leave empty texture ranges out

### `FrostyModSupport/Actions/Standard.cs`

1. **Infinite load screen with some texture mods.** Some `.daimod` files list a texture chunk with
   `rangeStart 0` and `rangeEnd 0` (e.g. Scarless Cullen - DA2 Beard 4k v2 Trespasser). Frosty wrote
   that into the bundle as a zero-byte always-loaded range, and the game waited forever for it.
   DAI Mod Manager's source (v0.59, decompiled for comparison) only writes the range when start and
   end differ. Frosty now does the same for DAI, when both adding and modifying chunks.

Checked against DAI Mod Manager while doing this: it also removes the matching `chunkMeta` entry
when it removes a chunk (same as the v2 fix), and it doesn't change texture headers.

---

## v6: Clearer load order labels

### `FrostyModManager/Windows/MainWindow.xaml`

1. **"List" and "Priority" didn't say which mod wins.** Both show the same mods; "Priority" just
   flips the display so the winning mod is on top. Renamed to **Bottom wins** and **Top wins**
   (the existing tooltips stay), and the box is wider to fit. Only the labels changed: the saved
   setting is still stored as List/Priority, so existing configs keep working.

---

## v7: Reconfigure converted .daimod files

Upstream PR #355 (branch `pr6-daimod-reconfigure`, based on #354).

### `FrostyModManager/Windows/MainWindow.xaml` and `MainWindow.xaml.cs`

1. **Configurable mods could only be configured on import.** Changing a setting meant uninstalling
   the mod and importing the `.daimod` again, which also put it at the bottom of the load order.
   Converted mods now have a right-click **Reconfigure...** option, in both the installed mods list
   and the applied mods (load order) list. It reopens the configuration window with the settings
   chosen last time, converts the mod again, and swaps it in where it was: same file name, same
   load order position and enabled state, in every pack. The right-click menu only appears on
   converted mods that have settings: ones with a saved settings file, or (for mods imported before
   v7) whose description ends with the converter's list of chosen settings. It used to appear on
   every converted mod, though most `.daimod` files have nothing to configure.
2. **The original and the chosen settings are kept.** When a configurable `.daimod` is imported, a
   copy of it and its settings are saved in `<mods folder>\DAI originals\` as `<name>.daimod` and
   `<name>.settings.json` (settings by id, culture-independent). Frosty only loads `.fbmod` files
   directly in the mods folder, so the subfolder is ignored when mods are loaded. Uninstalling the
   mod removes them.
3. **Mods imported before this version.** Frosty has no copy of these, so the first Reconfigure asks
   for the original `.daimod` (and checks its name matches), then keeps a copy. The window is
   pre-filled from the settings listed at the end of the mod's description, on a best-effort basis:
   check the values before pressing Save.
4. **Nothing changes until Save.** Reconfigure converts into a temporary folder first; closing the
   window without Save, a script error or a failed conversion leaves the installed mod as it was.
5. **Re-importing an installed `.daimod`** now starts from the settings chosen last time and replaces
   the mod in place. It used to appear twice in the list until the Mod Manager was restarted.
6. The converter moved out of `InstallMods` into `DaiConvertMod` (unchanged apart from the output
   location, the pre-filled settings and returning instead of `continue`), so import and
   Reconfigure share it. `DaiImportMod` handles import. `AddMod` is split into `LoadMod` + add.
7. The `.daimod` is now opened read-only during conversion (it used to ask for write access).

### `FrostyModManager/Windows/ConfigWindow.xaml.cs`

1. The window takes the saved settings and pre-fills every setting type (number, decimal,
   checkbox, dropdown, text) from them, clamped to the script's min/max. Settings the mod no longer
   has are ignored; new ones use the mod's default. `GetSettingValues` returns the chosen settings
   for saving. The window title now names the mod.

### `FrostyModManager/FrostyPack.cs`

1. New `ReplaceMod`: puts the re-converted mod in the old one's place in a pack, keeping its
   position and enabled state.

---

## v8: Faster cache build and mod apply

Branch `perf/cache-build-and-apply`; `perf/parallel-load` adds the multi-core loading below on top
of it. No change to what gets written, only how fast.

Cache build on an i9-9900K 16-thread CPU: originally 10-15 min, ~7 min with the safe fixes, 45 s with
`perf/parallel-load`, about 32 s with `perf/splash-files` (cache identical to the 45 s build).
Each step was checked in game (romances, textures, voice packs, item mods). Applying mods: 1m 24s.

### `FrostySdk/Managers/AssetManager.cs` (cache build)

1. **Writing the cache was quadratic.** The EBX, RES and chunk loops fetched entry *i* with
   `Values.ElementAt(i)`, which walks the dictionary from the start every time, so the "Writing to
   cache" bar slowed down as it went. They now loop over the values once. The cache file is
   identical (same order).
2. **Progress updates are throttled.** The splash screen (and taskbar) got one update per entry,
   hundreds of thousands in all. It now gets one per whole percent.

### `FrostySdk/Managers/Loaders/LegacyAssetLoader.cs` (cache build, `perf/parallel-load` only)

1. **Superbundles are read ahead on several cores.** DAI has hundreds of small superbundles (one
   per level layout), and "Loading Data" read and parsed them one after another on one core. Up to
   one superbundle per core is now read and parsed ahead (tocs and, for those with up to 256
   bundles, every bundle) while the loading thread adds the finished ones to the asset lists in the
   original order, so the cache comes out the same. Larger superbundles are read in batches of 256
   bundles, parsed in parallel. `AssetManager.ProcessTocChunks` is split so a toc read elsewhere
   can be processed.
2. **Binary superbundles load on several cores.** 109 of DAI's superbundles (mostly multiplayer
   and DLC layouts) use the older binary format, where each patched bundle is unpacked in full and
   compared with the base game's copy. A timing build showed they took 325 of the 329 seconds of
   "Loading Data", at about 0.3 s per bundle on one core. About one bundle per core is now unpacked
   and compared at a time, and the results are added in toc order. The forced garbage collection
   after each binary superbundle is gone.
   The unpacking also runs across superbundle boundaries (branch `perf/binary-overlap`): bundles of
   the next binary superbundles start while the current one is still being added, so the cores no
   longer wait for the largest bundle at the end of each superbundle. Up to two bundles per core
   are in flight.
3. **The splash screen shows the count and progress** (branch `perf/splash-progress`, on top of
   `perf/parallel-load`): "Loading Data 112/254 (up to 16 at once): <superbundle>", and the
   progress bar fills as superbundles finish. It used to show only the superbundle name, with no
   progress, which looked stuck now that several load at once. One update per superbundle.
   Branch `perf/splash-files` (on top of `perf/binary-overlap`) also makes the Mod Manager's splash
   screen taller while data loads and lists every superbundle in flight with what is happening to
   it (adding, adding bundle n/m, unpacking, reading, ready). The list updates at most ten times a
   second and goes away when loading is done. The loader only sends it to a logger that implements
   the new `ILoadingListLogger` (`FrostySdk/Interfaces/ILogger.cs`), so the Editor is unchanged.

### `FrostySdk/Managers/AssetManager.cs` (binary superbundles, `perf/parallel-load` only)

1. **Patched assets are matched to the base game with a lookup.** Each patched entry was compared
   with every base entry in turn (and each comparison boxed two Sha1 values). The base entries are
   now put in a dictionary by Sha1 first; the first match still wins, as before.
2. The matching and the recompression of changed data (`PrepareBundleData`) are split from the
   step that records them in the cache (`AddPreparedBundleData`), so the first can run on any
   thread while the second still runs in order.
3. **Changed data is recompressed for the cache at zlib level 1 instead of 9**, and the entries of
   a bundle are compressed in parallel. Level 9 was most of the remaining time; the data reads back
   the same, but `_sbdata.cas` comes out somewhat larger and no longer byte-identical to older
   caches. New `Utils.CompressFileForCache`; `Utils.CompressFile` is unchanged (level 9) for
   everything written into the game's files.

### CPU Threads and CPU Priority options (`perf/cpu-limit`)

Running every core flat out can make a PC feel locked up, and an unstable overclock can crash
under full load. Options > Mod Manager Options > Performance now has two dropdowns:

* **CPU Threads**: 1 up to the number of logical processors Windows reports. The default is that
  number minus 2 (at least 1), e.g. 14 on a 16-thread CPU.
* **CPU Priority**: Low (default), Below Normal, Normal, Above Normal, High. Realtime is not
  offered. These are the priorities of Frosty's worker threads, not of the whole process, so the
  game Frosty launches still starts at normal priority.

New `FrostySdk/WorkerPool.cs` runs that work on its own threads, because the .NET thread pool
can't be capped below one thread per core. The loader (`LegacyAssetLoader`, cache compression in
`AssetManager`) and, when applying mods, mod loading, handlers and the per-superbundle work
(`FrostyModExecutor`) use it; the loading thread also runs at the chosen priority. Nothing ever
waits on a job that hasn't started, so 1 thread works too, only slower. Settings are applied at
startup and when Options is saved. Garbage collection (server GC) still uses every core in short
bursts, and the non-DAI apply paths (Heat, manifest) are unchanged.

### `FrostyModManager/App.config` (`perf/parallel-load` only)

1. **Server garbage collection.** Loading creates millions of objects, and the default collector
   pauses every thread and works on one core. `gcServer` makes it use all cores. Uses more memory.

### `FrostyModSupport/Actions/Standard.cs` (Applying Mods)

1. **Unmodified DAI base bundles are no longer parsed.** Every bundle in a superbundle that a mod
   touches was read, but base bundles no mod changes are never written back (the toc keeps
   pointing at the game's copy), so reading them was wasted.
2. **Each `.sb` file is opened once per superbundle** instead of once per bundle.
3. **Duplicate checks on written data use a set** instead of scanning a list, which compared every
   entry each time.

---

## Confirmed in game (17 Nov 2026)

* A `.fbmod` that adds a new audio chunk (Bi-Bachelors' Voiceovers) plays correctly. This is the
  path the v1 chunk registration fix and the v2 chunk meta fix touch.
* Scarless Cullen - DA2 Beard 4k v2 Trespasser loads (v5); Cullen Face Retexture - No scar FIXED
  v1.1 looks right at every distance (v3).
* Converted `.daimod` romance mods (Bi Cullen 2.0, CullenRomance Parts 1 and 2, MRM_Full) work once
  the load order is right.

## Known issues / next fix

* Single-mip textures whose header claims a first mip they don't have (e.g. the normal map in
  Cullen Face Retexture) are left as they are; no visible problem so far.
* Converted mods are stamped with game version 0, so Frosty shows a "different game version"
  warning on each one. This is cosmetic and existing behaviour.
* If an earlier `.fbmod` changed an asset through a merge handler and a later `.daimod` replaces
  the same asset, Frosty keeps the merged version. This is existing behaviour.
* Frosty's re-creation of the old DAI Mod Manager script API (`FrostyLegacy.cs`) hasn't been
  checked against the original.

## Notes for users of this build

* Re-import `.daimod` files after updating. Fixes only apply at import time.
* Don't keep DAI Mod Manager output (e.g. `Update\Patch_ModManager Merge`) in the game folder. The
  game loads it over Frosty's data. If one was ever installed, delete Frosty's cache so it rebuilds.

# 2. J-Lyt's Frosty

Frosty for Dragon Age: The Veilguard, built on the original Frosty's unreleased 1.0.7. Its own
notes are in section 2 of [PATCH_NOTES.md](../PATCH_NOTES.md); the code and commit history are at
https://github.com/J-Lyt/FrostyToolsuite (branch `DragonAge`). Frosty by dduttonnc 1.7 starts from
its build "v1.0.7 - 2026.08.21.0" (commit 68f741f).

# 3. Original Frosty Toolsuite

The original Frosty Toolsuite by GalaxyMan2015 and the Frosty team. Its notes are in section 3 of
[PATCH_NOTES.md](../PATCH_NOTES.md); the code and commit history are at
https://github.com/CadeEvs/FrostyToolsuite. Frosty by dduttonnc 1.0.6.4-dai.1 starts from its
branch `1.0.6.4`.
