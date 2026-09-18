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

version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
check(bool(re.fullmatch(r"\d+\.\d+\.\d+(?:-(?:alpha|beta|rc\d+))?", version)), "invalid VERSION")
project_text = (ROOT / "project.godot").read_text(encoding="utf-8")
readme_text = (ROOT / "README.md").read_text(encoding="utf-8")
check(f'config/version="{version}"' in project_text, "project.godot version differs from VERSION")
check(f"версия: **{version}**" in readme_text, "README version differs from VERSION")

DATA = ROOT / "Definitions/Data"

def load_group(name):
    files = sorted((DATA / name).rglob("*.json"))
    check(bool(files), "missing definition group: " + name)
    result = []
    for path in files:
        value = json.loads(path.read_text(encoding="utf-8"))
        check(isinstance(value, dict), "definition file must contain one object: " + str(path.relative_to(ROOT)))
        if not isinstance(value, dict):
            continue
        result.append(value)
        if "id" in value:
            check(path.stem == value["id"], "definition id does not match filename: " + str(path.relative_to(ROOT)))
    return result

materials_list = load_group("Materials")
items_list = load_group("Items")
plants_list = load_group("Plants")
buildings_list = load_group("Buildings")
name_files = sorted((DATA / "Names").rglob("*.json"))
check(len(name_files) == 1, "Names must contain exactly one json file")
names = json.loads(name_files[0].read_text(encoding="utf-8")) if name_files else {}

recipes_list = []
for item in items_list:
    for recipe in item.get("recipes", []):
        check("output" not in recipe, "embedded recipe declares output: " + item.get("id", "<unknown>"))
        value = dict(recipe)
        value["output"] = item["id"]
        recipes_list.append(value)

data = {
    "materials": materials_list,
    "items": items_list,
    "plants": plants_list,
    "recipes": recipes_list,
    "buildings": buildings_list,
    "names": names,
}

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
