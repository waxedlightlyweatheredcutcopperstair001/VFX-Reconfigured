#!/usr/bin/env python3
"""Build everything: explosion shaders (Unity), the plugin, the tests, the package; optionally install into KSP.

Usage (Windows: "py tools\\make.py ..."; elsewhere "python3 tools/make.py ..."):
    make.py                 shaders + plugin + tests + package (release/VolumetricFXReconfigured-<version>.zip)
    make.py --deploy        ...and install into the KSP folder in config.local.json
    make.py --no-shaders    skip the Unity shader build (reuse the last bundle)
    make.py --preview       also render explosion preview sheets (VolumetricExplosionFX/work/preview)

config.local.json (next to this repository's README; not committed):
    { "kspRoot": "C:/Path/To/Kerbal Space Program",
      "unityEditor": "C:/Program Files/Unity/Hub/Editor/2019.4.18f1/Editor/Unity.exe" }

Needs: Python 3 with numpy, the .NET SDK (8 or newer), Unity 2019.4.18f1 with Windows Build Support (shaders only).
"""
import argparse, json, os, shutil, subprocess, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]
VEFX = ROOT / 'VolumetricExplosionFX'


def config():
    return localconfig.load()


def run(args, cwd=ROOT, env=None):
    print('>', ' '.join(str(a) for a in args), flush=True)
    r = subprocess.run([str(a) for a in args], cwd=cwd, env=env)
    if r.returncode != 0:
        sys.exit(f'Step failed (exit code {r.returncode}).')


def dotnet():
    home = Path.home() / '.dotnet' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    return str(home) if home.exists() else (shutil.which('dotnet') or sys.exit('The .NET SDK (dotnet) was not found.'))


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--deploy', action='store_true')
    p.add_argument('--no-shaders', action='store_true')
    p.add_argument('--no-tests', action='store_true')
    p.add_argument('--preview', action='store_true')
    a = p.parse_args()
    cfg = config()
    py = sys.executable

    if not a.no_shaders:
        editor = cfg.get('unityEditor')
        if not editor or not Path(editor).is_file():
            sys.exit('Set "unityEditor" in config.local.json to Unity 2019.4.18f1 (Unity.exe), or use --no-shaders.')
        # VEFX's own tool: bakes the fireball simulation (numpy) and builds Assets/vefx-windows.unity3d in a scratch
        # Unity project (VolumetricExplosionFX/work, not committed). It also regenerates Source/Core/FireSimTable.cs.
        run([py, VEFX / 'Tools' / 'shaders.py', '--target', 'windows', '--unity-editor', editor], cwd=VEFX)
        if a.preview:
            run([py, VEFX / 'Tools' / 'shaders.py', '--target', 'windows', '--unity-editor', editor, '--preview'], cwd=VEFX)
            print('Preview sheets: ' + str(VEFX / 'work' / 'preview'))

    run([py, ROOT / 'tools' / 'build.py'])

    if not a.no_tests:
        env = dict(os.environ, DOTNET_ROLL_FORWARD='Major')
        run([dotnet(), 'run', '--project', VEFX / 'Tests' / 'VolumetricExplosionFX.Tests.csproj', '--configuration', 'Release'], env=env)

    run([py, ROOT / 'tools' / 'package.py'] + (['--deploy'] if a.deploy else []))


if __name__ == '__main__':
    main()
