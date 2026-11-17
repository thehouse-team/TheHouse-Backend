# NV-13 — Fotografías y documentos — Task list

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `task.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-13-media-documents/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks totales | 10 |
| Tasks completadas | 0 |

## Reglas de esta lista

1. Todas las tasks están abiertas; escribir este documento no completa ninguna.
2. Una task solo se completa con aceptación, evidencia reproducible y revisión.
3. Ninguna task se ejecuta sin «EJECUTA»; las que dependen de una decisión PENDIENTE de la spec quedan BLOCKED.
4. Cada iteración revisa coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y busca brechas de seguridad antes de continuar.
5. Un control no ejecutado no se reporta como PASS.

## F1 — Contrato y dominio

- [ ] **T01 · Fotos de edificio y cuarto** — Requisito: NV-13-R01 · Fase: F1 · Aceptación: El dueño sube, reemplaza y quita la foto del edificio y hasta 4 por cuarto; validación por firma; re-codificación a WebP 1600/400 sin EXIF ni GPS; 409 `photos_limit`, `storage_limit` y `building_in_review`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».
- [ ] **T02 · Entrega de fotos** — Requisito: NV-13-R02 · Fase: F1 · Aceptación: Ruta pública solo con edificio `verified` (si no, 404), con `ETag`, 304 y caché inmutable; ruta privada para dueño y revisor con `no-store`; miniatura con `size=thumb`. · Evidencia: contrato OpenAPI, DTOs y pruebas unitarias del dominio. · Depende de: autorización «EJECUTA».

## F2 — Persistencia e integración

- [ ] **T03 · Documentos cifrados** — Requisito: NV-13-R03 · Fase: F2 · Aceptación: El dueño sube o reemplaza `ine` y `property_proof` (PDF, JPEG o PNG ≤ 10 MB); se guardan cifrados con AES-256-GCM; el dueño solo ve metadatos. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T04 · Revisión y retención de documentos** — Requisito: NV-13-R04 · Fase: F2 · Aceptación: Solo `reviewer` o `admin` leen el contenido descifrado, con bitácora; `retentionUntil` = decisión + 90 días; la limpieza periódica los borra. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.
- [ ] **T05 · Integración con NV-12, NV-14 y NV-21** — Requisito: NV-13-R05 · Fase: F2 · Aceptación: La publicación pide foto y documentos; NV-14 agrega `photos` y `thumbUrl`; NV-21 muestra fotos y metadatos de documentos; borrar cuarto o edificio borra sus archivos; respuestas anteriores sin cambios. · Evidencia: repositorio, índices y pruebas de integración. · Depende de: T01, T02.

## F3 — Seguridad, autorización y límites

- [ ] **T06 · Validación, límites y privacidad** — Requisito: NV-13-R06 · Fase: F3 · Aceptación: Firma de bytes, tamaños por ruta, megapíxeles, cuota, rate limits 429, Redis o MongoDB caídos 503, BOLA y nada de contenido en logs. · Evidencia: pruebas de seguridad y de autorización. · Depende de: T01, T02, T03, T04, T05.

## F4 — Pruebas y evidencia

- [ ] **T07 · Contrato para Mobile y despliegue** — Requisito: NV-13-R07 · Fase: F4 · Aceptación: Contrato §6.1 entregado a Mobile; secreto de la llave maestra creado con autorización; desplegado por el CD; prueba HTTP real en Azure (subir fotos, ver privada y pública, documentos, publicar, revisar y descargar como revisor). · Evidencia: reportes de prueba y `evidence.md`. · Depende de: T01, T02, T03, T04, T05, T06.
- [ ] **T08 · Pruebas unitarias y de integración** — Requisito: NV-13-R08 · Fase: F4 · Aceptación: Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago. · Evidencia: TRX y cobertura del run de CI. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T09 · Pruebas de seguridad** — Requisito: NV-13-R08 · Fase: F4 · Aceptación: BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs. · Evidencia: TRX de `TheHouse.SecurityTests`. · Depende de: T01, T02, T03, T04, T05, T06, T07.
- [ ] **T10 · Evidencia y revisión final** — Requisito: NV-13-R08 · Fase: F4 · Aceptación: `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED. · Evidencia: `evidence.md`. · Depende de: T08, T09.

## Matriz task → requisito

| Task | Requisito(s) | Fase | Estado |
| --- | --- | --- | --- |
| T01 | R01 | F1 | ABIERTA |
| T02 | R02 | F1 | ABIERTA |
| T03 | R03 | F2 | ABIERTA |
| T04 | R04 | F2 | ABIERTA |
| T05 | R05 | F2 | ABIERTA |
| T06 | R06 | F3 | ABIERTA |
| T07 | R07 | F4 | ABIERTA |
| T08 | R08 | F4 | ABIERTA |
| T09 | R08 | F4 | ABIERTA |
| T10 | R08 | F4 | ABIERTA |

## Cobertura requisito → task

| Requisito | Tasks |
| --- | --- |
| R01 | T01 |
| R02 | T02 |
| R03 | T03 |
| R04 | T04 |
| R05 | T05 |
| R06 | T06 |
| R07 | T07 |
| R08 | T08, T09, T10 |

## Resumen

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Tasks | 10 |
| Tasks abiertas | 10 |
| Tasks completadas | 0 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T10 autorizadas** |

Ninguna task puede ejecutarse sin la autorización humana «EJECUTA» con versión, tasks y rutas.
