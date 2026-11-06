# NV-14 — Búsqueda, filtros y mapa — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-14 |
| Documento | `plan.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Se diseña primero la vista de lectura y sus índices, porque determina el rendimiento; después los endpoints y al final las protecciones contra extracción.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02, R03, R04 | T01, T02, T03, T04 | Búsqueda con filtros; Mapa de rentas; Conteo por presupuesto; Detalle con sesión |
| F2 | Persistencia e integración | R05, R06 | T05, T06 | Búsqueda de colonias y ciudades; Índices y rendimiento |
| F3 | Seguridad, autorización y límites | R07 | T07 | Ubicación aproximada y anti-extracción |
| F4 | Pruebas y evidencia | R08 | T08, T09, T10 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Búsqueda con filtros** (R01): Combina filtros de precio, tipo, modalidad, servicios y distancia; orden estable; paginación con máximo 50; solo cuartos publicables.
- **T02 · Mapa de rentas** (R02): Marcadores dentro de un `bbox` válido, máximo 300, con precio y distancia a la universidad.
- **T03 · Conteo por presupuesto** (R03): `rooms/count` devuelve el total de cuartos cercanos dentro del presupuesto con los mismos criterios de publicación.
- **T04 · Detalle con sesión** (R04): Invitado recibe 401; con sesión devuelve el detalle público sin dirección exacta.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T05 · Búsqueda de colonias y ciudades** (R05): Coincidencia sin acentos ni mayúsculas, máximo 10 resultados y lista vacía con 200 si no hay coincidencias.
- **T06 · Índices y rendimiento** (R06): Índices geoespaciales y compuestos; prueba de carga básica que cumple p95 < 500 ms con datos de seed.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Ubicación aproximada y anti-extracción** (R07): La ubicación aproximada es estable y está a 150–300 m de la real; límites de `bbox`, `pageSize` y rate limit verificados.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

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
- T03 depende de: autorización «EJECUTA».
- T04 depende de: autorización «EJECUTA».
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Búsqueda con filtros | Pruebas automatizadas en CI |
| R02 | Mapa de rentas | Pruebas automatizadas en CI |
| R03 | Conteo por presupuesto | Pruebas automatizadas en CI |
| R04 | Detalle con sesión | Pruebas automatizadas en CI |
| R05 | Búsqueda de colonias y ciudades | Pruebas automatizadas en CI |
| R06 | Índices y rendimiento | Pruebas automatizadas en CI |
| R07 | Ubicación aproximada y anti-extracción | Pruebas automatizadas en CI |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Ningún endpoint público devuelve dirección exacta, coordenadas reales, `ownerId` ni teléfono (RNF-02).
- Rate limit 60/min por IP en búsqueda y mapa; `bbox` y `pageSize` acotados para evitar extracción masiva.
- El detalle exige sesión (RF-24) y registra la vista para NV-25 sin datos personales.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Índices geoespaciales incluidos |
| Mapas en el cliente | $0 en el backend | El proveedor de mapas es decisión del cliente móvil |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.0.0-borrador | BLOCKED |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §12 de la spec resueltas o aceptadas | BLOCKED |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F1 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
