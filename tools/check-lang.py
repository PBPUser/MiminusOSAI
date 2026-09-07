#!/usr/bin/env python3
"""Validates the translation catalogues against the source.

Reports three kinds of problem:
  * keys used by L.T / L.F that no catalogue defines,
  * keys defined in one language but not the other,
  * keys defined but never used (orphans),
plus a mismatch in {0}-style placeholders between the two languages, which
would make string.Format throw or silently drop an argument.

Run from the repository root:  python tools/check-lang.py
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src")
LANG = os.path.join(SRC, "Core", "lang")

USE = re.compile(r'\bL\.[TF]\("([a-z][a-z0-9_]*\.[a-z0-9_]+)"')
PLACEHOLDER = re.compile(r"\{(\d+)")


def source_keys():
    keys = {}
    for base, dirs, names in os.walk(SRC):
        dirs[:] = [d for d in dirs if d not in ("obj", "bin", "lang")]
        for n in names:
            if not n.endswith(".cs"):
                continue
            path = os.path.join(base, n)
            text = open(path, encoding="utf-8").read()
            for m in USE.finditer(text):
                keys.setdefault(m.group(1), set()).add(os.path.relpath(path, ROOT))
    return keys


def dynamic_key_fields():
    """Keys that reach L.T through a variable (node names, table rows) are
    recognised by the literal that assigns them."""
    pat = re.compile(r'(?:NameKey|TextKey|TooltipKey|LabelKey|SubKey|DescKey)\s*=\s*"([^"]+)"'
                     r'|FolderKey\("([^"]+)"\)'
                     r'|ByKey\([^,]+,\s*"([^"]+)"\)')
    tuple_pat = re.compile(r'"((?:icon|task|tray|taskbar|paint|sheet|site|start|cpl|sound|unit|track|player|props|about|openwith|theme|newitem)\.[a-z0-9_]+)"')
    found = set()
    for base, dirs, names in os.walk(SRC):
        dirs[:] = [d for d in dirs if d not in ("obj", "bin", "lang")]
        for n in names:
            if not n.endswith(".cs"):
                continue
            text = open(os.path.join(base, n), encoding="utf-8").read()
            for groups in pat.findall(text):
                found.update(g for g in groups if g)
            found.update(tuple_pat.findall(text))
    return found


def main():
    ru = json.load(open(os.path.join(LANG, "ru.json"), encoding="utf-8"))
    en = json.load(open(os.path.join(LANG, "en.json"), encoding="utf-8"))

    used = source_keys()
    used_names = set(used) | dynamic_key_fields()
    problems = 0

    missing = sorted(k for k in used_names if k not in ru and k not in en)
    if missing:
        problems += len(missing)
        print(f"MISSING ({len(missing)}): used in code, absent from both catalogues")
        for k in missing[:40]:
            where = ", ".join(sorted(used.get(k, {"(dynamic)"})))
            print(f"  {k}   <- {where}")

    only_ru = sorted(set(ru) - set(en))
    only_en = sorted(set(en) - set(ru))
    if only_ru or only_en:
        problems += len(only_ru) + len(only_en)
        print(f"UNTRANSLATED: {len(only_ru)} ru-only, {len(only_en)} en-only")
        for k in (only_ru + only_en)[:40]:
            print("  " + k)

    bad_fmt = []
    for k in sorted(set(ru) & set(en)):
        a = set(PLACEHOLDER.findall(ru[k]))
        b = set(PLACEHOLDER.findall(en[k]))
        if a != b:
            bad_fmt.append((k, sorted(a), sorted(b)))
    if bad_fmt:
        problems += len(bad_fmt)
        print(f"PLACEHOLDER MISMATCH ({len(bad_fmt)}):")
        for k, a, b in bad_fmt[:20]:
            print(f"  {k}: ru{a} vs en{b}")

    orphans = sorted(set(ru) - used_names)
    if orphans:
        print(f"orphans ({len(orphans)}): defined but never referenced")
        for k in orphans[:30]:
            print("  " + k)

    print(f"\n{len(ru)} keys, {len(used)} referenced literally, "
          f"{len(used_names)} referenced in total, {problems} problems")
    return 1 if problems else 0


sys.exit(main())
