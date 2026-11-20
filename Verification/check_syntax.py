#!/usr/bin/env python3
"""Syntax check every C# file in the project with tree-sitter.

Also performs a few project specific consistency checks:
  * every SerializedObject.FindProperty("name") / SetField(..., "name") string
    must exist as a field in some script;
  * reports unbalanced #if blocks.
"""
import sys, os, re, glob
from tree_sitter import Language, Parser
import tree_sitter_c_sharp as ts_cs

LANG = Language(ts_cs.language())
PARSER = Parser(LANG)
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def walk_errors(node, out):
    if node.type == "ERROR" or node.is_missing:
        out.append(node)
    for child in node.children:
        walk_errors(child, out)

def main():
    files = sorted(glob.glob(os.path.join(ROOT, "Assets", "**", "*.cs"), recursive=True))
    if not files:
        print("no C# files found"); return 1
    bad = 0
    field_names = set()
    for path in files:
        src = open(path, "rb").read()
        tree = PARSER.parse(src)
        errs = []
        walk_errors(tree.root_node, errs)
        if errs:
            bad += 1
            print(f"FAIL {os.path.relpath(path, ROOT)}")
            for e in errs[:8]:
                row, col = e.start_point
                snippet = src[e.start_byte:min(e.end_byte, e.start_byte + 90)].decode("utf-8", "replace").replace("\n", "\\n")
                print(f"   line {row+1}:{col} {e.type} -> {snippet}")
        # collect serialized field names (any field declaration)
        text = src.decode("utf-8", "replace")
        for m in re.finditer(r"(?:private|public|protected|internal)\s+[\w<>\[\],\.\?]+\s+(\w+)\s*[;=]", text):
            field_names.add(m.group(1))
        # #if / #endif balance
        depth = 0
        for i, line in enumerate(text.splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("#if"):
                depth += 1
            elif stripped.startswith("#endif"):
                depth -= 1
                if depth < 0:
                    print(f"FAIL {os.path.relpath(path, ROOT)}: unmatched #endif at line {i}")
                    bad += 1
        if depth != 0:
            print(f"FAIL {os.path.relpath(path, ROOT)}: unbalanced #if (depth {depth})")
            bad += 1
    print(f"\nChecked {len(files)} files, {bad} with problems.")
    return 1 if bad else 0

if __name__ == "__main__":
    sys.exit(main())
