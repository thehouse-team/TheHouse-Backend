# NV-21 — Verificación, reportes y moderación — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `plan.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-19 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; `infra/terraform/` solo hasta el `plan` (el `apply` requiere «OK PARA APPLY»); sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.2.0 T01–T10 (2026-11-19): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el cliente de Content Safety con un servidor falso y el umbral en pruebas unitarias; después la revisión en cada caso de uso de texto y en la subida de fotos; luego el registro de rechazos y la vista del administrador; al final Terraform con «OK PARA APPLY», el despliegue y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R02, R03 | T02, T03 | Moderación de texto antes de guardar; Moderación de imágenes antes de guardar |
| F2 | Persistencia e integración | R01, R04 | T01, T04 | Cliente de Content Safety; Registro de rechazos para el administrador |
| F3 | Seguridad, autorización y límites | R05, R06 | T05, T06 | Recurso de Azure con identidad administrada; Seguridad, privacidad y límites |
| F4 | Pruebas y evidencia | R07, R08 | T07, T08, T09, T10 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T02 · Moderación de texto antes de guardar** (R02): Cada campo de la lista del alcance pasa por la revisión antes de guardar; un texto inapropiado responde 422 y no se guarda; uno limpio se guarda como hoy (una prueba por módulo).
- **T03 · Moderación de imágenes antes de guardar** (R03): Foto del edificio y de los cuartos revisadas antes de guardar; documentos nunca enviados; imagen grande reducida antes de enviarla.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T01 · Cliente de Content Safety** (R01): Llamadas de texto e imagen con identidad administrada, umbral 4, tiempo máximo y un reintento; falla cerrado con 503; probado contra un servidor falso.
- **T04 · Registro de rechazos para el administrador** (R04): Rechazo registrado sin contenido; vista paginada y filtrable solo para admin o moderador; TTL de 180 días.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T05 · Recurso de Azure con identidad administrada** (R05): Terraform del recurso F0, la identidad y el rol; plan revisado; apply con «OK PARA APPLY»; costo $0 verificado.
- **T06 · Seguridad, privacidad y límites** (R06): Sin llaves ni contenido en logs; documentos excluidos; ruta admin protegida; Content Safety caído o sin cupo → 503 sin guardar.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Contrato para Mobile y despliegue** (R07): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba real en Azure: texto y foto limpios se guardan, inapropiados se rechazan con 422 y aparecen en la vista del administrador.
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

- T01 depende de: T02, T03.
- T02 depende de: autorización «EJECUTA».
- T03 depende de: autorización «EJECUTA».
- T04 depende de: T02, T03.
- T05 depende de: T02, T03, T01, T04.
- T06 depende de: T02, T03, T01, T04.
- T07 depende de: T02, T03, T01, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07.
- T10 depende de: T08, T09.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Cliente de Content Safety | Pruebas automatizadas en CI |
| R02 | Moderación de texto antes de guardar | Pruebas automatizadas en CI |
| R03 | Moderación de imágenes antes de guardar | Pruebas automatizadas en CI |
| R04 | Registro de rechazos para el administrador | Pruebas automatizadas en CI |
| R05 | Recurso de Azure con identidad administrada | Pruebas automatizadas en CI |
| R06 | Seguridad, privacidad y límites | Pruebas automatizadas en CI |
| R07 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R08 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Sin llaves: la API usa la identidad administrada de la Container App y la autenticación local del recurso está desactivada.
- Nunca se envían documentos de verificación ni datos de otras personas; solo el texto o la foto que el usuario está subiendo.
- El contenido rechazado no se guarda ni se registra en logs; los logs llevan solo la categoría y la severidad.
- Rol mínimo (`Cognitive Services User`) limitado al recurso; la identidad del CD no cambia.
- La ruta de administración exige rol `admin` o `moderator` (NV-21) y no devuelve contenido.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Azure AI Content Safety F0 | $0 | Plan gratuito; si se agota el cupo, Azure detiene el servicio sin cobrar (→ 503). Cupos a verificar en el portal antes del apply |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`moderation_rejections`) con TTL |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.2.0-borrador | PASS (M-BE-079) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-17) |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F2 | T01 |
| R02 | F1 | T02 |
| R03 | F1 | T03 |
| R04 | F2 | T04 |
| R05 | F3 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07 |
| R08 | F4 | T08, T09, T10 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.2.0 T01–T10 (2026-11-19): PASS WITH WARNINGS, ver `evidence.md`**.
