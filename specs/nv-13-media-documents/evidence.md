# NV-13 — Evidencia: fotografías y documentos

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
| Documento | `evidence.md` |
| Versión | 1.1.0 |
| Fecha | 2026-11-17 |
| Autorización | «Apruebo y EJECUTA» — Victor Manuel Jose Jose, 2026-11-17: NV-13 1.1.0, tasks T01–T10 (M-BE-073); cambio de librería aprobado el mismo día («Sí, usa SkiaSharp») |
| Rama | `feat/nv-13-media` → PR pendiente de revisión |
| Resultado | **🟡 EN CURSO**: T01–T06 completas y verificadas en local; T07–T10 esperan el secreto de la llave maestra, CI, despliegue y prueba HTTP real en Azure |

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
| R07 Contrato para Mobile y despliegue | Pendiente: secreto de la llave maestra, CI, despliegue y prueba HTTP real en Azure |
| R08 Pruebas, seguridad y evidencia | Pendiente: TRX y cobertura del CI |

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

## 5. Pendiente para cerrar 1.1.0

| Paso | Responsable | Estado |
| --- | --- | --- |
| Crear el secreto `media-document-master-key` y la variable `Media__DocumentMasterKey` en Container Apps (autorización aparte) | Victor | Pendiente |
| CI del PR en verde y merge | CI / Isai | Pendiente |
| Aprobación del CD a Production | Victor | Pendiente |
| Prueba HTTP real en Azure (`.azure-check/nv13_azure_check.py`) | Victor | Pendiente |
| Contrato §6.1 entregado a Mobile y cierre de esta evidencia | Claude | Pendiente |
