"""NV-09 T10/T14: gate sobre el reporte JSON de OWASP ZAP.

Falla si hay alertas de riesgo High (riskcode 3). Las excepciones viven en
`.zap/rules.tsv` como IGNORE, con justificación, responsable y vencimiento,
y ZAP ya no las reporta. Publica una tabla de alertas en el resumen del run.

Uso: python3 zap_gate.py reporte.json "Título"
"""

import collections
import json
import os
import sys

RISK = {"3": "High", "2": "Medium", "1": "Low", "0": "Informational"}


def main():
    path, title = sys.argv[1], sys.argv[2]
    with open(path, encoding="utf-8") as handle:
        report = json.load(handle)

    alerts = []
    for site in report.get("site", []):
        for alert in site.get("alerts", []):
            alerts.append(alert)

    counts = collections.Counter(RISK.get(a.get("riskcode"), "?") for a in alerts)
    lines = [f"### {title}", "", "| Riesgo | Alertas |", "| --- | --- |"]
    for risk in ("High", "Medium", "Low", "Informational"):
        lines.append(f"| {risk} | {counts.get(risk, 0)} |")
    if alerts:
        lines += ["", "| Riesgo | Regla | Alerta | Instancias |", "| --- | --- | --- | --- |"]
        for alert in sorted(alerts, key=lambda a: -int(a.get("riskcode", 0))):
            lines.append(
                f"| {RISK.get(alert.get('riskcode'), '?')} | {alert.get('pluginid')} | {alert.get('name')} | {alert.get('count', len(alert.get('instances', [])))} |"
            )
    high = counts.get("High", 0)
    lines += ["", f"Resultado: {'❌ BLOCKED' if high else '✅ PASS'} ({high} High)"]
    text = "\n".join(lines) + "\n"
    print(text)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(text + "\n")
    sys.exit(1 if high else 0)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
