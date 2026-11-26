"""NV-09 T15 (1.3.0-u2): convierte el OpenAPI 3.1 de la API a 3.0 para RESTler.

RESTler v9 entiende OpenAPI 3.0. ASP.NET Core 10 publica 3.1, donde los
nulos y los números que también aceptan texto se escriben como listas de
tipos. La API no cambia: solo se traduce el documento para el fuzzer.

  python3 openapi31_to_30.py entrada.json salida.json http://127.0.0.1:8081
"""
import json
import sys

NUMERIC = ("integer", "number")


def convert(node):
    if isinstance(node, list):
        return [convert(item) for item in node]
    if not isinstance(node, dict):
        return node

    # oneOf: [{type: null}, X] → X (RESTler no usa la nulabilidad).
    one_of = node.get("oneOf")
    if isinstance(one_of, list):
        rest = [item for item in one_of if item != {"type": "null"}]
        if len(rest) == 1 and len(rest) != len(one_of):
            merged = {key: value for key, value in node.items() if key != "oneOf"}
            merged.update(rest[0])
            return convert(merged)

    result = {}
    for key, value in node.items():
        if key == "examples" and isinstance(value, list):
            continue  # 3.1: lista de ejemplos; en 3.0 no existe en esquemas.
        if key in ("exclusiveMinimum", "exclusiveMaximum") and isinstance(value, (int, float)) and not isinstance(value, bool):
            result["minimum" if key == "exclusiveMinimum" else "maximum"] = value
            result[key] = True
            continue
        result[key] = convert(value)

    kind = result.get("type")
    if isinstance(kind, list):
        types = [item for item in kind if item != "null"]
        if len(types) != len(kind):
            result["nullable"] = True
        numeric = [item for item in types if item in NUMERIC]
        if numeric:
            result["type"] = numeric[0]
            result.pop("pattern", None)  # el patrón era para la forma de texto
        elif types:
            result["type"] = types[0]
        else:
            result.pop("type")
    elif kind == "null":
        result.pop("type")
        result["nullable"] = True
    return result


def main():
    source, target, server = sys.argv[1], sys.argv[2], sys.argv[3]
    with open(source, encoding="utf-8") as handle:
        document = json.load(handle)
    converted = convert(document)
    converted["openapi"] = "3.0.3"
    converted["servers"] = [{"url": server}]
    leftovers = json.dumps(converted).count('"type": [')
    if leftovers:
        sys.exit(f"Quedaron {leftovers} listas de tipos sin convertir.")
    with open(target, "w", encoding="utf-8") as handle:
        json.dump(converted, handle, indent=1)
    print(f"OpenAPI {document.get('openapi')} -> 3.0.3: {len(converted.get('paths', {}))} rutas.")


if __name__ == "__main__":
    main()
