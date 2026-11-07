# NV-12 — Arrendador, edificios y cuartos — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-12 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-07 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-07 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T13; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-12-landlord-buildings-rooms/`) |
| Estado de ejecución | **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio (estados, validaciones y distancia) con pruebas unitarias; después perfil, edificios y cuartos con persistencia y concurrencia; luego publicación y tablero; al final autorización, límites, pruebas de dos arrendadores y la prueba real en Azure antes de avisar a Mobile.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04, R05 | T01, T02, T03, T04, T05 | Alta de arrendador; Gestión de edificios; Generación de cuartos; Edición y acciones de cuartos; Máquinas de estados |
| F2 | Persistencia e integración | R06, R07, R08 | T06, T07, T08 | Cálculo de distancia; Persistencia y concurrencia; Publicación y tablero |
| F3 | Seguridad, autorización y límites | R09 | T09 | Autorización y límites |
| F4 | Pruebas y evidencia | R10, R11 | T10, T11, T12, T13 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Alta de arrendador** (R01): `POST /landlord/profile` activa el modo una sola vez (409 `landlord_exists`); valida teléfono, RFC si es `agency` y la versión de términos vigente; `GET` devuelve el perfil o 404.
- **T02 · Gestión de edificios** (R02): Crear, consultar, editar y borrar edificios propios con validación de dirección, ubicación, tipo, catálogo de servicios y universidad existente; límite de 20; borrado bloqueado con cuartos `rented`.
- **T03 · Generación de cuartos** (R03): `rooms/generate` crea cuartos numerados por piso (101, 102, 201…) con datos base; respeta 200 por edificio y no duplica números.
- **T04 · Edición y acciones de cuartos** (R04): Editar, duplicar (siguiente número del piso), copiar datos de otro cuarto del mismo edificio, pausar, reanudar y borrar respetan estados y versiones.
- **T05 · Máquinas de estados** (R05): Transiciones válidas de edificio y cuarto en el dominio, `setup` → `ready` automático al completar datos; transiciones inválidas 409 con `code`; prueba de cada transición.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T06 · Cálculo de distancia** (R06): Distancia (1 decimal) y minutos a pie (5 km/h) a la universidad elegida, recalculados al cambiar ubicación o universidad; pruebas con coordenadas del seed.
- **T07 · Persistencia y concurrencia** (R07): Índices únicos y geoespacial; `version` incrementada en cada escritura; dos ediciones simultáneas con la misma versión: una gana y la otra recibe 409.
- **T08 · Publicación y tablero** (R08): `publication` lista pasos; `publish` pasa a `in_review` solo si se cumplen (409 `publication_incomplete` si no); el tablero devuelve edificios, cuartos, disponibles, ocupación e ingreso mensual de cuartos `rented`.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T09 · Autorización y límites** (R09): Pruebas con dos arrendadores y un usuario sin perfil (404 ajeno, 403 sin perfil); asignación masiva 400; rate limits con 429 y `Retry-After`; Redis caído 503; `no-store`; teléfono, RFC y dirección fuera de logs.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T10 · Contrato para Mobile y despliegue** (R10): Contrato de la §6.1 validado y entregado a Mobile con URL desplegada, pruebas y prueba HTTP real en Azure (alta, edificio, generar, editar, pausar, publicar, ajeno 404).
- **T11 · Pruebas unitarias y de integración** (R11): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T12 · Pruebas de seguridad** (R11): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T13 · Evidencia y revisión final** (R11): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T04 depende de: autorización «EJECUTA».
- T05 depende de: autorización «EJECUTA».
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05.
- T08 depende de: T01, T02, T03, T04, T05.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09.
- T11 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T12 depende de: T01, T02, T03, T04, T05, T06, T07, T08, T09, T10.
- T13 depende de: T11, T12.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Alta de arrendador | Pruebas automatizadas en CI |
| R02 | Gestión de edificios | Pruebas automatizadas en CI |
| R03 | Generación de cuartos | Pruebas automatizadas en CI |
| R04 | Edición y acciones de cuartos | Pruebas automatizadas en CI |
| R05 | Máquinas de estados | Pruebas automatizadas en CI |
| R06 | Cálculo de distancia | Pruebas automatizadas en CI |
| R07 | Persistencia y concurrencia | Pruebas automatizadas en CI |
| R08 | Publicación y tablero | Pruebas automatizadas en CI |
| R09 | Autorización y límites | Pruebas automatizadas en CI |
| R10 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R11 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Ningún endpoint acepta `ownerId`, `userId`, `status`, `version` calculada ni campos calculados (asignación masiva → 400).
- Dirección exacta, teléfono, RFC y nombre completo solo salen en endpoints del dueño; nunca en logs (RNF-02).
- Validación estricta de RFC (persona moral, 12 caracteres con dígito verificador) y teléfono (E.164 de México).
- Rate limit en Redis: 60 lecturas y 30 escrituras por minuto por usuario; `rooms/generate` 10 por hora; Redis caído → 503.
- Pruebas con dos arrendadores y un usuario sin perfil: ninguno lee ni modifica lo del otro (404) y el usuario sin perfil recibe 403.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Tres colecciones pequeñas y un índice `2dsphere`; un comando Redis por solicitud |

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
| R04 | F1 | T04 |
| R05 | F1 | T05 |
| R06 | F2 | T06 |
| R07 | F2 | T07 |
| R08 | F2 | T08 |
| R09 | F3 | T09 |
| R10 | F4 | T10 |
| R11 | F4 | T11, T12, T13 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`**.
