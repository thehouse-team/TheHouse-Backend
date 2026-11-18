# NV-13 — Evidencia: fotografías y documentos

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-13 1.1.0, tasks T01–T10 (M-BE-073); cambio de librería aprobado el mismo día («Sí, usa SkiaSharp») |
| Rama | `feat/nv-13-media-v2` → PR #53, merge `011c2ad` (reemplazó a #52) |
| Resultado | **✅ PASS WITH WARNINGS** — T01–T10 completas: CI 934/934, desplegado en `ca-thehouse-u2--c011c2ad-48-1`, prueba HTTP real en Azure 23/23 |

No se leyeron ni mostraron `appsettings.Development.json`, `terraform.tfvars` ni `terraform.tfstate`. La llave maestra de producción no existe todavía y nunca se escribe en el repositorio; las pruebas usan una llave de prueba propia.

## 1. Cambios por capa

| Capa | Archivo | Cambio |
| --- | --- | --- |
| Domain | `Entities/Media.cs` (nuevo) | `MediaPhoto`, `VerificationDocument` (nonce y llave de datos envuelta), `DocumentKinds` |
| Application | `Abstractions/Media/IMediaProcessing.cs`, `IMediaPort.cs` (nuevos) | Puertos `IImageProcessor`, `IDocumentCipher`, `IMediaPort` (resumen de fotos, documentos, pasos de publicación, retención y borrado) y `MediaRejectedException` |
| Application | `Abstractions/Persistence/IMediaRepository.cs` (nuevo) | Metadatos y bytes de fotos y documentos, cuota, retención y borrado por edificio o cuarto |
| Application | `Contracts/Media/MediaContracts.cs` (nuevo) | `PhotoResponse`, `DocumentResponse` |
| Application | `UseCases/MediaServices.cs` (nuevo) | `MediaRules` (firma de bytes, límites, rutas), `MediaService` (fotos, documentos, entrega pública y privada, lectura del revisor con bitácora), `MediaPort`, `MediaOptions` |
| Application | `BuildingService`, `RoomService`, `SearchService`, `ModerationServices` (NV-12, NV-14, NV-21) | Puerto opcional al final del constructor: pasos `building_photo` y `documents`, `photo`/`photos` en lecturas, `thumbnailUrl` y `photos` en la búsqueda, `documents` en el detalle del revisor, retención al aprobar o rechazar, borrado de archivos al borrar cuarto o edificio |
| Application | `Contracts/Landlord`, `Contracts/Search`, `Contracts/Moderation` | Campos nuevos al final con valor por defecto (`photo`, `photos`, `documents`); ninguno se quita |
| Infrastructure | `Media/MediaAdapters.cs` (nuevo) | `SkiaImageProcessor` (SkiaSharp 4.154.0: encabezado primero, orientación, WebP 1600/400 sin metadatos) y `AesGcmDocumentCipher` (AES-256-GCM con llave de datos envuelta) |
| Infrastructure | `Persistence/Mongo/MongoMediaRepository.cs` (nuevo) | `media_items`, `verification_documents` y buckets GridFS `media` y `documents`; índice único `{ buildingId, kind }` |
| Infrastructure | `TheHouse.Infrastructure.csproj` | `SkiaSharp` y `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.154.0 (MIT) |
| API | `Http/MediaEndpoint.cs`, `Configuration/MediaConfiguration.cs` (nuevos), `Program.cs` | 9 endpoints; límite de cuerpo propio por ruta; `MediaRetentionWorker` cada 6 h con candado de Redis; llave desde `Media__DocumentMasterKey` |
| API | `Http/ProblemResponses.cs`, `UserResourceProblems.cs`, `UserEndpointSupport.cs` | Mensajes de archivos; `code` también en 400 y 415; `unsupported_media`, `image_too_large`, `photos_limit`, `storage_limit` |
| Pruebas | `UnitTests/MediaTests.cs`, `IntegrationTests/MediaHttpTests.cs`, `SecurityTests/MediaSecurityTests.cs`, `IntegrationTests/MediaTestDoubles.cs` | 6 unitarias, 4 de integración (SkiaSharp y cifrado reales) y 8 de seguridad; las pruebas de NV-12, NV-14 y NV-21 que listan campos agregan el campo nuevo; las listas OpenAPI agregan las 9 operaciones |

## 2. Comandos y resultados (local, 2026-11-17)

| Comando | Resultado |
| --- | --- |
| `dotnet build TheHouse.slnx -warnaserror` | 0 advertencias, 0 errores |
| `dotnet test TheHouse.slnx` (dos corridas seguidas) | 934/934 en ambas: unitarias 353, seguridad 115, integración 466 |
| `dotnet build TheHouse.slnx --configuration Release -warnaserror` y `dotnet test --configuration Release` (como el CI) | 0 advertencias; 934/934 |

## 3. Trazabilidad requisito → prueba

| Requisito | Pruebas |
| --- | --- |
| R01 Fotos de edificio y cuarto | `Owner_uploads_photos_and_documents_and_only_the_owner_sees_them_before_review` (reemplazo, 4 por cuarto, `photos_limit`, borrar), `Photos_are_reencoded_to_webp_without_exif_or_gps`, `Types_are_detected_by_byte_signature_not_by_name` |
| R02 Entrega de fotos | `Photo_urls_are_public_only_when_asked`, `Publication_requires_files_and_reviewers_see_photos_and_documents` (pública inmutable, `ETag` y 304 tras verificar), `Owner_uploads_photos_and_documents_and_only_the_owner_sees_them_before_review` (privada `no-store`, pública 404 antes de verificar) |
| R03 Documentos cifrados | `Documents_round_trip_and_tampering_is_detected`, `Without_a_valid_master_key_documents_are_unavailable`, `Owner_uploads_photos_and_documents_and_only_the_owner_sees_them_before_review` (contenido cifrado en el almacén, solo metadatos al dueño), `Replacing_a_document_keeps_one_per_kind_and_deleting_a_building_removes_its_files` |
| R04 Revisión y retención de documentos | `Publication_requires_files_and_reviewers_see_photos_and_documents` (revisor lee el original, bitácora `document.viewed`, dueño 403, retención al aprobar), `Documents_expire_90_days_after_the_review_decision` |
| R05 Integración con NV-12, NV-14 y NV-21 | `Publication_needs_the_building_photo_and_both_documents`, `Snapshot_groups_building_and_room_photos`, `Publication_requires_files_and_reviewers_see_photos_and_documents`, `Deleting_a_room_removes_its_photos_and_the_store_fails_closed`, pruebas de NV-12/NV-14/NV-21 con el campo nuevo |
| R06 Validación, límites y privacidad | `Files_are_validated_by_content_size_and_type` (SVG y ejecutable 415, `kind` inválido 400, JSON 415, 5 MB + 1 → 413, sin modo arrendador 403), `Huge_images_are_rejected_from_the_header_and_garbage_is_unsupported`, `Media_codes_have_a_spanish_detail`, BOLA en `Owner_uploads...` |
| R07 Contrato para Mobile y despliegue | Secreto creado; CD run 44200967522; prueba HTTP real en Azure 23/23 (§6); contrato entregado (§7) |
| R08 Pruebas, seguridad y evidencia | TRX del CI run 44200564148: 934/934; `TheHouse.SecurityTests` 115/115; cobertura en §5; esta evidencia |

## 4. Seguridad y coherencia

- **RNF-19:** los documentos se cifran con AES-256-GCM antes de guardarse (llave de datos por documento envuelta con la llave maestra); la manipulación se detecta; sin llave maestra los documentos responden 503 y las fotos siguen funcionando. Solo `reviewer`/`admin` leen el contenido, con bitácora; el dueño ve metadatos.
- **Metadatos:** toda foto se vuelve a codificar a WebP; la prueba inyecta un EXIF con coordenadas y verifica que la salida no lo contiene. El archivo original nunca se guarda.
- **Entradas hostiles:** tipo por firma de bytes (SVG, ejecutables y HTML rechazados); megapíxeles revisados en el encabezado antes de decodificar; tamaño acotado por ruta antes de leer el cuerpo; nombres de archivo ignorados.
- **BOLA:** archivos solo del dueño (otro arrendador 404, sin modo arrendador 403); foto privada para dueño o revisor; pública solo con edificio verificado.
- **Costo:** GridFS en Atlas M0 con cuota de 60 MB por arrendador; WebP y miniaturas reducen espacio y ancho de banda.
- **Coherencia:** las respuestas anteriores solo agregan campos al final; los edificios ya verificados no cambian; la exigencia de archivos para publicar se puede apagar solo por configuración (`Media:RequirePublicationFiles`), y en producción queda encendida por defecto.

### Decisiones de implementación (dentro de la spec)

| Tema | Decisión |
| --- | --- |
| Librería de imágenes | SkiaSharp 4.154.0 en lugar de ImageSharp 3.2.0, que ahora exige llave de licencia para compilar en Release (cambio aprobado por Victor y registrado en M-BE-073) |
| Miniatura en la búsqueda | Se llena el campo existente `thumbnailUrl` de NV-14 (antes siempre `null`) en lugar de agregar `thumbUrl` |
| Campos en respuestas de escritura | `photo`/`photos` se llenan en las lecturas (detalle, listados, revisión); en respuestas de crear o editar pueden venir `null` |
| Pruebas anteriores | `Media:RequirePublicationFiles=false` en la fábrica de pruebas para no cargar archivos en cada prueba de NV-12/NV-21; las pruebas de NV-13 lo encienden |
| Retención | La tarea corre cada 6 horas con candado de Redis; solo `approve` y `reject` fijan la retención |
| Llave de prueba | Se genera al iniciar las pruebas (32 bytes aleatorios); el primer PR (#52) la tenía fija y Gitleaks la marcó en el historial, por eso la implementación se re-subió en una rama nueva sin esa llave |

## 5. Despliegue y CI

| Paso | Resultado |
| --- | --- |
| PRs | [#51](https://github.com/thehouse-team/TheHouse-Backend/pull/51) (spec 1.1.0) y [#53](https://github.com/thehouse-team/TheHouse-Backend/pull/53) (aprobación e implementación), aprobados y mezclados por `IsaiAxel` (merge `011c2ad`). [#52](https://github.com/thehouse-team/TheHouse-Backend/pull/52) se cerró: su CI falló por una llave de prueba fija en el historial (Gitleaks) y por un método de SkiaSharp obsoleto en Release; #53 rehízo la rama sin esa llave. La rama remota de #52 se borró con autorización de Victor para que el escaneo de todo el historial pasara |
| CI de `main` (`011c2ad`, run 44200564148) | 7/7 checks obligatorios en verde (incluidos Gitleaks y `container-security` con la imagen que trae SkiaSharp); mutación semanal omitida |
| Pruebas en CI | 934/934: unitarias 353, integración 466, seguridad 115 |
| Cobertura de líneas (CI) | 68.0 % total (ramas 72.0 %). `MediaServices` 94.4 %, `MediaEndpoint` 90.9 %, `MediaAdapters` 79.2 % (ramas de orientación EXIF no ejercitadas), `MediaConfiguration` 55.6 % (el ciclo de la tarea de retención no corre en pruebas; su lógica está en `MediaPort` y sí se prueba). `MongoMediaRepository` en 0 %, como los demás adaptadores Mongo: se verifica en Azure |
| Secreto de la llave maestra (M-BE-038) | Creado por Victor el 2026-11-17 con `az containerapp secret set` (`media-document-master-key`) y la variable `Media__DocumentMasterKey=secretref:...`; la llave se generó en su terminal sin imprimirse y nunca pasó por el repositorio ni por esta sesión. Se verificó solo el nombre del secreto y la referencia, y que la variable siguió presente tras el despliegue |
| CD (run 44200967522) | resolve, publish, verify-ephemeral, deploy aprobado por Victor, DAST y resumen: todo en verde |
| Azure | revisión `ca-thehouse-u2--c011c2ad-48-1`, 100 % del tráfico, Healthy |
| Rol admin temporal (M-BE-065) | Autorizado por Victor el 2026-11-17 («autorizo rol»). `--grant-role` a la cuenta admin de prueba y `--revoke-role` al terminar (usuario `6afceb67b516a1f9f65bae98`), vía `az containerapp exec` en esa revisión |

## 6. Prueba HTTP real en Azure

`.azure-check/nv13_azure_check.py` (fuera del repositorio), corrida por Victor el 2026-11-17 con tres cuentas `@example.com` nuevas; solo imprime códigos, campos, tamaños y huellas, nunca tokens, contraseñas ni el contenido de los documentos.

- **Primera corrida (incompleta):** P1–F6 en verde; en F7 (archivo de 5 MB + 1) el servidor respondió 413 sin leer el cuerpo y cerró la conexión, y Python lo reportó como error de SSL. Los logs del contenedor confirman `Solicitud rechazada: HTTP 413`. Se ajustó el script para aceptar ese cierre; quedó un edificio de prueba en borrador con dos fotos (no público).
- **Segunda corrida: 22/23 PASS.** D3 falló por el propio script: comprobaba que la respuesta no contuviera la palabra `content`, que forma parte del campo de metadatos `contentType`. D1 de la misma corrida muestra que el documento responde solo `id, kind, contentType, bytes, uploadedAt`, y la prueba de integración verifica lo mismo; se corrigió D3 para comparar el conjunto exacto de campos. Resultado efectivo: **23/23**.

| Grupo | Casos | Resultado |
| --- | --- | --- |
| Preparación y publicación | Edificio creado; pasos `building_photo` y `documents`; publicar sin archivos 409 `publication_incomplete` | 3/3 |
| Fotos | Foto de 2400×1600 re-codificada a 1600 de ancho (**SkiaSharp funciona en el contenedor Linux**); foto de cuarto 201; miniatura privada WebP con `no-store`; otra cuenta 404; sin verificar no es pública; SVG disfrazado 415 `unsupported_media`; más de 5 MB rechazado con 413 antes de leer el cuerpo | 7/7 |
| Documentos | INE (PDF) y comprobante (PNG) 200; el dueño ve solo metadatos (D3 corregido, ver arriba) | 3/3 |
| Revisión | Publicar con archivos 202; el revisor ve foto, fotos del cuarto y 2 documentos; descarga la INE descifrada con SHA-256 igual al original; el dueño 403; aprobar 200 | 5/5 |
| Foto pública | 200 con `public, max-age=31536000, immutable` y `ETag`; 304 con `If-None-Match`; el detalle público trae `thumbnailUrl` y `photos` | 3/3 |
| Limpieza | Edificio suspendido; su foto deja de ser pública (404) | 2/2 |

## 7. Contrato entregable a Mobile

Entregado el 2026-11-17 junto con el aviso de despliegue. Es el contrato del §6.1 de `spec.md` (validado por Mobile antes de ejecutar) con la URL y las pruebas reales; las 9 rutas son nuevas y las respuestas anteriores solo agregan campos (`photo`, `photos`, `documents`; `thumbnailUrl` ahora se llena).

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io
TESTS        CI de main (run 44200564148): 934/934 (unitarias 353, integración 466, seguridad 115); prueba HTTP real en Azure 23/23 (22 PASS + D3 corregido, 2026-11-17)
```

## 8. Hallazgos y resultado

| # | Hallazgo | Severidad | Estado |
| --- | --- | --- | --- |
| H1 | ImageSharp 3.2.0 exige llave de licencia para compilar en Release | Media | Resuelto: SkiaSharp 4.154.0, cambio aprobado por Victor (M-BE-073) |
| H2 | Llave de prueba fija en el historial del primer PR (Gitleaks) | Media | Resuelto: llave generada en tiempo de prueba, rama rehecha (#53) y rama vieja borrada con autorización |
| H3 | Dos falsos negativos del script de Azure (F7 por cierre de conexión, D3 por coincidencia de texto) | Baja | Resuelto en el script; el API respondió bien en ambos (logs y D1) |
| H4 | Edificio de prueba en borrador con dos fotos de la primera corrida | Información | No es público; ocupa la cuota de la cuenta de prueba |
| H5 | Adaptadores Mongo y la tarea de retención sin cobertura en CI | Baja | Aceptado: se verifican en Azure; la lógica de retención está probada en `MediaPort` |

Resultado: **PASS WITH WARNINGS**. Costo: sin recursos nuevos de pago; colecciones `media_items` y `verification_documents` y buckets GridFS en MongoDB Atlas M0 con cuota de 60 MB por arrendador; un secreto más en Container Apps. Revisión de coherencia: spec, plan, tasks, README, memoria (M-BE-073 con el cambio de librería) y esta evidencia indican 1.1.0 completa 10/10.
