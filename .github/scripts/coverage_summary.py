"""Resumen de cobertura para el job build-test (NV-02-R03).

Combina los archivos Cobertura de las tres suites contando cada línea una sola
vez y escribe una tabla Markdown por proyecto. Es informativo: nunca falla el
job por el porcentaje.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

GENERATED = ("OpenApiXmlCommentSupport.generated.cs",)
sys.stdout.reconfigure(encoding="utf-8")

root = sys.argv[1] if len(sys.argv) > 1 else "TestResults"
lines = defaultdict(lambda: defaultdict(int))
branches = defaultdict(lambda: defaultdict(lambda: [0, 0]))

reports = glob.glob(os.path.join(root, "**", "coverage.cobertura.xml"), recursive=True)
for path in reports:
    for package in ET.parse(path).iter("package"):
        name = package.get("name")
        for cls in package.iter("class"):
            filename = cls.get("filename") or ""
            if filename.endswith(GENERATED):
                continue
            key = (name, filename)
            for line in cls.iter("line"):
                number = int(line.get("number"))
                lines[key][number] = max(lines[key][number], int(line.get("hits")))
                condition = line.get("condition-coverage")
                if line.get("branch") == "True" and condition:
                    covered, total = condition.split("(")[1].rstrip(")").split("/")
                    current = branches[key][number]
                    current[0] = max(current[0], int(covered))
                    current[1] = max(current[1], int(total))

per_project = defaultdict(lambda: [0, 0, 0, 0])
for (project, filename), hits in lines.items():
    totals = per_project[project]
    totals[0] += sum(1 for value in hits.values() if value > 0)
    totals[1] += len(hits)
    for covered, total in branches[(project, filename)].values():
        totals[2] += covered
        totals[3] += total


def pct(part, whole):
    return f"{100 * part / whole:.1f} %" if whole else "—"


print("## Cobertura de código (informativa, NV-02-R03)")
print()
if not reports:
    print("No se encontraron reportes de cobertura.")
    sys.exit(0)
print("| Proyecto | Líneas | Ramas |")
print("| --- | --- | --- |")
grand = [0, 0, 0, 0]
for project, (lc, lt, bc, bt) in sorted(per_project.items()):
    print(f"| `{project}` | {pct(lc, lt)} ({lc}/{lt}) | {pct(bc, bt)} ({bc}/{bt}) |")
    grand = [grand[0] + lc, grand[1] + lt, grand[2] + bc, grand[3] + bt]
print(f"| **Total** | **{pct(grand[0], grand[1])}** | **{pct(grand[2], grand[3])}** |")
print()
print(f"Reportes combinados: {len(reports)}. Se excluye código autogenerado.")
