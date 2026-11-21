#!/usr/bin/env python3
"""Publish a GitHub Release of VFX Reconfigured from the current tagged commit (run on Windows: the shader bundle is
built there).

Usage (Windows: "py tools\\release.py ..."):
    release.py                  build everything (tools/make.py), then publish release vX.Y.Z with the zip
    release.py --no-build       publish the zip already in release/ (built from this commit)
    release.py --no-shaders     build without the Unity step (reuse the last shader bundle)
    release.py --draft          create the release as a draft (publish it later on GitHub)
    release.py --yes            don't ask for confirmation

It checks first that:
  - the working tree is clean and HEAD is the commit tagged vX.Y.Z (X.Y.Z: the version in src/AssemblyInfo.cs), or a
    later one that only changes this script or the repo README, and the tag is on GitHub;
  - the zip holds the Windows explosion shader bundle;
  - there's no release for that tag yet.
The release notes are that version's section of package/CHANGELOG.md.

Needs the GitHub CLI (gh), logged in with a token that can write the repository's contents
(winget install GitHub.cli, then gh auth login).
"""
import argparse, re, shutil, subprocess, sys, tempfile, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAME = 'VolumetricFXReconfigured'
REPO = 'waxedlightlyweatheredcutcopperstair001/VFX-Reconfigured'
UNSHIPPED = {'tools/release.py', 'README.md'}   # changes after a tag that don't change the released mod


def out(args):
    r = subprocess.run(args, cwd=ROOT, capture_output=True, text=True)
    return r.returncode, r.stdout.strip(), r.stderr.strip()


def fail(msg):
    sys.exit('Release stopped: ' + msg)


def version():
    m = re.search(r'AssemblyInformationalVersion\("([^"]+)"\)', (ROOT / 'src' / 'AssemblyInfo.cs').read_text())
    return m.group(1) if m else fail('no version in src/AssemblyInfo.cs')


def notes(ver):
    text = (ROOT / 'package' / 'CHANGELOG.md').read_text(encoding='utf-8')
    m = re.search(r'^## ' + re.escape(ver) + r'\s*\n(.*?)(?=^## |\Z)', text, re.S | re.M)
    if not m or not m.group(1).strip():
        fail(f'no "## {ver}" section in package/CHANGELOG.md')
    return m.group(1).strip() + '\n'


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--no-build', action='store_true')
    p.add_argument('--no-shaders', action='store_true')
    p.add_argument('--draft', action='store_true')
    p.add_argument('--yes', action='store_true')
    a = p.parse_args()

    gh = shutil.which('gh') or fail('the GitHub CLI (gh) was not found: winget install GitHub.cli, then gh auth login')
    if out([gh, 'auth', 'status'])[0] != 0:
        fail('gh is not logged in: run gh auth login')

    ver = version(); tag = 'v' + ver
    code, status, _ = out(['git', 'status', '--porcelain', '--untracked-files=no'])
    if code != 0 or status:
        fail('the working tree has uncommitted changes:\n' + status)
    head = out(['git', 'rev-parse', 'HEAD'])[1]
    code, tagged, _ = out(['git', 'rev-list', '-n', '1', tag])
    if code != 0:
        fail(f'there is no tag {tag} (the version in src/AssemblyInfo.cs) here: git pull --tags, or tag this version first')
    if tagged != head:
        # Commits after the tag are fine if they change nothing that is shipped (this script, the repo's README).
        code, changed, _ = out(['git', 'diff', '--name-only', tag, 'HEAD'])
        shipped = [f for f in changed.splitlines() if f and f not in UNSHIPPED]
        if code != 0 or out(['git', 'merge-base', '--is-ancestor', tag, 'HEAD'])[0] != 0 or shipped:
            fail(f'HEAD is not version {tag}: files changed since the tag: {", ".join(shipped) or "?"}. '
                 f'Tag the new version first, or check out {tag}.')
    if not out(['git', 'ls-remote', '--tags', 'origin', 'refs/tags/' + tag])[1]:
        fail(f'tag {tag} is not on GitHub: git push origin {tag}')
    if out([gh, 'release', 'view', tag, '-R', REPO])[0] == 0:
        fail(f'release {tag} already exists on GitHub')
    body = notes(ver)

    if not a.no_build:
        args = [sys.executable, str(ROOT / 'tools' / 'make.py')] + (['--no-shaders'] if a.no_shaders else [])
        print('>', ' '.join(args), flush=True)
        if subprocess.run(args, cwd=ROOT).returncode != 0:
            fail('the build failed')
    z = ROOT / 'release' / f'{NAME}-{ver}.zip'
    if not z.exists():
        fail(f'{z.relative_to(ROOT)} not found: build it (run without --no-build)')
    with zipfile.ZipFile(z) as zf:
        names = zf.namelist()
    if f'{NAME}/Assets/vefx-windows.unity3d' not in names:
        fail('the zip has no explosion shader bundle (Assets/vefx-windows.unity3d): build with the shaders')
    if f'{NAME}/Plugins/{NAME}.dll' not in names:
        fail('the zip has no plugin DLL')

    title = f'VFX Reconfigured {ver}'
    print(f'\n{title} ({tag}{", draft" if a.draft else ""})\n  {z.name}: {z.stat().st_size / 1e6:.1f} MB, {len(names)} files\n')
    print(body)
    if not a.yes and input('Publish this release on GitHub? [y/N] ').strip().lower() not in ('y', 'yes'):
        sys.exit('Not published.')
    with tempfile.NamedTemporaryFile('w', suffix='.md', delete=False, encoding='utf-8') as f:
        f.write(body); notes_file = f.name
    args = [gh, 'release', 'create', tag, str(z), '-R', REPO, '--verify-tag', '--title', title, '--notes-file', notes_file]
    if a.draft:
        args.append('--draft')
    r = subprocess.run(args, cwd=ROOT)
    Path(notes_file).unlink(missing_ok=True)
    if r.returncode != 0:
        fail('gh release create failed (see above)')
    print('Published.' if not a.draft else 'Draft created: publish it on GitHub when ready.')


if __name__ == '__main__':
    main()
