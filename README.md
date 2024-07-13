# Show Plant Progress
This Valheim mod shows a plant grow progress when you hover over it. It changes color depending on the progress. Super simple, that's it.

## Automatic Installation
R2ModMan: https://r2modman.com/

## Manual Installation
To install this mod, you need to have BepInEx: https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
After installing BepInEx, extract ShowPlantProgress.dll into games install **"\Valheim\BepInEx\plugins"**

If you have any suggestions, feel free to let me know!

## Not my code
This is just a new Visual Studio project around the code of https://github.com/smallo92/PlantGrowProgress and https://github.com/rikaakiba/PlantGrowProgress.
New project generated with bepinex5plugin template: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html
Just changed target from net46 to net462 (fixes missing reference to netstandard)

## Development setup
Installed VS2022 with:
Workloads:
- .NET desktop environment
Individual components:
- .NET Framework 4.6.2
