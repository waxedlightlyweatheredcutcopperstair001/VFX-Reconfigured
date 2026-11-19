"""Read config.local.json, forgiving plain Windows paths (C:\\Games\\KSP) and a byte-order mark (Notepad)."""
import json, re, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(required=True):
    p = ROOT / 'config.local.json'
    if not p.exists():
        if required:
            sys.exit('Create config.local.json (copy config.example.json and set your paths).')
        return {}
    text = p.read_text(encoding='utf-8-sig')
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        # Single backslashes (C:\Users\...) aren't valid JSON escapes: read them as path separators.
        fixed = re.sub(r'\\(?![\\"/])', r'/', text)
        try:
            return json.loads(fixed)
        except json.JSONDecodeError as e:
            sys.exit(f'config.local.json is not valid JSON ({e}). Example:\n' + (ROOT / 'config.example.json').read_text())
