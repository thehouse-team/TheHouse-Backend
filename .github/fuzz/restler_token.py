"""NV-09 T15 (1.3.0-u2): tokens para RESTler (`--token_refresh_command`).

Registra una vez dos usuarios de prueba con contraseñas aleatorias (los guarda
en un archivo del directorio de trabajo efímero) e imprime, en el formato que
espera RESTler, una línea de metadatos y un encabezado `Authorization` por
usuario. Con dos usuarios RESTler también prueba que uno no pueda leer ni
cambiar los recursos del otro (checker de namespace).

Solo corre contra la API efímera del job (nunca contra producción). Los
tokens van a la salida estándar que lee RESTler; RESTler los oculta en sus
registros y el reporte del job no los incluye.
"""
import json
import os
import secrets
import sys
import urllib.error
import urllib.request

BASE = os.environ.get("FUZZ_BASE_URL", "http://127.0.0.1:8081")
STATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".fuzz-users.json")


def post(path, body):
    request = urllib.request.Request(
        BASE + path,
        data=json.dumps(body).encode(),
        method="POST",
        headers={"Content-Type": "application/json", "Accept": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            content = response.read()
            return response.status, json.loads(content) if content else None
    except urllib.error.HTTPError as error:
        return error.code, None


def users():
    if os.path.exists(STATE):
        with open(STATE, encoding="utf-8") as handle:
            return json.load(handle)
    created = []
    for index in (1, 2):
        account = {"email": f"fuzz-{index}-{secrets.token_hex(4)}@example.com", "password": "Fz-" + secrets.token_urlsafe(18) + "-9a!"}
        status, _ = post("/api/v1/auth/register", account)
        if status != 201:
            sys.exit(f"Registro del usuario de prueba {index}: HTTP {status}")
        created.append(account)
    with open(STATE, "w", encoding="utf-8") as handle:
        json.dump(created, handle)
    return created


def main():
    headers = []
    for index, account in enumerate(users(), start=1):
        status, body = post("/api/v1/auth/login", account)
        if status != 200 or not body or "accessToken" not in body:
            sys.exit(f"Inicio de sesión del usuario de prueba {index}: HTTP {status}")
        headers.append(f"Authorization: Bearer {body['accessToken']}")
    print("{u'user1':{}, u'user2':{}}")
    for header in headers:
        print(header)


if __name__ == "__main__":
    main()
