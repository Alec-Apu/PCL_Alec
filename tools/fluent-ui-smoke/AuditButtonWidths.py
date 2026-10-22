"""Audit repository-owned text buttons; icon-only hit targets keep their dimensions."""
from collections import Counter
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
TEXT_BUTTONS = {"MyButton", "MyIconTextButton", "MyExtraTextButton", "MyTextButton"}
WIDTHS = {"Width", "MinWidth", "MaxWidth"}
counts = Counter()
violations = []

for directory in (ROOT / "Plain Craft Launcher 2", ROOT / "PCL.Core"):
    for path in directory.rglob("*.xaml"):
        if any(part in {"bin", "obj", "publish"} for part in path.relative_to(directory).parts):
            continue
        for element in ET.parse(path).getroot().iter():
            kind = element.tag.rsplit("}", 1)[-1]
            if kind not in TEXT_BUTTONS and not (
                kind == "MyListItem" and element.get("IsNavigationChip") == "True"
            ):
                continue
            counts[kind] += 1
            constrained = WIDTHS.intersection(element.attrib)
            if constrained:
                violations.append(f"{path.relative_to(ROOT)}: {kind}: {sorted(constrained)}")

for path in (ROOT / "Plain Craft Launcher 2").rglob("*.cs"):
    if any(part in {"bin", "obj", "publish"} for part in path.parts):
        continue
    for match in re.finditer(r"new MyButton\s*\{([^}]+)\}", path.read_text(encoding="utf-8-sig")):
        counts["MyButton (code)"] += 1
        if re.search(r"\b(?:Width|MinWidth|MaxWidth)\s*=", match[1]):
            violations.append(f"{path.relative_to(ROOT)}: code-created MyButton width constraint")

dock_styles = ET.parse(ROOT / "Plain Craft Launcher 2/Resources/Fluent.xaml").getroot()
for style in dock_styles:
    if style.get("{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "Fluent.DockButton":
        counts["Dock style"] += 1
        for setter in style:
            if setter.get("Property") in WIDTHS:
                violations.append("Fluent.DockButton: fixed width constraint")

for kind, count in sorted(counts.items()):
    print(f"{kind}: {count}")
if violations:
    raise SystemExit("\n".join(violations))
print("PASS: repository text buttons and category chips use content-sized widths.")
