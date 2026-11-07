# NV-11 — Perfil, universidad y preferencias — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-11 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el contrato (incluido el de Mobile) y la validación de preferencias con pruebas unitarias; después la persistencia, la extensión de universidades y la búsqueda; al final límites, pruebas de dos usuarios y privacidad, el seed autorizado de coordenadas y la prueba real en Azure antes de avisar a Mobile.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03 | T01, T02, T03 | Perfil propio; Selección de universidad; Preferencias de búsqueda |
| F2 | Persistencia e integración | R04, R05, R06 | T04, T05, T06 | Búsqueda de universidades por texto; Búsqueda por cercanía; Persistencia e índices |
| F3 | Seguridad, autorización y límites | R07 | T07 | Autorización, límites y privacidad |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Perfil propio** (R01): `GET`/`PATCH /users/me/profile`; `name` de 1 a 80 caracteres; campos no permitidos 400; perfil inexistente responde 200 con valores vacíos.
- **T02 · Selección de universidad** (R02): `PUT /users/me/university` valida que el id exista (404 si no; 400 si el formato es inválido), es idempotente y se refleja en `GET /users/me/profile`.
- **T03 · Preferencias de búsqueda** (R03): `PUT` valida rangos, catálogos y repetidos; rechaza `quickSetupCompleted` y `version` del cliente; incrementa `version`; deriva `quickSetupCompleted`; `GET` sin datos devuelve los valores por defecto.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T04 · Búsqueda de universidades por texto** (R04): Encuentra por siglas, nombre o ciudad, parcial y sin distinguir acentos ni mayúsculas (`tula`, `UAEH`, `tepeji`); máximo 20; `q` fuera de 2–60 o combinado con coordenadas 400.
- **T05 · Búsqueda por cercanía** (R05): Con `lat`/`lng` devuelve las universidades más cercanas con `distanceKm`, ordenadas; las 10 del seed tienen coordenadas verificadas y cargadas con seed autorizado.
- **T06 · Persistencia e índices** (R06): Documento único por usuario en perfil y preferencias; índice `2dsphere` y `searchKey` en universidades; `GET /universities` y `/universities/{id}` sin cambios de contrato.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Autorización, límites y privacidad** (R07): Sin `userId` del cliente; pruebas con dos usuarios; rate limit 429 con `Retry-After`; Redis caído 503; `no-store`; las coordenadas y el nombre no aparecen en logs (prueba que lo verifica).

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (perfil, universidad, búsqueda por texto y cercanía, preferencias).
- **T09 · Pruebas unitarias y de integración** (R09): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T10 · Pruebas de seguridad** (R09): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T11 · Evidencia y revisión final** (R09): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F3 Seguridad, autorización y límites
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: T01, T02, T03.
- T05 depende de: T01, T02, T03.
- T06 depende de: T01, T02, T03.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Perfil propio | Pruebas automatizadas en CI |
| R02 | Selección de universidad | Pruebas automatizadas en CI |
| R03 | Preferencias de búsqueda | Pruebas automatizadas en CI |
| R04 | Búsqueda de universidades por texto | Pruebas automatizadas en CI |
| R05 | Búsqueda por cercanía | Pruebas automatizadas en CI |
| R06 | Persistencia e índices | Pruebas automatizadas en CI |
| R07 | Autorización, límites y privacidad | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Solo el dueño lee o modifica su perfil y preferencias (filtro por `sub`); no hay forma de pedir los de otro usuario.
- Rate limit en Redis de 60 solicitudes por 60 s por usuario para perfil y preferencias, y de 60 por 60 s por IP para la búsqueda pública; si Redis falla, 503.
- El texto de búsqueda se escapa antes de usarlo como patrón (sin inyección de expresiones regulares ni ReDoS).
- `name` es dato personal: nunca aparece en logs.
- Las coordenadas enviadas para buscar universidad no se registran en logs ni se persisten (RNF-02, RNF-11).
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Dos colecciones pequeñas y un índice `2dsphere` sobre 10 universidades; un comando Redis por solicitud |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F4 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
