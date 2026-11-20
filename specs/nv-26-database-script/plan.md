# NV-26 — Script de la base de datos para la entrega — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-26 |
| Documento | `plan.md` |
| Versión | 1.0.0 |
| Fecha | 2026-11-20 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-20 («Apruebo y EJECUTA NV-26»: aprobación de 1.0.0 con las decisiones de la §13 y «EJECUTA» de T01–T07; rutas `scripts/db/`, `TheHouse.IntegrationTests/` y `specs/nv-26-database-script/`; sin cambios en el código de la API ni comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.0.0 T01–T07 (2026-11-20): PASS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0 |
| Tasks | `task.md` 1.0.0 |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el inventario automático de índices del código; después el script de esquema y la prueba de alineación; luego el comando de exportación y el script de catálogos; al final la ejecución real contra el MongoDB efímero y el README.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Script de esquema; Script de catálogos |
| F2 | Persistencia e integración | R03, R04 | T03, T04 | Alineación con el código; Ejecución real |
| F4 | Pruebas y evidencia | R05 | T05, T06, T07 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Script de esquema** (R01): `01-schema.mongodb.js` crea las 31 colecciones, todos los índices con nombre y opciones del código y los 2 buckets de GridFS; repetirlo no falla.
- **T02 · Script de catálogos** (R02): `02-catalogs.mongodb.js` carga 10 universidades, 12 propiedades y 60 lugares con *upsert*, generado desde los datos del código; sin datos personales.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Alineación con el código** (R03): Prueba automática que compara índices y colecciones del código con los del script, en ambos sentidos.
- **T04 · Ejecución real** (R04): El script corre contra el MongoDB efímero (el mismo del CI) y se verifican colecciones, índices y conteos de catálogos; documentado en `scripts/db/README.md`.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T05 · Pruebas automáticas** (R05): Prueba de alineación código ↔ script en ambos sentidos, prueba de idempotencia (ejecutar dos veces) y prueba de que los catálogos no contienen correos, teléfonos ni datos de usuarios; todas en el CI con MongoDB aislado.
- **T06 · Revisión de seguridad** (R05): Sin cadenas de conexión, usuarios ni contraseñas en `scripts/db/`; Gitleaks limpio; el script no imprime la conexión.
- **T07 · Evidencia y revisión final** (R05): `evidence.md` con commit, comandos, resultados (colecciones, índices y conteos), hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

**Criterio de salida:** tasks de F4 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

## 4. Dependencias entre tasks

```text
F1 Contrato y dominio
  ↓
F2 Persistencia e integración
  ↓
F4 Pruebas y evidencia
```

- T01 depende de: autorización «EJECUTA».
- T02 depende de: autorización «EJECUTA».
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04.
- T07 depende de: T05, T06.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Script de esquema | Pruebas automatizadas en CI |
| R02 | Script de catálogos | Pruebas automatizadas en CI |
| R03 | Alineación con el código | Pruebas automatizadas en CI |
| R04 | Ejecución real | Pruebas automatizadas en CI |
| R05 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Ninguna credencial en el repositorio: la cadena de conexión se pasa al ejecutar `mongosh`.
- Gitleaks del CI revisa `scripts/db/` como el resto del repositorio.
- Ejecutarlo en producción solo con autorización explícita (no es necesario: el código ya crea todo).
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Archivos en el repositorio | $0 | Sin recursos nuevos |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0 | PASS (M-BE-083) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-20) |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F2 | T03 |
| R04 | F2 | T04 |
| R05 | F4 | T05, T06, T07 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.0.0 T01–T07 (2026-11-20): PASS, ver `evidence.md`**.
