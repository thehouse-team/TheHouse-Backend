# Script de la base de datos (NV-26)

Scripts de `mongosh` que crean desde cero la base de datos de TheHouse en MongoDB, idénticos a lo que crea el código de la API. Es el entregable del punto 20 del PDF del curso.

| Archivo | Qué hace |
| --- | --- |
| `01-schema.mongodb.js` | Crea las 35 colecciones, sus 72 índices (únicos, TTL, compuestos, parciales y `2dsphere`, con los mismos nombres y opciones que los repositorios) y los buckets `media` y `documents` de GridFS con sus 4 índices |
| `02-catalogs.mongodb.js` | Carga los catálogos públicos: 10 universidades, 12 propiedades y 60 lugares de búsqueda (43 localidades y 17 colonias), con *upsert* por su clave natural |

No contienen cadenas de conexión, usuarios, contraseñas ni datos personales. Los datos de usuarios nunca se exportan.

## Uso

Requisitos: [`mongosh`](https://www.mongodb.com/docs/mongodb-shell/) y una base MongoDB 7 o superior (local o Atlas).

```powershell
$env:THEHOUSE_DB = "thehouse_u2"     # opcional; por defecto thehouse_u2
mongosh "<cadena de conexión>" --file scripts/db/01-schema.mongodb.js
mongosh "<cadena de conexión>" --file scripts/db/02-catalogs.mongodb.js
```

- La cadena de conexión se escribe al ejecutar; nunca se guarda en el repositorio. El script no la imprime.
- El orden importa: las propiedades se ligan a las universidades por su código.
- Sin MongoDB instalado se puede usar la misma imagen del CI:

  ```powershell
  docker run -d --name thehouse-db -p 127.0.0.1:27017:27017 mongo:7.0.45
  docker cp scripts/db thehouse-db:/db
  docker exec thehouse-db mongosh --quiet --file /db/01-schema.mongodb.js
  docker exec thehouse-db mongosh --quiet --file /db/02-catalogs.mongodb.js
  ```

## Comportamiento

- **Idempotente:** se puede ejecutar varias veces. Lo que ya existe se reporta como «existente» y los catálogos como «sin cambios».
- **Nunca borra:** no elimina colecciones, índices ni documentos.
- **Conflictos:** si ya existe un índice con el mismo nombre y otras opciones, MongoDB responde `IndexKeySpecsConflict` o `IndexOptionsConflict` y el script se detiene con código de salida distinto de 0. Decidir qué hacer es una decisión humana.
- **Producción:** no hace falta ejecutarlo; la API crea las colecciones e índices al primer uso y los catálogos se cargan con `--seed-*`.
- **Sin cuartos demo:** los cuartos de demostración (`--seed-demo-rooms`) no son catálogo y no se incluyen.

## Cómo se genera

Los dos scripts **se generan desde el código y no se editan a mano**. `TheHouse.IntegrationTests/DatabaseScriptTests.cs` ejecuta los repositorios reales de MongoDB contra una base que solo registra (sin servidor) y convierte sus índices y las escrituras de los seeds en llamadas de `mongosh`. Si el archivo no coincide, la prueba falla en el CI.

Para regenerarlos después de cambiar un índice, una colección o un catálogo:

```powershell
$env:THEHOUSE_WRITE_DB_SCRIPTS = "1"
dotnet test TheHouse.IntegrationTests --filter DatabaseScriptTests
Remove-Item Env:THEHOUSE_WRITE_DB_SCRIPTS
git diff scripts/db
```

## Comparación contra MongoDB real (opcional)

`Script_database_equals_code_database_on_real_mongodb` crea el esquema y los catálogos con el **código** en una base temporal y los compara con la base que creó el **script**: colecciones, todos los índices (incluidos los de GridFS) y los documentos de los catálogos. Se activa con dos variables y borra su base temporal al terminar:

```powershell
# 1. Ejecutar los dos scripts con THEHOUSE_DB = "nv26_script" (ver «Uso»)
# 2. Comparar
$env:THEHOUSE_DBSCRIPT_MONGO = "<cadena de conexión del MongoDB local>"
$env:THEHOUSE_DBSCRIPT_DB = "nv26_script"
dotnet test TheHouse.IntegrationTests --filter Script_database_equals_code_database_on_real_mongodb
```

Sin las variables, la prueba no hace nada. No se ejecuta contra producción.
