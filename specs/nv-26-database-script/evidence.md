# NV-26 — Evidencia: script de la base de datos para la entrega

| Campo | Valor |
| --- | --- |
| Spec | `spec.md` 1.0.0 (M-BE-083) |
| Autorización | «Apruebo y EJECUTA NV-26» — Victor Manuel Jose Jose, 2026-11-20 (T01–T07) |
| Rama | `feat/nv-26-database-script` |
| Resultado | **PASS** |

## 1. Qué se entregó

| Archivo | Contenido |
| --- | --- |
| `scripts/db/01-schema.mongodb.js` | 31 colecciones, 65 índices (únicos, TTL, compuestos, parciales y `2dsphere`) y los buckets `media` y `documents` de GridFS con sus 4 índices |
| `scripts/db/02-catalogs.mongodb.js` | 82 *upserts*: 10 universidades, 12 propiedades y 60 lugares; las propiedades se ligan a la universidad por código (`universityId("UTTT")`) |
| `scripts/db/README.md` | Uso, comportamiento, cómo regenerarlo y la comparación opcional contra MongoDB real |
| `TheHouse.IntegrationTests/DatabaseScriptGenerator.cs` | Generador: ejecuta los repositorios reales de MongoDB y los seeds contra una base que solo registra (sin servidor) y emite los scripts |
| `TheHouse.IntegrationTests/DatabaseScriptTests.cs` | 5 pruebas (ver §3) |

Sin cambios en la API, en `TheHouse.Infrastructure` ni en workflows; sin paquetes nuevos; sin recursos ni costo.

## 2. Diseño

- **El código es la fuente de verdad.** El generador crea cada repositorio de `TheHouse.Infrastructure.Persistence.Mongo` con una `IMongoDatabase` de registro (`DispatchProxy`), ejecuta sus métodos `Ensure*Index*` y los `UniqueUserIndex`, y convierte cada `CreateIndexModel` con el serializador del driver (llaves, `unique`, `sparse`, `expireAfterSeconds` y `partialFilterExpression`). Cualquier otra opción de índice hace fallar el generador para no omitirla en silencio.
- **Colecciones:** las que usan los repositorios por propiedad más las de nombre literal en `TheHouse.Infrastructure` (por ejemplo `deleted_accounts`, que solo se escribe al eliminar una cuenta).
- **Catálogos:** los seeds reales (`UniversitySeedService`, `PropertySeedService`, `PlaceSeedService`) escriben en la base de registro; cada `UpdateOne` con *upsert* se convierte en una llamada de `mongosh` que conserva los tipos BSON (`NumberDecimal`, `NumberLong`, `Double`, `ISODate`, `ObjectId`).
- **Idempotencia:** `createCollection` solo si falta; `createIndex` con nombre y opciones del código (MongoDB no hace nada si ya existe igual y responde error si existe distinto); catálogos con *upsert* por clave natural.

## 3. Pruebas automáticas (T03, T05)

| Prueba | Qué verifica |
| --- | --- |
| `Schema_script_matches_the_indexes_and_collections_of_the_code` | 31 colecciones, buckets `documents` y `media`, índices sin nombres repetidos y `01-schema.mongodb.js` idéntico al generado (en ambos sentidos: cualquier índice de más o de menos cambia el archivo) |
| `Catalog_script_matches_the_seed_data_of_the_code` | 10 + 12 + 60 = 82 *upserts* y `02-catalogs.mongodb.js` idéntico al generado |
| `Scripts_contain_no_secrets_or_personal_data` | Ningún archivo de `scripts/db/` tiene correos, teléfonos, cadenas `mongodb://` ni contraseñas o llaves |
| `Scripts_never_drop_or_delete` | Los scripts no llaman `drop*`, `delete*` ni `remove` |
| `Script_database_equals_code_database_on_real_mongodb` | Opcional (T04): compara la base del script con la del código en un MongoDB real |

Suite completa en Release (`dotnet build -warnaserror`: 0 advertencias, 0 errores): unitarias 408/408, integración 501/501 (incluye las 5 de NV-26), seguridad 154/154 — 1063/1063.

Prueba de que la alineación detecta diferencias: al quitar a mano `unique: true` de `ux_users_email` en `01-schema.mongodb.js`, la prueba falla con «no coincide con el código»; se restauró el archivo.

## 4. Ejecución real (T04)

MongoDB `mongo:7.0.45` (la misma imagen del CI) en un contenedor local desechable, publicado solo en `127.0.0.1`; se borró al terminar.

| Paso | Resultado |
| --- | --- |
| `01-schema.mongodb.js` en una base vacía (`nv26_script`) | Código 0; 35 colecciones creadas (31 + 4 de GridFS) y 69 índices (65 + 4 de GridFS): «Esquema listo en nv26_script: 31 colecciones, 65 índices y 2 buckets de GridFS» |
| Segunda ejecución | Código 0; 104 «existente» (35 colecciones y 69 índices) y 0 creados |
| `02-catalogs.mongodb.js` | Código 0; universidades 10, propiedades 12 y lugares 60 insertados |
| Segunda ejecución | Código 0; 0 insertados, 0 actualizados, 82 sin cambios |
| Sin `THEHOUSE_DB` | Usa `thehouse_u2` (base local del contenedor) |
| Comparación contra el código (`Script_database_equals_code_database_on_real_mongodb`) | **PASS**: las 35 colecciones esperadas; 101 índices idénticos (incluidos `_id_` y GridFS) en las 32 colecciones que crea el código; universidades, propiedades (con `universityId` traducido a código) y lugares idénticos |
| Índice en conflicto (se recreó `users.ux_users_email` sin `unique` en otra base) | El script se detiene con código 1 y «ERROR en el índice users.ux_users_email: IndexKeySpecsConflict»; no borra ni recrea |
| Comparación contra esa base en conflicto | **Falla**, como se espera |

## 5. Seguridad (T06)

- Sin cadenas de conexión, usuarios, contraseñas ni datos personales en `scripts/db/` (prueba automática y revisión manual); la cadena se pasa al ejecutar `mongosh` y el script no la imprime.
- Los catálogos son los mismos datos públicos de los seeds (NV-07, NV-08, NV-14): sin direcciones exactas de propiedades, contactos ni usuarios.
- La prueba opcional contra MongoDB real solo corre con variables explícitas y borra su base temporal; nunca se ejecutó contra Atlas.
- Gitleaks del CI revisa `scripts/db/` como el resto del repositorio.
- No se ejecutó nada en producción.

## 6. Costo

$0: archivos en el repositorio y un contenedor local desechable.

## 7. Revisión de coherencia

| Punto | Resultado |
| --- | --- |
| Spec §4 alcance (dos scripts, README, prueba de alineación, ejecución real) | Cumplido |
| Spec §5 fuera de alcance (datos de usuarios, secretos, migraciones, `$jsonSchema`, cuartos demo) | Respetado |
| Spec §11 errores (conflicto detiene el script; nunca borra) | Verificado en la ejecución real |
| «~60 índices» de la spec | Son 65 (más 4 de GridFS); el número exacto lo fija el código |
| `constitution.md` y `AGENTS.md` (rama propia, sin secretos, sin Production, sin paquetes) | Cumplido |

Resultado: **PASS** — T01–T07 completas.
