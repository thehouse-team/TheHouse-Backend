# NV-18 — Cierre de trato con QR y renta — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `plan.md` |
| Versión | 1.1.0-borrador |
| Fecha | 2026-11-12 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-12 («Apruebo y EJECUTA»: aprobación de 1.1.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T11; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-18-deal-closing-rental/`; sin comandos en Production) |
| Estado de ejecución | **✅ COMPLETA — 1.1.0 T01–T11 (2026-11-12): PASS WITH WARNINGS, ver `evidence.md`** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Spec | `spec.md` 1.1.0-borrador |
| Tasks | `task.md` 1.1.0-borrador |

Plan preparado para revisión. No crea código, colecciones ni índices hasta la autorización «EJECUTA».

## 1. Enfoque

Primero el dominio del convenio y del código (estados, vencimiento, hash, intentos) con pruebas unitarias; después crear, revisar y aceptar con el cambio del cuarto y la prueba de concurrencia; luego los efectos sobre solicitudes y visitas, Mi renta y fin de renta; al final fuerza bruta, privacidad y la prueba real en Azure.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01, R02 | T01, T02 | Convenio desde una solicitud aceptada; Código de cierre seguro |
| F2 | Persistencia e integración | R03, R04, R05, R06 | T03, T04, T05, T06 | Revisar y aceptar; Efectos al rentar; Mi renta; Convenios del arrendador, cancelar y fin de renta |
| F3 | Seguridad, autorización y límites | R07 | T07 | Protección contra fuerza bruta y privacidad |
| F4 | Pruebas y evidencia | R08, R09 | T08, T09, T10, T11 | Contrato para Mobile y despliegue; Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Convenio desde una solicitud aceptada** (R01): El arrendador crea el convenio solo con solicitud `accepted` de su cuarto, cuarto `available` y edificio `verified`; valida montos, fechas, duración y día de pago; calcula `endDate`; 409 `request_state`, `room_unavailable` y `agreement_open`; lo ajeno 404; sin modo arrendador 403.
- **T02 · Código de cierre seguro** (R02): Crear y `/code` entregan `qrToken` y `backupCode` que vencen a los 10 minutos; solo se guarda su hash; un código nuevo invalida el anterior; el código se acepta sin guiones ni distinción de mayúsculas.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T03 · Revisar y aceptar** (R03): `preview` devuelve los términos y las reglas sin consumir el código; `accept` exige `acceptedTerms: true`, activa el convenio, consume el código y marca el cuarto `rented`; 410 vencido o usado; 404 de otra cuenta; prueba de concurrencia con dos aceptaciones y con un cambio del cuarto a mitad.
- **T04 · Efectos al rentar** (R04): Las demás solicitudes activas del cuarto quedan `rejected` con `not_available` y las visitas activas de otros estudiantes a ese cuarto quedan canceladas por el arrendador; el cuarto deja de salir en la búsqueda; los pasos se pueden repetir sin duplicar efectos.
- **T05 · Mi renta** (R05): Devuelve el convenio `active` con próximo pago calculado, dirección exacta, ubicación, reglas y `conversationId`; sin renta activa 404 `no_active_rental`; tras terminar la renta ya no sale la dirección.
- **T06 · Convenios del arrendador, cancelar y fin de renta** (R06): Listado y detalle por estado y cuarto sin el código; cancelar solo `pending`; terminar solo `active` con motivo opcional, el cuarto vuelve a `available`; historial conservado; puerto de historial de rentas para NV-19.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T07 · Protección contra fuerza bruta y privacidad** (R07): Rate limits y bloqueos por cuenta y por código verificados; hashes sin código en claro en base de datos ni logs; vistas sin datos de contacto; Redis caído 503.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T08 · Contrato para Mobile y despliegue** (R08): Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba HTTP real en Azure (crear, revisar con código, aceptar, Mi renta, cuarto fuera de la búsqueda, terminar y cuarto de nuevo disponible).
- **T09 · Pruebas unitarias y de integración** (R09): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T10 · Pruebas de seguridad** (R09): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T11 · Evidencia y revisión final** (R09): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T03 depende de: T01, T02.
- T04 depende de: T01, T02.
- T05 depende de: T01, T02.
- T06 depende de: T01, T02.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06, T07.
- T09 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T10 depende de: T01, T02, T03, T04, T05, T06, T07, T08.
- T11 depende de: T09, T10.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Convenio desde una solicitud aceptada | Pruebas automatizadas en CI |
| R02 | Código de cierre seguro | Pruebas automatizadas en CI |
| R03 | Revisar y aceptar | Pruebas automatizadas en CI |
| R04 | Efectos al rentar | Pruebas automatizadas en CI |
| R05 | Mi renta | Pruebas automatizadas en CI |
| R06 | Convenios del arrendador, cancelar y fin de renta | Pruebas automatizadas en CI |
| R07 | Protección contra fuerza bruta y privacidad | Pruebas automatizadas en CI |
| R08 | Contrato para Mobile y despliegue | Revisión de reportes y `evidence.md` |
| R09 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- BOLA: el arrendador solo ve y cambia convenios de sus cuartos y el estudiante solo revisa y acepta los suyos; lo ajeno responde 404.
- El código y el token del QR se guardan solo como SHA-256; la comparación es por hash con índice y en tiempo constante cuando se compara en memoria; nunca aparecen en logs.
- Fuerza bruta: rate limit de `preview` y `accept` 10/min por usuario y 30/min por IP; bloqueo por cuenta tras 5 códigos fallidos en 15 minutos y del código tras 5 fallos; con 32^8 combinaciones y 10 minutos de vida, adivinarlo no es práctico (RNF-17).
- El arrendador ve el nombre del perfil y la universidad del estudiante, nunca su correo, teléfono ni id; el estudiante ve solo el nombre del arrendador.
- Dirección exacta solo en Mi renta con convenio `active`; nunca en la vista previa ni en vistas del arrendador.
- Entradas con campos desconocidos → 400 (sin `studentId`, `roomId` ni `status` del cliente); rate limit 60/min por usuario en el resto; Redis caído → 503.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 y Redis Upstash | $0 | Una colección nueva (`agreements`) con pocos documentos e índices; un candado y contadores en Redis |

## 8. Puertas

| Puerta | Momento | Criterio | Si falla |
| --- | --- | --- | --- |
| G0-Doc | Antes de ejecutar | Aprobación documental de 1.1.0-borrador | PASS (M-BE-070) |
| G0-Ejec | Inicio | «EJECUTA» con tasks y rutas; decisiones de la §13 de la spec resueltas o aceptadas | PASS (2026-11-12) |
| G1 | Fin F1 | Tasks de F1 cerradas con evidencia | No avanzar |
| G2 | Fin F2 | Tasks de F2 cerradas con evidencia | No avanzar |
| G3 | Fin F3 | Tasks de F3 cerradas con evidencia | No avanzar |
| G4 | Fin F4 | Tasks de F4 cerradas con evidencia | No cerrar |

## 9. Trazabilidad

| Requisito | Fase | Tasks |
| --- | --- | --- |
| R01 | F1 | T01 |
| R02 | F1 | T02 |
| R03 | F2 | T03 |
| R04 | F2 | T04 |
| R05 | F2 | T05 |
| R06 | F2 | T06 |
| R07 | F3 | T07 |
| R08 | F4 | T08 |
| R09 | F4 | T09, T10, T11 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **✅ COMPLETA — 1.1.0 T01–T11 (2026-11-12): PASS WITH WARNINGS, ver `evidence.md`**.
