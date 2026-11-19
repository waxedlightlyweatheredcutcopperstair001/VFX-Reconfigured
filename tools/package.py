#!/usr/bin/env python3
"""Assemble GameData/VolumetricFXReconfigured from both mods' files, the built DLL and the shader bundles.

Usage:
    python3 tools/package.py              build/GameData/VolumetricFXReconfigured + release/VolumetricFXReconfigured-<version>.zip
    python3 tools/package.py --deploy     ...and copy it into the KSP install from config.local.json ("kspRoot")

Layout of the package:
    Plugins/VolumetricFXReconfigured.dll       the plugin (tools/build.py)
    Assets/vefx-windows.unity3d                explosion shaders (VolumetricExplosionFX/Tools/shaders.py --target windows)
    Shaders/GroundBlastFx.unity3d              ground effect shaders (shipped prebuilt by Volumetric Ground FX)
    Configs/Explosions.cfg                     explosion defaults (VOLUMETRIC_EXPLOSION_FX node)
    Configs/Ground/*.cfg                       ground effect physics, surfaces, sites, engines, strings
    Localization/Explosions/*.cfg, Localization/Ground/*.cfg
    PluginData/Ground/DefaultSettings.cfg      ground effect defaults (player settings are written next to it)
    LICENSE, CREDITS.md, README.md, CHANGELOG.md
"""
import argparse, json, re, shutil, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAME = 'VolumetricFXReconfigured'
VEFX = ROOT / 'VolumetricExplosionFX'
VGFX = ROOT / 'VolumetricGroundFX'
VEFX_DATA = VEFX / 'GameData' / 'VolumetricExplosionFX'
VGFX_DATA = VGFX / 'GameData' / 'Volumetric Ground FX'
OVERLAY = ROOT / 'package'   # hand-written files shipped in the package (README, CHANGELOG)


def version():
    m = re.search(r'AssemblyInformationalVersion\("([^"]+)"\)', (ROOT / 'src' / 'AssemblyInfo.cs').read_text())
    return m.group(1) if m else '0.0.0'


def copy(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)


def assemble(out, allow_missing_bundle):
    shutil.rmtree(out, ignore_errors=True)
    dll = ROOT / 'build' / (NAME + '.dll')
    if not dll.exists():
        sys.exit('build/' + NAME + '.dll not found: run tools/build.py first.')
    copy(dll, out / 'Plugins' / dll.name)

    # Explosion shader bundles (built by VEFX's shader tool into VolumetricExplosionFX/build/shader-bundles/<target>).
    bundles = []
    for target in ('windows', 'mac'):
        b = VEFX / 'build' / 'shader-bundles' / target / f'vefx-{target}.unity3d'
        if b.exists() and b.read_bytes()[:7] == b'UnityFS':
            copy(b, out / 'Assets' / b.name)
            bundles.append(target)
    if 'windows' not in bundles:
        msg = 'No explosion shader bundle (VolumetricExplosionFX/build/shader-bundles/windows/vefx-windows.unity3d): ' \
              'run tools/make.py on Windows (it builds it with Unity 2019.4.18f1).'
        if not allow_missing_bundle:
            sys.exit(msg)
        print('WARNING: ' + msg + ' Packaging without it: explosions fall back to plain particles.')

    copy(VGFX_DATA / 'Shaders' / 'GroundBlastFx.unity3d', out / 'Shaders' / 'GroundBlastFx.unity3d')
    copy(VEFX_DATA / 'Settings.cfg', out / 'Configs' / 'Explosions.cfg')
    for cfg in sorted((VGFX_DATA / 'Configs').glob('*.cfg')):
        copy(cfg, out / 'Configs' / 'Ground' / cfg.name)
    for cfg in sorted((VEFX_DATA / 'Localization').glob('*.cfg')):
        copy(cfg, out / 'Localization' / 'Explosions' / cfg.name)
    for cfg in sorted((VGFX_DATA / 'Localization').glob('*.cfg')):
        copy(cfg, out / 'Localization' / 'Ground' / cfg.name)
    copy(VGFX_DATA / 'PluginData' / 'DefaultSettings.cfg', out / 'PluginData' / 'Ground' / 'DefaultSettings.cfg')

    copy(ROOT / 'LICENSE', out / 'LICENSE')
    credits = '# Credits\n\n'
    credits += (VEFX / 'CREDITS.md').read_text(encoding='utf-8').strip() + '\n\n'
    credits += '---\n\n' + (VGFX_DATA / 'CREDITS.md').read_text(encoding='utf-8').strip() + '\n'
    (out / 'CREDITS.md').write_text(credits, encoding='utf-8')
    if OVERLAY.exists():
        for f in OVERLAY.rglob('*'):
            if f.is_file():
                copy(f, out / f.relative_to(OVERLAY))
    return bundles


def make_zip(out, ver):
    release = ROOT / 'release'
    release.mkdir(exist_ok=True)
    z = release / f'{NAME}-{ver}.zip'
    if z.exists():
        z.unlink()
    with zipfile.ZipFile(z, 'w', zipfile.ZIP_DEFLATED) as zf:
        for f in sorted(out.rglob('*')):
            if f.is_file():
                zf.write(f, (Path(NAME) / f.relative_to(out)).as_posix())
    return z


def deploy(out):
    cfg_path = ROOT / 'config.local.json'
    cfg = json.loads(cfg_path.read_text()) if cfg_path.exists() else {}
    ksp = cfg.get('kspRoot')
    if not ksp or not (Path(ksp) / 'GameData').is_dir():
        sys.exit('Set "kspRoot" in config.local.json to your KSP folder (the one containing GameData) to deploy.')
    gamedata = Path(ksp) / 'GameData'
    for old in ('VolumetricExplosionFX', 'Volumetric Ground FX', 'GroundBlastFx'):
        if (gamedata / old).exists():
            print(f'WARNING: GameData/{old} is still installed. VFX Reconfigured replaces it: remove that folder, '
                  'or every effect is drawn twice.')
    target = gamedata / NAME
    # Keep the player's own settings (PluginData) across deploys.
    keep = {}
    for rel in ('PluginData/Explosions', 'PluginData/Ground'):
        d = target / rel
        if d.exists():
            for f in d.rglob('*'):
                if f.is_file() and f.name != 'DefaultSettings.cfg':
                    keep[f.relative_to(target)] = f.read_bytes()
    shutil.rmtree(target, ignore_errors=True)
    shutil.copytree(out, target)
    for rel, data in keep.items():
        (target / rel).parent.mkdir(parents=True, exist_ok=True)
        (target / rel).write_bytes(data)
    print(f'Deployed to {target}' + (f' (kept {len(keep)} settings file(s))' if keep else ''))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--deploy', action='store_true', help='copy the package into the KSP install (config.local.json kspRoot)')
    p.add_argument('--allow-missing-bundle', action='store_true', help='package without the explosion shader bundle (test only)')
    a = p.parse_args()
    out = ROOT / 'build' / 'GameData' / NAME
    bundles = assemble(out, a.allow_missing_bundle)
    ver = version()
    z = make_zip(out, ver)
    files = sum(1 for f in out.rglob('*') if f.is_file())
    print(f'Packaged {NAME} {ver}: {files} files, shader bundles: {", ".join(bundles) or "none"}; {z.relative_to(ROOT)}')
    if a.deploy:
        deploy(out)


if __name__ == '__main__':
    main()
