# Frosty Mod Manager quick notes

The short version of what changed. Full notes: [PATCH_NOTES.md](PATCH_NOTES.md).

DAI = Dragon Age: Inquisition, DAV = Dragon Age: The Veilguard, MEA = Mass Effect: Andromeda.

## Not released yet

- All games: the window while mods are applied now looks like the cache window (game banner) and can show every step and what each CPU thread is doing. Options has a new Progress View setting: Detailed (the default) or Simple (one line, as before). It also covers the cache window
- DAI: texture mods from DAI Mod Manager that were converted with the original Frosty no longer show rainbow or black noise while loading or on Low texture quality (ModData rebuilds once, no re-import needed)
- DAI: two texture mods for the same texture (e.g. Cullen Face Retexture and Scarless Cullen) no longer mix: the winning mod's texture is used as if it were the only one. Removals in DLC and multiplayer areas now work too (same one-time ModData rebuild)
- DAI: a mod that changes one of the small files the game keeps inside its bundles now takes effect instead of the game's old copy being used
- The Launch Platform plugin is back (start the game through the EA app, Steam or Epic; off unless turned on), and a broken plugin no longer closes the Mod Manager at launch
- The Discord support links are hidden for now, since the Frosty team's servers don't support this version
- The download now includes README, PATCH_NOTES and these quick notes
- DAI: converted .daimods that match your game no longer show the 'different game version' warning, and a short note names any file a later game patch changed
- New Diagnostic Logging option (off by default): the log shows much more about the PC, the game install, the mods applied and how the game started and exited, and is saved to the Logs folder so it can be sent in

## 1.7.4

- DAI and MEA: applying mods (building ModData) is faster, most of all with many mods
- Launching with an unchanged mod list is quicker, and the log shows how long each step of applying mods took
- MEA: Bioware Localization 1.1.1.2 fixes now apply to MEA too (ModData rebuilds once)
- DAI: saves made with DAI Mod Manager can be loaded again (Frosty asks first, and Options has a new Dragon Age Inquisition page to change or undo it). Back up your saves first: new saves then only load through Frosty

## 1.7.3

- No more deleting Caches or ModData after an update: Frosty rebuilds them itself when needed. Both are now version-controlled.
- DAI: install .daimod mods straight from a .zip, .7z or .rar
- DAV: the EA app version no longer asks for a new SDK
- New SDKs after a game patch are made much faster
- DAI: Bioware Localization plugin 1.1.1.2, so mod texts no longer go missing (mostly non-English)
- DAI and MEA: old-format mods are no longer cut short
- DAI and MEA: the Dex menu no longer shows up with ReShade
- Clearer message when a mod doesn't fit your game version
- Old mod collections no longer show the Dex icon
- DAI: a broken .daimod no longer closes the Mod Manager
- Untested (Steam/Epic only): DAI and MEA Steam copies launch through Steam again; DatapathFix settings are kept per game

## 1.7.2

- DAI: ModData is much smaller (about 7 GB down to 2 GB in one setup) and loads faster

## 1.7.1

- MEA: fully supported, and texture mods (e.g. Beard Styles) no longer hang saves and area changes
- DAI: replaced textures no longer sparkle or look pixelated
- Other games can be opened with an "untested" note instead of being blocked

## 1.7

- One Mod Manager for DAI and DAV
- Faster DAV cache build and a new loading screen
- DAI no longer asks for a new SDK
- A mod that fails to load is skipped instead of closing the Mod Manager
- Check for Updates looks at this fork's releases
