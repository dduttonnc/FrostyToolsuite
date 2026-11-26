# Frosty Toolsuite (Dragon Age: Inquisition, Dragon Age: The Veilguard and Mass Effect: Andromeda)
The most advanced modding platform for games running on DICE's Frostbite game engine.

This version runs Dragon Age: Inquisition, Dragon Age: The Veilguard and Mass Effect: Andromeda from one Frosty Mod Manager, with fixes for DAI `.daimod` mods and a much faster cache build and mod apply.

<a title="Downloads" href="https://github.com/dduttonnc/FrostyToolsuite/releases">
  <img alt="Downloads" src="https://img.shields.io/github/downloads/dduttonnc/FrostyToolsuite/total?color=white&label=Downloads&logo=github&style=flat-square"></a>

## Release
Download the latest release from [releases](https://github.com/dduttonnc/FrostyToolsuite/releases/latest). What changed in each version is in [PATCH_NOTES.md](PATCH_NOTES.md), with a quick summary in [QUICK_NOTES.md](QUICK_NOTES.md).

## Build

1. Clone the repository: ``git clone https://github.com/dduttonnc/FrostyToolsuite.git --recurse-submodules``
2. Open the solution (found under FrostyEditor) with **Visual Studio 2022** or newer, and set the configuration to ``Developer - Debug`` or ``Release - Final``.
3. Only build the projects themselves, never the solution. Build FrostySdk and FrostyModManager (or FrostyEditor) first, then the plugins you need.

## Credits
This is a fork of a fork. Thanks to everyone whose work it is built on:

* **Original Frosty Toolsuite**: GalaxyMan2015 and the Frosty team, https://github.com/CadeEvs/FrostyToolsuite
* **Frosty for Dragon Age: The Veilguard**: J-Lyt, https://github.com/J-Lyt/FrostyToolsuite
* **Contributors along the way**: Mophead, Wannkunstbeikor, Brawltendo, Dyvinia, Zhpete, NoOneOfSignificance, AllyJaxx, MrJTGaming, Wavebend, CosmicDreamsofCode
* **This version** (DAI, Veilguard and Mass Effect: Andromeda in one build, DAI and MEA mod fixes, multithreading): dduttonnc
* **Frosty Toolsuite v2**, the next generation, is at https://github.com/FrostyToolsuite/FrostyToolsuite

The full list is in the About window of the Mod Manager and Editor.

## License
The Content, Name, Code, and all assets are licensed under a Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License. This version is published with permission from the Frosty team.
