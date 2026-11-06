# NV-18 — Cierre de trato con QR y renta

| Campo | Valor |
| --- | --- |
| ID | NV-18 |
| Documento | `spec.md` |
| Versión | 1.0.0-borrador |
| Fecha | 2026-11-06 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Aprobación humana | Pendiente |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | RF-81, RF-82, RF-83, RF-84 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-02, RNF-09, RNF-17, RNF-18 |
| Plan | `plan.md` 1.0.0-borrador |
| Tasks | `task.md` 1.0.0-borrador |
| Módulo | Rentals |
| Precondición | NV-12 y NV-16 |
| Relación | Cambia el estado del cuarto (NV-12) y cierra solicitudes (NV-16); habilita reseñas (NV-19) |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

## 1. Contexto

Hoy el cierre de trato con QR del prototipo funciona solo en un dispositivo porque no hay backend. El propietario arma el convenio (estudiante, renta, depósito, inicio, duración y día de pago) y genera un QR con un código de respaldo que vence en 10 minutos; el estudiante lo escanea o escribe el código, revisa el convenio y lo acepta, y el cuarto pasa a Rentado para ambos. TheHouse no procesa pagos (RES-03, RES-04).

## 2. Objetivo

Definir convenios, códigos de cierre de un solo uso validados en servidor, la confirmación atómica que marca el cuarto como Rentado y la vista Mi renta.

## 3. Precondiciones

1. NV-12 (cuartos) y NV-16 (solicitud aceptada).
2. Autorización «EJECUTA».

## 4. Alcance

1. Crear convenio en borrador.
2. Emitir código QR y código de respaldo de un solo uso que vence en 10 minutos.
3. Canjear el código y aceptar el convenio con transacción.
4. Mi renta del estudiante.
5. Fin de renta por el propietario.

## 5. Fuera de alcance

- Pagos, cobranza o depósitos dentro de la app (RES-03).
- Firma electrónica de contrato formal (RES-04).

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/landlord/agreements` | Dueño del cuarto | Crea convenio en borrador | 201, 400, 404, 409 |
| POST | `/api/v1/landlord/agreements/{id}:issue-code` | Dueño | Emite QR y código de respaldo (10 min) | 200, 404, 409, 429 |
| POST | `/api/v1/agreements:redeem` | Estudiante | Canjea `{ code }` o `{ qrToken }` y devuelve el convenio | 200, 400, 401, 404, 410, 429 |
| POST | `/api/v1/agreements/{id}:accept` | Estudiante del convenio | Acepta; el cuarto pasa a Rentado | 200, 404, 409, 410 |
| GET | `/api/v1/users/me/rental` | Estudiante | Mi renta: próximo pago, convenio, dirección exacta y contacto | 200, 401, 404 |
| POST | `/api/v1/landlord/agreements/{id}:end` | Dueño | Termina la renta; el cuarto vuelve a Libre | 200, 404, 409 |

- Convenio: `roomId`, `studentId` (de una solicitud aceptada), `monthlyRent`, `deposit`, `startDate`, `durationMonths` o `minContract`, `paymentDay` (1–28).
- `issue-code` devuelve `qrToken` (firmado, opaco), `backupCode` (8 caracteres sin ambigüedad) y `expiresAt`.
- `redeem` con código vencido o usado responde 410.

## 7. Módulo, datos propios e integración

Módulo **Rentals** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `agreements`, `closing_codes` |
| Usa (puertos de otros módulos) | Messaging `IRequestQuery` (NV-16); Listings `IRoomOccupancy` (NV-12), que participa en la misma transacción; Listings `IRoomQuery` (NV-12) |
| Publica (eventos de dominio) | `AgreementIssued`, `AgreementActivated`, `AgreementEnded` |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `agreements`: `roomId`, `ownerId`, `studentId`, términos, `status` (`draft`, `code_issued`, `active`, `ended`, `cancelled`), fechas; índice único parcial de un convenio activo por cuarto.
- `closing_codes`: `agreementId`, `codeHash`, `qrTokenHash`, `expiresAt`, `usedAt`, `attempts`; índice TTL.

## 9. Reglas de negocio

1. Solo para un estudiante con solicitud aceptada del mismo cuarto y con el cuarto Libre.
2. Un solo código vigente por convenio; emitir otro invalida el anterior.
3. El código expira a los 10 minutos, es de un solo uso y tiene 5 intentos de canje antes de invalidarse (RNF-17).
4. Aceptar ejecuta una transacción de MongoDB: Rentals marca el convenio `active` y consume el código, y el puerto `IRoomOccupancy` de Listings marca el cuarto Rentado en la misma sesión (Listings no depende de Rentals); después se publica `AgreementActivated` y Messaging cierra las demás solicitudes del cuarto (RNF-18).
5. Mi renta muestra la dirección exacta y el contacto del propietario solo mientras el convenio está activo.
6. Terminar la renta deja el cuarto Libre y conserva el convenio como historial.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Códigos guardados solo como hash; comparación en tiempo constante.
- Rate limit de canje: 10/min por usuario y 30/min por IP; bloqueo del código tras 5 fallos.
- El QR no contiene datos personales ni términos del convenio, solo un token opaco.
- Solo el estudiante destinatario puede canjear y aceptar (BOLA).

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Cuarto no Libre o convenio en estado inválido | 409 | Conflicto |
| Código vencido o usado | 410 | Recurso no disponible |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-18-R01 | Convenio en borrador | El dueño crea un convenio válido solo con solicitud aceptada y cuarto Libre; valida montos, fechas y día de pago. | F1 | T01 |
| NV-18-R02 | Código de cierre seguro | QR y código de respaldo de un solo uso, 10 minutos, 5 intentos; emitir uno nuevo invalida el anterior. | F1 | T02 |
| NV-18-R03 | Canje y aceptación atómica | Canjear devuelve el convenio; aceptar actualiza convenio, cuarto, solicitudes y código en una transacción; prueba de concurrencia con dos aceptaciones. | F2 | T03 |
| NV-18-R04 | Mi renta | Devuelve próximo pago calculado, términos, dirección exacta y contacto solo con convenio activo. | F2 | T04 |
| NV-18-R05 | Fin de renta | El dueño termina el convenio y el cuarto vuelve a Libre; historial conservado. | F2 | T05 |
| NV-18-R06 | Protección contra fuerza bruta | Rate limits y bloqueo por intentos verificados; códigos en hash; comparación en tiempo constante. | F3 | T06 |
| NV-18-R07 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T07, T08, T09 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Transacciones en Atlas M0 | Usar transacciones multi-documento (M0 es replica set) | PROPUESTO |
| Contacto en Mi renta | Chat de NV-16 y teléfono del arrendador solo con convenio activo | PENDIENTE |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| MongoDB Atlas M0 | $0 | Transacciones incluidas |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-18-RK01 | Adivinar el código de respaldo | Renta suplantada | 8 caracteres, 5 intentos, 10 minutos y rate limit |
| NV-18-RK02 | Doble aceptación | Dos rentas para un cuarto | Transacción e índice único parcial |
| NV-18-RK03 | Exponer la dirección sin renta | Privacidad | Regla por estado del convenio |

## 16. Dependencias

- NV-12 (estado del cuarto).
- NV-16 (solicitud aceptada).
- NV-19 (reseña tras renta).
- NV-20 (notificaciones).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 7 |
| Fases del plan | 4 |
| Tasks | 9 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 0 |
| Estado documental | **🟡 BORRADOR — PENDIENTE DE APROBACIÓN** |
| Estado de ejecución | **⛔ NO EJECUTAR HASTA «EJECUTA»** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
