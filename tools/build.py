#!/usr/bin/env python3
"""Build Volumetric FX Reconfigured's plugin: one DLL from both upstream source trees.

Usage: python3 tools/build.py            (KSP's Managed folder from config.local.json "kspRoot", or KSP_ROOT)
Output: build/VolumetricFXReconfigured.dll
"""
import json, os, subprocess, sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import localconfig

ROOT = Path(__file__).resolve().parents[1]
NAME = 'VolumetricFXReconfigured'
SOURCE_TREES = [ROOT / 'src', ROOT / 'VolumetricExplosionFX' / 'Source', ROOT / 'VolumetricGroundFX' / 'Source' / 'GroundBlastFx']
EXCLUDE = {
    'VolumetricExplosionFX/Source/Properties/AssemblyInfo.cs',       # replaced by src/AssemblyInfo.cs
    'VolumetricGroundFX/Source/GroundBlastFx/Core/AssemblyInfo.cs',  # replaced by src/AssemblyInfo.cs
    'VolumetricGroundFX/Source/GroundBlastFx/Core/DevAutomation.cs', # upstream dev tooling, never shipped
}


def ksp_managed():
    cfg = localconfig.load(required=False)
    ksp = os.environ.get('KSP_ROOT') or cfg.get('kspRoot')
    if not ksp:
        sys.exit('Set KSP_ROOT or "kspRoot" in config.local.json (a KSP 1.12.5 folder containing KSP_x64_Data/Managed).')
    managed = Path(ksp).expanduser() / 'KSP_x64_Data' / 'Managed'
    if not (managed / 'Assembly-CSharp.dll').exists():
        sys.exit(f'KSP assemblies not found in {managed}')
    return managed


def compiler():
    dotnet = os.environ.get('DOTNET') or (str(Path.home() / '.dotnet' / 'dotnet') if (Path.home() / '.dotnet' / 'dotnet').exists() else 'dotnet')
    sdks = subprocess.check_output([dotnet, '--list-sdks'], text=True).strip().splitlines()
    version, base = sdks[-1].split(' ', 1)
    return dotnet, Path(base.strip('[]')) / version / 'Roslyn' / 'bincore' / 'csc.dll'


def sources():
    files = []
    for tree in SOURCE_TREES:
        for p in sorted(tree.rglob('*.cs')):
            rel = p.relative_to(ROOT).as_posix()
            if rel in EXCLUDE or '/obj/' in rel or '/bin/' in rel:
                continue
            files.append(p)
    return files


def main():
    managed = ksp_managed()
    dotnet, csc = compiler()
    out = ROOT / 'build'
    out.mkdir(exist_ok=True)
    refs = [managed / f'{n}.dll' for n in ['mscorlib', 'System', 'System.Core', 'System.Xml', 'Assembly-CSharp', 'Assembly-CSharp-firstpass']]
    refs += sorted(managed.glob('UnityEngine*.dll'))
    args = [dotnet, str(csc), '/noconfig', '/nostdlib+', '/nologo', '/langversion:9.0', '/target:library', '/optimize+',
            '/deterministic+', '/nowarn:1591', f'/out:{out / (NAME + ".dll")}']
    args += [f'/reference:{r}' for r in refs]
    args += [str(s) for s in sources()]
    rsp = out / 'csc.rsp'
    rsp.write_text('\n'.join(f'"{a}"' for a in args[3:]) + '\n')
    result = subprocess.run([dotnet, str(csc), '/noconfig', f'@{rsp}'], cwd=ROOT, text=True, capture_output=True)
    lines = [l for l in (result.stdout + result.stderr).splitlines() if l.strip()]
    errors = [l for l in lines if ': error ' in l]
    for l in lines:
        print(l.replace(str(ROOT) + '/', ''))
    if result.returncode != 0:
        sys.exit(f'Build failed: {len(errors)} error(s).')
    print(f'Built build/{NAME}.dll ({len(sources())} source files).')


if __name__ == '__main__':
    main()
