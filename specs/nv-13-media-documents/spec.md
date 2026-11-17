# NV-13 — Fotografías y documentos

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `spec.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-76, RF-77 (foto del edificio, fotos de cuartos e identificación); RNF-19 (documentos sensibles) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09, RNF-19 |
| Plan | `plan.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |
| Módulo | Media |
| Precondición | NV-12, NV-14 y NV-21 desplegadas |
| Relación | Completa la publicación de NV-12; NV-14 muestra las fotos en la búsqueda y el detalle; NV-21 revisa fotos y documentos |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.1.0-borrador (2026-11-17):** se adapta a lo ya desplegado y resuelve las decisiones de 1.0.0. Los archivos se guardan en **MongoDB GridFS** dentro de Atlas M0 (persistencia ya aprobada, $0), con cuota por arrendador. Las fotos se re-codifican a WebP con **SixLabors.ImageSharp** (paquete nuevo, solo código administrado), lo que borra EXIF y GPS. Los documentos se cifran con AES-256-GCM y una llave maestra en un secreto de Container Apps (M-BE-038). Como Kestrel limita los cuerpos a 64 KB, las rutas de carga suben su propio límite. La publicación de NV-12 pide ahora la foto del edificio y los dos documentos (los edificios ya verificados no cambian). Las fotos se sirven por un endpoint propio: públicas con caché inmutable si el edificio está verificado, y privadas para el dueño y el revisor antes de eso; NV-14 y NV-21 agregan los campos de fotos sin quitar ninguno.

## 1. Contexto

Hoy los edificios y cuartos se publican sin fotos y sin documentos, así que la búsqueda y el detalle no muestran imágenes y el revisor de NV-21 aprueba sin pruebas. El prototipo pide una foto del edificio al registrarlo («Subir foto · JPG o PNG»), hasta 4 fotos por cuarto, y en «Verificación del propietario» la identificación oficial (INE o pasaporte) y un comprobante de propiedad (predial, escritura o contrato), con el aviso «Tus documentos no se publican. Solo los revisa TheHouse». COST-001 exige $0, y los documentos son datos personales sensibles (RNF-19).

## 2. Objetivo

Que el arrendador suba la foto del edificio, las fotos de cada cuarto y sus documentos de verificación, que los estudiantes vean las fotos de lo publicado y que solo el equipo de revisión vea los documentos, todo con costo $0.

## 3. Precondiciones

1. NV-12, NV-14 y NV-21 desplegadas.
2. Validación del contrato §6.1 con el responsable de Mobile.
3. Autorización «EJECUTA».
4. Antes de desplegar: Victor crea en Container Apps el secreto de la llave maestra de documentos (autorización aparte; nunca en el repositorio).

## 4. Alcance

1. Foto del edificio (una; subir, reemplazar y quitar).
2. Fotos de cada cuarto (hasta 4; agregar y quitar).
3. Validación por firma de bytes, re-codificación a WebP (1600 px y miniatura de 400 px) y borrado de metadatos EXIF y GPS.
4. Entrega de fotos: pública con caché inmutable si el edificio está verificado; privada para el dueño y el revisor antes de eso.
5. Documentos de verificación (`ine` y `property_proof`): subir o reemplazar, cifrados; el dueño ve solo metadatos.
6. Lectura de documentos solo por el equipo de revisión de NV-21, con registro en la bitácora.
7. Retención: los documentos se borran 90 días después de la decisión de revisión.
8. Integración: la publicación de NV-12 exige foto del edificio y los dos documentos; NV-14 y NV-21 agregan los campos de fotos; borrar un cuarto o un edificio borra sus archivos.
9. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Antivirus con motor externo (los documentos nunca se sirven al público y las fotos se re-codifican).
- Lectura automática de la INE o validación con padrones.
- Video, recorridos 360 y más de 4 fotos por cuarto.
- Almacenamiento en Azure Blob o servicios de imágenes externos (no son $0 permanente o requieren excepción a constitution §30).
- Fotos en reseñas o en el perfil del usuario.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| PUT | `/api/v1/landlord/buildings/{id}/photo` | Arrendador (dueño) | Sube o reemplaza la foto del edificio (multipart, campo `file`) | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| DELETE | `/api/v1/landlord/buildings/{id}/photo` | Arrendador (dueño) | Quita la foto del edificio | 204, 400, 401, 403, 404, 409, 429, 503 |
| POST | `/api/v1/landlord/rooms/{id}/photos` | Arrendador (dueño) | Agrega una foto al cuarto (máximo 4) | 201, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| DELETE | `/api/v1/landlord/rooms/{id}/photos/{photoId}` | Arrendador (dueño) | Quita una foto del cuarto | 204, 400, 401, 403, 404, 409, 429, 503 |
| PUT | `/api/v1/landlord/buildings/{id}/documents/{kind}` | Arrendador (dueño) | Sube o reemplaza `ine` o `property_proof` (multipart) | 200, 400, 401, 403, 404, 409, 413, 415, 429, 503 |
| GET | `/api/v1/landlord/buildings/{id}/documents` | Arrendador (dueño) | Metadatos de los documentos, sin contenido | 200, 400, 401, 403, 404, 429, 503 |
| GET | `/api/v1/media/{id}` | Público si el edificio está `verified` | Foto o miniatura (`size=full\|thumb`) | 200, 304, 400, 404, 429, 503 |
| GET | `/api/v1/media/{id}/private` | Dueño o revisor | Foto de un edificio aún no verificado | 200, 400, 401, 404, 429, 503 |
| GET | `/api/v1/admin/buildings/{id}/documents/{documentId}` | Revisor o admin | Contenido descifrado del documento (auditado) | 200, 400, 401, 403, 404, 429, 503 |

- Foto: `{ id, url, thumbUrl, width, height, bytes, createdAt }`. `url` y `thumbUrl` son rutas relativas a `/api/v1/media/{id}` (`?size=thumb` para la miniatura); antes de la verificación usan `/private`.
- Fotos: JPEG, PNG o WebP de hasta 5 MB; se valida la firma de bytes (no la extensión ni el `Content-Type`), se rechazan imágenes de más de 40 megapíxeles y se guardan como WebP de máximo 1600 px y miniatura de 400 px, sin metadatos.
- Documento: `{ id, kind: "ine" | "property_proof", contentType, bytes, uploadedAt }`. PDF, JPEG o PNG de hasta 10 MB; subir otro del mismo `kind` reemplaza el anterior. El contenido nunca sale al dueño ni al público.
- Entrega pública: `Cache-Control: public, max-age=31536000, immutable` y `ETag` (el id cambia si cambia la foto); privada y documentos: `no-store`.
- Campos agregados (sin quitar ninguno): `photo` en el edificio del arrendador y del revisor; `photos` en el cuarto del arrendador, del revisor y en el detalle público de NV-14; `thumbUrl` en cada resultado de la búsqueda de NV-14; `documents` (metadatos) en el detalle del revisor de NV-21.
- Publicación (NV-12): pasos nuevos `building_photo` y `documents` en `GET /landlord/buildings/{id}/publication`.
- Códigos nuevos: 409 `photos_limit` (más de 4 por cuarto), 409 `storage_limit` (cuota del arrendador), 409 `building_in_review` / `building_state` (como NV-12, no se cambian archivos en revisión), 415 `unsupported_media` (firma de bytes no permitida), 413 (tamaño), 400 `image_too_large` (megapíxeles).

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/photo
METHOD       PUT
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      Content-Type: multipart/form-data
REQUEST      campo `file` (JPEG, PNG o WebP ≤ 5 MB)
RESPONSE     200 { id, url, thumbUrl, width, height, bytes, createdAt }
STATUS CODES 200, 400 code=image_too_large, 401, 403, 404, 409 code=building_in_review | storage_limit, 413, 415 code=unsupported_media, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/rooms/{id}/photos
METHOD       POST
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      Content-Type: multipart/form-data
REQUEST      campo `file`
RESPONSE     201 { id, url, thumbUrl, width, height, bytes, createdAt }
STATUS CODES 201, 400, 401, 403, 404, 409 code=photos_limit | storage_limit | building_in_review, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/photo | /api/v1/landlord/rooms/{id}/photos/{photoId}
METHOD       DELETE
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      ids en la ruta
RESPONSE     204
STATUS CODES 204, 400, 401, 403, 404, 409 code=building_in_review, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/documents/{kind}
METHOD       PUT
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      Content-Type: multipart/form-data
REQUEST      `kind` = ine | property_proof; campo `file` (PDF, JPEG o PNG ≤ 10 MB)
RESPONSE     200 { id, kind: "ine" | "property_proof", contentType, bytes, uploadedAt }
STATUS CODES 200, 400, 401, 403, 404, 409 code=building_in_review | storage_limit, 413, 415, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/landlord/buildings/{id}/documents
METHOD       GET
AUTH         Bearer JWT con perfil de arrendador (NV-12)
HEADERS      —
REQUEST      id en la ruta
RESPONSE     200 { items: [{ id, kind: "ine" | "property_proof", contentType, bytes, uploadedAt }] }
STATUS CODES 200, 400, 401, 403, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/media/{id}?size=thumb
METHOD       GET
AUTH         Público (edificio `verified`)
HEADERS      If-None-Match (opcional)
REQUEST      size = full | thumb
RESPONSE     200 image/webp | 304
STATUS CODES 200, 304, 400, 404, 429, 503
PAGINATION   No
```

```text
ENDPOINT     /api/v1/media/{id}/private?size=thumb
METHOD       GET
AUTH         Bearer JWT del dueño o de un revisor
HEADERS      —
REQUEST      size = full | thumb
RESPONSE     200 image/webp (no-store)
STATUS CODES 200, 400, 401, 404, 429, 503
PAGINATION   No
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Media** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `media_items`, `verification_documents`, `GridFS media y documents` |
| Usa (puertos de otros módulos) | Listings `IBuildingRepository`, `IRoomRepository` e `ILandlordProfileRepository` (NV-12) para dueño y estado; Moderation `StaffService` (NV-21) para el rol de revisión y la bitácora |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- GridFS `media` (fotos WebP y miniaturas) y `documents` (contenido cifrado) en la base actual; metadatos en `media_items`: `ownerId`, `buildingId`, `roomId` (null para la del edificio), `fileId`, `thumbFileId`, `width`, `height`, `bytes`, `sha256`, `createdAt`.
- `verification_documents`: `ownerId`, `buildingId`, `kind`, `fileId`, `contentType`, `bytes`, `sha256` (del contenido sin cifrar), `nonce`, `wrappedKey`, `uploadedAt`, `retentionUntil`; único `{ buildingId, kind }`.
- Índices: `{ buildingId, roomId }` y `{ ownerId }` en `media_items` (cuota por arrendador con la suma de `bytes`); `{ retentionUntil }` en `verification_documents` para la limpieza.
- Sin migraciones: los edificios ya verificados quedan igual; solo los nuevos envíos a revisión piden foto y documentos.

## 9. Reglas de negocio

1. Solo el dueño sube o quita archivos de su edificio o cuarto, y solo con el edificio en `draft` o `verified` (en revisión → 409 `building_in_review`; rechazado o suspendido → 409 `building_state`).
2. Una foto de edificio y hasta 4 por cuarto (409 `photos_limit`); cuota de 60 MB por arrendador entre fotos y documentos (409 `storage_limit`).
3. Tipo por firma de bytes; tamaño máximo por ruta (5 MB fotos, 10 MB documentos) aplicado antes de leer el cuerpo completo; imágenes de más de 40 megapíxeles → 400 `image_too_large`.
4. Las fotos se re-codifican siempre (el archivo original nunca se guarda); los documentos se cifran antes de guardarse y no se re-codifican.
5. Una foto es pública solo si su edificio está `verified` y el cuarto sigue existiendo; si el edificio se suspende, deja de servirse en la ruta pública.
6. Enviar a revisión (NV-12) exige foto del edificio y los dos documentos.
7. Al decidir la revisión (NV-21: aprobar o rechazar) se fija `retentionUntil` = decisión + 90 días; una tarea periódica con candado de Redis borra los vencidos. Si el edificio vuelve a revisión, el dueño sube documentos nuevos.
8. Borrar un cuarto o un edificio (NV-12) borra sus fotos y documentos.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- BOLA: archivos del arrendador solo por su dueño (lo ajeno 404); documentos solo para `reviewer` o `admin`, y cada lectura queda en la bitácora de NV-21.
- Documentos cifrados con AES-256-GCM y una llave de datos por documento envuelta con la llave maestra del secreto de Container Apps; la llave nunca va al repositorio, a logs ni a la base.
- Re-codificación con ImageSharp en versión fijada y escaneo SCA en CI (NV-02); límites de megapíxeles y de memoria para evitar bombas de descompresión.
- Nombres de archivo del cliente ignorados; ids aleatorios; `Content-Disposition` controlado al servir documentos.
- Rate limit: 30 cargas por hora por arrendador y 120 lecturas por minuto por IP en la ruta pública; Redis caído → 503.
- Nada de contenido ni nombres de archivo en logs.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Sin rol de revisión | 403 | Acceso denegado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Límite de fotos, cuota o edificio en revisión | 409 | Conflicto (`code`: `photos_limit`, `storage_limit`, `building_in_review`, `building_state`) |
| Archivo mayor al límite de la ruta | 413 | Solicitud demasiado grande |
| Tipo de archivo no permitido por su firma de bytes | 415 | Tipo de contenido no admitido (`code`: `unsupported_media`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-13-R01 | Fotos de edificio y cuarto | El dueño sube, reemplaza y quita la foto del edificio y hasta 4 por cuarto; validación por firma; re-codificación a WebP 1600/400 sin EXIF ni GPS; 409 `photos_limit`, `storage_limit` y `building_in_review`. | F1 | T01 |
| NV-13-R02 | Entrega de fotos | Ruta pública solo con edificio `verified` (si no, 404), con `ETag`, 304 y caché inmutable; ruta privada para dueño y revisor con `no-store`; miniatura con `size=thumb`. | F1 | T02 |
| NV-13-R03 | Documentos cifrados | El dueño sube o reemplaza `ine` y `property_proof` (PDF, JPEG o PNG ≤ 10 MB); se guardan cifrados con AES-256-GCM; el dueño solo ve metadatos. | F2 | T03 |
| NV-13-R04 | Revisión y retención de documentos | Solo `reviewer` o `admin` leen el contenido descifrado, con bitácora; `retentionUntil` = decisión + 90 días; la limpieza periódica los borra. | F2 | T04 |
| NV-13-R05 | Integración con NV-12, NV-14 y NV-21 | La publicación pide foto y documentos; NV-14 agrega `photos` y `thumbUrl`; NV-21 muestra fotos y metadatos de documentos; borrar cuarto o edificio borra sus archivos; respuestas anteriores sin cambios. | F2 | T05 |
| NV-13-R06 | Validación, límites y privacidad | Firma de bytes, tamaños por ruta, megapíxeles, cuota, rate limits 429, Redis o MongoDB caídos 503, BOLA y nada de contenido en logs. | F3 | T06 |
| NV-13-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; secreto de la llave maestra creado con autorización; desplegado por el CD; prueba HTTP real en Azure (subir fotos, ver privada y pública, documentos, publicar, revisar y descargar como revisor). | F4 | T07 |
| NV-13-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Almacenamiento | MongoDB GridFS en Atlas M0 (persistencia ya aprobada, $0) con cuota de 60 MB por arrendador; Azure Blob y servicios de imágenes externos fuera | PROPUESTO |
| Procesamiento de imágenes | SixLabors.ImageSharp 3.x (paquete nuevo, administrado, sin binarios nativos; su licencia permite uso en proyectos con ingresos menores a 1 M USD) en versión fijada; alternativa: SkiaSharp | PROPUESTO |
| Cifrado de documentos | AES-256-GCM con llave de datos por documento envuelta por una llave maestra en un secreto de Container Apps (M-BE-038); Victor crea el secreto con autorización aparte | PROPUESTO |
| Publicación | NV-12 exige foto del edificio, INE y comprobante para enviar a revisión; los edificios ya verificados no cambian | PROPUESTO |
| Entrega de fotos | Endpoint propio: público e inmutable para edificios verificados, privado para dueño y revisor; sin URLs firmadas | PROPUESTO |
| Tamaños | Fotos ≤ 5 MB y documentos ≤ 10 MB (el prototipo decía 15 MB; se baja por el límite de 512 MB de M0) | PROPUESTO |
| Retención | Documentos 90 días después de la decisión de revisión; limpieza periódica con candado de Redis | PROPUESTO |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 (GridFS) | $0 | Dentro de 512 MB: fotos WebP (~200 KB + ~30 KB de miniatura) y documentos; cuota por arrendador y alerta en evidencia si la colección pasa de 350 MB |
| Ancho de banda de imágenes | $0 | Dentro de la concesión de Container Apps; miniaturas y caché inmutable para reducirlo |
| SixLabors.ImageSharp | $0 | Paquete NuGet; licencia válida para este proyecto académico |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-13-RK01 | Llenar los 512 MB de M0 | Base de datos llena | Re-codificación a WebP, cuota por arrendador, límite de 4 fotos y retención de documentos |
| NV-13-RK02 | Fuga de un documento de identidad | Daño grave a la persona | Cifrado con llave fuera de la base, lectura solo por revisores con bitácora, nunca en logs ni URLs públicas |
| NV-13-RK03 | Imagen maliciosa (bomba de descompresión o exploit del decodificador) | Caída o ejecución de código | Límite de megapíxeles, versión fijada con SCA y re-codificación en proceso con límites de memoria |
| NV-13-RK04 | Edificios de demostración sin fotos | Búsqueda sin imágenes | Se muestran sin foto; el seed de NV-14 puede agregarlas en una versión posterior |
| NV-13-RK05 | Pérdida de la llave maestra | Documentos ilegibles | Secreto respaldado por Victor fuera del repositorio; los documentos solo se necesitan durante la revisión |

## 16. Dependencias

- NV-12 (edificios, cuartos y publicación), NV-14 (búsqueda y detalle) y NV-21 (revisión y bitácora).
- Secreto de Container Apps creado por Victor (M-BE-038).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
