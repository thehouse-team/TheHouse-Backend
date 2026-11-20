# NV-26 — Script de la base de datos para la entrega

| Campo | Valor |
| --- | --- |
| ID | NV-26 |
| Documento | `spec.md` |
| Versión | 1.0.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo y EJECUTA NV-26»: aprobación de 1.0.0 con las decisiones de la §13 y «EJECUTA» de T01–T07; rutas `scripts/db/`, `TheHouse.IntegrationTests/` y `specs/nv-26-database-script/`; sin cambios en el código de la API ni comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.0.0: T01–T07 autorizadas** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | PDF del curso, punto 20 (entregar el script de la base de datos); punto 14 (base de datos NoSQL) (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-08, RNF-09 |
| Plan | `plan.md` 1.0.0 |
| Tasks | `task.md` 1.0.0 |
| Módulo | DatabaseScript |
| Precondición | NV-01 a NV-25 en `main` (colecciones e índices actuales) |
| Relación | Documenta y reproduce el esquema que crean los repositorios de MongoDB de NV-04 a NV-25 y los catálogos de NV-07, NV-08 y NV-14; no cambia el código de acceso a datos |

Spec aprobada y autorizada («Apruebo y EJECUTA NV-26», 2026-11-20) para T01–T07 en las rutas indicadas.

**Spec nueva 1.0.0 (2026-11-20):** el punto 20 del PDF del curso pide entregar «el script de la base de datos». Hoy las colecciones e índices los crea cada repositorio de MongoDB al primer uso y los catálogos se cargan con `--seed-*`; no existe un archivo entregable. Esta spec agrega un script de `mongosh` reproducible e idempotente que crea las 31 colecciones, sus ~60 índices (únicos, TTL y compuestos con los mismos nombres y opciones que el código) y los 2 buckets de GridFS, y un segundo script que carga los catálogos públicos. Una prueba automática impide que el script y el código se desalineen.

## 1. Contexto

La base de datos de TheHouse vive en MongoDB Atlas (`thehouse_u2`). Cada repositorio crea sus índices la primera vez que se usa y los catálogos se cargan con comandos de la API dentro de la Container App. Eso funciona en producción, pero el curso pide entregar un script de la base de datos junto con el proyecto, y un evaluador debe poder crear la base desde cero sin desplegar la API.

## 2. Objetivo

Entregar un script de `mongosh` que cree desde cero toda la estructura de la base de datos de TheHouse (colecciones, índices y buckets) y cargue los catálogos públicos, idéntico a lo que crea el código, repetible sin errores y sin datos personales ni secretos.

## 3. Precondiciones

1. NV-01 a NV-25 en `main`.
2. «Apruebo» de NV-26 1.0.0 y «EJECUTA».

## 4. Alcance

1. `scripts/db/01-schema.mongodb.js`: crea las colecciones, todos los índices con sus nombres y opciones (`unique`, `expireAfterSeconds`, compuestos y parciales) y los buckets `media` y `documents` de GridFS.
2. `scripts/db/02-catalogs.mongodb.js`: carga los catálogos públicos (10 universidades de `UniversitySeedData`, 12 propiedades de `PropertySeedData` y 60 lugares de búsqueda de `PlaceSeedData`: 43 localidades y 17 colonias) con *upsert* por su clave natural.
3. `scripts/db/README.md`: cómo ejecutarlo contra una base local o Atlas y qué crea.
4. Prueba automática de que cada índice que declara el código existe en el script con el mismo nombre y las mismas opciones, y viceversa.
5. Prueba real: ejecutar el script contra el MongoDB efímero del CI y comprobar colecciones, índices y catálogos.

## 5. Fuera de alcance

- Datos de usuarios, de prueba o de producción (nunca se exportan).
- Cadenas de conexión, usuarios o contraseñas dentro del script.
- Migraciones de datos existentes (el script es para crear desde cero; sobre una base existente solo agrega lo que falte).
- Validadores `$jsonSchema` (la validación vive en el dominio; agregarlos podría rechazar documentos válidos para la app).
- Cuartos demo (`--seed-demo-rooms`): son datos de demostración, no catálogo.

## 6. Uso del script (sin contrato HTTP)

Esta spec no agrega ni cambia rutas; no hay contrato con Mobile. El entregable son archivos de `mongosh`:

```powershell
$env:THEHOUSE_DB = "thehouse_u2"     # opcional; por defecto thehouse_u2
mongosh "<cadena de conexión>" --file scripts/db/01-schema.mongodb.js
mongosh "<cadena de conexión>" --file scripts/db/02-catalogs.mongodb.js
```

- La cadena de conexión se escribe al ejecutar; nunca se guarda en el repositorio.
- Idempotente: crear una colección o un índice que ya existe con las mismas opciones no falla; los catálogos usan *upsert* por su clave natural (`code` de universidad, `id` de propiedad, nombre y tipo de lugar).
- Salida: una línea por colección e índice creado o ya existente, y los conteos finales de catálogos.

## 7. Módulo, datos propios e integración

Módulo **DatabaseScript** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | — |
| Usa (puertos de otros módulos) | — |
| Publica (eventos de dominio) | — |
| Consume (eventos de dominio) | `Definiciones de índices de los repositorios de MongoDB (Infrastructure) y datos de los catálogos (NV-07, NV-08, NV-14)` |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- Sin colecciones nuevas: el script refleja las 31 colecciones actuales (identidad, perfil, listados, media, favoritos, visitas, mensajería, rentas, reseñas, notificaciones, moderación, compañeros y analítica) y los buckets de GridFS.
- Los catálogos se generan a partir de los mismos datos del código (`UniversitySeedData`, `PropertySeedData`, `PlaceSeedData`), con un comando de exportación para que el script no se edite a mano.

## 9. Reglas de negocio

1. El script es la fuente de entrega, pero el código sigue siendo la fuente de verdad: la prueba compara los índices del código con los del script y falla ante cualquier diferencia (nombre, campos, orden, `unique`, TTL).
2. Los nombres de colecciones, índices y buckets son exactamente los del código.
3. Sin datos personales, de prueba ni secretos en ningún archivo de `scripts/db/`.
4. El script imprime solo lo que crea (colección e índice), nunca la cadena de conexión.

## 10. Seguridad

Esta spec no agrega endpoints; los controles comunes de autenticación y autorización no aplican. Controles de esta spec:

- Ninguna credencial en el repositorio: la cadena de conexión se pasa al ejecutar `mongosh`.
- Gitleaks del CI revisa `scripts/db/` como el resto del repositorio.
- Ejecutarlo en producción solo con autorización explícita (no es necesario: el código ya crea todo).

## 11. Errores

| Caso | Comportamiento |
| --- | --- |
| Sin conexión o credenciales inválidas | `mongosh` termina con código distinto de 0; el script no imprime la cadena de conexión |
| Índice existente con el mismo nombre y opciones distintas | El script se detiene con el nombre del índice y la diferencia; no borra ni recrea índices (decisión humana) |
| Base con datos | Solo agrega lo que falta; nunca borra colecciones, índices ni documentos |
| Catálogo con documento distinto | El *upsert* actualiza solo los campos del catálogo; no toca documentos que no son del catálogo |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-26-R01 | Script de esquema | `01-schema.mongodb.js` crea las 31 colecciones, todos los índices con nombre y opciones del código y los 2 buckets de GridFS; repetirlo no falla. | F1 | T01 |
| NV-26-R02 | Script de catálogos | `02-catalogs.mongodb.js` carga 10 universidades, 12 propiedades y 60 lugares con *upsert*, generado desde los datos del código; sin datos personales. | F1 | T02 |
| NV-26-R03 | Alineación con el código | Prueba automática que compara índices y colecciones del código con los del script, en ambos sentidos. | F2 | T03 |
| NV-26-R04 | Ejecución real | El script corre contra el MongoDB efímero (el mismo del CI) y se verifican colecciones, índices y conteos de catálogos; documentado en `scripts/db/README.md`. | F2 | T04 |
| NV-26-R05 | Pruebas, seguridad y evidencia | Prueba de alineación (código ↔ script) en ambos sentidos, prueba de que los catálogos no tienen datos personales y ejecución real del script contra MongoDB 7 con conteos verificados; `evidence.md` con comandos, resultados y revisión de coherencia. | F4 | T05, T06, T07 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Formato | Dos scripts de `mongosh` (esquema y catálogos) más README | APROBADO (M-BE-083) |
| Fuente de verdad | El código; el script se comprueba contra él con una prueba automática | APROBADO (M-BE-083) |
| Catálogos | Generados desde los datos del código con un comando de exportación; sin cuartos demo | APROBADO (M-BE-083) |
| Validadores | Sin `$jsonSchema` | APROBADO (M-BE-083) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Archivos en el repositorio | $0 | Sin recursos nuevos |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-26-RK01 | Script desalineado con el código | El evaluador crea una base distinta | Prueba automática en el CI |
| NV-26-RK02 | Datos personales en el script | Fuga de información | Solo catálogos públicos; revisión en la prueba y Gitleaks |
| NV-26-RK03 | Ejecutarlo sobre producción por error | Índices duplicados o conflictos | Idempotente; solo agrega lo que falte; README advierte que no hace falta en producción |

## 16. Dependencias

- NV-04 a NV-25 (colecciones e índices).
- NV-07, NV-08 y NV-14 (catálogos).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 5 |
| Fases del plan | 3 |
| Tasks | 7 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.0.0: T01–T07 autorizadas** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
