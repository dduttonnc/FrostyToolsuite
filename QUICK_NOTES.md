# Frosty Mod Manager quick notes

The short version of what changed. Full notes: [PATCH_NOTES.md](PATCH_NOTES.md).

DAI = Dragon Age: Inquisition, DAV = Dragon Age: The Veilguard, MEA = Mass Effect: Andromeda.

## Not released yet

- DAI: saves made with DAI Mod Manager can be loaded again (Frosty asks first, and Options has a new Dragon Age Inquisition page to change or undo it)

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
