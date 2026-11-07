# Specs del backend de TheHouse

Índice único de las specs (SDD). Fuente de requerimientos: `Requerimientos_TheHouse_v2` (RF-01 a RF-86, RNF-01 a RNF-20, RES-01 a RES-09). Autoridad: `constitution.md` → `AGENTS.md` → `memoria.md` → `specs/`.

## 1. Mapa de specs

Orden de bloques del proyecto (M-BE-027 y regla del equipo): base (NV-01) → infraestructura (NV-03) → endpoints (NV-04 a NV-08 y NV-10 a NV-25) → CI/CD (NV-02 y NV-09). El roadmap funcional se recorre a partir de NV-10 (M-BE-049, M-BE-054).

| ID | Spec | Estado documental | Estado de ejecución | Tasks |
| --- | --- | --- | --- | --- |
| NV-01 | [Foundation (U2)](nv-01-foundation/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** (T13 excluida) | 18/19 |
| NV-02 | [CI y DevSecOps (U2)](nv-02-devsecops/spec.md) | **✅ APROBADA** | **✅ COMPLETADA — PASS WITH WARNINGS (2026-11-05)** | 15/15 |
| NV-03 | [Scaling Cloud U2](nv-03-scaling-cloud/spec.md) | **✅ APROBADA PARA U2** | **🟡 EJECUCIÓN PARCIAL — 5 de 25 tasks (T13, T15, T23, T24, T25); ver `evidence.md`** | 5/25 |
| NV-04 | [User Registration (U2)](nv-04-registration/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** | 11/11 |
| NV-05 | [Login (U2)](nv-05-login/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** | 11/11 |
| NV-06 | [Logout (U2)](nv-06-logout/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA, VALIDADA Y CERRADA PARA U2** | 10/10 |
| NV-07 | [Property Core (U2)](nv-07-property-core/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** | 13/13 |
| NV-08 | [University Catalog (U2)](nv-08-university-catalog/spec.md) | **✅ APROBADA PARA U2** | **✅ EJECUTADA Y VALIDADA; DESPLEGADA EN AZURE POR EL CD (run `42180858665`, 2026-11-06)** | 14/14 |
| NV-09 | [Entrega continua (CD) U2](nv-09-cd/spec.md) | **✅ APROBADA** | **🟡 CD EN PRODUCCIÓN — 15 de 20 tasks; T20 se cierra con el primer merge de solo documentación; T15–T17 y T19 pendientes** | 15/20 |
| NV-10 | [Sesión: refresh token y cuenta propia](nv-10-account-session/spec.md) | **✅ APROBADA** | **✅ COMPLETADA — DESPLEGADA EN AZURE (revisión `c55feed2-7-1`) Y VERIFICADA 13/13 (2026-11-06)** | 11/11 |
| NV-11 | [Perfil, universidad y preferencias](nv-11-profile-preferences/spec.md) | **✅ APROBADA** | **✅ COMPLETA — T01–T11 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** | 11/11 |
| NV-12 | [Arrendador, edificios y cuartos](nv-12-landlord-buildings-rooms/spec.md) | **✅ APROBADA** | **✅ COMPLETA — T01–T13 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** | 13/13 |
| NV-13 | [Fotografías y documentos](nv-13-media-documents/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/9 |
| NV-14 | [Búsqueda, filtros y mapa](nv-14-search-map/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/10 |
| NV-15 | [Favoritos](nv-15-favorites-compare/spec.md) | **✅ APROBADA** | **✅ COMPLETA — T01–T10 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** | 10/10 |
| NV-16 | [Contacto, solicitudes y chat](nv-16-contact-chat-requests/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/9 |
| NV-17 | [Visitas: solicitudes (v1)](nv-17-visits/spec.md) | **✅ APROBADA** | **✅ COMPLETA — T01–T12 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** | 12/12 |
| NV-18 | [Cierre de trato con QR y renta](nv-18-deal-closing-rental/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/9 |
| NV-19 | [Reseñas y calificaciones](nv-19-reviews/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/7 |
| NV-20 | [Notificaciones](nv-20-notifications/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/8 |
| NV-21 | [Verificación, reportes y moderación](nv-21-verification-moderation/spec.md) | **✅ APROBADA** | **✅ COMPLETA — T01–T14 (2026-11-07): PASS WITH WARNINGS, ver `evidence.md`** | 14/14 |
| NV-22 | [Búsqueda de compañero de vivienda](nv-22-roommates/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/7 |
| NV-23 | [Smart Match — compatibilidad con Machine Learning](nv-23-smart-match-ml/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/10 |
| NV-24 | [Smart Match — explicaciones con IA generativa](nv-24-smart-match-genai/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/8 |
| NV-25 | [Estadísticas del propietario y métricas de Smart Match](nv-25-metrics-stats/spec.md) | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** | **⛔ NO EJECUTAR HASTA «EJECUTA»** | 0/9 |

## 2. Cobertura de requerimientos funcionales

Cada RF del documento de requerimientos tiene al menos una spec responsable, salvo los que solo existen en el cliente móvil o están bloqueados por costo. RF sin spec: **0**.

| RF | Spec(s) |
| --- | --- |
| RF-01 | NV-04, NV-10 |
| RF-02 | NV-05, NV-10 |
| RF-03 | NV-06 |
| RF-04 | NV-10 |
| RF-05 | NV-11 |
| RF-06 | NV-12 |
| RF-07 | NV-08, NV-11 |
| RF-08 | NV-11, NV-14 |
| RF-09 | NV-11 |
| RF-10 | NV-11 |
| RF-11 | NV-11 |
| RF-12 | NV-11 |
| RF-13 | NV-12 |
| RF-14 | NV-12 |
| RF-15 | NV-13 |
| RF-16 | NV-12 |
| RF-17 | NV-12 |
| RF-18 | NV-12 |
| RF-19 | NV-12 |
| RF-20 | NV-12 |
| RF-21 | NV-12 |
| RF-22 | NV-12 |
| RF-23 | NV-07, NV-14 |
| RF-24 | NV-07, NV-14 |
| RF-25 | NV-14 |
| RF-26 | NV-14 |
| RF-27 | NV-14 |
| RF-28 | NV-14 |
| RF-29 | NV-14 |
| RF-30 | NV-14 |
| RF-31 | NV-15 |
| RF-32 | NV-15 |
| RF-33 | NV-15 |
| RF-34 | NV-16 |
| RF-35 | NV-16 |
| RF-36 | NV-10 |
| RF-37 | NV-13, NV-21 |
| RF-38 | NV-21 |
| RF-39 | NV-21 |
| RF-40 | NV-21 |
| RF-41 | NV-14 |
| RF-42 | NV-21 |
| RF-43 | NV-21 |
| RF-44 | NV-21 |
| RF-45 | NV-12 |
| RF-46 | NV-22 |
| RF-47 | NV-14, NV-22 |
| RF-48 | NV-11, NV-23 |
| RF-49 | NV-23 |
| RF-50 | NV-23 |
| RF-51 | NV-23 |
| RF-52 | NV-23 |
| RF-53 | NV-23 |
| RF-54 | NV-23 |
| RF-55 | NV-23 |
| RF-56 | NV-23 |
| RF-57 | NV-23 |
| RF-58 | NV-23 |
| RF-59 | NV-24 |
| RF-60 | NV-24 |
| RF-61 | NV-24 |
| RF-62 | NV-24 |
| RF-63 | NV-24 |
| RF-64 | NV-23 |
| RF-65 | NV-23 |
| RF-66 | NV-25 |
| RF-67 | Solo cliente móvil — Introducción (pantallas del cliente) |
| RF-68 | NV-14, NV-23 |
| RF-69 | Solo cliente móvil — Aviso para iniciar sesión (el backend solo responde 401; NV-10/NV-14) |
| RF-70 | BLOCKED — Verificación por SMS: sin opción $0 (COST-001); fuera de alcance en NV-10 |
| RF-71 | NV-10 |
| RF-72 | NV-11 |
| RF-73 | NV-14 |
| RF-74 | Solo cliente móvil — Menú de navegación |
| RF-75 | NV-20 |
| RF-76 | NV-12 |
| RF-77 | NV-12 |
| RF-78 | NV-12 |
| RF-79 | NV-21 |
| RF-80 | NV-17 |
| RF-81 | NV-18 |
| RF-82 | NV-18 |
| RF-83 | NV-18 |
| RF-84 | NV-18 |
| RF-85 | NV-19 |
| RF-86 | NV-25 |

## 3. Orden recomendado de las specs funcionales

| Prioridad | Specs | Motivo |
| --- | --- | --- |
| 1 | NV-10 ✅, NV-15 (favoritos v1) y NV-17 (visitas v1) | Lo que Mobile ya tiene listo en UX (acordado el 2026-11-07); funcionan sobre las propiedades actuales de NV-07 |
| 2 | NV-11 | Perfil, universidad y preferencias |
| 3 | NV-12, NV-13, NV-21 | Arrendadores, fotos y verificación: desbloquean la confirmación de visitas y el chat |
| 4 | NV-16, NV-14, NV-18 | Chat (requiere arrendadores), búsqueda y mapa, cierre con QR |
| 5 | NV-19, NV-20, NV-22 | Confianza y comunicación |
| 6 | NV-23, NV-24, NV-25 | Smart Match y métricas (requieren datos de las anteriores) |

## 4. Buenas prácticas SDD del proyecto (lista de verificación)

Cada spec debe cumplir, y se revisa en cada iteración:

1. **Separación spec → plan → task**: la spec dice *qué* y *por qué*; el plan, *cómo* y en qué fases; la task list es el checklist de ejecución. No se usa `checklist.md` (M-BE-027); `evidence.md` registra lo ejecutado.
2. **Requerimientos de origen**: cada spec cita los RF/RNF que cubre; el §2 de este índice demuestra la cobertura.
3. **Requisitos verificables**: cada requisito tiene un criterio de aceptación comprobable con una prueba o evidencia.
4. **Trazabilidad bidireccional**: cada requisito lista sus tasks y cada task cita su requisito; la tabla del plan coincide con la de la spec. Se verifica automáticamente.
5. **Un solo estado por spec**: `spec.md`, `plan.md` y `task.md` muestran el mismo estado documental y de ejecución; el conteo de tasks completadas coincide con las casillas marcadas.
6. **Aprobación antes de ejecutar**: borrador → aprobación documental → «EJECUTA» con versión, tasks y rutas; `terraform apply` además con «OK PARA APPLY». Crear una spec nunca autoriza implementarla. *Fase B* (término de U2) equivale a «EJECUTA».
7. **Decisiones explícitas**: lo no resuelto aparece como PENDIENTE en la spec y bloquea las tasks que dependen de ello; lo aprobado se registra en `memoria.md` (M-BE-xxx).
8. **Seguridad por diseño**: cada spec funcional tiene sección de seguridad con OWASP API Top 10 (BOLA, autenticación, BOPLA, rate limit, autorización por función) y revisión al cierre de cada fase.
9. **Costo $0 (COST-001)**: cada spec tiene tabla de costo; lo que no se demuestre en $0 queda BLOCKED.
10. **Evidencia honesta**: un control no ejecutado no se reporta como PASS; las evidencias son registros fechados y no se reescriben.
11. **Versionado**: los cambios aprobados suben la versión y dejan una nota de cambio en la spec; el historial vive en Git.
12. **Contrato para Mobile**: toda spec que exponga endpoints a la app incluye la sección "Contrato para Mobile" (ENDPOINT, METHOD, AUTH, HEADERS, REQUEST, RESPONSE, STATUS CODES, PAGINATION) y, al desplegar, se entrega a Mobile con DEPLOYED URL y TESTS reales (acuerdo con Mobile del 2026-11-07).

## 5. Auditoría SDD del 2026-11-06

| # | Hallazgo | Specs | Corrección |
| --- | --- | --- | --- |
| A1 | `spec.md` y `plan.md` decían "⛔ NO EJECUTAR HASTA AUTORIZACIÓN DE FASE B" aunque `task.md` y la evidencia mostraban la spec ejecutada | NV-01, NV-03 a NV-08 | Estados alineados con `task.md` |
| A2 | Conteo de "Tasks completadas" desactualizado en la trazabilidad de la spec | NV-01 a NV-09 | Conteos iguales a las casillas marcadas |
| A3 | Referencias a "NV-02 ⛔ NO EJECUTAR" cuando la CI ya está completada | NV-01, NV-03 a NV-08 | Actualizadas a NV-02 completada y NV-09 (CD); las `evidence.md` se dejan como registro histórico |
| A4 | Trazabilidad no bidireccional: tasks que citan un requisito que no las lista | NV-03 (T24, T25), NV-07 (T12), NV-08 (T13) | Requisitos y plan actualizados |
| A5 | Sin vínculo con los RF/RNF del documento de requerimientos | NV-01 a NV-08 | Fila "Requerimientos de origen" en cada spec |
| A6 | Roadmap M-BE-023 desfasado (NV-09 pasó a ser CD) y sin specs para el resto del proyecto | — | Specs NV-10 a NV-25 en borrador y M-BE-054 |
| A7 | Contradicción entre RF-24 (detalle con sesión) y NV-07 (detalle público en U2) | NV-07, NV-14 | Registrada como decisión PENDIENTE en NV-14 |
| A8 | No existía un índice de specs ni una matriz de cobertura de RF | — | Este documento |

## 6. Revisión contra `constitution.md` y `AGENTS.md` (2026-11-06)

### 6.1 Ajustes aplicados a NV-10..NV-25

| # | Norma | Ajuste |
| --- | --- | --- |
| C1 | constitution §6 (monolito modular) | Cada spec declara su módulo, sus colecciones propias, los puertos que usa y los eventos que publica y consume; verificación automática: 16 módulos sin dependencias circulares |
| C2 | constitution §6 (propiedad de datos) | Ningún módulo escribe en colecciones ajenas: borrado de cuenta por evento `UserDeleted`, promedios de reseñas en `rating_summaries` (Reviews), roles como datos de Identity, transacción de cierre de trato por el puerto `IRoomOccupancy` |
| C3 | constitution §5 (Clean Architecture) | ML.NET y el proveedor de IA quedan como adaptadores de Infrastructure detrás de puertos (`ICompatibilityModel`, `IExplanationGenerator`) |
| C4 | constitution §17 (COST-001) | Nota común: un plan gratuito no prueba por sí solo costo $0; se exige evidencia vigente |
| C5 | constitution §30 (proveedores permitidos) | Correo, almacenamiento e IA generativa replanteados: opciones de AWS/Azure/Google o excepción registrada en `memoria.md` (precedente M-BE-034) |
| C6 | constitution §20 (refresh tokens) | Rotación atómica ante concurrencia y transporte en el cuerpo, sin cookies (sin CSRF) |
| C7 | constitution §14 y §15 (stateless, dos instancias) | Eventos idempotentes y tarea diaria de NV-25 con ejecución única entre réplicas |
| C8 | constitution §23 (caché) | Contador de notificaciones con `Cache-Control: private` |
| C9 | AGENTS (estados M-BE válidos) | RF-70 como DESCARTADO y M-BE-055 como PROPUESTO |
| C10 | AGENTS ("No crear nuevas specs funcionales sin solicitud") | Cumplido: las specs se crearon por solicitud de Victor (2026-11-06) y quedan en borrador, sin autorización de ejecución |

### 6.2 Hallazgos en los documentos superiores (requieren decisión humana; no se modificaron)

| # | Documento | Hallazgo | Propuesta |
| --- | --- | --- | --- |
| H1 | constitution §2, §4, §27–§29, §35; AGENTS "Estado de esta entrega" | Describen la fase inicial solo documental ("NV-01 a NV-03 ⛔ NO EJECUTAR", "no crear workflows, Dockerfile ni Terraform", "SPEC ACTIVA: NINGUNA"), aunque NV-01 a NV-09 ya se ejecutaron con autorización | Reemitir constitution 1.1.0 y AGENTS con el estado vigente y el protocolo «EJECUTA» / «OK PARA APPLY» |
| H2 | constitution §30 | "Ninguno está seleccionado", pero Azure está seleccionado y desplegado (NV-03, M-BE-036 a M-BE-041) | Registrar Azure como proveedor seleccionado |
| H3 | constitution §33 | Git Flow con `develop` y `feature/<spec>-...`; la práctica real son ramas cortas hacia `main` con PR, checks obligatorios y ruleset | Actualizar la convención a la práctica aprobada |
| H4 | AGENTS (rutas protegidas) | "No hacer commit ni push", mientras el flujo aprobado sí lo hace a petición humana en ramas con PR | Precisar: commit y push solo a petición humana, nunca directo a `main` |
| H5 | AGENTS "Cierre de esta tarea documental" | Se refiere a la verificación inicial de 27 archivos | Retirar o mover a la evidencia histórica |
| H6 | constitution §6 y M-BE-022 | Cualquier servicio separado para Smart Match o IA generativa contradice "monolito modular, no microservicios" | Mantenerlos como módulos; un servicio aparte solo con reforma de la constitución |

**Estado:** H1 a H5 atendidos en `constitution.md` 1.1.0 y `AGENTS.md` 1.1.0 (M-BE-057), aprobados por Victor el 2026-11-06; H6 queda codificado en constitution §6 1.1.0.

## 7. Mantenimiento de este índice

Este archivo se regenera cuando se crea o cambia de estado una spec. El mapa y la cobertura se calculan a partir de los encabezados de cada `spec.md` y de las casillas de cada `task.md`.
