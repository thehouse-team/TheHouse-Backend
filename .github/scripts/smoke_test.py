"""NV-09 T09/T12: smoke tests de la API (solo librería estándar).

Modos:
  full      Entorno efímero (NV-09-R05): flujo completo con escrituras.
  readonly  Producción (NV-09-R10): sin escrituras persistentes.

Uso: python3 smoke_test.py --base-url URL --mode full|readonly [--report archivo.md]
Variable SMOKE_SIMULATE_FAILURE=true: fuerza un fallo al final (prueba del rollback, T13).
Nunca imprime tokens, contraseñas ni cuerpos con datos de usuario.
"""

import argparse
import json
import os
import secrets
import sys
import time
import urllib.error
import urllib.request

API = "/api/v1"


class Client:
    def __init__(self, base_url, timeout=20, forwarded_proto=None):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout
        self.forwarded_proto = forwarded_proto

    def request(self, method, path, body=None, headers=None):
        data = None
        hdrs = {"Accept": "application/json"}
        if self.forwarded_proto:
            # Entorno efímero: simula el ingress TLS (la API redirige HTTP a HTTPS).
            # La API exige simetría: X-Forwarded-For y X-Forwarded-Proto juntos.
            hdrs["X-Forwarded-Proto"] = self.forwarded_proto
            hdrs["X-Forwarded-For"] = "198.51.100.10"
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            hdrs["Content-Type"] = "application/json"
        hdrs.update(headers or {})
        req = urllib.request.Request(self.base_url + path, data=data, method=method, headers=hdrs)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                return resp.status, dict(resp.headers), resp.read()
        except urllib.error.HTTPError as err:
            return err.code, dict(err.headers), err.read()


def header(headers, name):
    for key, value in headers.items():
        if key.lower() == name.lower():
            return value
    return None


class Runner:
    def __init__(self):
        self.results = []

    def check(self, name, ok, detail=""):
        self.results.append(("PASS" if ok else "FAIL", name, detail))
        print(f"[{'PASS' if ok else 'FAIL'}] {name}{' — ' + detail if detail else ''}", flush=True)
        return ok

    def warn(self, name, detail=""):
        self.results.append(("WARN", name, detail))
        print(f"[WARN] {name}{' — ' + detail if detail else ''}", flush=True)

    @property
    def failed(self):
        return [r for r in self.results if r[0] == "FAIL"]

    def markdown(self, mode, base_url):
        lines = [f"### Smoke tests ({mode})", "", f"Destino: `{base_url}`", "", "| Resultado | Verificación | Detalle |", "| --- | --- | --- |"]
        icons = {"PASS": "✅", "FAIL": "❌", "WARN": "⚠️"}
        for status, name, detail in self.results:
            lines.append(f"| {icons[status]} {status} | {name} | {detail} |")
        passed = sum(1 for r in self.results if r[0] == "PASS")
        lines += ["", f"Total: {passed} PASS, {len(self.failed)} FAIL, {sum(1 for r in self.results if r[0] == 'WARN')} WARN"]
        return "\n".join(lines) + "\n"


def wait_ready(client, run, attempts, delay):
    # Arranque en frío con 0 réplicas (NV-09-RK05): reintentos acotados.
    last = None
    for i in range(1, attempts + 1):
        try:
            status, _, _ = client.request("GET", "/health/ready")
            last = status
            if status == 200:
                return run.check("GET /health/ready", True, f"200 tras {i} intento(s)")
        except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as exc:
            last = type(exc).__name__
        time.sleep(delay)
    return run.check("GET /health/ready", False, f"sin 200 tras {attempts} intentos (último: {last})")


def catalog(client, run, resource, require_items):
    status, headers, body = client.request("GET", f"{API}/{resource}?page=1&pageSize=5")
    ok = run.check(f"GET {API}/{resource}", status == 200, f"HTTP {status}")
    if not ok:
        return
    payload = json.loads(body)
    items = payload.get("items", [])
    total = payload.get("totalItems", 0)
    if require_items:
        run.check(f"{resource}: catálogo con datos", total > 0 and len(items) > 0, f"totalItems={total}")
    elif total == 0:
        run.warn(f"{resource}: catálogo vacío", "pendiente del seed en producción (T18)")
    etag = header(headers, "ETag")
    cache = header(headers, "Cache-Control") or ""
    run.check(f"{resource}: ETag y Cache-Control public", bool(etag) and "public" in cache, f"Cache-Control: {cache or '—'}")
    if etag:
        status304, _, _ = client.request("GET", f"{API}/{resource}?page=1&pageSize=5", headers={"If-None-Match": etag})
        run.check(f"{resource}: revalidación con If-None-Match", status304 == 304, f"HTTP {status304}")
    if items:
        item_id = items[0].get("id")
        status_d, _, _ = client.request("GET", f"{API}/{resource}/{item_id}")
        run.check(f"GET {API}/{resource}/{{id}}", status_d == 200, f"HTTP {status_d}")
    status_nf, _, _ = client.request("GET", f"{API}/{resource}/000000000000000000000000")
    run.check(f"GET {API}/{resource}/{{id}} inexistente", status_nf == 404, f"HTTP {status_nf}")


def security_headers(client, run, https):
    status, headers, _ = client.request("GET", f"{API}/universities?page=1&pageSize=1")
    if https:
        hsts = header(headers, "Strict-Transport-Security")
        run.check("HSTS en HTTPS", bool(hsts), hsts or "ausente")
    if (header(headers, "X-Content-Type-Options") or "").lower() != "nosniff":
        # Hallazgo registrado en NV-09 evidence: el cambio es en TheHouse/ (ruta protegida).
        run.warn("X-Content-Type-Options: nosniff", "ausente; corrección pendiente en NV-01 (ruta protegida)")


def readonly_auth(client, run):
    status, _, _ = client.request("POST", f"{API}/auth/register", body={"email": "no-es-un-correo", "password": "corta"})
    run.check("POST register con datos inválidos", status == 400, f"HTTP {status} (sin escritura)")
    email = f"smoke-{secrets.token_hex(6)}@thehouse.invalid"
    status, _, _ = client.request("POST", f"{API}/auth/login", body={"email": email, "password": secrets.token_urlsafe(16)})
    run.check("POST login con credenciales inexistentes", status == 401, f"HTTP {status}")
    status, _, _ = client.request("POST", f"{API}/auth/logout")
    run.check("POST logout sin token", status == 401, f"HTTP {status}")


def full_auth(client, run, login_email_limit):
    email = f"smoke-{secrets.token_hex(6)}@thehouse.test"
    password = secrets.token_urlsafe(18)
    status, _, _ = client.request("POST", f"{API}/auth/register", body={"email": email, "password": password})
    run.check("POST register", status == 201, f"HTTP {status}")
    status, _, _ = client.request("POST", f"{API}/auth/register", body={"email": email, "password": password})
    run.check("POST register duplicado", status == 409, f"HTTP {status}")
    status, _, _ = client.request("POST", f"{API}/auth/register", body={"email": "no-es-un-correo", "password": "corta"})
    run.check("POST register inválido", status == 400, f"HTTP {status}")

    status, _, body = client.request("POST", f"{API}/auth/login", body={"email": email, "password": password})
    token = json.loads(body).get("accessToken") if status == 200 else None
    run.check("POST login", status == 200 and bool(token), f"HTTP {status}")
    if token:
        auth = {"Authorization": f"Bearer {token}"}
        status, _, _ = client.request("POST", f"{API}/auth/logout", headers=auth)
        run.check("POST logout", status == 204, f"HTTP {status}")
        status, _, _ = client.request("POST", f"{API}/auth/logout", headers=auth)
        run.check("POST logout con token revocado (reuso)", status == 401, f"HTTP {status}")
    status, _, _ = client.request("POST", f"{API}/auth/logout")
    run.check("POST logout sin token", status == 401, f"HTTP {status}")

    # Rate limit por email (NV-05): credenciales incorrectas hasta recibir 429.
    got_429 = False
    for attempt in range(1, login_email_limit + 3):
        status, headers, _ = client.request("POST", f"{API}/auth/login", body={"email": email, "password": "incorrecta-" + secrets.token_hex(4)})
        if status == 429:
            got_429 = True
            run.check("Rate limit de login (429)", True, f"en el intento {attempt}; Retry-After: {header(headers, 'Retry-After') or '—'}")
            break
        if status != 401:
            run.check("Rate limit de login (429)", False, f"HTTP {status} inesperado en el intento {attempt}")
            return
    if not got_429:
        run.check("Rate limit de login (429)", False, f"sin 429 tras {login_email_limit + 2} intentos")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--mode", choices=["full", "readonly"], required=True)
    parser.add_argument("--report")
    parser.add_argument("--ready-attempts", type=int, default=30)
    parser.add_argument("--ready-delay", type=float, default=5)
    parser.add_argument("--login-email-limit", type=int, default=5)
    parser.add_argument("--forwarded-proto", choices=["https"])
    args = parser.parse_args()

    client = Client(args.base_url, forwarded_proto=args.forwarded_proto)
    run = Runner()
    # Primero readiness con reintentos: con 0 réplicas la primera solicitud
    # activa la réplica (arranque en frío) y puede exceder el timeout.
    if wait_ready(client, run, args.ready_attempts, args.ready_delay):
        try:
            status, _, _ = client.request("GET", "/health/live")
        except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
            status = type(exc).__name__
        run.check("GET /health/live", status == 200, f"HTTP {status}")
        full = args.mode == "full"
        catalog(client, run, "universities", require_items=full)
        catalog(client, run, "properties", require_items=full)
        security_headers(client, run, args.base_url.startswith("https://"))
        if full:
            full_auth(client, run, args.login_email_limit)
        else:
            readonly_auth(client, run)

    if os.environ.get("SMOKE_SIMULATE_FAILURE", "").lower() == "true":
        run.check("Falla controlada (simulate_smoke_failure)", False, "solicitada para probar el rollback (T13)")

    report = run.markdown(args.mode, args.base_url)
    if args.report:
        with open(args.report, "w", encoding="utf-8") as handle:
            handle.write(report)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(report + "\n")
    sys.exit(1 if run.failed else 0)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
