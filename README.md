# Volumetric FX Reconfigured: source and build

A Kerbal Space Program 1.12 mod that unifies **Volumetric Explosion FX** (VEFX) and
**Volumetric Ground FX** (VGFX) by Warnix into a single mod, with reworked explosions and ground
effects. Both originals are MIT licensed; see `LICENSE` and each folder's `CREDITS.md`.

Status: repository set up; no changes to the upstream code yet.

## Layout

| Path | What |
|---|---|
| `VolumetricExplosionFX/` | Volumetric Explosion FX 1.1.1, imported with its full history |
| `VolumetricGroundFX/` | Volumetric Ground FX 1.1.0, imported with its full history |
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

## Build requirements

- .NET SDK (8+) and KSP 1.12.5's `KSP_x64_Data/Managed` folder for the plugin.
- Unity **2019.4.18f1** with Windows Build Support (Mono) to rebuild the shader bundles
  (the target is DirectX 11, Windows KSP).
- Python 3 for the build and packaging tools.
