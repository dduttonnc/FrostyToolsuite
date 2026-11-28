# Frosty Toolsuite: detailed changes

Like [PATCH_NOTES.md](../PATCH_NOTES.md), this file has one section per Frosty program, newest first,
because each has its own version numbers:

1. [Frosty by dduttonnc](#1-frosty-by-dduttonnc): v1.7.6, v1.7.5, v1.7.4, v1.7.3, v1.7.2, v1.7.1, v1.7 and v1.0.6.4-dai.1, in full detail below.
2. [J-Lyt's Frosty](#2-j-lyts-frosty): v1.0.7 builds.
3. [Original Frosty Toolsuite](#3-original-frosty-toolsuite): v1.0.0 to v1.0.6.4.

# 1. Frosty by dduttonnc

Every code change in this section is marked with a `[DAI fix]` comment.

## 1.7.6

Shows as **1.7.6 - 2026.11.27.1**. Release tag: `2026.11.27.1`. No data numbers raised: nothing in this release changes the cache or ModData for any game (the .daimod changes only affect import), so nothing is rebuilt.

| Change | Files |
|---|---|
| All games: `PrintLog` (ModData Build Log) writes `Logs\ModDataBuild_<yyyy-MM-dd_HH-mm-ss>.txt` next to the exe (`DiagnosticLog.LogsFolder`) instead of `log.txt` in the current folder, with a first line naming the game and mod pack, and logs where it went; if the file can't be created it logs a warning and the apply goes on (before, the exception would have stopped it). Dated files instead of one overwritten file, so each build's list is kept and the age-based cleanup has something to go by. `DiagnosticLog.DeleteOldFiles` (Automatic Log Cleanup) also deletes `ModDataBuild_*.txt` | `FrostyModSupport/FrostyModExecutor.cs`, `FrostySdk/DiagnosticLog.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs` |
| All games: Mod Manager Options page regrouped and ordered. The property grid sorted categories alphabetically (`SortedDictionary`) and settings with an unstable `Array.Sort` on `FieldIndex` (none of the options have one), so their order was arbitrary. New `IPropertyGridLayout` interface: a class lists each category followed by its property names, and `ProcessClassWithCategories` uses that for the category of each listed property (including inherited ones from `BaseOptionsData`), the order inside each category, and the category order; unlisted properties and classes without the interface are unchanged. `ModManagerOptionsData` implements it: Launch, Mods & ModData, Logging, Interface, Performance, Updates. `PrintLog` display name 'Print Log' -> 'ModData Build Log' with a new description (base class, so the Editor shows it too). Config keys unchanged | `FrostyPlugin/Controls/FrostyPropertyGrid.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostySdk/DiagnosticLog.cs` |
| All games: new `AutoLogCleanup` (bool, default off) and `LogCleanupDays` (14/30/60/90, default 30) options under Logging (was Manager); `LogCleanupDays` is greyed out while `AutoLogCleanup` is off. `ApplyLogCleanup` runs at Mod Manager startup (before Diagnostic Logging opens its new file) and on Options save, and `DiagnosticLog.DeleteOldFiles` deletes only `Logs\Diagnostics_*.txt` (and, since the ModData Build Log change, `Logs\ModDataBuild_*.txt`) whose last write is older than the chosen days, logging how many it deleted. The `DiagnosticLogging` setting is never changed by it. `DiagnosticLog.Start` no longer keeps only the newest 10 files, so with the cleanup off every file is kept | `FrostySdk/DiagnosticLog.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyModManager/App.xaml.cs` |
| All games: `InstallErrorsWindow` (Installation Report): long messages were cut off (single-line cells, `StackPanel`, fixed 22 px rows), so the newer import notes needed sideways scrolling. Fixed column widths (Filename 190, Status 490), text wraps (`DockPanel` so the text gets the column width), rows grow with the text, no horizontal scroll bar; window 720 x 360 (was 600 x 300) | `FrostyModManager/Windows/InstallErrorsWindow.xaml` |
| All games: removed the `ApplyingThreadCount` option (General). It came in with the Veilguard fork merge and only called `ThreadPool.SetMaxThreads` around the apply step, which .NET refuses below the processor count; the DAI, MEA and Veilguard apply paths already run on `WorkerPool` (CPU Threads / CPU Priority). The FIFA/Madden and SWBF2/BFV/Squadrons apply paths now post to `WorkerPool` too instead of `ThreadPool`. The old key is removed from `manager_config.json` when the options are saved and no longer listed by Diagnostic Logging | `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyModSupport/FrostyModExecutor.cs`, `FrostyPlugin/DiagnosticReport.cs` |
| All games: LaunchPlatformPlugin skips its pre- and post-launch actions (with a log warning) when `DatapathFixEnabled` is on for the game and the DatapathFix plugin is loaded. With both on, DatapathFix (loaded first) swapped `<game>.exe` for its stub; Launch Platform then killed and restarted every launcher, and its post-launch wait latched onto the short-lived stub (or `<game>.orig.exe`) and killed the launchers again when that exited, in the middle of the relaunch DatapathFix depends on. DatapathFix's own guard (its options `Save` sets `PlatformLaunchingEnabled` to false) was undone by LaunchPlatformPlugin's `Save`, which runs after it, and answering Yes to its first-launch question skipped it. The 'Platform Launching Enabled' description now says it's skipped while DatapathFix is on. Untested: no Steam or Epic copy was available | `Plugins/LaunchPlatformPlugin/Actions/LaunchExecutionAction.cs`, `Plugins/LaunchPlatformPlugin/Options/LaunchOptions.cs` |
| All games: LaunchPlatformPlugin's post-launch action hooked `Exited` on the first running process whose name starts with `<ProfileName>`. A game that goes through the EA app can first run a bootstrap copy that hands over to the EA app and exits within seconds (seen on MEA with the EA app: exit code 0x000186AA, then a relaunch with `-dataPath`; a DAI Steam copy, which needs the EA app, is assumed to do the same), so `KillPlatformProcesses` closed Steam and the EA app mid-launch and, with `m_shouldRelaunchPlatform` (`ShouldRelaunchPlatform`, or the platform was open before the launch), restarted the platform without the game attached (likely why there was no Steam overlay). It now starts a background watcher (`CloseLaunchersAfterGame`) that polls once a second and closes the launchers only after 180 s with no `<ProfileName>*` process running, or 10 s once one has run for 30 s in a row. A newer launch bumps `m_launchId` under `m_launchLock` in the pre-launch action; the watcher re-checks it under the same lock before closing anything, so it can't close a launcher a newer launch just started. An exception on the watcher thread is logged as a warning instead of closing Frosty (the old `Exited` handler had no catch). The closing step goes to Diagnostic Logging, with whether the game closed or never came back. Tested with the EA app (Platform EADesktop on DAI: the launchers closed and the EA app restarted about 10 s after the game exited). Untested with Steam and Epic: no copy was available | `Plugins/LaunchPlatformPlugin/Actions/LaunchExecutionAction.cs` |
| DAI and MEA: before the pre-launch actions run, `PluginManager.SkipDatapathFixQuestionForSteamCopy` sets `DatapathFixEnabled` and `DatapathFixFirstLaunch` to false for the game (per-game keys) and logs it when the DatapathFix launch action is loaded, the question hasn't been answered (`DatapathFixFirstLaunch` still true) and `steam_appid.txt` is in the game folder, so DatapathFix never asks. Its question tells Steam copies that need the EA app to enable it, but `FrostyModExecutor.LaunchGame` has launched those copies through `steam://run` since 1.7.3 (only while DatapathFix is off), and a player's Diagnostic Logging log showed DatapathFix starting the stub exe directly, outside Steam, so no Steam overlay; with it off, mods and the overlay worked. Existing answers are left alone. Untested: no Steam copy was available | `FrostyPlugin/Managers/PluginManager.cs` |
| DAI: `.daimod` import. DAI Mod Manager saves the official patch as `Patch.daimod` in its own folder (details name 'Official Patch', author 'Bioware'; v0.59 `MainWindow`), and that file is the only source of `patch="2"` resources: entries the game rebuilds from `baseSha1` + `deltaSha1`, which all point at `resourceId="0"` because the file carries no data for them. The converter took it as a normal mod, so each of its 13,910 delta entries would have got resource 0's data. It is now refused with an import error, and any other `patch="2"` resource is skipped and counted in one import warning. No mod in the 21-file test set has `patch="2"` (all are `1` or have none), so their conversion is unchanged. ModData is unchanged | `FrostyModManager/Windows/MainWindow.xaml.cs` |
| DAI: `.daimod` settings scripts, matched to DAI Mod Manager v0.59's `Scripting` and `ModConfigWindow`. `SetResourceEnabled(int)` now takes the position in the mod's `<resource>` list (it took the data block number, so with `remove` entries in the mod it switched off the wrong entry, and removals could not be switched off); the on/off state and data block are kept per entry (`ScriptResourceEntry`), and a disabled entry, removals included, gets no actions. New calls: `SetResourceEnabled(string)`, `ChangeResourceId(int, int)`, `ChangeResourceId(string, int)` (name match ignores case; bad names or numbers are logged and ignored, as DAI MM logs them), `ModConfigElementsList.AddColorElement` and the `DAIMod.Color` type (float R, G, B, A; the window shows a swatch and a `#AARRGGBB` text box); a script that used one failed to compile. Enum dropdowns give the script the list position and take `defValue` as one, as DAI MM and Frosty 1.0.6.4 did (1.0.6.4-dai.1 to 1.7.5 used the enum value); settings are now saved and shown as the choice's name, settings files from those versions are read as enum values, and old description lines as positions. A chunk whose data a script replaced or repointed is split by its texture header (`DaiCalcTextureRange`) when there is one, else gets DAI MM's values: logical offset/size from the new length, range reset to the whole compressed chunk when it had one (it kept the old numbers, with only an import warning when the logical offset was not 0). With a script, only data blocks an enabled entry uses go into the archive. Checked by running the converter of main and of this build on the 21 .daimods (plus DAI MM's Patch.daimod): every .fbmod and archive is byte-identical, except sv_plot_flag_ring_tool's description, which now shows its 7 dropdown choices by name ('Action Type _0_Invalid' instead of 'Action Type 0'). A test script on a copy of Cullen Face Retexture (enum with values 5/10/20, a colour, a removal and a chunk switched off, a chunk repointed, a chunk's data replaced) gave the expected entries, and saved settings in both old forms and as names restored the right choices. Setting labels are plain `TextBlock`s inside the `Label`, so WPF no longer reads an underscore as an access key (it hid the `_` and underlined the next letter). ModData is unchanged | `FrostyModSupport/FrostyLegacyScripting.cs`, `FrostyModManager/Windows/ConfigWindow.xaml.cs`, `FrostyModManager/Windows/MainWindow.xaml.cs` |
| All games: the launch process check (`DisableLaunchProcessCheck` off) no longer just refuses when `<ProfileName>.exe` is running. `EndRunningGame` asks Yes/No (unsaved progress lost); on Yes it calls `Process.Kill` on each match, and for any that throw `Win32Exception` (access denied, e.g. a game running as admin) and are still running it starts `taskkill /F /PID ...` with the `runas` verb, so only that step is elevated. It then polls `Process.GetProcessesByName` (needs no handle, so works for elevated processes) every 250 ms for up to 20 s for the same process IDs and only goes on with the launch once they are gone. If any remain, or the UAC prompt was declined (error 1223, no wait), it shows Task Manager steps (Ctrl+Shift+Esc, or Ctrl+Alt+Del > Task Manager, More details, Details tab, `<exe>` > End task, confirm End process, restart if needed) and returns -1. No cancels as before. New `TerminateStuckGame` option (bool, default on, Launch group, after Disable Launch Process Check; listed by Diagnostic Logging): when off it says the game is already running and asks Yes/No to turn the option on (saved to the config) and end it; Yes goes straight to ending it without the second question, No cancels. Each step goes to Diagnostic Logging. Shared code, so the Editor's Launch gets it too | `FrostyModSupport/FrostyModExecutor.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyPlugin/DiagnosticReport.cs` |
| All games: switchable color themes and a restyle of the Mod Manager. `FrostyThemeManager` (FrostyControls) loads `Themes/Colors/<name>.xaml` (Dark, Light, Frosty, DarkFrosty, Original; every file defines the same keys; Original uses the colors of the original `Generic.xaml` with grey in place of an accent) as the last merged dictionary of `Application.Resources`. `Generic.xaml` merges Dark.xaml as the default, so the Editor (which never calls the manager) stays Dark. Mod Manager XAML uses `DynamicResource` for theme keys, so a change applies live. New 'Theme' option (Mod Manager Options, Interface, config key `Theme`, global, default Dark), applied at Mod Manager startup and on Save (`ModManagerOptionsData.ApplyTheme`). New shared styles `AccentButtonStyle`, `GhostButtonStyle`, `GlyphTextStyle` and `FrostyCaptionButtonStyle`; white PNG icons are replaced by Segoe MDL2/Fluent glyphs (`IconFontFamily`) or tinted with an opacity mask, and the Options window is 680 wide (was 600) so its plugin tabs fit. Behaviour, names, handlers and bindings are unchanged. The XAML restyle is not marked line by line | `FrostyControls/FrostyThemeManager.cs`, `FrostyControls/Themes/Colors/*.xaml`, `FrostyControls/Themes/Generic.xaml`, `FrostyControls/FrostyControls.csproj`, `FrostyPlugin/Windows/OptionsWindow.xaml(.cs)`, `FrostyPlugin/Themes/Generic.xaml`, `FrostyPlugin/Themes/Controls/Notification.xaml`, `FrostyPlugin/Windows/*.xaml`, `FrostyPlugin/Controls/FrostyHighlightTextblock.cs`, `FrostyPlugin/Controls/Editors/FrostyReadOnlyEditor.cs`, `FrostyModManager/App.xaml.cs`, `FrostyModManager/Themes/Generic.xaml`, `FrostyModManager/Controls/SplashHeader.xaml`, `FrostyModManager/Windows/*.xaml`, `FrostyModManager/Windows/MainWindow.xaml.cs`, `FrostyModManager/Windows/ConfigWindow.xaml.cs` |

## 1.7.5

Shows as **1.7.5 - 2026.11.26.1**. Release tag: `2026.11.26.1`. DAI ModData version raised to 4 (the fixes below), so DAI ModData is rebuilt once; MEA and Veilguard numbers unchanged.

| Change | Files |
|---|---|
| DAI: a .daimod made with the DAI modding tools replaces a texture in three steps: modify the texture header (res), remove the game's chunk from the bundle, add a chunk with a new id. The original Frosty's .daimod import skipped every `action="remove"` resource (fixed for new imports in 1.0.6.4-dai.1), so mods converted before that (FBMODV3) left the game's chunk in the bundle next to the new one, both with the same `h32` (texture name hash) and a `firstMip` in `chunkMeta`. The unmodded game never has two such chunks in one bundle, and the game then used the wrong one for the bundled range (the always-loaded small mips): rainbow or black noise until the full mips streamed in, and permanently on Low texture quality. The missing removal is now restored by `ReconcileTextureChunks` (next row; it was first done per mod by `RestoreLegacyTextureChunkRemovals`, which it replaces with the same result): where the mod's header names a new chunk and the mod adds that chunk to the bundle, the game's chunk is removed from that bundle. A mod that already has the remove actions gets the same removals as before. Checked on the real mods and game data: HairyNudeMale and HairyNudeMaleJockStrap (converted Dec 2025) each get the one removal their .daimod lists (`532c298d-...` in `basegame_bundle`), re-imported mods (Scarless Cullen, Cullen Face Retexture) keep exactly the removals they had, and in-place mods (Cullen 4k Armor Textures) get none | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI: overlapping texture mods. A texture mod changes the texture header (res) in some bundles and supplies the chunk the header names: .daimods remove the game's chunk and add their own, Frosty-made mods overwrite the game's chunk in place. Only the last mod's header was kept, but every mod's chunk actions were applied, so two .daimods on one texture left both new chunks in each bundle (two chunks with the texture's `h32`, the state behind the rainbow textures above); a losing .daimod's removal deleted the game's chunk that a winning Frosty-made mod's header points at; and a winning .daimod's chunk was missing from bundles only the other mod listed. New `ReconcileTextureChunks` runs for DAI once all mods and handlers are loaded: for each DxTexture header written into a bundle it reads the winning header's chunk id (offset 28) and name hash (offset 108, equal to the chunk meta `h32` in all 146,631 game bundles checked) and the game header's chunk id. If the bundle has a chunk of that texture (the game's, or one a mod adds with that `h32`), it ends up with exactly the one the winning header names: the game's chunk is kept (any removal of it dropped) or removed and the winner's chunk added, and other mods' chunks with that `h32` are dropped. Header-only bundles (26,574 in the base game) stay header-only. Dropped chunks that no bundle uses any more are also taken out of the `chunks` pseudo-bundle, so they are not written to chunks0 or the cas. A winning chunk that a bundle newly gets is added to the `chunks` pseudo-bundle (chunks0) too, also when no mod had set that pseudo-bundle up. Checked by running the mod loader and bundle writer of this build and of 1.7.4's code on the real game data: with the current 27-mod load order both give the same bundle actions, and byte-identical superbundles that match the existing ModData (`arborwilds_main`, `dlcblue_winterpalace`); with Cullen Face Retexture + Scarless Cullen (either order) 165 bundle/texture pairs had two chunks before and 0 now, and the bundle actions equal those of the winning mod alone; the same for HairyNudeMale + HairyNudeMaleJockStrap (1 pair) | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI (binary superbundles: DLC and multiplayer levels, `_o` overrides): mod remove actions for ebx, res and chunks were ignored there. They are now applied before the bundle's modifications, and a removed asset skips the base copy's data blocks (an asset that only exists in the patch delta has none to skip). Checked on `dlc_red_dungeon`: removing a patch-only and a base chunk, res and ebx and a chunk modified by Cullen 4k Armor Textures takes exactly those 7 assets out, and every other asset of the 109 bundles reads back with the same data and chunk meta | `FrostyModSupport/Actions/StandardModExecutor.cs` |
| DAI: a modified ebx or res that the mod doesn't mark inline kept the game's `idata` (the copy stored inside the bundle) next to the new sha1 and size. Chunks already dropped it. The cas bundle writer now removes `idata` from such entries, so the game loads the modded data from the cas. Affected: entries inline in bundles the game still loads from the base game (3,582 non-texture res: occluder meshes, asset banks, animation data, 69 atlas textures, a few text resources), modified by a converted .daimod or old-format .fbmod (they only mark DxTexture inline), or by a new-format mod whose asset entry is not marked inline although that bundle's copy is. DAI Mod Manager drops `idata` from every entry of each bundle it writes. Checked with the mod loader and bundle writer on the real game data: a forced modify of an inline occluder mesh in `mor_crossroads` kept the game's 121-byte `idata` with the new sha1 before this change and has none now; with the current 27-mod load order all 249 superbundles written have the same content as before this change (11 of them differ only in entry order, as two runs of the old code also do; one DLC text superbundle was skipped because the test tool can't run the localization plugin) | `FrostyModSupport/Actions/StandardModExecutor.cs` |
| DAI: ModData version 4, so ModData built by older versions is rebuilt once with the fixes above | `FrostySdk/DataVersions.cs` |
| All games: LaunchPlatformPlugin is built and copied into the Editor's and Mod Manager's `Plugins` folders again; its PostBuild xcopy had been commented out in J-Lyt's base. Its source is back to the 1.0.6.3 version (v1.0.1.0 by t3m1X: Steam fix, 'Keep Platform Launched' option), which J-Lyt's base had replaced with the older v1.0.0.0, with `App.FileSystem` changed to `App.FileSystemManager`: the 1.0.6.3 DLL threw `MissingFieldException` on that field at launch. New `PluginManager.RunPreLaunchActions` / `RunPostLaunchActions` run each plugin's launch action in a try/catch (cancellation still passes through) and log other errors, so an outdated plugin no longer crashes the launch; the Mod Manager and Editor launch code use them. Untested through Steam and Epic: no Steam or Epic copy was available | `Plugins/LaunchPlatformPlugin/`, `FrostyPlugin/Managers/PluginManager.cs`, `FrostyModManager/Windows/MainWindow.xaml.cs`, `FrostyEditor/Windows/MainWindow.xaml.cs` |
| All games: Detailed view for the apply window (`FrostyTaskWindow`). `FrostyTaskLogger` now implements `ILoadingListLogger` (the interface the cache window's superbundle list already uses) and puts the text in a new `Details` row, collapsed when empty; while it has text the window is 600 wide instead of 350 and stays centered as it grows. `FrostyModExecutor.Run` builds the text with `ProgressDetails`: each finished step with its time (the same steps and times as the `... took Xs` log lines, which are unchanged), the current step with a count and elapsed time, and the tasks in flight: one line per running `SuperBundleAction` (DAI, MEA: phase and bundle n of m) or `CasBundleAction` (Veilguard: elapsed time), in start order. The actions only set a few fields (no locks or allocations); the text is built on the waiting thread at most every 100 ms. The "remaining games" wait loop now reports progress through `ReportProgress` (0.1% steps) like the others instead of every millisecond | `FrostyPlugin/Windows/FrostyTaskWindow.xaml(.cs)`, `FrostyPlugin/FrostyTaskLogger.cs`, `FrostyModSupport/FrostyModExecutor.cs`, `FrostyModSupport/Actions/StandardModExecutor.cs`, `FrostyModSupport/Actions/CasModExecutor.cs` |
| All games: the Detailed apply window looks like the cache window. The splash banner (logo, game banner, version, game name) moved from `SplashWindow.xaml` into a shared `SplashHeader` control; `MainWindow` passes one to the new `FrostyTaskWindow.ShowWithHeader` (a new method, so plugins built against the old `Show` still load), which shows it in place of the task name at the splash's width, with the detail lines on the splash's darker background. Simple keeps the old small window | `FrostyModManager/Controls/SplashHeader.xaml(.cs)`, `FrostyModManager/Windows/SplashWindow.xaml(.cs)`, `FrostyModManager/Windows/MainWindow.xaml.cs`, `FrostyPlugin/Windows/FrostyTaskWindow.xaml(.cs)`, `FrostyModManager/FrostyModManager.csproj` |
| Release: `README.md`, `PATCH_NOTES.md` and `QUICK_NOTES.md` from the repository root, and this file as `docs\CHANGELOG.md` (so the links between them still work), are linked into the Mod Manager project as Content (PreserveNewest), so every build's output folder (e.g. `bin\Release\Final`) gets the current copies; before, only a hand-copied QUICK_NOTES.md was there. QUICK_NOTES.md gets the missing 1.7.4 items. README.md: the two Discord badges are removed, and the build steps now say to use Build Solution (with the C++ build tools to install) instead of building single projects | `FrostyModManager/FrostyModManager.csproj`, `QUICK_NOTES.md`, `README.md` |
| All games: 'Progress View' option (Detailed / Simple, config key `ProgressView`, global) on the Mod Manager Options page. One setting covers both the cache window (splash superbundle list) and the apply window; Simple ignores the detail text, so both windows look as in 1.7.4 and earlier | `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyModManager/Windows/SplashWindow.xaml.cs` |
| All games: the General Support and Veilguard Support Discord links (About window, Editor start page) are collapsed while the new `App.ShowDiscordLinks` is false; set it to true to show them again (Veilguard Support still only with Veilguard loaded). The links and the Discord rich presence code are unchanged | `FrostyPlugin/App.cs`, `FrostyPlugin/Windows/AboutWindow.xaml(.cs)`, `FrostyEditor/Windows/MainWindow.xaml(.cs)` |
| DAI: `.daimod` import. DAI Mod Manager only modifies an ebx or res when the game's current copy has the hash in the mod's `originalSha1`; otherwise it logs 'not found' and skips it. Frosty replaced by name without checking. The converter now compares each modified ebx and res with the game's asset list (`DaiPatchCheck.FindChangedFiles`) and lists the ones a later patch changed in the `[DAI]` log line and in a short import note (warning level); the mod is still converted as before. When every file matches and every bundle was found in the game data, the converted mod gets `gameVersion` = the game's head instead of 0, so it no longer shows the 'different game version' warning. Checked by calling the built Mod Manager's check on the game data for the 21 .daimods in the test set: 148 files checked, 1 differs (`systems/plot/plotconfiguration` in InventoryCapacity.daimod, made for patch 5; the patch 12 copy is 10,448 bytes against the mod's 10,192). ModData is unchanged | `FrostyModManager/Windows/MainWindow.xaml.cs` |
| All games: 'Diagnostic Logging' option (Mod Manager Options, Manager, config key `DiagnosticLogging`, off by default). New `FrostySdk.DiagnosticLog`: `Log` adds a `[Diagnostics]` line to the Log tab only while it's on (text passed as a format argument, so braces in paths are safe), `Detail` writes long lists to the file only. While it's on, `FrostyLogger` copies every log line (now appended under a lock, since mods are applied on several threads) to `Logs\Diagnostics_<date>.txt` next to the exe, with the log so far written first; the 10 newest files are kept. New `Frosty.Core.DiagnosticReport`: after the profile loads (or when the option is turned on) it logs Windows version, RAM, drive formats and free space, admin rights, the game exe (size, date, file version), layout.toc head with the Veilguard Steam/EA patch it matches, the SDK file and whether its version matches the head, the key file, data folders, platform and loader files in the game folder, all .dll/.asi/.exe/.ini/.cfg/.txt files there, the cache file (read before loading too: format and the head it was built for), loaded plugins, launch actions, settings and each ModData pack. `FrostyModExecutor.Run` logs the pack, paths and arguments, why ModData is (re)built, each mod (file, game version it was made for, resource counts by type), the totals, the links it creates, one line per `CasBundleAction` superbundle (Veilguard and other cas games: bundles changed, toc/sb and cas files written; changed bundle names in the file) and any superbundle error, the files and links ModData ends up with, CryptBase.dll, and the exact launch (exe and arguments or steam:// URL). `ExecuteProcess` logs the started process or why it couldn't start (it used to swallow the error). `PluginManager` logs each launch action it runs. After a launch a background thread follows every `<game>.exe` that starts (up to 10, until none has run for 90 s, so a copy that Steam or the EA app relaunches is caught too), each on its own thread: process id, start time, command line (WMI), run time and exit code, and for a non-zero exit the matching Application Error, Application Hang or Windows Error Reporting entries from the Windows Application event log. Every call returns at once while the option is off, so output and the normal log are unchanged; no data version change | `FrostySdk/DiagnosticLog.cs`, `FrostyPlugin/DiagnosticReport.cs`, `FrostyPlugin/FrostyLogger.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyPlugin/Managers/PluginManager.cs`, `FrostyModSupport/FrostyModExecutor.cs`, `FrostyModSupport/Actions/CasModExecutor.cs`, `FrostyModManager/App.xaml.cs`, `FrostyModManager/Windows/SplashWindow.xaml.cs`, `FrostyModManager/Windows/MainWindow.xaml.cs` |

## 1.7.4

Shows as **1.7.4 - 2026.11.25.1**. Release tag: `2026.11.25.1`. MEA ModData version raised to 2 (localization write changes), so MEA ModData is rebuilt once; DAI and Veilguard numbers unchanged.

| Change | Files |
|---|---|
| All games (old-format mods: converted .daimods, FBMODV1-3): `ProcessLegacyModResources` read every resource on one thread, opening the `.archive` (or game cas file) again for each one, and hashed it there. `ReadLegacyResourceData` now reads and hashes a mod's resources first on `WorkerPool`, each worker keeping its files open; the loop that decides which mod wins still runs in mod order, so the result is the same. A missing archive still fails at that asset | `FrostyModSupport/FrostyModExecutor.cs` |
| All games: `Utils.GenerateSha1` used `SHA1Managed`; it now uses `SHA1CryptoServiceProvider` (one per thread), which gives the same hash several times faster | `FrostySdk/Utils.cs` |
| DAI and MEA: when a superbundle had a modded bundle, every delta bundle in it was read with `DbReader` and written again with `DbWriter`, even the untouched ones. Untouched bundles are now copied byte for byte from the game's sb (same range the asset loader reads). A DAI ModData built with 1 CPU thread was byte for byte identical to one built by the code before these changes (236 files, 2.1 GB, same symlinks) | `FrostyModSupport/Actions/StandardModExecutor.cs` |
| DAI and MEA: superbundle jobs start largest .sb first, so a big one is not left running alone at the end. Results are still collected in the original order, so the cas files are written in the same order | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI (binary superbundles, e.g. the DLC levels): for each modded delta bundle, every base ebx, res and chunk scanned the whole patch list for its match, and every patch asset scanned the result list (`FindIndex`), so a big level bundle cost the square of its asset count; `dlc_red_dungeon` ran alone for about 35 s at the end of a test apply. Lookup tables now give the same first match | `FrostyModSupport/Actions/StandardModExecutor.cs` |
| All games: the mod list check before launch (`GenerateModInfoList`) and the mod list used for applying built `FrostyMod` objects that read the icon and 4 screenshots. New `FrostyMod(path, loadImages)` overload skips them; the old constructor is unchanged | `FrostyPlugin/Mod/FrostyMod.cs`, `FrostyModSupport/FrostyModExecutor.cs` |
| All games: `ReportProgress` now only updates the window when the bar moves by 0.1% (it was called for every modified asset in the handler step) | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI (binary superbundles, e.g. the DLC levels; any other game that has them too): each modded delta bundle recompressed its changed and new assets (zlib level 9) one after another on one thread. They are now compressed first by the new `Utils.CompressFiles`, which splits every asset into its 64 KB blocks and compresses all blocks of the bundle on `WorkerPool` (one big asset no longer runs on one thread), then writes them in the same order, so the output is the same | `FrostySdk/Utils.cs`, `FrostyModSupport/Actions/StandardModExecutor.cs` |
| All games: `WorkerPool.For` helpers now run before queued jobs that haven't started. They used to wait behind every queued superbundle, so a `For` inside a superbundle job ran on one thread until the queue was empty | `FrostySdk/WorkerPool.cs` |
| All games: each apply step's time (loading the cache, loading mods, handlers, cleanup, applying mods, writing archive data) is written to the log, plus the 3 slowest superbundles (with time spent unpacking binary bundles, and how many MB were compressed in how long) | `FrostyModSupport/FrostyModExecutor.cs`, `FrostyModSupport/Actions/StandardModExecutor.cs` |
| DAI: DAI Mod Manager writes its merged patch's `package.mft` as `Version` = `Update\Patch` version + 1, and saves store that number (FBHEADER key `0x0346EAF1`, as text), so the game refused them at the real patch. ModData's `Update\Patch\package.mft` is no longer copied for DAI; `DaiPatchVersion.WriteModDataManifest` writes it on every launch with `Version` = the chosen patch (never below the real one), only when the content changed, so a new choice needs no ModData rebuild. At launch the Mod Manager reads the version from each save in `Documents\BioWare\Dragon Age Inquisition\Save` and, if one needs more than the reported patch, asks on every launch while the 'ask' checkbox is ticked (No keeps the current patch). New DAI-only Options page with the drop-down (`DaiPatchVersion`, 0 = real patch; real +1 to +3 and every higher save version listed) and the 'ask' checkbox (`DaiPatchVersionAsk`); lowering it asks for confirmation. The pop-up warns that new saves then only load through Frosty or DAI Mod Manager, asks the player to back up their saves, and Yes opens the save folder. ModData version not raised: ModData's content is unchanged | `FrostyPlugin/DaiPatchVersion.cs`, `FrostyPlugin/Windows/OptionsWindow.xaml.cs`, `FrostyModManager/Windows/MainWindow.xaml.cs`, `FrostyModSupport/FrostyModExecutor.cs` |
| MEA: the BiowareLocalizationPlugin 1.1.1.2 write changes (text id order, 4-byte padding, shared identical texts, LF, recalculated node offset) now apply to MEA too. `ResourceUtils.IsDai` is replaced by `ResourceUtils.IsVeilguard`: only Veilguard keeps CRLF text and plain id sorting in the editor, and its write path is unchanged | `Plugins/BiowareLocalizationPlugin/` |
| MEA: ModData version 2, because the localization change alters what Frosty writes for MEA text mods. DAI (ModData speedup verified byte for byte, patch version written at every launch) and Veilguard stay at 1 | `FrostySdk/DataVersions.cs` |

## 1.7.3

Shows as **1.7.3 - 2026.11.24.3**. Release tag: `2026.11.24.3`. First release with per-game data numbers (MEA cache revision 1, ModData version 1 for every game), so nothing needs deleting by hand.

| Change | Files |
|---|---|
| All games: new `DataVersions` holds a cache revision and a ModData version per game. The cache header stores `CacheVersion` + the game's revision, so a mismatch rebuilds only that game's cache (no format change). ModData gets `moddata_version.txt` next to `mods.json`; a missing or different number rebuilds that pack on its next launch. `mods.json` keeps its old format, so older builds still read it. Raise a game's number in any release that changes its cache or mod-apply output | `FrostySdk/DataVersions.cs`, `FrostySdk/FrostySdk.csproj`, `FrostySdk/Managers/AssetManager.cs`, `FrostyModSupport/FrostyModExecutor.cs` |
| MEA: cache revision 1 (header version 5), so caches built before the 1.7.1 chunks0 superbundle index fix are rebuilt. DAI and Veilguard caches stay at 4. ModData version 1 for every game, so existing ModData (no version file) is rebuilt once | `FrostySdk/DataVersions.cs` |
| DAI and MEA: `ProcessLegacyModResources` left the whole mod (`return`) when an old-format mod replaced an ebx, res or chunk that an earlier mod had changed through a handler, so its data was not in the archive list. 1.0.6.3 and 1.0.6.4 skipped only that asset (`continue`); restored. Veilguard has no old-format mods | `FrostyModSupport/FrostyModExecutor.cs` |
| DAI and MEA: `LaunchGame` uses the `steam://run` link whenever `steam_appid.txt` is in the game folder, as 1.0.6.3 did. The 1.0.7 base only did that with the 'Use Steam protocol' option, which is off by default. Not applied when DatapathFix is enabled, because it handles the launch itself. Other games are unchanged. An empty `steam_appid.txt` now falls back to starting the exe instead of failing. Untested: no Steam copy was available | `FrostyModSupport/FrostyModExecutor.cs` |
| All games: DatapathFix (J-Lyt's submodule, unchanged) reads and writes `DatapathFixEnabled` and `DatapathFixFirstLaunch` in the global config, so one answer applied to every game. `Config` now redirects those two keys to the loaded game's options. A game with no value of its own falls back to the old global one, so existing setups are unchanged until a game is changed. Debug Mode stays global. Untested: no Steam or Epic copy | `FrostyPlugin/Config.cs` |
| The DAVExtender check read `d3d11.dll exists \|\| (DAVE.asi exists && Veilguard)`, so any game with a `d3d11.dll` (ReShade) got the Dex menu and tab. The Veilguard test now covers both files | `FrostyModManager/Windows/MainWindow.xaml.cs` |
| All games: the friendly incompatible-mod message only caught `AggregateException`, but `WorkerPool.ForEach` rethrows the original `NullEntryException` unwrapped, so the message never showed. Both forms are caught now | `FrostyModSupport/FrostyModExecutor.cs` |
| All games: `HasDexResource` compared with `""` only, and an old `.fbcollection` has no Dex field (null), so it counted as having one. Null and empty are treated the same now | `FrostyPlugin/Mod/FrostyModDetails.cs` |
| DAI: BiowareLocalizationPlugin 1.1.1.2 (KrrKs, upstream PR #348) merged onto the 1.7 plugin, Veilguard support kept. Primary and adjective text ids are written in `TextID` order (variant ids such as 0x8... female lines right after their base id, as in vanilla), the text data is padded to 4 bytes, identical texts are stored once, CRLF becomes LF and the node offset is recalculated. These write changes are behind `ResourceUtils.IsDai`, so MEA and Anthem write as in 1.1.1.1. The unknown header blocks are now read as the crafting name mappings (same bytes written back). New options PrintVerificationTexts and ReuseOriginalResourceTextBits (off). Mod format unchanged. ModData version not raised: the unreleased per-game data versions already rebuild ModData once | `Plugins/BiowareLocalizationPlugin/` |
| Veilguard: the profile SDK is checked against the game's `head`, and the bundled `DragonAgeTheVeilguardSDK.dll` is from the Steam build (2373991). The EA app build of the same patch is 3377309 and has a few platform-only types (Origin instead of Steam presence backends; the rest matches), so EA installs always regenerated it. `FileSystemManager.Initialize` passes the head to `ProfilesLibrary.SetGameHead`; for a known EA head `SDKFilename` becomes `DragonAgeTheVeilguardSDK_EA`, and an SDK generated from the EA app build (3377309) ships under that name. Steam installs and other games are unchanged | `FrostySdk/ProfilesLibrary.cs`, `FrostySdk/Managers/FileSystemManager.cs`, `FrostySdk/FrostySdk.csproj`, `FrostySdk/Profiles/DragonAgeTheVeilguardSDK_EA.dll` |
| All games (shared SDK generator): `CreateSDK`/`ProcessClass` did linear searches per class and field (`m_values.Find`/`FindIndex` by name, `outList` scan, `List<EbxClass>.Contains`, which compares structs by reflection), so the 'Creating SDK' step grew with the square of the type count (about 15 min for Veilguard's ~24k types). They are now dictionaries/hash sets that return the same first match, and `CreateClassObject` uses a `HashSet` too. In a test harness with old and new code side by side on generated type lists, the SDK source written was byte-identical. `MemoryReader` reads a 4 KB page per `ReadProcessMemory` and serves values from it (falls back to the old single read when a value crosses a page or the page can't be read). The SDK window shows each step's time | `FrostyPlugin/Sdk/ClassesSdkCreator.cs`, `FrostyPlugin/IO/MemoryReader.cs`, `FrostyPlugin/Windows/SdkUpdateWindow.xaml.cs` |
| DAI: the archive branch of `InstallMods` only collected `.fbmod`, `.fbcollection`, `.fbpack` and `.archive` entries, and the `.daimod` converter was only reached for a file picked on its own, so an archive with only .daimods gave 'Archive contains no installable mods'. `.daimod` entries are now extracted with `SharpSevenZipDecompressor.ExtractToFile` (streamed to disk, matched by full path) into a temp folder and passed to `DaiImportMod`, which keeps its own copy in `DAI originals` for Reconfigure; the temp folder is deleted afterwards. Same-named .daimods in different folders get the folder added to the file name so they don't replace each other, and a .daimod is skipped when the archive has an .fbmod with the same name. zip, 7z and rar (incl. RAR5) already open through SharpSevenZip and the bundled 7z.dll, so no new dependency | `FrostyModManager/Windows/MainWindow.xaml.cs`, `FrostyModManager/Compression/LZMA.cs`, `FrostyModManager/Compression/IDecompressor.cs` |
| DAI: an exception from `DaiImportMod` (e.g. a .daimod whose XML doesn't parse) escaped the install task, and the app's unhandled-exception handler closes the Mod Manager. It is now caught, logged and listed in the install errors. A .daimod is only converted when Dragon Age: Inquisition is loaded; other games get 'Mod was not designed for this game.' | `FrostyModManager/Windows/MainWindow.xaml.cs` |

## 1.7.2

Shows as **1.7.2 - 2026.11.24.2**. Release tag: `2026.11.24.2`. DAI users delete ModData once.

| Change | Files |
|---|---|
| DAI: chunks in the `chunks` pseudo-bundle's add list were appended to every superbundle whose name contains "chunks": `win32/chunks0` (one cas copy) and the 8 binary DLC `chunkgroups/chunksNN` superbundles, which each got the full chunk data inline (1.0.6.4 did the same). The chunkgroups now only get added chunks that the asset manager already knows, i.e. that replace a game chunk. Those copies are needed: a replaced chunk keeps its vanilla entry in base `Data\Win32\chunks0.toc`, and with only the plain patch `chunks0` entry the game streamed the vanilla mips under the modded texture header (Cullen 4k Armor Textures showed sparkles in a test that left them out). Brand-new chunks (4k Map textures, Scarless Cullen, Creation Magic Expanded) are in no vanilla toc and only go into `win32/chunks0` | `FrostyModSupport/Actions/StandardModExecutor.cs` |

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
