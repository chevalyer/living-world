#!/usr/bin/env python3
"""Dependency-free structural checks. This is not a replacement for dotnet build."""
from pathlib import Path
import json, re, sys
import xml.etree.ElementTree as ET
from csharp_lex import validate

ROOT = Path(__file__).resolve().parents[1]
errors = []
checks = 0

def check(condition, message):
    global checks
    checks += 1
    if not condition: errors.append(message)

files = sorted(p for p in ROOT.rglob("*.cs") if not {"obj", "bin", ".godot"}.intersection(p.relative_to(ROOT).parts))
for path in files:
    try: validate(path.read_text(encoding="utf-8")); check(True, "")
    except ValueError as ex: check(False, str(path.relative_to(ROOT)) + ": " + str(ex))
for project in ROOT.rglob("*.csproj"):
    tree = ET.parse(project)
    for reference in tree.iter("ProjectReference"):
        check((project.parent / reference.attrib["Include"]).exists(), "missing reference: " + reference.attrib["Include"])
ET.parse(ROOT / "Directory.Build.props")
ET.parse(ROOT / "NuGet.Config")

data = {p.stem: json.loads(p.read_text(encoding="utf-8")) for p in (ROOT / "Definitions/Data").glob("*.json")}
for kind in ("materials", "items", "plants", "recipes", "buildings"):
    identifiers = [entry["id"] for entry in data[kind]]
    check(len(identifiers) == len(set(identifiers)), "duplicate " + kind + " id")
    for identity in identifiers: check(bool(re.fullmatch(r"[a-z][a-z0-9_]*", identity)), "unstable id: " + identity)
materials = {d["id"] for d in data["materials"]}
items = {d["id"]: d for d in data["items"]}
for item in items.values():
    check(item["material"] in materials, "unknown material: " + item["id"])
    check(item["mass"] > 0 and item["volume"] > 0, "invalid physical size: " + item["id"])
for plant in data["plants"]:
    check(plant["product"] in items, "unknown yield: " + plant["id"])
    check(plant["growthDays"] > 0 and plant["regrowthDays"] > 0, "invalid growth period: " + plant["id"])
for recipe in data["recipes"]:
    check(recipe["output"] in items, "unknown recipe output: " + recipe["id"])
    for ingredient, count in recipe["inputs"].items(): check(ingredient in items and count > 0, "invalid recipe input: " + recipe["id"])
for building in data["buildings"]:
    check(building["material"] in materials and building["resource"] in items, "invalid building references")
names = data["names"]
check(not {"male", "female", "surnames"}.intersection(names), "word-list name generation still present")
for kind in ("vowels", "consonants"):
    alphabet = names[kind]
    check(len(alphabet) >= 3 and alphabet.isalpha() and "ё" not in alphabet.lower() and len(set(alphabet)) == len(alphabet), "invalid name alphabet")
check(not set(names["vowels"]).intersection(names["consonants"]), "name alphabet overlaps")
check(bool(names["patterns"]) and all(1 <= len(p) <= 4 and "V" in p and set(p) <= {"C", "V"} for p in names["patterns"]), "invalid syllable patterns")
for part in ("first", "last"):
    check(1 <= names[part+"MinSyllables"] <= names[part+"MaxSyllables"] <= 6, "invalid syllable count")

component_ids = []
actions = []
for path in files:
    text = path.read_text(encoding="utf-8")
    if "Simulation" in path.parts or "Definitions" in path.parts:
        check(not re.search(r"\busing\s+Godot\b", text), "engine dependency in " + str(path))
    component_ids.extend(re.findall(r'\[Component\("([^"\n]+)"\)\]', text))
    if path.parent.name == "Actions":
        match = re.search(r'public sealed class (\w+) : SimAction', text)
        if match:
            identifier = re.search(r'Id\s*=>\s*"([^"\n]+)"', text)
            check(identifier is not None, "action id missing: " + path.name)
            if identifier: actions.append((match[1], identifier[1]))
check(len(component_ids) == len(set(component_ids)), "duplicate component save id")
check(len(actions) == len({identity for _, identity in actions}), "duplicate action id")
session = (ROOT / "Simulation/Core/SimulationSession.cs").read_text()
for action, _ in actions: check(bool(re.search(r"new\s+" + action + r"\s*\(", session)), "unregistered action: " + action)
root_project = ET.parse(ROOT / "LivingWorld.csproj")
check(root_project.findtext("PropertyGroup/EnableDefaultCompileItems") == "false", "recursive engine compilation enabled")
scene = (ROOT / "Presentation/Main.tscn").read_text()
for path in re.findall(r'path="res://([^"\n]+)"', scene): check((ROOT / path).exists(), "missing scene resource " + path)

for name in ("GameRoot.cs", "CameraRig.cs", "Rendering/WorldView.cs", "Rendering/TerrainTexture.cs", "UI/SimulationHud.cs"):
    text = (ROOT / "Presentation" / name).read_text()
    check("Game.Session" not in text and "session.State" not in text, "live simulation accessed by renderer: " + name)
runner = (ROOT / "Presentation/Runtime/SimulationRunner.cs").read_text()
check("Godot." not in runner, "Godot called on simulation worker")
check("Thread" in runner and "Volatile.Write" in runner, "worker or publication missing")
report = {
    "structural_checks": checks,
    "structural_status": "passed" if not errors else "failed",
    "csharp_files": len(files),
    "actions": len(actions),
    "component_ids": len(component_ids),
    "regression_cases_authored": (ROOT / "Tests/TestSuite.cs").read_text().count('Test("'),
    "csharp_compilation": "not_part_of_structural_check",
    "regression_execution": "not_part_of_structural_check",
    "godot_visual_qa": "not_part_of_structural_check",
    "errors": errors
}
print(json.dumps(report, ensure_ascii=False, indent=2))
if "--report" in sys.argv:
    (ROOT / "docs/source-check-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
sys.exit(bool(errors))
