# NV-07 — Property Core (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-07 |
| Documento | `spec.md` |
| Versión | 1.2.0-u2 |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-23 y RF-24 (parcial: listado y detalle públicos; filtros, mapa y sesión en NV-14) (`Requerimientos_TheHouse_v2`) |
| Plan | `plan.md` 1.2.0-u2 |
| Tasks | `task.md` 1.2.0-u2 |
| Precondición | NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza CRUD ni otros módulos |

**Cambio 1.2.0-u2 (2026-11-07) — APROBADO Y AUTORIZADO por Victor Manuel Jose Jose el 2026-11-07 («okey si esta bien una vez que quedo esto me dices», respuesta al paquete propuesto: aprobación documental, «EJECUTA» de NV-08 T14, NV-07 T13, NV-01 T19, NV-09 T13, T18 y T20, NV-03 T08, T18 y T19, y autorización del seed en producción; `terraform apply` sigue requiriendo «OK PARA APPLY»):** agrega NV-07-R09 (seed regional de doce propiedades vinculadas al catálogo de NV-08 1.2.0-u2) y la task T13. Las tasks 1.1.0-u2 siguen cerradas.

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B.

**Cambio 1.1.0-u2 (2026-11-05):** agrega NV-07-R08 (caché HTTP condicional) y las tasks T11 y T12. Aprobación documental: Victor Manuel Jose Jose, 2026-11-05 (M-BE-046). T11 y T12 quedan **⛔ NO EJECUTAR** hasta la autorización humana «EJECUTA»; las tasks 1.0.0-u2 siguen cerradas.

## 1. Contexto

U2 requiere dos lecturas públicas de propiedades: listado paginado y detalle por identificador. No se implementa creación, edición, borrado, favoritos, visitas, chat, Smart Match ni búsqueda avanzada. El modelo debe exponer solo datos públicos seguros.

## 2. Objetivo

Definir el contrato verificable de `GET /api/v1/properties` y `GET /api/v1/properties/{id}`, su paginación, validación, acceso a MongoDB, proyección segura, errores y pruebas.

## 3. Precondiciones

1. NV-01 U2 provee Problem Details, validación, paginación y configuración.
2. NV-03 U2 aprobada documentalmente para la verificación desplegada; las pruebas locales pueden usar MongoDB y Redis aislados.
3. Existe autorización de Fase B.
4. Existe una colección `properties` con datos de prueba o seed controlado. El seed idempotente de doce propiedades de demostración (1.2.0-u2; antes seis) se activa solo por configuración (`PropertySeed__Enabled=true` y `--seed-properties`), requiere el seed de universidades NV-08 y en `Production` solo se permite con `--allow-production-seed`, registrado en el log; no crea contenido masivo.

## 4. Alcance

1. `GET /api/v1/properties` con paginación.
2. `GET /api/v1/properties/{id}`.
3. Validación de `page`, `pageSize` e `id`.
4. Proyección de campos públicos.
5. Índice MongoDB para el orden del listado.
6. Problem Details.
7. Seed idempotente de doce propiedades de demostración (1.2.0-u2; antes seis) vinculadas a las universidades del seed NV-08.
8. Pruebas unitarias, de integración y de seguridad.
9. Caché HTTP condicional (`ETag`, `Cache-Control`, 304) en listado y detalle.

## 5. Fuera de alcance

- Crear, actualizar o borrar propiedades mediante la API; el seed de inicialización es la única escritura y no forma parte del contrato HTTP.
- Favoritos, visitas, chat, Smart Match, reviews o notificaciones.
- Filtros por universidad, precio, ubicación o texto; no estaban aprobados para U2.
- Exponer dirección exacta, coordenadas, propietario, contacto o campos internos.
- Autenticación o autorización por usuario; las lecturas U2 son públicas.
- Implementar MongoDB o infraestructura.
- Ejecutar NV-02.
- Declarar pruebas no ejecutadas.

## 6. Contrato HTTP

### 6.1 Listado

```http
GET /api/v1/properties?page=1&pageSize=20
```

| Parámetro | Tipo | Default | Regla |
| --- | --- | --- | --- |
| `page` | integer | 1 | >= 1 |
| `pageSize` | integer | 20 | 1–50 |

No se aceptan filtros adicionales en U2. Parámetros desconocidos se ignoran; valores inválidos de `page`/`pageSize` producen 400.

Response 200:

```json
{
  "items": [
    {
      "id": "665f1c2a9b1e4a0012345678",
      "title": "Apartamento cerca del campus",
      "price": 450.0,
      "currency": "USD",
      "city": "San José",
      "universityId": "665f1c2a9b1e4a0012345679",
      "bedrooms": 2,
      "bathrooms": 1,
      "areaM2": 65,
      "createdAt": "2026-11-02T12:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

- Orden estable: `createdAt` descendente y `_id` descendente.
- Resultado vacío: 200 con `items: []`, `totalItems: 0`, `totalPages: 0`.
- `totalPages` se calcula con `ceil(totalItems / pageSize)`.
- `page` fuera de rango devuelve 200 con lista vacía, no error.

### 6.2 Detalle

```http
GET /api/v1/properties/{id}
```

Response 200:

```json
{
  "id": "665f1c2a9b1e4a0012345678",
  "title": "Apartamento cerca del campus",
  "description": "Descripción pública de la propiedad.",
  "price": 450.0,
  "currency": "USD",
  "city": "San José",
  "universityId": "665f1c2a9b1e4a0012345679",
  "bedrooms": 2,
  "bathrooms": 1,
  "areaM2": 65,
  "amenities": ["wifi", "amueblado"],
  "createdAt": "2026-11-02T12:00:00Z"
}
```

No se devuelven `ownerId`, dirección exacta, coordenadas, contacto, estado de moderación ni campos internos.

### 6.3 Validación

| Entrada | Regla | Error |
| --- | --- | --- |
| `id` | ObjectId hexadecimal de 24 caracteres | 400 |
| `id` inexistente | No hay documento | 404 |
| `page` | Entero >= 1 | 400 |
| `pageSize` | Entero 1–50 | 400 |
| `page`/`pageSize` no numéricos | — | 400 |

### 6.4 Status codes

| Código | Condición |
| --- | --- |
| 200 | Listado o detalle |
| 304 | `If-None-Match` coincide con el `ETag` actual; sin cuerpo |
| 400 | ID o paginación inválida |
| 404 | Propiedad no encontrada |
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

## 7. Persistencia y datos públicos

- Colección `properties` en MongoDB.
- Proyección explícita; no se devuelve el documento completo.
- Índice `{ createdAt: -1, _id: -1 }` para el listado.
- `universityId` es una referencia; no se expande a datos internos.
- `price` es decimal no negativo; `currency` es código ISO 4217 de tres letras.
- Los campos públicos permitidos son los del contrato; cualquier campo nuevo requiere revisión de la spec.
- Los datos de prueba no deben contener información personal real.

## 8. Seguridad

- Lecturas públicas; no hay BOLA porque no se accede a recursos de un usuario autenticado.
- No exponer dirección exacta, coordenadas, propietario, contacto ni campos internos.
- Validar `id` antes de consultar MongoDB para evitar entradas malformadas.
- No registrar documentos completos ni datos personales.
- Rate limit: 60 solicitudes/60 s por IP en Redis; 429 con `Retry-After: 60`.
- Si Redis no está disponible, el endpoint falla cerrado con 503.
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
| NV-07-R01 | Contrato de listado paginado | `GET /api/v1/properties` acepta `page`/`pageSize`, devuelve `items`, `page`, `pageSize`, `totalItems`, `totalPages`; orden estable; resultado vacío 200. | F1 | T01, T02, T04, T07, T08 |
| NV-07-R02 | Contrato de detalle | `GET /api/v1/properties/{id}` devuelve 200 con campos públicos o 404; no expone campos internos. | F1 | T01, T02, T05, T08 |
| NV-07-R03 | Validación de ID y paginación | ID ObjectId de 24 hex; `page` >= 1; `pageSize` 1–50; entradas inválidas 400. | F1 | T02, T05, T07 |
| NV-07-R04 | Acceso MongoDB e índice | Proyección explícita, orden `createdAt`/`_id` descendente e índice `{createdAt:-1,_id:-1}`; sin N+1. | F2 | T03, T04, T05, T08 |
| NV-07-R05 | Datos públicos seguros | Respuestas excluyen `ownerId`, dirección exacta, coordenadas, contacto y moderación. | F2 | T01, T03, T06, T07, T09 |
| NV-07-R06 | Problem Details y límites | 400/404/429/503/500 usan Problem Details; rate limit 60/60 s por IP en Redis; fallo de Redis 503. | F3 | T04, T05, T06, T08, T09 |
| NV-07-R07 | Pruebas y evidencia | Unitarias, integración y seguridad; evidencia de comandos, resultados y hallazgos. | F4 | T09, T10, T12 |
| NV-07-R08 | Caché HTTP condicional | GET 200 de listado y detalle devuelven `ETag` débil por contenido y `Cache-Control: public, max-age=60`; `If-None-Match` coincidente devuelve 304 sin cuerpo; los errores mantienen `no-store` sin `ETag`; no se cachean respuestas autenticadas. | F3 | T11, T12 |
| NV-07-R09 | Seed regional de propiedades | Doce propiedades de demostración en la región Tula–Tepeji–Jilotepec, vinculadas por `code` a las universidades de NV-08 1.2.0-u2; precios en MXN realistas para estudiantes; sin direcciones exactas, coordenadas, contactos ni datos personales; upsert idempotente por `id`. | F2 | T13 |

## 11. Testing

- Unitarias: paginación, cálculo de `totalPages`, validación de ObjectId, proyección y DTO.
- Integración: listado con datos, vacío, página fuera de rango, detalle 200, 404, 400, 429, 503.
- Seguridad: ausencia de `ownerId`/dirección/coordenadas, errores sin stack trace, rate limit.
- MongoDB/Redis: dependencia efímera con Testcontainers solo si Fase B autoriza el paquete; alternativa aislada.
- Caché HTTP: `ETag` presente y estable para el mismo contenido, 304 con `If-None-Match` (incluidos lista y `*`), 200 si no coincide, errores sin `ETag` y con `no-store`, interacción con compresión y rate limit.

## 12. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-07-RK01 | Exponer dirección exacta o coordenadas | Privacidad | Proyección y pruebas |
| NV-07-RK02 | Paginación sin máximo | Abuso | `pageSize` máximo 50 |
| NV-07-RK03 | Orden inestable | Duplicados/omisiones | `createdAt` + `_id` |
| NV-07-RK04 | N+1 o `universityId` expandido | Latencia/fuga | Proyección y referencia |
| NV-07-RK05 | MongoDB caído | Error no controlado | 503 Problem Details |
| NV-07-RK06 | Filtros no aprobados | Scope creep | Rechazar en spec |
| NV-07-RK07 | Caché de datos desactualizados o de respuestas no públicas | Datos viejos o fuga | `max-age` corto, `ETag` por contenido y solo endpoints públicos |

## 13. Dependencias

- NV-01 U2 para Problem Details, validación y paginación.
- NV-03 U2 para MongoDB, Redis, HTTPS y despliegue.
- NV-08 comparte convenciones de paginación, pero es independiente.
- NV-02 (CI) quedó completada el 2026-11-05 y la CD se especifica en NV-09 (M-BE-049).

## 14. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 9 |
| Fases del plan | 4 |
| Tasks | 13 |
| Tasks U2 | 13 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 12 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** |

## 15. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta autorización de Fase B.

Cambio 1.1.0-u2: aprobación documental de Victor Manuel Jose Jose, 2026-11-05 (M-BE-046). T11 y T12 requieren la autorización humana «EJECUTA» antes de implementarse.
