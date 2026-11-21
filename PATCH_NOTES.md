v1.7 - 2026.11.20.1
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
- J-Lyt's SDK-style plugin projects have the correct project type in the solution, so Visual Studio 2022 can parse them

v1.0.6.4-dai.1 - 2026.11.19.0
--------
Dragon Age: Inquisition build on CadeEvs' Frosty 1.0.6.4.

Improvements:
- Much faster cache build: about 32 seconds instead of 10-15 minutes on a 16-thread CPU
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
