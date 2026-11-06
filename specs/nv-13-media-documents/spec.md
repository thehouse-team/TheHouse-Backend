# NV-13 — Fotografías y documentos

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-15, RF-37 (carga de INE y comprobante) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-19 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Módulo | Media |
| Precondición | NV-12 |
| Relación | NV-12 exige documentos para publicar; NV-21 los revisa; NV-14 muestra las fotos |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

El propietario agrega una foto del edificio y hasta cuatro por cuarto, y al publicar sube su identificación oficial (INE) y un comprobante de propiedad. Las fotos son públicas una vez verificado el edificio; los documentos son datos personales sensibles que deben guardarse cifrados y solo puede verlos el equipo de revisión (RNF-19).

## 2. Objetivo

Definir la carga, validación, almacenamiento y entrega segura de fotos y documentos, con costo $0.

## 3. Precondiciones

1. NV-12 (edificios y cuartos).
2. Decisión de almacenamiento y de cifrado resuelta (§13).
3. Autorización «EJECUTA».

## 4. Alcance

1. Carga y borrado de foto del edificio y de hasta 4 por cuarto.
2. Validación por contenido (firma de bytes), re-codificación y eliminación de metadatos EXIF (incluido GPS).
3. Miniaturas para listados.
4. Carga de documentos de verificación cifrados.
5. Acceso a documentos solo para revisores mediante URL temporal (NV-21).

## 5. Fuera de alcance

- Escaneo antivirus con motor externo de pago.
- Reconocimiento automático de la INE.
- Video o recorridos 360.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/landlord/buildings/{id}/photo` | Dueño | Sube o reemplaza la foto del edificio (multipart) | 200, 400, 404, 413, 415 |
| POST | `/api/v1/landlord/rooms/{id}/photos` | Dueño | Agrega una foto al cuarto (máximo 4) | 201, 400, 404, 409, 413, 415 |
| DELETE | `/api/v1/landlord/rooms/{id}/photos/{photoId}` | Dueño | Quita una foto | 204, 404 |
| POST | `/api/v1/landlord/buildings/{id}/documents` | Dueño | Sube INE o comprobante (`kind`) | 201, 400, 404, 413, 415 |
| GET | `/api/v1/landlord/buildings/{id}/documents` | Dueño | Lista metadatos (sin contenido) | 200, 404 |
| GET | `/api/v1/media/{photoId}` | Público si el edificio está Verificado | Entrega la foto o su miniatura | 200, 304, 404 |

- Fotos: JPEG, PNG o WebP, máximo 5 MB, se re-codifican a WebP de 1600 px y miniatura de 400 px.
- Documentos: PDF, JPEG o PNG, máximo 10 MB; `kind` = `ine` o `property_proof` (predial, escritura o contrato).
- Los documentos nunca tienen URL pública; los revisores los obtienen por NV-21 con un enlace que vence en 5 minutos.

## 7. Módulo, datos propios e integración

Módulo **Media** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `media`, `verification_documents` |
| Usa (puertos de otros módulos) | Listings `IListingOwnership` (NV-12) |
| Publica (eventos de dominio) | `VerificationDocumentsCompleted`, `PhotosChanged` |
| Consume (eventos de dominio) | `BuildingReviewDecided (NV-21) para iniciar la retención`, `RoomStatusChanged (NV-12) para borrar fotos de cuartos eliminados` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `media`: `ownerId`, `buildingId`, `roomId`, `storageKey`, `thumbKey`, `contentType`, `bytes`, `sha256`, `createdAt`.
- `verification_documents`: `ownerId`, `buildingId`, `kind`, `storageKey`, `encryptedDataKey`, `sha256`, `uploadedAt`, `retentionUntil`.

## 9. Reglas de negocio

1. Máximo 1 foto de edificio y 4 por cuarto (409 al exceder).
2. El tipo se valida por firma de bytes, no por extensión ni `Content-Type`.
3. Las fotos se re-codifican y pierden todos los metadatos antes de guardarse.
4. Los documentos se cifran con AES-256-GCM antes de salir del proceso; la llave de datos se cifra con una llave maestra fuera de la base.
5. Los documentos se borran 90 días después de la decisión de revisión (propuesta de retención).

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Límite de tamaño aplicado en Kestrel y en el endpoint (413) para evitar consumo de memoria.
- Sin procesamiento de imágenes con librerías vulnerables: versión fijada y escaneo SCA en CI (NV-02).
- Documentos: cifrado en reposo, acceso solo por rol revisor, auditoría de cada lectura (NV-21) y nunca en logs.
- Nombres de archivo del cliente ignorados; claves de almacenamiento aleatorias.
- Fotos de edificios no verificados no se sirven públicamente.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Límite de fotos alcanzado | 409 | Conflicto |
| Archivo demasiado grande | 413 | Archivo demasiado grande |
| Tipo no permitido | 415 | Tipo de archivo no permitido |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-13-R01 | Carga de fotos | Acepta solo JPEG/PNG/WebP ≤ 5 MB validados por contenido; respeta los límites por edificio y cuarto; devuelve id y URL de miniatura. | F1 | T01 |
| NV-13-R02 | Procesamiento seguro de imágenes | Re-codificación a WebP, miniatura y eliminación de EXIF/GPS verificadas con una imagen de prueba que contiene coordenadas. | F2 | T02 |
| NV-13-R03 | Carga de documentos cifrados | INE y comprobante ≤ 10 MB cifrados con AES-256-GCM; la base guarda solo metadatos y la llave de datos cifrada. | F2 | T03 |
| NV-13-R04 | Entrega de fotos | `GET /media/{id}` sirve fotos de edificios Verificados con `ETag` y caché pública; 404 para no verificados. | F2 | T04 |
| NV-13-R05 | Acceso restringido a documentos | Ningún endpoint público o de dueño devuelve el contenido; solo NV-21 genera enlaces temporales; pruebas de acceso denegado. | F3 | T05 |
| NV-13-R06 | Retención y borrado | Borrar una foto o un cuarto elimina sus archivos; documentos con `retentionUntil` y tarea de limpieza. | F3 | T06 |
| NV-13-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Almacenamiento de archivos | Recomendado: MongoDB GridFS (persistencia ya aprobada, dentro de los 512 MB de M0) con fotos comprimidas. Azure Blob es un proveedor permitido pero no es $0 permanente; un servicio de imágenes externo requiere excepción a constitution §30 | PENDIENTE |
| Llave maestra de cifrado | Secreto de Container Apps (M-BE-038) en lugar de Key Vault para mantener $0 | PENDIENTE |
| Retención de documentos | 90 días después de la decisión | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Almacenamiento de fotos y documentos | $0 solo con la opción elegida dentro de su plan gratuito | PENDIENTE DE VERIFICAR |
| Ancho de banda de imágenes | $0 dentro de la concesión de Container Apps | Miniaturas y caché HTTP para reducirlo |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-13-RK01 | Fuga de documentos de identidad | Daño grave a propietarios | Cifrado, rol revisor, URL de 5 minutos y auditoría |
| NV-13-RK02 | Metadatos GPS en fotos | Revela ubicación exacta | Re-codificación sin metadatos |
| NV-13-RK03 | Llenado del almacenamiento gratuito | Servicio interrumpido | Límites por cuarto, compresión y monitoreo |
| NV-13-RK04 | Archivos maliciosos | Explotación del procesador de imágenes | Validación por contenido y librería actualizada |

## 16. Dependencias

- NV-12 (dueño de edificios y cuartos).
- NV-21 (lectura de documentos por revisores).
- NV-14 (fotos en resultados).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
