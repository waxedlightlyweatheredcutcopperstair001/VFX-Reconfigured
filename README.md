# Volumetric FX Reconfigured: source and build

A Kerbal Space Program 1.12 mod that unifies **Volumetric Explosion FX** (VEFX) and
**Volumetric Ground FX** (VGFX) by Warnix into a single mod, with reworked explosions and ground
effects. Both originals are MIT licensed; see `LICENSE` and each folder's `CREDITS.md`.

Player documentation: `package/README.md` (shipped in the mod folder). Changes: `package/CHANGELOG.md`.

## Layout

| Path | What |
|---|---|
| `VolumetricExplosionFX/` | Volumetric Explosion FX 1.1.1, imported with its full history |
| `VolumetricGroundFX/` | Volumetric Ground FX 1.1.0, imported with its full history |
| `src/` | Shared code: assembly info, `ModPaths` (folder layout) |
| `tools/build.py` | Builds `build/VolumetricFXReconfigured.dll` from both source trees |
| `tools/package.py` | Assembles `build/GameData/VolumetricFXReconfigured`, zips it into `release/`, `--deploy` installs it |
| `tools/make.py` | Everything in order: explosion shaders (Unity), plugin, tests, package (`--deploy`) |
| `package/` | Hand-written files shipped in the mod folder (README, CHANGELOG) |
| `GameData/` | The packaged mod (generated, not tracked) |
| `release/` | Release zips (not tracked; published on GitHub Releases) |

## Upstream

The upstream histories are merged in as subtrees, and their tags are kept as
`upstream/vefx/<tag>` and `upstream/vgfx/<tag>`. The remotes `vefx` and `vgfx` point at
https://github.com/warnix91/Volumetric-Explosion-FX and
https://github.com/warnix91/Volumetric-Ground-FX. To bring in a later upstream release:

```bash
git fetch vefx --no-tags
git merge -X subtree=VolumetricExplosionFX/ vefx/main
# likewise vgfx with -X subtree=VolumetricGroundFX/
```

## Build

Explosion shaders can only be rebuilt on Windows (Unity 2019.4.18f1 can't be licensed on current Linux). On Windows:

1. Install Git, Python 3 (`py -m pip install numpy`), the .NET SDK 8 or newer, and Unity Hub with Unity
   **2019.4.18f1** (Windows Build Support is included on Windows).
2. `git clone https://github.com/waxedlightlyweatheredcutcopperstair001/VFX-Reconfigured.git`
3. Copy `config.example.json` to `config.local.json` and set your KSP folder and `Unity.exe`.
4. `py tools\make.py --deploy` builds everything and installs it into KSP (`--no-shaders` skips the Unity step).

Elsewhere, `python3 tools/build.py` compiles the plugin (only `kspRoot` needed: a folder with `KSP_x64_Data/Managed`)
and `dotnet run --project VolumetricExplosionFX/Tests/VolumetricExplosionFX.Tests.csproj -c Release` runs the tests.

## Main changes from upstream

- One assembly (`VolumetricFXReconfigured`), one GameData folder, one settings window (the ground settings are a page).
- `VolumetricExplosionFX/Source/Core/Reconfig.cs`: explosion speed, glow time, smoke half-life and limits, read by
  `VolumeEvolution` (fireball/smoke on a faster clock, shock and dust on real time) and `BlastParticles`.
- `VisualEnergy`/`Residue`: what explodes and burns by part kind; `BlastParticles`: dust for non-exploding parts,
  faint smoke around fireballs; `HeroVolume.shader` `_Reconf`: glow at the plume's top and edges.
- `CombatBridge.WillDetonate`: BDArmory warheads only explode in full when BDArmory sets them off.
- Nuclear air bursts: `BlastShape` (2.5 fireball radii), `VolumeEvolution.SimLobe` (cap cut), `NukeCloud` (dust ring).
- VGFX: jet engines (`EngineSampler.IsAirBreathing`) and launch-pad latching; `Physics.cfg` `jetDustScale`,
  `jetCloudRadiusScale`.

## Build requirements

- .NET SDK (8+) and KSP 1.12.5's `KSP_x64_Data/Managed` folder for the plugin.
- Unity **2019.4.18f1** with Windows Build Support (Mono) to rebuild the shader bundles
  (the target is DirectX 11, Windows KSP).
- Python 3 for the build and packaging tools.
