"""Gate de dependencias vulnerables para el job sca-sbom (NV-02-R05).

Lee la salida JSON de `dotnet list package --vulnerable --include-transitive
--format json`, escribe una tabla Markdown y falla si hay vulnerabilidades
High o Critical. Las excepciones aprobadas se listan en
`.github/sca-exceptions.txt` (un identificador de paquete por línea, con
justificación, responsable y vencimiento en comentarios `#`).
"""
import json
import os
import sys

BLOCKING = {"high", "critical"}
sys.stdout.reconfigure(encoding="utf-8")

with open(sys.argv[1], encoding="utf-8-sig") as handle:
    report = json.load(handle)

exceptions = set()
exceptions_file = ".github/sca-exceptions.txt"
if os.path.exists(exceptions_file):
    with open(exceptions_file, encoding="utf-8") as handle:
        for raw in handle:
            entry = raw.split("#", 1)[0].strip().lower()
            if entry:
                exceptions.add(entry)

findings = []
for project in report.get("projects", []):
    project_name = os.path.basename(project.get("path", "?"))
    for framework in project.get("frameworks", []):
        for kind in ("topLevelPackages", "transitivePackages"):
            for package in framework.get(kind, []):
                for vulnerability in package.get("vulnerabilities", []):
                    findings.append((
                        project_name,
                        package.get("id", "?"),
                        package.get("resolvedVersion", "?"),
                        "directa" if kind == "topLevelPackages" else "transitiva",
                        vulnerability.get("severity", "?"),
                        vulnerability.get("advisoryurl", ""),
                    ))

print("## Dependencias vulnerables (NV-02-R05)")
print()
if not findings:
    print("No se encontraron dependencias vulnerables conocidas. ✅")
    sys.exit(0)

print("| Proyecto | Paquete | Versión | Tipo | Severidad | Aviso |")
print("| --- | --- | --- | --- | --- | --- |")
blocking = []
for project, package, version, kind, severity, url in sorted(set(findings)):
    excepted = package.lower() in exceptions
    mark = " (excepción registrada)" if excepted else ""
    print(f"| `{project}` | `{package}` | {version} | {kind} | {severity}{mark} | {url} |")
    if severity.lower() in BLOCKING and not excepted:
        blocking.append(package)

print()
if blocking:
    print(f"**Resultado:** BLOCKED — High/Critical sin excepción: {', '.join(sorted(set(blocking)))}")
    sys.exit(1)
print("**Resultado:** PASS (sin High/Critical sin excepción)")
