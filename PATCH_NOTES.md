# Frosty Mod Manager 1.0.6.3, DAI personal build: patch notes

Base: tag `v1.0.6.3` of https://github.com/CadeEvs/FrostyToolsuite
Branch: `dai-fixes` on https://github.com/dduttonnc/FrostyToolsuite (v1 to v6 in one commit,
"My DAI build"; later versions in their own commits). Every change is marked in the code with a
`[DAI fix]` comment (34 in MainWindow.xaml.cs, 3 in MainWindow.xaml, 23 in ConfigWindow, 1 in
FrostyPack, 9 in the executor, 6 in Standard.cs).

| Version | Change | Summary |
|---|---|---|
| v1 | Fix Dragon Age: Inquisition .daimod import | Converter and config window rewrite, bundle removal fix |
| v2 | More DAI fixes from code review | Import crash, chunk meta, thread safety |
| v3 | Fix texture streaming ranges in converted .daimod files | Black/rainbow textures at a distance |
| v4 | Load game data for .daimod bundle checks | Add vs modify decided from the real game data |
| v5 | Leave empty texture ranges out | Infinite load screen with some texture mods |
| v6 | Clearer load order labels | "List"/"Priority" renamed to "Bottom wins"/"Top wins" |
| v7 | Reconfigure converted .daimod files | Right-click > Reconfigure... changes a mod's settings in place |

## Pull requests to CadeEvs/FrostyToolsuite (base 1.0.6.3)

| PR | Contents |
|---|---|
| #350 | Load order labels (v6) |
| #351 | Bundle removal order + chunkMeta removal (v1 Standard.cs, v2) |
| #352 | Empty chunk range (v5) |
| #353 | Thread safety: archive ref counts + addedBundles (v2) |
| #354 | .daimod import: converter, config window, texture ranges, game data check (v1, v2, v3, v4); depends on #351 |

All five merge cleanly together on 1.0.6.3. On the unreleased 1.0.6.4 branch, all but #353 apply
cleanly (1.0.6.4 already has the #327 part of it).

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
   `HashSet` of added bundles is now locked when written.

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

Not in any upstream PR yet.

### `FrostyModManager/Windows/MainWindow.xaml` and `MainWindow.xaml.cs`

1. **Configurable mods could only be configured on import.** Changing a setting meant uninstalling
   the mod and importing the `.daimod` again, which also put it at the bottom of the load order.
   Converted mods now have a right-click **Reconfigure...** option, in both the installed mods list
   and the applied mods (load order) list. It reopens the configuration window with the settings
   chosen last time, converts the mod again, and swaps it in where it was: same file name, same
   load order position and enabled state, in every pack. The right-click menu only appears on mods
   converted from a `.daimod`.
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
