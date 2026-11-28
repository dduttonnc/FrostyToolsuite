# Frosty Toolsuite patch notes

These notes cover three Frosty programs, each with its own version numbers. Newest first:

1. [Frosty by dduttonnc](#1-frosty-by-dduttonnc): v1.7.6, v1.7.5, v1.7.4, v1.7.3, v1.7.2, v1.7.1, v1.7 and v1.0.6.4-dai.1
2. [J-Lyt's Frosty](#2-j-lyts-frosty): v1.0.7 builds
3. [Original Frosty Toolsuite](#3-original-frosty-toolsuite): v1.0.0 to v1.0.6.4

Frosty by dduttonnc is built on J-Lyt's Frosty, which is built on the original Frosty Toolsuite. Detailed notes for section 1 are in [docs/CHANGELOG.md](docs/CHANGELOG.md), and a quick summary is in [QUICK_NOTES.md](QUICK_NOTES.md).

# 1. Frosty by dduttonnc

One Frosty Mod Manager for Dragon Age: Inquisition, Dragon Age: The Veilguard and Mass Effect: Andromeda. https://github.com/dduttonnc/FrostyToolsuite

Not released yet
--------
Improvements:
- Dead Space (2023): the Mod Manager no longer shows the "hasn't been tested with this version of Frosty" message when you pick the Dead Space profile, and Dead Space is listed with the tested games

Bug Fixes:
- Dead Space (2023): Frosty no longer asks to rebuild the Dead Space SDK. The SDK that came with Frosty was made for an early 2023 version of the game, so Frosty saw a different game version and asked for a rebuild, and every Frosty update put the old SDK back. It now comes with an SDK for the current game version (made from the Steam copy; EA app copies are untested)
- Dead Space (2023): applying mods now writes them into ModData. Before, Frosty treated Dead Space like a game with a Patch folder, which it doesn't have, so it built ModData with links to the game's own files only and none of the mods' changes (the mods showed as applied but the game was unchanged). Changed files now go into ModData's Data folder, the way Need for Speed Unbound and Veilguard work, and new archive files are numbered after the highest one the game has. Frosty rebuilds Dead Space ModData once. Based on how HarGabt's Frosty 1.0.7 fork (github.com/HarGabt/FrostyToolsuite) handles Dead Space.
- Dead Space (2023): mods now load in game. The game never reads its command line, so it ignored -dataPath and always loaded its normal Data folder. It does load the Data folder next to the exe it was started from, so Frosty now puts hard links to the game's exe and DLLs into ModData (no extra disk space, renewed on every launch) and starts the game from there. No loader DLL is added to the game folder, and starting the game from Steam as usual stays unmodded. Tested with Steam. EA app copies are untested
- All games: if ModData was made by a build that laid it out differently and the folder where changed files go is a link to the game's own folder, ModData is now rebuilt from scratch, removing only the links. Before, cleaning it up followed the link and deleted the game's archive files (seen with Dead Space ModData from 1.7.6, whose Data folder was a link to the game's Data). Cleanup also no longer goes into any linked folder

v1.7.6 - 2026.11.27.1
--------
No need to delete Caches or ModData after updating: nothing Frosty writes for the games changed in this version. Dragon Age: Inquisition: if DAI Mod Manager's Patch.daimod was ever added as a mod (it shows as 'Official Patch' by Bioware), remove it from the mod list and apply mods again; Frosty doesn't need it and it gives thousands of game files the wrong data. Full details: docs/CHANGELOG.md

Improvements:
- All games: the ModData Build Log (formerly Print Log) is saved in the Logs folder next to the exe as ModDataBuild_ with the date and time, one file per ModData build, instead of log.txt in the Frosty folder (overwritten each time). Automatic Log Cleanup deletes old ones too. Each file starts with the game and mod pack it was built for. A log.txt left in the Frosty folder by an older version can be deleted by hand
- All games: the Mod Manager Options page is sorted into clearer groups, in this order: Launch (Launch Game Using Steam Protocol, Disable Shader Compilation At Launch, Disable Launch Process Check, Command Line Arguments), Mods & ModData (Enable Custom Mods Directory, Custom Mods Directory, CAS Max File Size, Alternate SymLink Method), Logging (ModData Build Log, Diagnostic Logging, Automatic Log Cleanup, Delete Logs Older Than), Interface (Theme, Remember Profile Choice, Progress View), Performance (CPU Priority, CPU Threads) and Updates (Check for Updates). Before, most settings were mixed together under General and Manager in no particular order. Within each group a switch now comes before the setting it controls. 'Print Log' is renamed 'ModData Build Log' with a clearer description (each time ModData is built it lists what the mods added or changed, for mod authors). Every setting keeps its saved value. The Editor's Options page is unchanged apart from the Print Log rename
- All games: new 'Automatic Log Cleanup' option (Options, Mod Manager Options, Logging), off by default, with a 'Delete Logs Older Than' choice of 14, 30 (the default), 60 or 90 days. When it's on, Diagnostic Logging and ModData Build Log files in the Logs folder older than that are deleted each time the Mod Manager starts and when Options are saved. Only those Frosty files are touched, and the Diagnostic Logging setting itself is never changed, so it can stay on as long as wanted. Diagnostic Logging no longer deletes files on its own (it kept the last 10); with the cleanup off, every file is kept
- All games: the separate 'Applying Thread Count' option (Options, Mod Manager Options, General) is gone. Applying mods follows the Performance options, CPU Threads and CPU Priority, the same as building the cache. Dragon Age: Inquisition, Mass Effect: Andromeda and Veilguard already applied mods with those options: the old setting only capped Windows' shared thread pool, which can't be set below one thread per CPU core, so it had no real effect there. A value saved by an older version is ignored and removed the next time Options are saved
- All games: when the game is still running in the background (for example its window closed but it never fully shut down), launching no longer just refuses with 'Unable to launch process'. Frosty asks whether to end it (anything not saved in that game is lost) and on Yes ends it, asking for admin rights through the usual Windows prompt only if Windows won't let it end the game otherwise (Frosty itself keeps running as before). The game is only launched once the old one has fully closed. If it hasn't closed after about 20 seconds, or the admin prompt is declined, Frosty explains how to end it in Task Manager. Answering No cancels the launch as before. The new 'Terminate Stuck Game' option (Options, Mod Manager Options, Launch), on by default, turns this on or off; when off, Frosty says the game is already running and asks whether to turn the option on and end the game (No cancels the launch). 'Disable Launch Process Check' still turns the whole check off. The Editor's Launch button does the same
- All games: the Mod Manager has a new look, with five color themes to choose from in Options, Mod Manager Options, Interface, Theme: Dark (the default), Light, Frosty (snow white and light blues, with snowflakes in the title bar), Dark Frosty (midnight blue with ice blue snowflakes) and Original (the classic Frosty greys and white text, with the new shapes and icons). The new theme shows as soon as the options are saved. Buttons, lists, menus, tabs, check boxes and scroll bars are restyled, the icons are crisp symbols that suit every theme, the main actions (Launch, Apply Mod(s), Save) stand out, and mods without an icon show a round 'NO ICON' placeholder. How mods are added, applied and launched is unchanged, and every setting keeps its saved value. The Editor uses the new Dark look and has no theme choice

Bug Fixes:
- All games: the Installation Report window (shown after adding mods with errors or notes) wraps long messages onto several lines instead of cutting them off at the window's edge, and is a little larger
- All games: the Launch Platform plugin no longer runs when the DatapathFix plugin is enabled for the same game, so the two no longer break each other's launch (Launch Platform closed the EA app and Steam while DatapathFix was still relaunching the game through them). When both are on, DatapathFix wins and Launch Platform skips its restart of the launchers; the log says Launch Platform was skipped. DatapathFix only tried to turn Platform Launching off when its own options were saved, and the Launch Options page turned it straight back on. Untested: this only matters for Steam and Epic copies, and none was available
- All games: the Launch Platform plugin no longer closes Steam and the EA app a few seconds after the game starts. A game that goes through the EA app can first run a short-lived copy that hands the launch over to the EA app and closes, and the EA app then starts the game again (seen with Mass Effect: Andromeda; a Steam copy of Dragon Age: Inquisition, which needs the EA app, is expected to do the same). Launch Platform took that first copy closing for the game closing, so it closed the launchers while the game was still starting. That is most likely why the Steam overlay was missing: with 'Keep Platform Launched' on, Steam was opened again, but the game was already starting without it. Launch Platform now closes them only after 3 minutes with no copy of the game running, which gives the EA app time to start and start the game again, or after about 10 seconds once the game has been running for 30 seconds. Tested with the EA app. Untested with Steam and Epic: no copy was available to test with
- Dragon Age: Inquisition and Mass Effect: Andromeda: a Steam copy with steam_appid.txt in its game folder no longer gets DatapathFix's first-launch question, and DatapathFix stays off for it. The question says Steam copies need DatapathFix, but since 1.7.3 Frosty launches these copies through Steam, which loads mods without it. With DatapathFix on, the game was started outside Steam, so the Steam overlay, screenshots and achievements were missing (a player confirmed mods and the overlay work with it off). The log says DatapathFix was left off. Anyone who already turned it on can turn it off in Options, DatapathFix Options. Epic copies, and Steam copies without steam_appid.txt, still get the question. Untested: no Steam copy was available to test with
- Dragon Age: Inquisition: DAI Mod Manager's own Patch.daimod (the copy of the official game patch it makes in its folder) is no longer installed as a mod. Adding it now shows a short note that Frosty doesn't need it. Before, it became an 'Official Patch' mod that would have given about 14,000 game files the wrong data. A file inside a .daimod that is stored as a patch delta (only DAI Mod Manager's patch copy has these) is now skipped with a note instead of installed with the wrong data
- Dragon Age: Inquisition: .daimods with a settings window now run their settings the way DAI Mod Manager does. A setting that turns part of the mod off now turns off the right part, including a step that removes something from the game (with removal steps in the mod it used to turn off a different file, and a removal could never be turned off). Mods that use DAI Mod Manager's colour settings, or pick between their own files by name or by swapping one file's data for another's, no longer fail to import. Dropdown settings give the mod the position of the choice again, as DAI Mod Manager and the original Frosty did (1.0.6.4-dai.1 to 1.7.5 gave a different number when the choices have their own numbers), and the mod's description shows the choice by name. When a setting replaces texture data, the texture's size information now comes from the new data. Settings saved by older versions carry over to Reconfigure. Of the 21 .daimods it was checked with, three have settings and all three give the same result as before (one now shows its dropdown choices by name in its description). Setting names with an underscore in them now show it; it was hidden and the letter after it underlined

v1.7.5 - 2026.11.26.1
--------
Dragon Age: Inquisition: Frosty rebuilds each mod pack's ModData once on its next launch, so the fixes below are applied. Nothing needs deleting by hand, and no mod needs importing again. Full details: docs/CHANGELOG.md

Improvements:
- All games: the Discord support links in the About window and on the Editor's start page are hidden for now. They lead to the Frosty team's and the Veilguard modding community's servers, and this version is not supported there. These will eventually return pointing to the original Frosty discord as well as my discord for this version.
- All games: the window shown while mods are applied has a new Detailed view that looks like the cache window, with the same Frosty logo, game banner and version. It lists each step with how long it took, and while mods are applied, every superbundle being worked on (one per CPU thread) with how far along it is (Veilguard: how long it has been running). The new 'Progress View' option (Options, Mod Manager Options, Manager) switches both the cache window and this window between Detailed (the default) and Simple, the one-line window from before. Applying mods is not slowed down (the window is updated at most ten times a second), and the step times in the log are unchanged. The Editor's Launch window shows the same lines, always in the Detailed view and without the banner
- All games: the download now always includes README.md, PATCH_NOTES.md, QUICK_NOTES.md and docs\CHANGELOG.md (every change in full detail), and QUICK_NOTES.md is caught up with 1.7.4
- Dragon Age: Inquisition: importing a .daimod now checks every game file it changes against your game. A converted mod whose files all match no longer shows the 'Mod was designed for a different game version' warning (mods imported before keep it until they are imported again or reconfigured). If a game patch updated one of those files after the mod was made, the import shows a short note naming it: the mod is installed and works as usual and simply uses its own copy of that file, which is usually harmless (DAI Mod Manager skipped such a file instead). Most mods are made for older patches and still match: of 21 .daimods made for patches 5 to 12, only one file in one mod (Inventory Capacity) differs from the current game
- All games: new 'Diagnostic Logging' option (Options, Mod Manager Options, Manager), off by default, to help find out why mods or the game misbehave on someone else's PC. When it's on, the Log tab also shows: Windows version, RAM and drives; the game's folder, exe, version and platform (e.g. Veilguard Steam, or EA app/Epic); the SDK and cache and whether they match the game; platform and loader files in the game folder (e.g. steam_appid.txt, CryptBase.dll, d3d11.dll); the loaded plugins and settings; each applied mod with the game version it was made for and what it contains; why ModData is rebuilt; what is written to ModData (each superbundle for Veilguard; the full file list is in the saved file only); how the game is started and with which arguments; and when the game exits, its exit code and any crash report Windows recorded for it. The whole log is also saved to a file in the Logs folder next to FrostyModManager.exe (the last 10 are kept), so it can be sent in. The file has folder paths, which can include the Windows user name, but no keys or passwords. Nothing changes while the option is off

Bug Fixes:
- Dragon Age: Inquisition: texture mods made for DAI Mod Manager (.daimod) that were converted with the original Frosty (1.0.6.4 or older) no longer show rainbow or black noise while an area loads, when zooming, or all the time on Low texture quality (e.g. HairyNudeMale). These mods swap a texture's data for new data: remove the old, add the new. The original Frosty's converter skipped every 'remove' step, so the game's old data stayed in the bundle next to the mod's and the game read the wrong one for the small, always-loaded versions of the texture. Frosty now restores the missing step when it applies such a mod. Mods imported with 1.0.6.4-dai.1 or newer already had it, and mods that overwrite the game's data in place (e.g. Cullen 4k Armor Textures) were not affected
- Dragon Age: Inquisition: two texture mods that change the same texture (e.g. Cullen Face Retexture and Scarless Cullen, or HairyNudeMale and HairyNudeMaleJockStrap) no longer leave both mods' texture data in the game's files, which could show rainbow or black noise while an area loads, when zooming, or all the time on Low texture quality. The mod that wins in the load order now decides, as if it were the only one installed, and the losing mod's texture data is no longer written to ModData. This also covers a .daimod texture mod together with a Frosty texture mod for the same texture
- Dragon Age: Inquisition: mods that remove something from a DLC or multiplayer area (Jaws of Hakkon, The Descent, Trespasser, Black Emporium, the multiplayer maps) now have it removed; Frosty used to skip removals there, so a texture swap in those areas could leave the game's old texture data next to the mod's
- Dragon Age: Inquisition: a mod that changes a small file the game keeps inside its bundle files (e.g. collision meshes for line of sight, sound banks, animation data, some effect textures) now takes effect. Frosty wrote the mod's version but left the game's own copy in the bundle, which the game could keep using. It mostly affects converted .daimods and old-format .fbmods; none of the mods it was checked with were affected
- All games: the Launch Platform plugin is included again (Frosty 1.0.6.3 had it, but this version's builds left it out). Its 'Launch Options' page in Options can start the game through the EA app, Steam or Epic with mods; it is off unless turned on, and settings from an older Frosty carry over. A Launch Platform DLL copied over from Frosty 1.0.6.3 or older closed this version's Mod Manager when a game was launched; the bundled one is the same plugin, updated for this version. Also, a plugin that fails while a game is launched no longer closes the Mod Manager or the Editor: the error is written to the log and the launch goes on. Launching through Steam or Epic with the plugin is untested: no Steam or Epic copy was available to test with

v1.7.4 - 2026.11.25.1
--------
Mass Effect: Andromeda: Frosty rebuilds each mod pack's ModData once on its next launch, so text mods get the new localization fix. Nothing needs deleting by hand. Full details: docs/CHANGELOG.md

Improvements:
- Dragon Age: Inquisition and Mass Effect: Andromeda: applying mods (building ModData) is faster, most of all with many mods. Old-format mods (converted .daimods and old .fbmods) are now read and checked on all CPU threads instead of one, parts of the game files that no mod changes are copied as they are instead of being taken apart and put back together, the biggest parts start first, changed bundles in DLC levels are matched up with the game's originals much faster, and their changed data is compressed on all CPU threads instead of one.
- All games: launching with an unchanged mod list no longer reads every mod's icon and screenshots, and the progress bar is updated less often while mods are applied
- All games: the log shows how long each step of applying mods took, and the 3 slowest superbundles

Bug Fixes:
- Dragon Age: Inquisition: saves made with DAI Mod Manager can be loaded. DAI Mod Manager's merged patch tells the game it is one patch newer (e.g. 13 instead of 12), saves record that number, and through Frosty the game refused them with 'This save data was created with a newer version of the game'. When some saves need a higher patch, Frosty now asks at launch whether to report that patch to the game. The new 'Dragon Age Inquisition' page in Options shows the patch Frosty reports (the game's real patch by default, the next three, and any patch your saves need) and can turn the question off. Warning: saves made while a higher patch is reported only load when the game is launched through Frosty (or DAI Mod Manager), not when it is started normally. Back up your saves before saying Yes (the pop-up opens the save folder); Frosty only reads them and never changes them. Nothing changes when all saves match the game's patch
- Mass Effect: Andromeda: the bundled Bioware Localization plugin now uses the 1.1.1.2 changes for Mass Effect: Andromeda too (1.7.3 only used them for Dragon Age: Inquisition), so texts changed by mods are stored the way the game expects. Only checked briefly, in Russian

v1.7.3 - 2026.11.24.3
--------
No need to delete Caches or ModData after updating: Frosty rebuilds each mod pack's ModData once on its next launch, and a Mass Effect: Andromeda cache from before 1.7.1 at the next start. Deleting them by hand is still fine. Full details: docs/CHANGELOG.md

Improvements:
- All games: after a Frosty update, the cache and ModData are rebuilt automatically when the update changes what Frosty writes for that game, so they no longer need to be deleted by hand. Each game has its own data number, so an update for one game does not rebuild the others. This update rebuilds each mod pack's ModData once, on its next launch
- Bioware Localization plugin: two new options on the 'Bioware Localization Options' page, 'Log verification texts' (debug logging) and 'Reuse original resource's text bits' (faster writing). Both are off by default
- Dragon Age: The Veilguard: the EA app version no longer has to generate a new SDK (with the game running) the first time Frosty opens it. Frosty only shipped the SDK made from the Steam version, and the EA app version of the same patch has a different version number and a few platform-only types, so it always asked for a new one. Frosty now ships both and loads the one that matches the installed game
- All games: generating a new SDK (needed after a game patch) is much faster. The 'Creating SDK' step searched long lists for every type and field, which took about 15 minutes for Dragon Age: The Veilguard; it now uses lookup tables, and the game's type data is read from memory a page at a time instead of a few bytes at a time. The SDK it makes is unchanged. The SDK window now shows how long each step took
- Dragon Age: Inquisition: DAI Mod Manager mods (.daimod) can be installed straight from the downloaded .zip, .7z or .rar, without unpacking it first (Add Mod or drag and drop). Every .daimod in the archive is converted as if it had been picked on its own, so configurable mods still show their settings window and Reconfigure... still works. If the archive also has a Frosty version (.fbmod) of the same mod, only the Frosty version is installed

Bug Fixes:
- Dragon Age: Inquisition: the bundled Bioware Localization plugin is updated to 1.1.1.2 (by KrrKs), which text mods made for 1.1.1.2 need, e.g. Fixes of Russian localization. Texts changed by mods no longer go missing in game, mostly in non-English versions: female variants of a line are now stored right after the male line and the text data is padded, as the game expects. Mass Effect: Andromeda keeps the 1.1.1.1 behaviour, and Veilguard support is unchanged
- Mass Effect: Andromeda: a cache built before 1.7.1 is now rebuilt automatically at the next start, so texture mods work without deleting Caches\masseffect.cache by hand
- Dragon Age: Inquisition and Mass Effect: Andromeda: an old-format mod (old .fbmod or converted .daimod) is no longer cut short while loading. When it replaced an asset that an earlier mod had already changed through a plugin (e.g. text edited with the Bioware Localization plugin), the rest of that mod was skipped; only that one asset is skipped now, as in 1.0.6.3
- Dragon Age: Inquisition and Mass Effect: Andromeda: Steam copies launch through Steam again without ticking 'Use Steam protocol' in Options, as in 1.0.6.3, so mods load. Nothing changes when the DatapathFix plugin is enabled, and nothing changes for Dragon Age: The Veilguard. Untested: no Steam copy was available to test with
- All games: the DatapathFix plugin's Enabled and First Launch settings are now kept per game. Its first-launch question is asked once for each game, and turning it on or off for one game no longer changes the others. Existing settings carry over until they are changed for a game. Untested: no Steam or Epic copy was available to test with
- Dragon Age: Inquisition and Mass Effect: Andromeda: the DAVExtender(Dex) menu entry, the empty Dex Mods tab and the 'DAVExtender(Dex) is installed' log line no longer appear when d3d11.dll is in the game folder (e.g. ReShade). They are for Dragon Age: The Veilguard only
- All games: a mod that refers to an asset the game does not have shows the '"X" is incompatible with the installed version of the game' message again and the launch is cancelled cleanly. Since the mod loading speedup the message was skipped and a general error box appeared instead
- All games: mod collections (.fbcollection) made before Veilguard support no longer show the 'Has DAVExtender(Dex) Archive' icon, and their mods are no longer scanned for a Dex archive at launch
- Dragon Age: Inquisition: a .daimod that Frosty can't read is now listed in the install errors instead of closing the Mod Manager
- Other games: a .daimod is now refused with 'Mod was not designed for this game.' instead of being converted for the wrong game

v1.7.2 - 2026.11.24.2
--------
Dragon Age: Inquisition users: delete the game's ModData folder once after updating, so it is rebuilt at the smaller size. Full details: docs/CHANGELOG.md

Improvements:
- Dragon Age: Inquisition: ModData is much smaller and the main menu and saves load faster with mods that add new chunks (e.g. 4k Map textures). Brand-new chunks now go only into the global chunk list (chunks0), as DAI Mod Manager did; each of the 8 DLC chunk files used to get its own full copy of every one (about 5 GB of a 7.5 GB ModData in one 28-mod setup). Chunks that replace one of the game's own chunks still get their copy in the DLC chunk files, which the game needs

v1.7.1 - 2026.11.24.1
--------
Dragon Age: Inquisition users: delete the game's ModData folder once after updating. Mass Effect: Andromeda users: delete the MEA cache (Caches\masseffect.cache) once. Full details: docs/CHANGELOG.md

Improvements:
- Mass Effect: Andromeda is now a supported game: selecting it no longer shows the 'not supported' message
- Other games are no longer blocked or called 'not supported': they show an 'untested with this version' note and can be opened anyway (the profile picker used to refuse them)

Bug Fixes:
- Mass Effect: Andromeda: fixed texture mods leaving the streamed full-resolution copy of a texture unchanged, so save loads and area transitions hung (e.g. Beard Styles). Toc chunks were recorded under the previous superbundle, so chunks0 was never updated. The MEA cache must be rebuilt once
- Mass Effect: Andromeda: old-format texture mods are now split at the texture's first streamed mip, as Frosty's texture importer does, instead of putting the whole texture into the bundle
- Dragon Age: Inquisition: old-format mods that replace existing textures (e.g. Cullen 4k Armor Textures) no longer show sparkles, pixelated or wrongly colored textures. 1.7 ignored the mod's own chunk instructions and marked the replaced textures as patch deltas in the game's chunk list; chunks are now handled as in 1.0.6.4. New chunks from some mods (e.g. Creation Magic Expanded) were also left out and are now added again

v1.7 - 2026.11.21.1
--------
One Frosty Mod Manager for Dragon Age: Inquisition and Dragon Age: The Veilguard. Built on J-Lyt's Veilguard Frosty (v1.0.7 - 2026.08.21.0) with every change from v1.0.6.4-dai.1 carried over. Full details: docs/CHANGELOG.md

Improvements:
- Dragon Age: Inquisition can be selected again, alongside Dragon Age: The Veilguard
- Faster Veilguard cache build: tocs and bundles of several superbundles now load at once on all CPU Threads
- Veilguard now uses the taller loading screen, with the superbundle count, per-bundle progress and the list of superbundles being loaded

Bug Fixes:
- Fixed issue where DAI asked to regenerate its SDK, and the regeneration failed (all game SDKs are included in the build again)
- Fixed issue where DAI mods were applied without the 1.0.6.4 fixes for binary superbundles, patched toc chunks, missing base tocs and new bundles
- Fixed issue where old-format .fbmod files failed to load ("Object reference not set to an instance of an object")
- Fixed issue where one mod or collection that failed to load closed the Mod Manager; it's now logged and skipped

Misc:
- Version is now 1.7, in the Mod Manager and the Editor
- Updated Credits and About window with the fork history and links
- Check for Updates now checks this fork's releases (github.com/dduttonnc/FrostyToolsuite); with no release or no connection it's only logged

Technical Changes:
- The DAI cache uses the 1.0.7 cache format, so the first launch rebuilds it
- J-Lyt's SDK-style plugin projects have the correct project type in the solution, so Visual Studio 2022 can parse them (DatapathFix itself stays old-style, so Visual Studio doesn't warn about its Veilguard configurations, and the Release build no longer fails on a missing DatapathFix(DEBUG).exe)

v1.0.6.4-dai.1 - 2026.11.19.0
--------
Dragon Age: Inquisition build on CadeEvs' Frosty 1.0.6.4.

Improvements:
- Much faster cache build: about 32 seconds instead of 10-15 minutes on an i9-9900K 16-thread CPU
- Faster mod apply: unmodified base bundles are skipped and mod data loads on several cores
- Added 'CPU Threads' and 'CPU Priority' options (Options > Performance), so Frosty doesn't take over the whole PC
- Taller loading screen with the superbundle count, progress and the list of superbundles being loaded
- Added 'Reconfigure...' to the right-click menu of converted .daimod mods that have settings
- Configurable .daimod files keep a copy of the original and the chosen settings in 'DAI originals'
- Re-importing an installed .daimod replaces it in place and starts from the last settings
- Load order modes 'List' and 'Priority' renamed to 'Bottom wins' and 'Top wins'
- Each .daimod import writes a summary to the log, and script 'LogLn' output goes to the log

Bug Fixes:
- Fixed issue where assets added by a .daimod were dropped
- Fixed issue where add vs modify for .daimod entries wasn't decided from the game data
- Fixed issue where .daimod archive offsets could point at the wrong data
- Fixed issue where the last texture/audio chunk of a .daimod wasn't registered
- Fixed issue where a texture's first mip was read with the wrong byte order
- Fixed issue where .daimod remove actions were ignored
- Fixed issue where size and hash weren't updated after a configuration script changed data
- Fixed black or rainbow textures at a distance in converted .daimod files
- Fixed infinite load screen with texture mods that have empty texture ranges
- Fixed issue where removing two or more entries from one bundle removed the wrong ones
- Fixed issue where chunk meta got out of step after a chunk was removed
- Fixed crash when importing a .daimod with some resource types
- Fixed issue where closing the .daimod settings window with X imported the mod unconfigured
- Fixed crashes from .daimod script errors; they're now listed as import errors
- Fixed issue where settings dropdowns passed the wrong value, and a value still being typed was lost
- Fixed race conditions on shared mod data while loading mods

Misc:
- Version shows as 1.0.6.4-dai.1 in the title bar, splash screen, About window and log

Technical Changes:
- Added WorkerPool for loading and applying mods, limited by the CPU Threads option
- Server garbage collection enabled
- Cache data is compressed at zlib level 1, so cache files differ from older builds
- Writing the cache is no longer quadratic, and progress updates are throttled

NOTE: Re-import .daimod files after updating. The fixes only apply when a mod is imported.

# 2. J-Lyt's Frosty

Frosty for Dragon Age: The Veilguard, built on the original Frosty's unreleased 1.0.7. Every version is v1.0.7 plus a build date. https://github.com/J-Lyt/FrostyToolsuite

v1.0.7 - 2026.08.21.0
--------
Bug Fixes:
- Fixed issue where ProjectReader/Writer did not use RIFF
- Fixed issues with RIFF EBX assets
- Fixed issue where duplicated meshes could cause the game to crash
- Fixed exploding shadows on some meshes
- Fixed issue where LODs would be imported with incorrect weights
- Fixed issue where LODs would be imported with broken skinning

Technical Changes:
- SharedTypeDescriptors is no longer written to 'SharedTypeDescriptors.ebx'
- Use reflection ids from 'SharedTypeDescriptors' when writing RIFF EBX assets

v1.0.7 - 2026.03.25.1
--------
Improvements:
- Added an alternate method of creating symbolic links for ModData (Tools > Options)
- Added additional mesh export and import options

Bug Fixes:
- Fixed issue where the Editor would hang when reverting an added asset
- Fixed issue where binary data was not used when importing .bin file
- Fixed issue where depth sections for certain meshes would be created incorrectly (Exploding Shadows)
- Fixed issue where width for 'FrostyImportExportBox' would initially cut off text

Misc:
- Added additional validation when creating a profile within the Mod Manager

Technical Changes:
- Removed 'Patch' source from 'DragonAgeTheVeilguard' Profile
- Updated FBX SDK to version '2020.3.9'

v1.0.7 - 2026.02.01.0
--------
Bug Fixes:
- Fixed issue where an asset with the same name but differing in location would fail to open if another asset with the same name was already open

Plugin Changes:
- Added 'MeshVariationDbPlugin' to Frosty Editor

v1.0.7 - 2026.01.30.1
--------
Improvements:
- Added Tooltips to 'Add' and 'Remove' buttons for the Marked Assets/Bundles feature

Bug Fixes:
- Fixed issue where 'Remove' buttons for the Marked Assets/Bundles feature were missing their commands

Misc:
- Changed order of buttons for the Marked Assets/Bundles feature

v1.0.7 - 2026.01.30.0
--------
Improvements:
- Added 'Marked Assets/Bundles' Feature to Bundle Editor
- Added 'Open Unmodified' option to assets

v1.0.7 - 2025.12.13.0
--------
Improvements:
- Added Read and Write Support for Reflection IDs

v1.0.7 - 2025.10.14.0
--------
Improvements:
- Added 'Drag and Drop' functionality to Applied Mods list
- Added progress bar to 'Exporting Pack' TaskWindow
- Added "Remember Profile" CheckBox to game selection window

Bug Fixes:
- Fixed issue where packs would fail to export if a mod was null i.e. Separator

Misc:
- DAVExtender(Dex) 'Open Mods Folder' MenuItem is now disabled by default
- Updated Credits
- Added 'App.Title' and replaced instances of "Frosty" and "Frosty Core"
- Icon for Core Windows now change based on MainWindow Icon
- Added Icon and minor style changes for KeyPromptWindow
- Title for 'About' window is now "About Frosty Mod Manager" instead of "About Frosty Editor" when opened within Mod Manager
- Title, Text and other changes for AddProfileWindow
- Added 'Export Pack' button to Toolbar
- When generating the SDK, the 'Finish' button will now restart the Editor/Mod Manager instead of closing it
- Restored 'Select Game' option in 'File' MenuItem

Technical Changes:
- Updated SharpSevenZip to 2.0.32 (7z.dll to 25.01)
- Added ILRepack to merge System Dll files for SharpSevenZip
- Changed Newtonsoft.Json to NuGet Package
- SharpSevenZip is now used to decompress .rar, .zip, and .fbpack archives (Removed LZMA.dll and UnRAR.dll)
- Cleanup

v1.0.7 - 2025.9.23.0
--------
Improvements:
- Improved directory selection for 'Custom Mods Directory' option
- Added 'Custom Autosave Directory' option within the Editor
- Added option to modify game path for selected profile

Bug Fixes:
- Fixed issue where Editor would not load correctly if launched via .fbproject
- Fixed issue where ContextMenu for FrostyPropertyGrid had an inconsistent style
- Fixed issue where 'Resources' tab would update regardless of which tab was selected
- Fixed issue where Editor/Mod Manager would crash if selection without a 'EbxClassMetaAttribute' was copied

Misc:
- If game path can not be found for a profile, the user will now be prompted to either update the game path or remove the profile.
- Modified order for 'Discord RPC' options
- Selecting 'DAVExtender(Dex) Mods' tab will update contents

v1.0.7 - 2025.9.15.0
--------
Improvements:
- Added 'Unify Assets' plugin that detects and replaces incompatible chunks within assets. (Allows incompatible assets from the Steam or EA version of the game to be compatible with both.)

Bug Fixes:
- Fixed various issues with Mod hyperlinks

Misc:
- Corrected DisplayName for 'Dragon Age™: The Veilguard'

Plugin Changes:
- [UnifyAssetsPlugin] Added Plugin

v1.0.7 - 2025.9.7.0
--------
Bug Fixes:
- Fixed issue where Editor/Mod Manager would crash if registry key could not be found

v1.0.7 - 2025.9.6.0
--------
NOTE: Projects created or saved with this update will not be compatible with previous versions of Frosty. It is recommended that you back up your projects.

Improvements:
- Added validation when creating 'Separators' for the Applied Mods list
- Added support for DAVExtender(Dex) Mods
- Added 'Flags' column that indicates if a mod has a 'DAVExtender(Dex) Archive' or 'Root Description File'

Bug Fixes:
- Fixed issue where 'UseDefaultProfile' would attempt to load if a profile was present in a config file
- Fixed issue where some assets would fail to duplicate if they had more than one 'TypeInfoGuid' attribute
- Fixed issue where ModData would not be regenerated when updating mods if it was in the same position within the Applied Mods list
- Fixed issue where Editor could crash when copying text to clipboard
- Fixed issue where Mod Manager would crash when checking resource conflicts if a missing mod was enabled in the Applied Mods list
- Fixed issue where Editor/Mod Manager would crash when retrieving an empty string from a config file

Misc:
- Added MessageBox to alert user if game exe has 'Disable full-screen optimisations' enabled
- Minor changes to descriptions within Options window
- Added styling to Mod hyperlinks
- Increased size of 'Check for Updates' window

Technical Changes:
- Added additional invalid characters to validation
- Removed restriction where only 'Dragon Age: The Veilguard' could be loaded
- Changes to Default profile loading functionality
- Changes to Mod patch warning functionality

v1.0.7 - 2025.6.22.0
--------
Improvements:
- Added the option to create 'Separators' for the Applied Mods list
- Added index value to Applied Mods and text to indicate which is High/Low Priority
- Config files for the Editor/Mod Manager can now be opened from: 'Open Config' under 'Tools'

Bug Fixes:
- Fixed an issue where certain archives would not decompress when added through the mod manager

Technical Changes:
- 'DragonAgeTheVeilguardSDK.dll' is now the only profile included with the Editor/Mod Manager
- Config will now save when: Add Mod, Remove Mod, Move Mod Up/Down
- Added try-catch for PrintLog

v1.0.7 - 2025.6.12.0
--------
NOTE: Projects created or saved with this update will not be compatible with previous versions of Frosty. It is recommended that you back up your projects.

Improvements:
- Added option to choose a custom mods directory for the Mod Manager
- Added sorting options for Applied Mods list in Mod Manager
- Added a new 'Recent Projects' MenuItem under File, which can store up to 10 recently opened projects.
- Added a 'Link' column to the Available Mods list that shows a hyperlink to a mod page

Bug Fixes:
- Fixed issue where 'DiscordRPCHideAsset', 'DiscordRPCHideProject' and 'PrintLog' would always be enabled if not found in config file
- Fixed issue where the mod manager would crash when creating ModData if an incompatible mod was applied
- Fixed issue where the 'Link' would not be read or written when exporting a mod

Misc:
- Available Mods list is now sorted alphabetically
- Minor fixes to the 'Applied' mods column and icon
- Mods in the Applied Mods list that don't have an icon will use the default
- Added tooltip to mods in the Applied Mods list when the .fbmod is missing

Technical Changes:
- Fixed an issue where the Config could not save values that are not a convertible or a JToken, i.e. Lists, Arrays, classes, etc.
- Mod export validation clean-up

Plugin Changes:
- [DatapathFixPlugin] Plugin is now disabled by default
- [DatapathFixPlugin] User will only be prompted to enable the plugin if the EA App version of 'Dragon Age: The Veilguard' is launched. (Will always be prompted for other games unless support is added.)

v1.0.7 - 2025.6.3.1
--------
Bug Fixes:
- Reverted "Fixed issue where the mod manager would crash when creating ModData if an incompatible mod was applied"

v1.0.7 - 2025.6.3.0
--------
Improvements:
- Added support for importing meshes that need tangent space compression
- When an exception is generated, it will now be translated to English when possible.

Bug Fixes:
- Fixed issue where DiscordRPC would cause the editor to crash when reading the header of a null asset
- Fixed issue where a duplicated mesh asset would be reverted after an invalid mesh import
- Fixed issue where the mod manager would crash when creating ModData if an incompatible mod was applied
- Fixed issue where the editor would crash when opening an invalid duplicated asset

Misc:
- Set default skeleton for Dragon Age: The Veilguard
- Added option to allow end-users to print log when creating ModData
- Changes to 'Actions' (now 'Resources') Tab within Frosty Mod Manager
- Added MessageBox to alert user if Frosty installation is in OneDrive
- Additional exception information will be added to the output (Type, HResult, message and source)
- The end-users operating system information and which application the exception occurred on will be added to the exception
- Added buttons 'Copy Exception', 'Copy Log' and 'Open Logs Folder' to the exception window

Plugin Changes:
- [TestPlugin] Re-added Plugin (Removed unintentionally from the previous release)

v1.0.7 - 2025.5.15.0
--------
Improvements:
- Mod Manager can now load menu extensions from plugins

Bug Fixes:
- Fixed issue where incorrect guids were assigned causing the editor to crash when saving certain ebx files

Misc:
- Update window will now only show the changelog for versions newer than the current version
- Warning message within the Mod Manager now includes which patch a mod was designed for

Technical Changes:
- Profile selection for Mod Manager now occurs before main window has loaded
- Added 'PathMap' to obscure local path in stacktrace

Plugin Changes:
- [DatapathFixPlugin] Added messagebox when launching game for the first time
- [DatapathFixPlugin] Removed 'Check for Updates' Functionality
- [DatapathFixPlugin] Added 'Dragon Age: The Veilguard' icon to Datapathfix.exe

v1.0.7 - 2025.4.30.0
--------
Misc:
- Various 'Check for Updates' Changes
- Experimental workaround for .fbproject files that are being used by another process

Plugin Changes:
- Added 'DatapathFixPlugin' to Frosty Editor and Mod Manager

v1.0.7 - 2025.4.24.0
--------
Improvements:
- Additional options for DiscordRPC
- Only show columns with conflicting resources when 'Show replacements only' is checked.

Bug Fixes:
- Fixed issue where ModData would not be deleted when new game patch is detected.

Misc:
- Removed Frosty Website Link and Added link for 'Frosty DAV Modding' discord.

Technical Changes:
- Changed versioning to build date and revision

Plugin Changes:
- [SoundEditorPlugin] Updated 'System.Drawing.Common' Package to 4.7.2  

v1.0.7 - 2025.4.20.0
--------
Improvements:
- Show replacements only by default when selecting the 'Actions' tab

Bug Fixes:
- Fixed 'Remember Profile Choice' Option

Misc:
- Removed 'Select Game' Option from 'File' menu (Frosty Mod Manager)
- Change geometry data for splash based on app

Technical Changes:
- Added additional versioning information
- Moved AboutWindow to FrostyCore
- Set Default MaxCasFileSize to 1GB


v1.0.7 - 2025.4.13.0
--------
Improvements:
- Validation has been added to the encryption key window to prevent invalid keys from being entered
- Bookmarks will now save and load correctly
- During mesh import, exceptions will be thrown when Vertex Groups are more than eight for any given vertex and when Weights for any given vertex are below 0.002
- Minor QOL Improvements to applying mods

Bug Fixes:
- Fixed issue where temporary editor mods did not get removed
- Fixed various crashing issues
- Fix out of range exception when generating typeinfo.cache

Misc:
- The banner for the 'Dragon Age: The Veilguard' Profile has been changed

Technical Changes:
- Used HashSets instead of Lists
- Moved manage ModData functionality to FrostyCore
- Changed ealayer3.dll to release build
- Fixes writing cat files
- Removed 'BlankPlugin', 'LaunchPlatformPlugin' and 'TestPlugin'


v1.0.7 - 2025.3.1.0
--------
Improvements:
- .Bin Import/Export: Assets export to .bin by default instead of .xml. During import, you now have the option to only import the data, which keeps the original FileGuid and RootInstanceGuid
- TexturePlugin: All supported image formats will be shown when importing textures
- More mod categories have been added for export
- Validation has been added during the mod export process
- During mesh import, exceptions will be thrown if an issue is detected with your .fbx file
- Improved support for plugins that use the 'ToolbarExtension'

Bug Fixes:
- Fixed issue where FirstMip/H32 data was not being stored for chunks
- Fixed various bundle related issues
- Fixed various crashing issues

Options:
- Added an option in the profile selection window to remove game profile

Misc:
- UI Fixes and Changes
- Prevented the user from selecting any profile except Dragon Age: The Veilguard

Plugin Changes:
- Added support for additional assets to the BundleEditorPlugin and DuplicationPlugin
- When duplicating an asset within the editor, you now have the option to add the duplicated asset to the bundles of the original asset

v1.0.7 alpha5v4
-----------------------
New Features:
- Added minor support for Mesh import
- Added support for Localization modding & previewing (Revamped Bioware Localization Plugin)
- Added support for Mesh previewing
- Added support for Texture modding & previewing
- Added minor support for Ebx modding

Improvements:
- Experimental: significantly improved data explorer responsiveness
- Ebx: Added YAML export option
- Ebx: Added option to export debug offsets (Tools->Options)
- Bundle editor: support for extra types & remove from bundle command
- Added relevant display names to the fbx export window
- Added a column that shows Applied Mods. (Frosty Mod Manager)
- Mods can now be filtered depending on their applied state. (Frosty Mod Manager)

Bug Fixes:
- Fixed some crashing issues due to missing assets when rewriting bundles
- Improved Profile Launch asynchronous logic
- Fixed shader compilation not triggering at launch
- Fixed mod manager not exiting properly
- Added crash prevention for registry scan errors
- Added some fallback parameters for the mesh preview
- Fixed a crashing issue related to duplicated meshes
- Fixed default posing issue when importing fem meshes.

Options:
- Added an option in Tools -> Options to disable shader compilation at launch, for faster launching

Misc:
- When importing an ebx as .bin file, bypass the ebx writer entirely as a workaround to ebxwriter limitations

# 3. Original Frosty Toolsuite

The original Frosty Toolsuite by GalaxyMan2015 and the Frosty team. https://github.com/CadeEvs/FrostyToolsuite (v1.0.6.4 had no changelog entry of its own; that entry lists its changes from the repository)

v1.0.6.4
--------
- Added startup action support to the Mod Manager (#332)
- Added an icon for Steam shortcuts (#330)
- Added post launch and minimize options to Manage ModData (#344)
- Minor quality of life improvements when applying mods (#328)
- Fixed property grid search crashes (#340)
- Fixed issue when importing over a duplicated Atlas Texture (#334)
- Fixed issue where EnumerateRes ignored added bundles (#331)
- Fixed Remember Profile issue (#329)
- Fixed thread unsafe HashSet when applying mods (#327)
- Plugins are no longer compiled automatically, and fixed an xcopy issue (#337)

v1.0.6.3
--------
- Improved performance of the EbxWriter
- Improved import for collections (#317)
- Added ReadOnly Star Wars Squadrons profile (#301)
- Added full Battlefield V profile (#301)
- Added option to manage ModData from the ModManager (#271, #314)
- Added whitelist option for bundles (#275)
- Added rename and duplicate pack options to the ModManager (#280)
- Added MenuExtension support to ModManager (#311)
- Added Math expressions in number textboxes (#291)
- Fixed launching issues on Steam
- Fixed bundle adding for manifest games
- Fixed crashing issues (#260, #261)
- Fixed small UI inconsistencies (#265, #267, #268)
- Fixed issue where editor mods didnt get removed (#274)
- Changed Rotations from YPR to XYZ (#304)

Plugin Changes
- Added support for more types to the duplication and bundle editor plugins (#276, #277)
- Added importing support for composite meshes (#262)
- Added importing support for raw ealayer3 audio
- Fixed crashing issues when adding meshes to bundles (#313)
- Fixed crashing issue when exporting certain textures

v1.0.6.3 (Beta 2)
--------
- Fixed crashing issues when launching
- Added MenuExtension support to ModManager (#311)
- Added ReadOnly Star Wars Squadrons profile (#301)
- Added full Battlefield V profile (#301)
- Added Math expressions in number textboxes (#291) 

Plugin Changes
- Fixed issue with mesh importing composite meshes
- Fixed crashing issues when adding meshes to bundles (#313)

v1.0.6.3 (Beta 1)
--------
- Fixed crashing issues (#260, #261)
- Fixed small UI inconsistencies (#265, #267, #268)
- Fixed launching issues on Steam
- Fixed bundle adding for manifest games
- Fixed issue where editor mods didnt get removed (#274)
- Improved performance of the EbxWriter
- Changed Rotations from YPR to XYZ (#304)
- Added option to manage ModData from the ModManager (#271)
- Added whitelist option for bundles (#275)
- Added rename and duplicate pack options to the ModManager (#280)

Plugin Changes
- Fixed crashing issue when exporting certain textures
- Added importing support for composite meshes (#262)
- Added support for more types to the duplication and bundle editor plugins (#276, #277)
- Added importing support for raw ealayer3 audio

v1.0.6.2
--------
- Fixed memory issue and writing of cat files
- Fixed issue with bundle editing in older games
- Fixed crashing issues
- Improved applying speed in Mod Manager
- Improved Loaded Plugins List in Editor
- Added filter to PointerRef Editor
- Added Recent Projects to Editor
- Added option to change ordering of Mod Manager's Applied Mods list

Plugin Changes
- Fixed MeshSetEditor inverted tangets
- Fixed issues with DAI texture edits
- Fixed issue with texture duplication
- Fixed issues with rivals meshes and textures
- Fixed issues with ealayer3 sounds
- Updated BWLocalizationPlugin
- Added CompositeMeshAsset part exporting

v1.0.6.2 (Beta 2)
--------
- Improved applying speed in Mod Manager (#159)
- Fixed issue with bundle editing in older games (#240, #248)
- Fixed crashing issues (#242)

Plugin Changes
- Fixed crash when the localization chunk doesnt exist but gets modified (#239)
- Fixed issue with texture duplication (#243)
- Fixed crash when loading sounds where one chunk doesnt exist (#245)
- Fixed issues with rivals meshes and textures (#246)
- Moved DifficultyWeaponTableData into its own plugin (#247)

v1.0.6.2 (Beta 1)
--------
- Fixed memory issue and writing of cat files (#222)
- Fixed about menu support link being cut off (#224)
- Changed error text for loading a wrong project (#225)
- Fixed issue with ealayer3 library (#235)

Plugin Changes
- Fixed crash when exporting textures (#223)
- Added bundle box to Chunk/Res Editor (#226)
- Fixed crash in BiowareLocalizationPlugin (#232)
- Fixed issue for importing sounds (#236)

v1.0.6.2 (Alpha 2)
--------
- Fixed issue with logging legacy mods (#217)

Plugin Changes
- Fixed issue copying over to Mod Manager (#219)

v1.0.6.2 (Alpha 1)
--------
- Improved applying speed in Mod Manager (#159)
- Improved Loaded Plugins List in Editor (#192)
- Added filter to PointerRef Editor (#161)
- Added Recent Projects to Editor (#174)
- Added CompositeMeshAsset part exporting (#178)
- Added option to change ordering of Mod Manager's Applied Mods list (#159)
- Fixed cache being written twice (#167)
- Fixed issues with DAI texture edits (#198)

Plugin Changes
- Added LaunchPlatformPlugin option for keeping platform open after game exit (#179)
- Updated BWLocalizationPlugin (#211)
- Fixed LaunchPlatformPlugin failing to launch Steam platform (#179)
- Fixed MeshSetEditor inverted tangets (#155)
- Fixed issue with importing over rigid meshes (#205)

Technical Changes
- Added additional information to update panel during cache writing (#167)
- Fixed issues with CStringControl and FsLocalizationStringDatabase (#172)
- Fixed issues with ealayer3 sounds (#214)

v1.0.6.1
--------
- Fixed NFS: Heat profile being read only
- Temporarily disabled Update Checker

v1.0.6
--------
- Added plugin support
- Added Sound Import functionality
- Added Mesh Import functionality
- Added Bundle Editor
- Added TextureAsset duplication support (For some games)
- Added "Copy Guid" button to PointerRef Property Grid items
- Added cancel button to Mod Applying process
- Added modding categories to Mod Export window
- Added experimental NFS: Heat support
- Added InitFS Viewer
- Added Type Viewer
- Added Localized String Editor
- Added Scan for Games functionality to Prelaunch window
- Added Refresh MeshVariations functionality
- Added MeshVariation caching
- Added TypeInfo caching
- Added warnings for games containing EasyAntiCheat
- Added BoxedValueRef editing functionality
- Added a new Type Editor for Event/Property/Link Connections in the Property Grid
- Added pack zipping and loading
- Added collections
- Moved location of the Editor and Explorer menu items (Moved from Tools to View)
- Moved to json format for Frosty Editor and Mod Manager configs
- Moved config files to local folder under AppData for global access between builds
- Revamped Sound Editor
- Revamped layout for Prelaunch window
- Revamped Home Page
- Redesigned theme

Plugin Changes
- Added TypeEditor extension
- Added Menu extension
- Added Tab extension
- Added Option extension
- Added Data Explorer Context Menu Item extension
- Added Asset Definition extension
- Added Localized String Database extension
- Added ThirdPartyDll extension
- Added Type Override extension
- Added Custom Handler extension
- Added Custom Asset Manager extension
- Added Custom User Shader extension
- Added Custom Type Field extension
- Added Startup Action extension
- Added ExecutionAction extension

v1.0.6 (Beta 5)
--------
- Added "ModDb" support when setting links during mod export process
- Fixed crash when selecting description tab of collection
- Fixed issues with Battlefield 1 mesh importing (#89)
- Updated naming scheme for Flatten Hierarchy
- Updated Mod Manager window width to account for Collections (#85)

Technical Changes
- Fixed RangeStart calculation for manifest chunks (#94)

v1.0.6 (Beta 4)
--------
- Added ability to add legacy mods to collections (#73)
- Fixed comparing mods.txt file when generating ModData (#73)
- Fixed launching legacy mods (#73)
- Fixed filtering available mods with collections (#73)
- Fixed removing packs (#73)
- Fixed crash when pressing the "Apply Mod(s)" button (#76)
- Fixed update checking release versions

v1.0.6 (Beta 3)
--------
- Added collections
- Added version checking functionality
- Added missing SoundEditor plugin

Technical Changes
- Fixed compression loading issue
- Fixed character crash issue (#52)
- Fixed localization issue (#55)

v1.0.6 (Beta 2)
--------
- Modified Mod Manager loading process so mods no longer get removed from packs when the file cannot be found
- Improved option descriptions (#42)
- Fixed crashing issue when loading Garden Warfare 1 and 2 (#41)

Technical Changes
- Added missing PVZDeobfuscator to FrostySdk project (#41)
- Improved version logic

v1.0.6 (Beta 1)
--------
- Added caching for type info loading process (#31)
- Added caching to MeshVariationDataBase loading process (#22)
- Added drag and drop functionality for editor tabs in the Editor (#20)
- Added InitFs reading support for Plants vs Zombies: Garden Warfare 1 and 2 (#11)
- Added anti-aliasing to mod icons in the Mod Manager (#3)
- Added visual when hovering over tabs in the Editor (#8)
- Added styling for ComboBox (#9)
- Added more functionality for moving mods under the Applied Mods list (#3)
- Added cancel functionality to actions tab in the Mod Manager (#37)
- Added source code link to convient links section in Editor (#18)
- Revamped styling for cancel functionality during loading process (#37)
- Modified DisplayModuleInId option to be false by default
- Fixed InitFs loading for Need for Speed: Rivals, Need for Speed: Online, and Battlefield 4 (#10)
- Fixed references under reference tab not updating until selection changes (#17)
- Fixed numerous profile banners (#19)
- Fixed ScalableEmitterDocument asset not using the correct icon (#6)
- Fixed hard crashing when config gets corrupted
- Fixed minor inconsistencies with UI 

Technical Changes
- Added writing functionality for ulong (#17)
- Added BC1_UNorm extended header for texture arrays (#17)
- Fixed opening certain textures that use SRGB flags (#33)
- Removed the reading and writing of int32 in Texture resource for Star Wars Battlefront II (#2)

Plugin Changes
- Added editor support for Localized Strings (#19)
- Fixed crash when removing all additional meshes in scene (#19)
- Fixed localized string export encoding (#1)
- Fixed escaping of characters in exported localized strings which have both quotes and commas (#1)

v1.0.6 (Alpha 5)
--------
- Added pack zipping and loading
- Config files are now stored in AppData
- Fixed an issue where the Mod Manager wouldn't save removing packs
- Fixed an issue where type overrides weren't appearing in non-categorized property grids
- Fixed an issue where the Lua Editor wasn't open properly

v1.0.6 (Alpha 4)
--------
- Separated "Loading Mods" and "Applying Handlers"
- Added progress bar to "Applying Handlers"
- Modified the modlist to use the mod name instead of the file name

v1.0.6 (Alpha 3)
--------
- Added support for launching games on other platforms (EA Desktop, EpicGames, and Steam)
- Added the ability for the Editor to apply exported mods located in "Mods/<selected_profile/"
- Added options menu to the Mod Manager
- Added a "Show only replacements" option to Mod Manager's Actions tab
- Moved the Mod Manager's launch command arguments into options menu
- Moved the Mod Manager ini configs to the new json system
- Renamed the Mod Manager's "Profiles" to "Packs"
- Fixed an issue where the Mod Manager would crash when applying a mod using a custom handler and another using a third party method.
- Fixed an issue where the Mod Manager wasn't properly detecting if the applied mods list stayed the same between launches
- Fixed an issue where an sdk couldn't be created for Battlefield V

Plugin Support
- Added "LaunchPlatformPlugin"
- Added icon support for the Data Explorer Context Menu Item extension
- Modified the CustomEditorExecution extension into ExecutionActions (Example within the TestPlugin project)

v1.0.6 (Alpha 2)
--------
- Added TextureAsset duplication support (For some games)
- Added "Copy Guid" button to PointerRef Property Grid items
- Redesigned Mod Manager
- Revamped Home Page
- Revamped InitFs Explorer
- Modified the PointerRefEditor objects list to prioritize objects with custom transient ids when sorting
- Modified the location of the Editor and Explorer menu items (Moved from Tools to  View)
- Modified the StructEditor's text to be a read-only Textbox instead of a TextBlock (Guids can now be selected again)
- Modified SubWorldReferenceObjectData to use a PointerRef  (Bundle) instead of a string (BundleName) apart of TestPlugin
- Modified the "Enter Key" window to specify the developers cannot provide the encryption key
- Fixed an issue where the ModData cas files weren't getting deleted between launches
- Fixed an issue where importing incorrect meshes through the MeshSetEditorPlugin would close the Editor instead of printing into the log
- Fixed an issue where the PropertyGrid couldn't filter non-exportable guids
- Fixed an issue where duplicated assets weren't getting assigned a new root instance guid
- Fixed an issue where Plants Vs Zombies Garden Warfare 1 would crash on startup
- Fixed an issue where assets couldn't be opened in the Legacy Explorer
- Fixed an issue where the Mod Manager's version name was "1.0.5.10"
- Fixed an issue where the Mod Manager's Prelaunch window is titled "Frosty Editor"
- Fixed an issue where the Chunk/Res Explorer had an inconsistent style
- Fixed an issue where the Bundle Editor had an inconsistent style
- Fixed an issue where the Legacy Database Editor had an inconsistent style
- Fixed an issue where the Sound Editor's toolbar had an inconsistent style
- Fixed an issue where the Sound Editor's properties panel couldn't be scaled
- Fixed an issue where the Editor had visual artifacts due to an incorrect render edge mode

Plugin Support
- Moved duplication functionality to DuplicationPlugin
- Moved GetUnmodifiedEbx() into GetEbx() as a bool parameter, "getUnmodifiedData", and defaults to false.
- Added Custom Asset Manager extension
- Added Custom User Shader extension
- Added Custom Type Field extension
- Added Startup Action extension
- Added GetEbxByPrimaryInstanceGuid()
- Added "Modification Reason" and "Index" parameters to ItemModifiedEventArgs
- Modified the PluginValidForProfile and PluginNotValidForProfile to now take in the Profile Version instead of the Profile Name
- Modified numerous functions and members to be virtual within DeferredRenderScreen2 (Can now be overridden through plugins)

v1.0.6 (Alpha 1)
--------
- Added plugin support
- Moved all editors to plugin system
- Added Sound Import functionality
- Added Mesh Import functionality
- Added Bundle Editor
- Added cancel button to Mod Applying process
- Added modding categories to Mod Export window
- Added experimental NFS: Heat support
- Added InitFS Viewer
- Added Type Viewer
- Added Scan for Games functionality to Prelaunch window
- Added Refresh MeshVariations functionality
- Added warnings for games containing EasyAntiCheat
- Added BoxedValueRef editing functionality
- Added a new Type Editor for Event/Property/Link Connections in the Property Grid
- Moved to json format for Frosty Editor config
- Revamped Localized String Editor
- Revamped Sound Editor
- Revamped layout for Prelaunch window
- Redesigned theme

Plugin Support
- Added TypeEditor extension
- Added Menu extension
- Added Tab extension
- Added option extension
- Added Data Explorer Context Menu Item extension
- Added Asset Definition extension
- Added Localized String Database extension
- Added ThirdPartyDll extension
- Added Type Override extension
- Added Custom Handler extension

v1.0.5.9
--------
 - Added Battlefield: Hardline profile
 - Fix for FIFA20 launching issues

v1.0.5.8
--------
 - Added Plants vs Zombies: Battle for Neighbourville Read Only profile
 - Added the ability to update a games profile SDK when the game is updated
 - Export settings for meshes will now remember the last skeleton used
 - Added the ability to select the render device the viewport will use (From options)
 - Added the ability to merge ShaderBlockDepot changes from multiple mods
 - Fixed issue with exporting BFV meshes with skeletons

v1.0.5.7
--------
- Added FIFA20 full profile
- Updated SDK for SWBF2
- Updated SDK for BFV
- Updated SDK for Anthem
- Fixed crash when attempting to load meshes in BFV

v1.0.5.6
--------
- Added filtering to the property grid. Can filter by property name or by an internal/external guid
- Fixed an issue where newer format EBX were not rewriting correctly
- Fixed an issue where the editor would crash when trying to display an invalid reference
- Fixed some more reference assigning issues

v1.0.5.5
--------
- Fix for legacy db changes causing game to crash in Madden20
- Fix for crashing when attempting to edit network registry assets
- Small fix for ShaderBlockDepot boolean edits
- Fix for extra / detection in duplicate asset filename

v1.0.5.4
--------
- Added Madden20 full profile
- Added Plants vs Zombies Garden Warfare read only profile
- Added the ability to add and remove new root level instances to an ebx (via the View Instances dialog)
- Added the ability to rename and duplicate instances via same dialog
- Added the ability to copy and paste objects and arrays
- Added the ability to duplicate an asset (and change its type) comes with a long list of caveats
- Modifying mesh materials and object variations in SWBF2 will now update the appropriate ShaderBlockDepot files
- Added more inline classes to SWBF2 profile
- Fixed crash that would result when you saved at the same time an autosave was being performed
- Fixed an issue that would result in the pitch in FIFA19 appearing white (as well as potentially other bundle related issues)
- Fixed a couple of type info generation related issues for Mass Effect
- Fixed an issue where the PointerRef dropdown box would appear cut off, not allowing you to scroll all the way to the bottom
- Fixed setting a shader to null in a mesh material would result in a crash

v1.0.5.3
--------
- Fix to numerous assets not being able to be opened

v1.0.5.1
--------
- Fixed importing mods made in a prior patch causing error/crash
- Fixed saving project with kit previewer open crashing
- Fixed crash when exporting skinned meshes from Anthem
- Fixed sound continuing to play when using the close all documents command

v1.0.5
------
- Added a reference viewer, allowing the user to see where an asset is referenced and what it references
- Added a bundle reference viewer, allowing the user to see what bundles an asset is in (was previously a right click to log option)
- Added read only profile for Anthem
- Added read only profile for Battlefield V
- Added read only profile for Need for Speed Rivals
- Added an experimental kit previewer for Fifa
- Added the ability to bookmark legacy items
- Added a few legacy asset editors/viewers (DB, DDS, BIG, AST, etc.)
- Added a shader compilation system for the renderer allowing the end user to author their own shaders to use in place of the built in shader
- Added experimental resource explorer, allowing the exporting and importing of raw res and chunk files
- Added experimental localized string viewer with the ability to export all strings and ids to a csv file (no editing)
- Added experimental ebx export to xml (found under tools)
- Added discord RPC functionality
- Added mod category grouping to the mod manager
- Added a linear transform editor that shows the position, rotation (as Yaw/Pitch/Roll) and scale
- Added a patch summary window when a game has been updated showing which files have been added/modified/removed
- Added profile selection window to mod manager
- Added an options window for configuring some basic Frosty options
- Added the ability to view texture and variation db locations for each variation of a mesh material
- Added FIFA19 full profile (editing now enabled)
- Added a new skeleton picker (allows filtering and folders)
- Added a branch tree view for conversations in DAI, MEA and Anthem, allowing the user to access previously inaccessible items
- Exporting a composite mesh will now export any embedded composite part transforms
- Improved start times of the application and apply times for most games
- Moved legacy viewer for Fifa/Madden to the same location as the data explorer with a menu item to switch between the two
- Revamped the property grid, allowing for increased functionality
- Revamped the mesh renderer, better rendering, and more responsive
- Fixed the save on close prompt, will cancel the close request if the user clicks the x instead of Yes or No
- Fixed affected files listing freezing up the UI when mods have thousands of edited files
- Fixed a crash when exporting uncompressed volume textures as non DDS formats
- Fixed FIFA19 meshes exporting incorrectly
- Updated SDKs to the latest patch version

v1.0.4.5
--------
- Added a Battlefield4 profile
- Added the ability to import/export textures (2d/array/cube) to and from PNG/TGA/HDR and DDS
- Fixed an issue with Windows 10 update 1809 unable to launch modded games
- Updated various SDKs to latest patch version
- And other nice things

v1.0.4.4
--------
 - Added a read-only FIFA19 profile
 - Added Cubemap/Volume and Texture array import support
 - Fixed a crash when viewing specific SWBF2 models
 - Mouse over effects on list/tree controls now more consistent
 - Fixed an issue where PVZ2 changes would cause the game to crash

v1.0.4.3
--------
 - Added Madden NFL 19 profile
 - Added Plants vs Zombies 2 profile
 - Added Battlefield1 profile
 - Added a screen to show mod conflicts in the mod manager
 - Fixed numerous frosty window issues and enabled DPI support
 - Out of date mods will now show a warning on import and are flagged via a warn symbol
 - Fixed crash with NFS16 and DAI when initially loading the data
 - Potentially fixed corrupted textures in DAI when using daimods
 - Fixed an issue where legacy file changes in Fifa/Madden were not being applied correctly on project load
 - Fixed some 7z mod import issues

v1.0.4.2
--------
 - Fixed the modding process for Star Wars: Battlefront II after the latest patch changed the layout
 - Added taskbar progress to all tasks with a progress bar
 - Fixed issue with new projects with no modifications were being autosaved
 
v1.0.4.1
--------
 - Can now load other language databases via config using category Init and key Language (some examples include English,Russian,SimplifiedChinese,TraditionalChinese,etc.)
 - If language database for specified language does not exist, editor will not crash
 - Fixed issue with BC5 textures being improperly exported

v1.0.4
------
 - Config selection at launch
 - SVG Preview/Import/Export
 - AtlasTexture Preview/Import/Export
 - DelayLoadBundle explorer for DAI
 - IES Preview
 - NFS Payback support
 - Dialog paths saved to INI
 - New mod export window
 - Mod settings saved to project, mod author can also be set in INI to be used as default using category ModSettings and key Author
 - Various prompts to save project if unsaved
 - Autosave with options settable in INI using category Autosave and keys MaxCount and Period
 - Various UI tweaks
 - Auto hash text entered into int/uint fields and convert from hex if starting with 0x
 - Ebx writing speed improvements per change
 - Using actual Frostbite BRDF's in mesh preview renderer
 - Some classes now have descriptions
 - Saving project will correctly update open tab headers
 - Copy/Paste for property grid for all basic object types
 - Insert and RemoveAll added to list context menu
 - daimod files can be imported and configured via Frosty Mod Manager
 - Variation database now only loads when opening a mesh for the first time (speeds up initial loading)
 - Prompt appears when opening an asset, instead of freezing UI on large assets
 - Data is accessible and editable while the game is running
 - “Find in Data Explorer” will now clear the Search Filter
 - You can now double click a class to create it in the Class Selection window
 - Filter auto focused on class selection window open
 - Improvements to texture import logic

v1.0.3.1
--------
- Fixed texture replacement crashing in SWBF2
- Added verification for symbolic link creation, error produced if any failed to create.
- Added the ability to create new VisualUnlockSkin/Part assets and MeshAndVariationPair assets
- Fixed issue with ini file getting corrupted causing a crash, now ini is recreated (user must reselect executable)

v1.0.3final
-----------
- Added Star Wars: Battlefront 2 game profile
- Added Wireframe render mode to mesh preview
- Filtering in data explorer now ignores case
- Fixed bug with Fifa18 meshes crashing on preview
- Updated SDKs for various games
- Set bcrypt.dll to compile without any VC dependencies (may or may not fix peoples issues)

v1.0.3alpha4
------------
- Added bookmark folders
- Added bookmark renaming
- Added Fifa18 game profile
- Added scrollable tab control for the main tab panel (instead of stacking)
- Added custom splash screen banners for each supported game
- Added new task dialog, provides more feedback during import/export activities
- Added new view modes for mesh preview (Base/Specular color, Normals, MaterialAO, Smoothness, Metallic, Reflectance, Reflections and Ambient)
- Changes to mesh materials are now reflected dynamically in render viewport (For tints/colors that have been implemented in the shader)
- Rendering improvements across all games (except NFS). Preview should now reflect game rendering more closely.
- Fixed numerous issues with composite mesh previews and mesh preview state
- Fixed sound preview to disable export/play buttons on unsupported sounds, and correctly reports sound has been exported
- Moved back to v1.0.2b bcrypt.dll until issues can be resolved properly

v1.0.3alpha3
------------
- Fixed bcrypt.dll compilation issues
- Fixed issue with DAI infinite load when a specific bundle was edited
- Fixed issue with DAI and insufficient memory, attempting to load localized texts
- Fixed crash with previewing and exporting meshes in SWBF and Fifa17

v1.0.3alpha2
------------
- Fixed out of date SDK used in MEA
- Disabled non working import button from sounds
- Packaged fifaconfig workaround with editor

v1.0.3alpha
-----------
- Fixed an issue with compressing certain files (Compression Failed error)
- LZ4 compression now used to compress data for games that use it (MEC, SWBF)
- Drastically reduced time and storage capacity it take to apply mods for all games
- Implemented new texture formats for preview and export
- Added Searching/Filtering of assets in the data explorer
- Added the ability to save bookmarks
- Added more advanced asset editing capabilities, including class instantiation, and removing elements from the middle of arrays
- Added mesh 3d preview (import to come)
- Added sound preview and export (import to come)
- Added lua asset editing (with lua compilation if required)
- Added support for DragonAge, NeedForSpeed, Fifa17
- Added rudimentary legacy file editing support for Fifa17

v1.0.2a
-------
 - Fixed certain assets showing a blank properties page

v1.0.2
------
- Array and ref editing added to property grid
- Localized string parser and index editor for property grid added
- Double click now expands property grid entry
- Tabbing through property grid values should be more consistent
- Unreferenced ebx classes can now be selected in the property grid for editing

v1.0.1b
-------
- Relaxed the checks for importing textures as it was flagging even correct 2d textures as not

v1.0.1a
-------
- Hotfix to not try and open a superbundle if it does not exist, but the toc does.

v1.0.1
------
- All ThirdParty DLLs are now unblocked automatically
- Ascii FBX, OBJ, and DAE options added to exporting meshes
- Previewing and exporting 2d array, cubemap, 3d textures now work correctly
- Disabled importing of 2d array, cubemap and 3d textures for now (original implementation would import them incorrectly)
- Can view individual slices of multi dimensional textures using the slices combobox
- Added an exception window for all unhandled global exceptions

v1.0.0 (Public Release)
-----------------------
- First release
