"""Check fixed XAML rows against the minimum height of shared text controls."""
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PRESENTATION = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
NAME = "{http://schemas.microsoft.com/winfx/2006/xaml}Name"
MINIMUM_HEIGHT = {"MyButton": 40, "MyComboBox": 40, "MyTextBox": 40}
violations = []
page_count = 0


def kind(element):
    return element.tag.rsplit("}", 1)[-1]


def fixed_height(value):
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


for path in (ROOT / "Plain Craft Launcher 2/Pages").rglob("*.xaml"):
    page = ET.parse(path).getroot()
    page_count += 1
    parents = {child: parent for parent in page.iter() for child in parent}
    for search in (node for node in page.iter() if kind(node) == "MySearchBox"):
        ancestor = parents.get(search)
        pinned = False
        while ancestor is not None:
            if kind(ancestor) == "MyScrollViewer.FixedHeader":
                pinned = True
            if kind(ancestor) in {"MyScrollViewer", "ScrollViewer"}:
                if not pinned:
                    violations.append(
                        f"{path.relative_to(ROOT)}: {search.get(NAME, 'search')} scrolls away with its results"
                    )
                break
            ancestor = parents.get(ancestor)
    for container in page.iter():
        height = fixed_height(container.get("Height"))
        if kind(container) in {"Grid", "StackPanel", "WrapPanel"} and height is not None:
            for child in container.iter():
                required = MINIMUM_HEIGHT.get(kind(child))
                if required and height < required:
                    violations.append(
                        f"{path.relative_to(ROOT)}: {container.get(NAME, kind(container))} "
                        f"fixes {height:g}px around {child.get(NAME, kind(child))}, which needs {required}px"
                    )
        if kind(container) == "WrapPanel":
            item_height = fixed_height(container.get("ItemHeight"))
            if item_height is not None:
                for child in container:
                    required = MINIMUM_HEIGHT.get(kind(child))
                    if required and item_height < required:
                        violations.append(f"{path.relative_to(ROOT)}: ItemHeight clips {kind(child)}")
        if kind(container) == "Grid":
            rows = container.find(PRESENTATION + "Grid.RowDefinitions")
            if rows is None:
                continue
            for child in container:
                required = MINIMUM_HEIGHT.get(kind(child))
                if required is None or child.get("Grid.RowSpan", "1") != "1":
                    continue
                row = int(child.get("Grid.Row", "0"))
                if row >= len(rows):
                    continue
                row_height = fixed_height(rows[row].get("Height"))
                if row_height is not None and row_height < required:
                    violations.append(
                        f"{path.relative_to(ROOT)}: row {row} fixes {row_height:g}px "
                        f"around {child.get(NAME, kind(child))}, which needs {required}px"
                    )

if violations:
    raise SystemExit("\n".join(violations))
print(f"PASS: {page_count} page XAML files have no undersized fixed text-control rows or scrolling search fields.")
