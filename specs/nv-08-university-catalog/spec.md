# NV-08 — University Catalog (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-08 |
| Documento | `spec.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-07 (parcial: catálogo; búsqueda y selección en NV-11) (`Requerimientos_TheHouse_v2`) |
| Plan | `plan.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Precondición | NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza búsqueda avanzada ni otros módulos |

**Cambio 1.2.0-u2 (2026-11-07) — PENDIENTE DE APROBACIÓN Y «EJECUTA»:** el seed pasa a un catálogo regional de diez universidades de Tula–Tepeji–Jilotepec (NV-08-R04, task T14) y se corrige el comando de seed documentado, que eludía M-BE-043. Las tasks 1.1.0-u2 siguen cerradas.

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B.

**Cambio 1.1.0-u2 (2026-11-05):** agrega NV-08-R08 (caché HTTP condicional) y las tasks T12 y T13. Aprobación documental: Victor Manuel Jose Jose, 2026-11-05 (M-BE-046). T12 y T13 quedan **⛔ NO EJECUTAR** hasta la autorización humana «EJECUTA»; las tasks 1.0.0-u2 siguen cerradas.

## 1. Contexto

U2 requiere un catálogo mínimo de universidades para las lecturas de propiedades y para demostrar el funcionamiento del backend. Se implementan dos lecturas públicas y un seed pequeño, idempotente y reproducible.

## 2. Objetivo

Definir el contrato verificable de `GET /api/v1/universities` y `GET /api/v1/universities/{id}`, la paginación, el seed y las pruebas.

## 3. Precondiciones

1. NV-01 U2 provee Problem Details, validación, paginación y configuración.
2. NV-03 U2 aprobada documentalmente para la verificación desplegada; las pruebas locales pueden usar MongoDB y Redis aislados.
3. Existe autorización de Fase B.
4. El seed se ejecuta solo con autorización explícita; en `Production` exige `--allow-production-seed` (M-BE-043) y la autorización de NV-09 T18.

## 4. Alcance

1. `GET /api/v1/universities` con paginación.
2. `GET /api/v1/universities/{id}`.
3. Validación de `page`, `pageSize` e `id`.
4. Seed idempotente del catálogo regional Tula–Tepeji–Jilotepec: diez instituciones (1.2.0-u2).
5. Índice único por `code` e índice de orden.
6. Problem Details.
7. Pruebas unitarias, de integración y de seguridad.
8. Caché HTTP condicional (`ETag`, `Cache-Control`, 304) en listado y detalle.

## 5. Fuera de alcance

- Crear, actualizar o borrar universidades desde la API.
- Búsqueda por texto, filtros por país/ciudad o geolocalización; no aprobados para U2.
- Catálogo masivo o scraping.
- Datos personales, contactos privados o información no pública.
- Implementar MongoDB o infraestructura.
- Ejecutar NV-02.
- Declarar pruebas no ejecutadas.

## 6. Contrato HTTP

### 6.1 Listado

```http
GET /api/v1/universities?page=1&pageSize=20
```

| Parámetro | Tipo | Default | Regla |
| --- | --- | --- | --- |
| `page` | integer | 1 | >= 1 |
| `pageSize` | integer | 20 | 1–50 |

Response 200:

```json
{
  "items": [
    {
      "id": "665f1c2a9b1e4a0012345678",
      "code": "UTTT",
      "name": "Universidad Tecnológica de Tula-Tepeji",
      "city": "Tula de Allende",
      "country": "México",
      "createdAt": "2026-11-02T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

- Orden estable: `name` ascendente y `_id` ascendente.
- Resultado vacío: 200 con `items: []`, `totalItems: 0`, `totalPages: 0`.
- No se aceptan filtros adicionales; parámetros desconocidos se ignoran.

### 6.2 Detalle

```http
GET /api/v1/universities/{id}
```

Response 200 con los mismos campos públicos. 404 si no existe. No se exponen campos internos.

### 6.3 Validación

| Entrada | Regla | Error |
| --- | --- | --- |
| `id` | ObjectId hexadecimal de 24 caracteres | 400 |
| `page` | Entero >= 1 | 400 |
| `pageSize` | Entero 1–50 | 400 |
| `page`/`pageSize` no numéricos | — | 400 |

### 6.4 Status codes

| Código | Condición |
| --- | --- |
| 200 | Listado o detalle |
| 304 | `If-None-Match` coincide con el `ETag` actual; sin cuerpo |
| 400 | ID o paginación inválida |
| 404 | Universidad no encontrada |
| 429 | Rate limit excedido; `Retry-After` |
| 503 | MongoDB no disponible; sin detalles |
| 500 | Error interno; Problem Details sin stack trace |

### 6.5 Caché HTTP (1.1.0-u2)

- Las respuestas 200 de listado y detalle incluyen un `ETag` débil (`W/"<hash>"`) calculado con SHA-256 sobre el cuerpo JSON sin comprimir y `Cache-Control: public, max-age=60` (valor PROPUESTO, configurable entre 0 y 300 s).
- Si la solicitud trae `If-None-Match` y coincide con el `ETag` actual (comparación débil de RFC 9110, incluida una lista separada por comas y `*`), la respuesta es `304 Not Modified` sin cuerpo, con el mismo `ETag` y `Cache-Control`.
- Las respuestas de error (4xx/5xx) conservan `Cache-Control: no-store` y no llevan `ETag`.
- El rate limit se evalúa antes que la caché; un 304 también cuenta como solicitud.
- Se envía `Vary: Accept-Encoding`; el `ETag` débil es válido con y sin compresión.
- Solo aplica a estos endpoints públicos; nunca a respuestas autenticadas ni con datos de usuario.
- Cubre el requisito académico 9 (caché en frontend): el cliente móvil reutiliza la respuesta y revalida con `If-None-Match`.

## 7. Seed U2

### 7.1 Datos mínimos

**1.2.0-u2:** el seed contiene diez instituciones de educación superior de la región Tula–Tepeji–Jilotepec (Hidalgo y Estado de México), donde se hará la primera demostración. Sustituye a los tres registros de 1.0.0-u2; se retira la sede de Pachuca, fuera de la región.

| `code` | `name` | `city` | `country` | Fuente pública |
| --- | --- | --- | --- | --- |
| UTTT | Universidad Tecnológica de Tula-Tepeji | Tula de Allende | México | uttt.edu.mx |
| UAEH-ESTE | Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tepeji del Río | Tepeji del Río de Ocampo | México | uaeh.edu.mx/campus/tepeji |
| UAEH-ESTL | Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Tlahuelilpan | Tlahuelilpan | México | uaeh.edu.mx/campus/tlahuelilpan |
| UAEH-ESAT | Universidad Autónoma del Estado de Hidalgo, Escuela Superior de Atotonilco de Tula | Atotonilco de Tula | México | uaeh.edu.mx/campus/atotonilco |
| ITSOEH | Instituto Tecnológico Superior del Occidente del Estado de Hidalgo | Mixquiahuala de Juárez | México | seed 1.0.0-u2 |
| TESJI | Tecnológico de Estudios Superiores de Jilotepec | Jilotepec | México | tesji.edomex.gob.mx |
| UMB-JILO | Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Jilotepec | Jilotepec | México | informes oficiales UMB |
| UMB-VDC | Universidad Mexiquense del Bicentenario, Unidad de Estudios Superiores Villa del Carbón | Villa del Carbón | México | informes oficiales UMB |
| UCAN | Universidad Canadiense | Tula de Allende | México | whed.net (IAU); validar vigencia |
| UNE-TULA | Universidad del Nuevo México | Tula de Allende | México | universidadesdemexico.mx; validar vigencia |

Son nombres institucionales públicos consultados el 2026-11-07 en las fuentes indicadas. Las dos instituciones privadas tienen fuentes secundarias: el equipo valida su vigencia antes de la evaluación y puede sustituirlas sin cambiar el contrato. El origen y la validación de los datos se registran en `evidence.md` durante la implementación. No se agregan contactos, direcciones exactas ni datos personales.

### 7.2 Idempotencia y reproducibilidad

- `upsert` por `code`; ejecutar el seed dos veces no duplica registros.
- No elimina universidades existentes ajenas al seed.
- No usa fechas aleatorias; `createdAt` se conserva o se fija de forma reproducible.
- El comando de seed se documenta y no contiene secretos.
- La evidencia (`evidence.md`) registra fecha, entorno, cantidad antes/después, origen y validación de los datos.

### 7.3 Índices

- Único `{ code: 1 }`.
- Orden `{ name: 1, _id: 1 }`.

### 7.4 Activación controlada

El seed está deshabilitado por defecto en `appsettings.json` y solo se activa por configuración:

- `--seed-universities` requiere `UniversitySeed__Enabled=true`.
- `--seed-properties` requiere `PropertySeed__Enabled=true`.
- En `Production` ambos comandos se rechazan salvo con la bandera explícita `--allow-production-seed`, registrada en el log (M-BE-043).
- El seed de propiedades requiere que el seed de universidades se haya ejecutado primero.

Comando documentado para una ejecución autorizada, sin credenciales embebidas. En 1.2.0-u2 se retira `ASPNETCORE_ENVIRONMENT=Staging` del comando anterior, porque eludía la protección de `Production` (M-BE-043):

```text
az containerapp exec -n ca-thehouse-u2 -g rg-thehouse-u2 --container thehouse-backend --command "/bin/sh -c 'UniversitySeed__Enabled=true PropertySeed__Enabled=true dotnet TheHouse.dll --seed-universities --seed-properties --allow-production-seed'"
```

Requiere que el contenedor ya tenga `MongoDB__ConnectionString` como secreto de Azure Container Apps. La ejecución real debe coordinarse con NV-03, no debe ejecutarse contra una base con datos productivos y sus conteos antes/después quedan como evidencia pendiente; no se realiza en esta revisión.

## 8. Seguridad

- Lecturas públicas; no hay BOLA.
- No exponer datos personales ni contactos privados.
- Validar `id` antes de consultar MongoDB.
- No registrar documentos completos ni datos sensibles.
- Rate limit: 60 solicitudes/60 s por IP en Redis; 429 con `Retry-After: 60`.
- Fallo de Redis: 503; no se omite el límite.
- HTTPS obligatorio en el entorno desplegado.
- Errores sin stack traces ni detalles de MongoDB.

## 9. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance`, `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | `Validación fallida` |
| No encontrado | 404 | `Recurso no encontrado` |
| Rate limit | 429 | `Demasiadas solicitudes` |
| MongoDB no disponible | 503 | `Servicio no disponible` |
| Interno | 500 | `Error interno del servidor` |

## 10. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-08-R01 | Contrato de listado paginado | `GET /api/v1/universities` acepta `page`/`pageSize`, devuelve `items` y metadatos, orden estable y resultado vacío 200. | F1 | T01, T02, T05, T09 |
| NV-08-R02 | Contrato de detalle | `GET /api/v1/universities/{id}` devuelve 200 con campos públicos o 404; sin campos internos. | F1 | T01, T02, T06, T10 |
| NV-08-R03 | Validación de ID y paginación | ID ObjectId de 24 hex; `page` >= 1; `pageSize` 1–50; inválidos 400. | F1 | T02, T06, T08 |
| NV-08-R04 | Seed idempotente | Diez registros del catálogo regional definidos (1.2.0-u2); upsert por `code`; ejecutar dos veces no duplica; sin datos personales; origen y validación en `evidence.md`. | F2 | T04, T08, T09, T10, T14 |
| NV-08-R05 | Acceso MongoDB e índices | Proyección explícita, índice único `{code:1}`, orden `{name:1,_id:1}` y sin N+1. | F2 | T03, T05, T06, T09 |
| NV-08-R06 | Problem Details y límites | 400/404/429/503/500 usan Problem Details; rate limit 60/60 s por IP en Redis; fallo de Redis 503. | F3 | T05, T06, T07, T09 |
| NV-08-R07 | Pruebas y evidencia | Unitarias, integración y seguridad; evidencia del seed y de los endpoints. | F4 | T10, T11, T13 |
| NV-08-R08 | Caché HTTP condicional | GET 200 de listado y detalle devuelven `ETag` débil por contenido y `Cache-Control: public, max-age=60`; `If-None-Match` coincidente devuelve 304 sin cuerpo; los errores mantienen `no-store` sin `ETag`; no se cachean respuestas autenticadas. | F3 | T12, T13 |

## 11. Testing

- Unitarias: paginación, validación de ObjectId, DTO, seed idempotente.
- Integración: listado con datos, vacío, detalle 200/404, 400, 429, 503, seed dos veces sin duplicados.
- Seguridad: sin datos personales, errores sin stack trace, rate limit compartido.
- MongoDB/Redis: dependencia efímera con Testcontainers solo si Fase B autoriza el paquete; alternativa aislada.
- Caché HTTP: `ETag` presente y estable para el mismo contenido, 304 con `If-None-Match` (incluidos lista y `*`), 200 si no coincide, errores sin `ETag` y con `no-store`, interacción con compresión y rate limit.

## 12. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-08-RK01 | Seed duplicado | Catálogo inconsistente | Upsert por `code` e índice único |
| NV-08-RK02 | Datos inventados o no verificados | Información falsa | Registros públicos con fuente y evidencia |
| NV-08-RK03 | Catálogo masivo | Costo/ruido | Catálogo regional acotado a diez registros |
| NV-08-RK04 | Paginación sin máximo | Abuso | `pageSize` máximo 50 |
| NV-08-RK05 | MongoDB caído | Error no controlado | 503 Problem Details |
| NV-08-RK06 | Datos personales en seed | Privacidad | Solo nombres institucionales |
| NV-08-RK07 | Caché de datos desactualizados o de respuestas no públicas | Datos viejos o fuga | `max-age` corto, `ETag` por contenido y solo endpoints públicos |

## 13. Dependencias

- NV-01 U2 para Problem Details, validación y paginación.
- NV-03 U2 para MongoDB, Redis, HTTPS y despliegue.
- NV-07 comparte convenciones de paginación, pero es independiente.
- NV-02 (CI) quedó completada el 2026-11-05 y la CD se especifica en NV-09 (M-BE-049).

## 14. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 14 |
| Tasks U2 | 14 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 13 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |

## 15. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta autorización de Fase B.

Cambio 1.1.0-u2: aprobación documental de Victor Manuel Jose Jose, 2026-11-05 (M-BE-046). T12 y T13 requieren la autorización humana «EJECUTA» antes de implementarse.
