# NV-02 — Evidencia CI y DevSecOps U2

| Campo | Valor |
| --- | --- |
| ID | NV-02 |
| Documento | `evidence.md` |
| Versión | 1.1.0-u2 |
| Fecha | 2026-11-05 |
| Aprobación | Victor Manuel Jose Jose — 2026-11-05 («EJECUTA» de NV-02 1.1.0-u2) |
| Rama | `fix/u2-review` |
| Resultado | **PASS WITH WARNINGS (local)**: implementación lista; falta el primer run en GitHub Actions |

Ninguna task se marca como completada todavía: su aceptación exige evidencia de un run real en GitHub Actions. Esta evidencia registra la implementación y la validación local.

## 1. Archivos creados o modificados

| Archivo | Task | Contenido |
| --- | --- | --- |
| `.github/workflows/ci.yml` | T01–T04, T06–T12 | Workflow `CI` con los jobs `build-test`, `sast-semgrep`, `sca-sbom`, `secrets-gitleaks`, `container-security`, `iac-terraform`, `mutation-stryker` y `quality-gate` |
| `.github/scripts/coverage_summary.py` | T03 | Combina los reportes Cobertura y publica la tabla de cobertura en el resumen del run |
| `.github/scripts/sca_gate.py` | T06 | Tabla de dependencias vulnerables; falla con High/Critical sin excepción (`.github/sca-exceptions.txt`) |
| `.semgrep/thehouse.yml` y `.semgrep/thehouse.cs` | T05 | 9 reglas propias (7 ERROR, 2 WARNING) con casos que deben y no deben coincidir |
| `.gitleaks.toml` | T08 | Reglas por defecto de Gitleaks; única exclusión: `.semgrep/thehouse.cs` (credenciales ficticias de prueba) |
| `u2-linux-validation.yml`, `u2-container-validation.yml` | T13 | Retirados: `build-test` y `container-security` los reemplazan. `u2-ghcr-publish.yml` se conserva para NV-09 |

## 2. Versiones fijadas (consultadas el 2026-11-05)

| Componente | Versión | Fijación |
| --- | --- | --- |
| `actions/checkout` | v7.0.1 | SHA `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/setup-dotnet` | v6.1.0 | SHA `0f4a354dd5547a28c70b7853dbd95d5b67886e73` |
| `actions/upload-artifact` | v7.0.2 | SHA `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |
| `hashicorp/setup-terraform` | v4.0.1 | SHA `dfe3c3f87815947d99a8997f908cb6525fc44e9e` |
| Semgrep CE | 1.184.0 | Imagen `semgrep/semgrep:1.184.0` |
| Gitleaks | 8.30.1 | Imagen `zricethezav/gitleaks:v8.30.1` (sin la action que exige licencia) |
| Trivy | 0.76.2 | Imagen `aquasec/trivy:0.76.2` |
| Dockle | 0.4.15 | Imagen `goodwithtech/dockle:v0.4.15` |
| CycloneDX (.NET) | 6.3.1 | `dotnet tool install --version` |
| Stryker.NET | 5.0.0 | `dotnet tool install --version` |
| Terraform | 1.17.1 | `setup-terraform` |

## 3. Reglas Semgrep propias

| Regla | Severidad | Detecta |
| --- | --- | --- |
| `thehouse-log-sensitive-data` | ERROR | Contraseñas, tokens, llaves o cadenas de conexión pasados a un logger |
| `thehouse-hardcoded-mongodb-credentials` | ERROR | URI de MongoDB con usuario y contraseña en el código |
| `thehouse-hardcoded-signing-key` | ERROR | Llave de firma JWT literal |
| `thehouse-tls-validation-disabled` | ERROR | Validación de certificados TLS desactivada |
| `thehouse-weak-hash` | ERROR | MD5 o SHA-1 |
| `thehouse-insecure-random` | WARNING | `System.Random` fuera de pruebas |
| `thehouse-jwt-validation-disabled` | ERROR | Validación de JWT desactivada |
| `thehouse-exception-details-to-client` | ERROR | Mensajes o trazas de excepción en respuestas |
| `thehouse-bson-parse-input` | WARNING | `BsonDocument.Parse` con datos variables (inyección NoSQL) |

Antes de escribirlas se verificó con búsquedas en el código que ninguna coincide con el código actual (sin falsos positivos esperados).

## 4. Validación local (2026-11-05)

| Verificación | Resultado |
| --- | --- |
| YAML de `ci.yml` y de las reglas Semgrep | Válido; 4 disparadores, 8 jobs, todos con `timeout-minutes`; acciones fijadas por SHA; `permissions: contents: read` |
| `dotnet build` Release con `-warnaserror` | 0 errores, 0 advertencias |
| `dotnet test` | 429 aprobadas, 0 fallidas |
| `sca_gate.py` sobre `dotnet list package --vulnerable --include-transitive` | Sin dependencias vulnerables conocidas |
| `coverage_summary.py` sobre los reportes Cobertura | Líneas 71.6 %, ramas 72.6 % (sin código autogenerado); Domain 97.3 %, Application 97.6 %, API 85.9 %, Infrastructure 27.6 % |
| Historial Git (todas las ramas) con patrones de secretos | Sin hallazgos (búsqueda local; Gitleaks lo confirmará en CI) |

No se ejecutaron localmente Semgrep, Gitleaks, Trivy, Dockle, CycloneDX ni Stryker.NET (requieren descargar sus imágenes o herramientas); se ejecutan en GitHub Actions.

## 5. Protección de rama (T14) — guía

Estado al 2026-11-05: el repositorio ya es **público**, pero `main` no tiene ningún ruleset (la API devuelve `[]` para las reglas de `main` y la lista de rulesets está vacía). Las capturas del profesor muestran la configuración a replicar.

Después del primer run de `CI` en el pull request:

1. GitHub → **Settings → Rules → Rulesets → New ruleset → New branch ruleset**.
2. Nombre `Main Proteccion`, **Enforcement status: Active**, **Target branches: Include default branch** (`main`).
3. Activar **Restrict deletions**, **Block force pushes** y **Require a pull request before merging** (1 aprobación).
4. Activar **Require status checks to pass** y **Require branches to be up to date before merging**; en **Add checks** agregar `build-test`, `sast-semgrep`, `sca-sbom`, `secrets-gitleaks`, `container-security`, `iac-terraform` y `quality-gate`.
5. Guardar y verificar con `gh api repos/thehouse-team/TheHouse-Backend/rules/branches/main`.

## 6. Pendiente para cerrar las tasks

- Primer run de `CI` en el pull request de `fix/u2-review`: resultados por job, minutos consumidos y hallazgos (corregir o registrar excepciones).
- Ejecución manual de `mutation-stryker` (`run_mutation = true`) para la línea base (T11).
- Ruleset de `main` creado y verificado (T14).
- Revisión final de seguridad y coherencia (T15).
