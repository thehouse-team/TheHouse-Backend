"""NV-09 T15 (1.3.0-u2): resumen de RESTler para el job y el artefacto.

  python3 restler_report.py <directorio de trabajo> <salida.md>

Lee los resultados de los modos `test` y `fuzz-lean` (`testing_summary.json`
y `bug_buckets.txt` de cada experimento) y escribe un Markdown sin tokens.
"""
import glob
import json
import os
import re
import sys

TOKEN = re.compile(r"Bearer\s+[A-Za-z0-9._\-]+")


def latest(pattern):
    found = sorted(glob.glob(pattern, recursive=True), key=os.path.getmtime)
    return found[-1] if found else None


def summary(work, mode):
    lines = [f"### Modo `{mode}`", ""]
    path = latest(os.path.join(work, mode, "**", "testing_summary.json"))
    if not path:
        return lines + ["Sin `testing_summary.json` (el modo no corrió o no terminó).", ""]
    with open(path, encoding="utf-8") as handle:
        data = json.load(handle)
    coverage = data.get("final_spec_coverage", "—")
    valid = data.get("num_fully_valid", data.get("num_fully_valid_requests", "—"))
    total = data.get("num_total_requests", data.get("total_requests_sent", {}))
    lines += [
        "| Dato | Valor |",
        "| --- | --- |",
        f"| Cobertura de la especificación | {coverage} |",
        f"| Peticiones totalmente válidas | {valid} |",
        f"| Peticiones enviadas | {json.dumps(total)} |",
    ]
    codes = data.get("errors", {}) or data.get("response_code_counts", {})
    if codes:
        lines.append(f"| Códigos de error | {json.dumps(codes)} |")
    buckets = data.get("bug_buckets", {})
    lines += [f"| *Bug buckets* | {json.dumps(buckets) if buckets else 'ninguno'} |", ""]
    text = latest(os.path.join(work, mode, "**", "bug_buckets.txt"))
    if text:
        with open(text, encoding="utf-8", errors="replace") as handle:
            content = TOKEN.sub("Bearer <oculto>", handle.read())
        lines += ["<details><summary>bug_buckets.txt</summary>", "", "```text", content[:6000], "```", "", "</details>", ""]
    return lines


def main():
    work, output = sys.argv[1], sys.argv[2]
    lines = [
        "## NV-09 T15 — Fuzzing con RESTler (entorno efímero)",
        "",
        "RESTler `v9.3.1` (commit `eaacd92`) contra la API efímera (`Testing`, límites del perfil `scan`), con dos usuarios de prueba. Informativo: cada 500 se analiza en `specs/nv-09-cd/evidence.md`.",
        "",
    ]
    for mode in ("Test", "FuzzLean"):
        lines += summary(work, mode)
    with open(output, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines))
    print("\n".join(lines))


if __name__ == "__main__":
    main()
