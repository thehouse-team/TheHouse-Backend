# NV-07 — Property Core (U2)

| Campo | Valor |
| --- | --- |
| ID | NV-07 |
| Documento | `spec.md` |
| Versión | 1.0.0-u2 |
| Fecha | 2026-11-02 |
| Estado documental | **✅ APROBADA PARA U2** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-02 |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Plan | `plan.md` 1.0.0-u2 |
| Tasks | `task.md` 1.0.0-u2 |
| Precondición | NV-01 U2 para el contrato; NV-03 U2 para la verificación desplegada; M-BE-027; M-BE-029 |
| Relación | Spec funcional independiente; no autoriza CRUD ni otros módulos |

Este documento está aprobado documentalmente para U2. No autoriza implementación: la ejecución requiere autorización humana de Fase B.

## 1. Contexto

U2 requiere dos lecturas públicas de propiedades: listado paginado y detalle por identificador. No se implementa creación, edición, borrado, favoritos, visitas, chat, Smart Match ni búsqueda avanzada. El modelo debe exponer solo datos públicos seguros.

## 2. Objetivo

Definir el contrato verificable de `GET /api/v1/properties` y `GET /api/v1/properties/{id}`, su paginación, validación, acceso a MongoDB, proyección segura, errores y pruebas.

## 3. Precondiciones

1. NV-01 U2 provee Problem Details, validación, paginación y configuración.
2. NV-03 U2 aprobada documentalmente para la verificación desplegada; las pruebas locales pueden usar MongoDB y Redis aislados.
3. Existe autorización de Fase B.
4. Existe una colección `properties` con datos de prueba o seed controlado; no se crea contenido masivo.

## 4. Alcance

1. `GET /api/v1/properties` con paginación.
2. `GET /api/v1/properties/{id}`.
3. Validación de `page`, `pageSize` e `id`.
4. Proyección de campos públicos.
5. Índice MongoDB para el orden del listado.
6. Problem Details.
7. Pruebas unitarias, de integración y de seguridad.

## 5. Fuera de alcance

- Crear, actualizar o borrar propiedades.
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
| 400 | ID o paginación inválida |
| 404 | Propiedad no encontrada |
| 429 | Rate limit excedido; `Retry-After` |
| 503 | MongoDB no disponible; sin detalles |
| 500 | Error interno; Problem Details sin stack trace |

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
| Validación | 400 | `Validation failed` |
| No encontrado | 404 | `Not found` |
| Rate limit | 429 | `Too many requests` |
| MongoDB no disponible | 503 | `Service unavailable` |
| Interno | 500 | `Internal server error` |

## 10. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-07-R01 | Contrato de listado paginado | `GET /api/v1/properties` acepta `page`/`pageSize`, devuelve `items`, `page`, `pageSize`, `totalItems`, `totalPages`; orden estable; resultado vacío 200. | F1 | T01, T02, T04, T07, T08 |
| NV-07-R02 | Contrato de detalle | `GET /api/v1/properties/{id}` devuelve 200 con campos públicos o 404; no expone campos internos. | F1 | T01, T02, T05, T08 |
| NV-07-R03 | Validación de ID y paginación | ID ObjectId de 24 hex; `page` >= 1; `pageSize` 1–50; entradas inválidas 400. | F1 | T02, T05, T07 |
| NV-07-R04 | Acceso MongoDB e índice | Proyección explícita, orden `createdAt`/`_id` descendente e índice `{createdAt:-1,_id:-1}`; sin N+1. | F2 | T03, T04, T05, T08 |
| NV-07-R05 | Datos públicos seguros | Respuestas excluyen `ownerId`, dirección exacta, coordenadas, contacto y moderación. | F2 | T01, T03, T06, T07, T09 |
| NV-07-R06 | Problem Details y límites | 400/404/429/503/500 usan Problem Details; rate limit 60/60 s por IP en Redis; fallo de Redis 503. | F3 | T04, T05, T06, T08, T09 |
| NV-07-R07 | Pruebas y evidencia | Unitarias, integración y seguridad; evidencia de comandos, resultados y hallazgos. | F4 | T09, T10 |

## 11. Testing

- Unitarias: paginación, cálculo de `totalPages`, validación de ObjectId, proyección y DTO.
- Integración: listado con datos, vacío, página fuera de rango, detalle 200, 404, 400, 429, 503.
- Seguridad: ausencia de `ownerId`/dirección/coordenadas, errores sin stack trace, rate limit.
- MongoDB/Redis: dependencia efímera con Testcontainers solo si Fase B autoriza el paquete; alternativa aislada.

## 12. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-07-RK01 | Exponer dirección exacta o coordenadas | Privacidad | Proyección y pruebas |
| NV-07-RK02 | Paginación sin máximo | Abuso | `pageSize` máximo 50 |
| NV-07-RK03 | Orden inestable | Duplicados/omisiones | `createdAt` + `_id` |
| NV-07-RK04 | N+1 o `universityId` expandido | Latencia/fuga | Proyección y referencia |
| NV-07-RK05 | MongoDB caído | Error no controlado | 503 Problem Details |
| NV-07-RK06 | Filtros no aprobados | Scope creep | Rechazar en spec |

## 13. Dependencias

- NV-01 U2 para Problem Details, validación y paginación.
- NV-03 U2 para MongoDB, Redis, HTTPS y despliegue.
- NV-08 comparte convenciones de paginación, pero es independiente.
- NV-02 permanece **⛔ NO EJECUTAR**.

## 14. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 10 |
| Tasks U2 | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA PARA U2** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B** |

## 15. Aprobación documental y ejecución

Aprobación documental: Victor Manuel Jose Jose, 2026-11-02, versión `1.0.0-u2` de `spec.md`, `plan.md` y `task.md`. La ejecución sigue bloqueada hasta autorización de Fase B.
