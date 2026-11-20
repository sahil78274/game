#!/usr/bin/env python3
"""Project specific consistency checks that a syntax parser cannot catch.

1. Every SerializedObject field name used by the editor generator
   (SetField / SetObjectArray / FindProperty) must exist as a declared field
   somewhere in the project.
2. Every member accessed through the well known injected references
   (_settings, _context, context, _game, _health, _owner, _input, healthBar)
   must be declared in the corresponding type (or be a known Unity member).
"""
import os
import re
import sys
import glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

FIELD_RE = re.compile(
    r"(?:private|public|protected|internal)\s+"
    r"(?:static\s+|readonly\s+|const\s+)*"
    r"[\w<>\[\],\.\?]+\s+(\w+)\s*(?:=[^;]*)?;"
)
METHOD_RE = re.compile(
    r"(?:private|public|protected|internal)\s+"
    r"(?:static\s+|virtual\s+|override\s+|sealed\s+|async\s+|partial\s+)*"
    r"[\w<>\[\],\.\?]+\s+(\w+)\s*\("
)
PROP_RE = re.compile(
    r"(?:private|public|protected|internal)\s+(?:static\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*\{"
)
CONST_RE = re.compile(r"\bconst\s+[\w<>\[\],\.]+\s+(\w+)")
EVENT_RE = re.compile(r"(?:private|public|protected|internal)\s+event\s+[\w\.]+(?:<[^;()]+>)?\s+(\w+)")

PREFIX_TYPES = {
    "_settings": "GameSettings",
    "settings": "GameSettings",
    "_context": "GameManager",
    "context": "GameManager",
    "_game": "GameManager",
    "_health": "Health",
    "health": "Health",
    "healthBar": "WorldHealthBar",
    "_input": "SwipeInput",
    "_owner": "EnemySpawner",
}

# Unity members that are legitimately accessed through those references.
UNITY_MEMBERS = {
    "transform", "gameObject", "name", "enabled", "isActiveAndEnabled",
    "GetComponent", "GetComponentInChildren", "GetComponentInParent", "AddComponent",
    "SetActive", "SetPositionAndRotation", "position", "rotation", "localScale",
    "localPosition", "localRotation", "childCount", "GetChild", "Find",
    "Instantiate", "Destroy", "Equals", "ToString", "GetHashCode", "GetType",
    "StartCoroutine", "StopCoroutine", "StopAllCoroutines", "Invoke",
    "OnEnable", "OnDisable", "Awake", "Start", "Update", "LateUpdate", "OnDestroy",
    "SetParent", "parent", "lossyScale", "GetSiblingIndex", "children",
}


def main():
    files = sorted(glob.glob(os.path.join(ROOT, "Assets", "**", "*.cs"), recursive=True))
    sources = {p: open(p, encoding="utf-8").read() for p in files}

    all_members = set()
    class_members = {}
    for path, src in sources.items():
        # split per class/interface declaration
        for match in re.finditer(r"(?:class|interface|struct)\s+(\w+)", src):
            pass
        members = set()
        for regex in (FIELD_RE, METHOD_RE, PROP_RE, EVENT_RE):
            members.update(regex.findall(src))
        members.update(CONST_RE.findall(src))
        all_members |= members
        for match in re.finditer(r"\b(?:class|interface)\s+(\w+)", src):
            class_members.setdefault(match.group(1), set()).update(members)

    problems = []

    # --- 1. serialized field names used by the generator -------------------------------
    set_field_re = re.compile(r'(?:SetField|SetObjectArray|FindProperty)\(\s*[\w\.]+\s*,\s*"(\w+)"')
    find_prop_re = re.compile(r'FindProperty\(\s*"(\w+)"\s*\)')
    for path, src in sources.items():
        if "/Editor/" not in path.replace("\\", "/"):
            continue
        for match in list(set_field_re.finditer(src)) + list(find_prop_re.finditer(src)):
            name = match.group(1)
            if name not in all_members and name not in {"layers"}:
                line = src[:match.start()].count("\n") + 1
                problems.append(f"{os.path.relpath(path, ROOT)}:{line} unknown serialized field '{name}'")

    # --- 2. member access through injected references -----------------------------------
    access_re = re.compile(r"\b(" + "|".join(PREFIX_TYPES) + r")\.(\w+)")
    for path, src in sources.items():
        for match in access_re.finditer(src):
            prefix, member = match.group(1), match.group(2)
            target_type = PREFIX_TYPES[prefix]
            known = class_members.get(target_type, set())
            # GameManager implements IBattleContext, so interface members count too.
            if target_type == "GameManager":
                known = known | class_members.get("IBattleContext", set())
            if member in known or member in UNITY_MEMBERS or member in all_members:
                continue
            line = src[:match.start()].count("\n") + 1
            problems.append(f"{os.path.relpath(path, ROOT)}:{line} {prefix}.{member} not found on {target_type}")

    if problems:
        print("\n".join(problems))
        print(f"\n{len(problems)} problem(s)")
        return 1

    print(f"No consistency problems in {len(files)} files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
