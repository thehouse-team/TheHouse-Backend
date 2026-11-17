# NV-13 — Fotografías y documentos — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-17 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-13-media-documents/`; sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.1.0: T01–T06 completas; T07–T10 esperan secreto, CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero la validación y re-codificación de imágenes y el cifrado de documentos con pruebas unitarias; después la carga, la entrega pública y privada y los documentos con GridFS; luego la integración con la publicación de NV-12, NV-14 y NV-21 y la retención; al final límites, privacidad y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Fotos de edificio y cuarto; Entrega de fotos |
| F2 | Persistencia e integración | R03, R04, R05 | T03, T04, T05 | Documentos cifrados; Revisión y retención de documentos; Integración con NV-12, NV-14 y NV-21 |
| F3 | Seguridad, autorización y límites | R06 | T06 | Validación, límites y privacidad |
| F4 | Pruebas y evidencia | R07, R08 | T07, T08, T09, T10 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Fotos de edificio y cuarto** (R01): El dueño sube, reemplaza y quita la foto del edificio y hasta 4 por cuarto; validación por firma; re-codificación a WebP 1600/400 sin EXIF ni GPS; 409 `photos_limit`, `storage_limit` y `building_in_review`.
- **T02 · Entrega de fotos** (R02): Ruta pública solo con edificio `verified` (si no, 404), con `ETag`, 304 y caché inmutable; ruta privada para dueño y revisor con `no-store`; miniatura con `size=thumb`.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Documentos cifrados** (R03): El dueño sube o reemplaza `ine` y `property_proof` (PDF, JPEG o PNG ≤ 10 MB); se guardan cifrados con AES-256-GCM; el dueño solo ve metadatos.
- **T04 · Revisión y retención de documentos** (R04): Solo `reviewer` o `admin` leen el contenido descifrado, con bitácora; `retentionUntil` = decisión + 90 días; la limpieza periódica los borra.
- **T05 · Integración con NV-12, NV-14 y NV-21** (R05): La publicación pide foto y documentos; NV-14 agrega `photos` y `thumbUrl`; NV-21 muestra fotos y metadatos de documentos; borrar cuarto o edificio borra sus archivos; respuestas anteriores sin cambios.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T06 · Validación, límites y privacidad** (R06): Firma de bytes, tamaños por ruta, megapíxeles, cuota, rate limits 429, Redis o MongoDB caídos 503, BOLA y nada de contenido en logs.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Contrato para Mobile y despliegue** (R07): Contrato §6.1 entregado a Mobile; secreto de la llave maestra creado con autorización; desplegado por el CD; prueba HTTP real en Azure (subir fotos, ver privada y pública, documentos, publicar, revisar y descargar como revisor).
- **T08 · Pruebas unitarias y de integración** (R08): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T09 · Pruebas de seguridad** (R08): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T10 · Evidencia y revisión final** (R08): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02.
- T06 depende de: T01, T02, T03, T04, T05.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Fotos de edificio y cuarto | Pruebas automatizadas en CI |
| R02 | Entrega de fotos | Pruebas automatizadas en CI |
| R03 | Documentos cifrados | Pruebas automatizadas en CI |
| R04 | Revisión y retención de documentos | Pruebas automatizadas en CI |
| R05 | Integración con NV-12, NV-14 y NV-21 | Pruebas automatizadas en CI |
| R06 | Validación, límites y privacidad | Pruebas automatizadas en CI |
| R07 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: archivos del arrendador solo por su dueño (lo ajeno 404); documentos solo para `reviewer` o `admin`, y cada lectura queda en la bitácora de NV-21.
- Documentos cifrados con AES-256-GCM y una llave de datos por documento envuelta con la llave maestra del secreto de Container Apps; la llave nunca va al repositorio, a logs ni a la base.
- Re-codificación con ImageSharp en versión fijada y escaneo SCA en CI (NV-02); límites de megapíxeles y de memoria para evitar bombas de descompresión.
- Nombres de archivo del cliente ignorados; ids aleatorios; `Content-Disposition` controlado al servir documentos.
- Rate limit: 30 cargas por hora por arrendador y 120 lecturas por minuto por IP en la ruta pública; Redis caído → 503.
- Nada de contenido ni nombres de archivo en logs.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 (GridFS) | $0 | Dentro de 512 MB: fotos WebP (~200 KB + ~30 KB de miniatura) y documentos; cuota por arrendador y alerta en evidencia si la colección pasa de 350 MB |
| Ancho de banda de imágenes | $0 | Dentro de la concesión de Container Apps; miniaturas y caché inmutable para reducirlo |
| SixLabors.ImageSharp | $0 | Paquete NuGet; licencia válida para este proyecto académico |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | PASS (M-BE-073) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-17) |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F2 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **🟡 EN EJECUCIÓN — 1.1.0: T01–T06 completas; T07–T10 esperan secreto, CI, despliegue y Azure**.
