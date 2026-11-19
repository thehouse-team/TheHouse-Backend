# NV-21 — Verificación, reportes y moderación

| Campo | Valor |
| --- | --- |
| ID | NV-21 |
| Documento | `spec.md` |
| Versión | 1.2.0-borrador |
| Fecha | 2026-11-17 |
| Estado documental | **✅ APROBADA** |
| Aprobación humana | Victor Manuel Jose Jose — 2026-11-19 («Apruebo y EJECUTA»: aprobación de 1.2.0 con las decisiones de la §13, validación previa del contrato §6.1 por el responsable de Mobile, y «EJECUTA» de T01–T10; rutas `TheHouse/`, `TheHouse.Application/`, `TheHouse.Domain/`, `TheHouse.Infrastructure/`, los tres proyectos de pruebas y `specs/nv-21-verification-moderation/`; `infra/terraform/` solo hasta el `plan` (el `apply` requiere «OK PARA APPLY»); sin comandos en Production) |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas (recurso aplicado); T07–T10 esperan CI, despliegue y Azure** |
| Autoridad | `constitution.md` → `AGENTS.md` → `memoria.md` |
| Requerimientos de origen | Requisito del curso §16 (evaluar con una API de la nube antes de almacenar); RNF-03 (prevención de fraude: verificación, reporte y moderación); RF-39 a RF-44 siguen en 1.1.0 (`Requerimientos_TheHouse_v2`) |
| No funcionales | RNF-01, RNF-03, RNF-10 |
| Plan | `plan.md` 1.2.0-borrador |
| Tasks | `task.md` 1.2.0-borrador |
| Módulo | Moderation |
| Precondición | NV-21 1.1.0, NV-11, NV-12, NV-13, NV-16, NV-17, NV-19 y NV-22 desplegadas; NV-03 (Terraform) para el recurso nuevo |
| Relación | Se aplica en los casos de uso que guardan texto o fotos de Profile (NV-11), Listings (NV-12), Media (NV-13), Messaging (NV-16), Visits (NV-17), Reviews (NV-19), Roommates (NV-22) y en los reportes de 1.1.0; los documentos de verificación (INE y comprobante) no se envían |

Spec funcional en borrador para revisión humana. No autoriza código, paquetes, índices, colecciones, workflows ni cambios en Azure: la ejecución requiere aprobación documental y la autorización «EJECUTA» con versión, tasks y rutas.

**Cambio 1.2.0-borrador (2026-11-17):** la versión 1.1.0 (verificación de edificios, reportes y moderación humana) quedó completa y desplegada (ver `evidence.md`). El curso pide que el texto, las imágenes y los videos que se suben a la app **se evalúen con una API de la nube antes de guardarlos** y que lo inapropiado **no se permita subir** (§16 Móvil). Esta versión agrega esa revisión automática con **Azure AI Content Safety** (plan gratuito F0) en cada texto público y en cada foto, antes de guardarlos; los reportes y la moderación humana de 1.1.0 siguen igual. La app no sube videos hoy; si se agregan, se moderarán con la misma regla en otra versión. Requiere un recurso nuevo en Azure (Terraform con «OK PARA APPLY») autenticado con identidad administrada, sin llave ni secreto.

## 1. Contexto

Con 1.1.0 la moderación es reactiva: alguien reporta y un administrador decide. El curso exige además una revisión automática previa: antes de guardar texto, imágenes o videos se consulta una API de la nube y, si el contenido es inapropiado, se rechaza. Hoy cualquier texto o foto válidos se guardan sin esa revisión.

## 2. Objetivo

Que ningún texto público ni foto quede guardado sin pasar antes por Azure AI Content Safety, que el contenido inapropiado se rechace con un mensaje claro en español y que los administradores vean los rechazos para detectar abuso, sin enviar documentos personales a la nube y sin costo.

## 3. Precondiciones

1. NV-21 1.1.0 y los módulos que guardan contenido desplegados.
2. Plan de Terraform revisado y «OK PARA APPLY» para el recurso de Content Safety, la identidad y el rol.
3. Cupos del plan F0 (textos e imágenes por mes y solicitudes por segundo) verificados en el portal de Azure y registrados en `evidence.md` antes del apply.
4. Validación del contrato §6.1 con el responsable de Mobile.
5. Autorización «EJECUTA».

## 4. Alcance

1. Revisión de texto antes de guardar: nombre de perfil (NV-11), nombre y descripción de edificios y cuartos (NV-12), solicitudes y mensajes de chat (NV-16 y NV-22), mensaje de visita (NV-17), comentario de reseña (NV-19), perfil de compañero (NV-22) y detalles de reportes (NV-21).
2. Revisión de imágenes antes de guardar: foto del edificio y fotos de los cuartos (NV-13).
3. Rechazo con 422 `content_rejected` y la categoría, en español.
4. Registro de rechazos sin el contenido y vista paginada para administradores.
5. Recurso de Azure AI Content Safety F0 con identidad administrada (Terraform).
6. Contrato para Mobile y despliegue verificado en Azure.

## 5. Fuera de alcance

- Documentos de verificación (INE y comprobante): son privados, no se publican y los revisa un administrador (1.1.0).
- Videos: la app no los sube; una versión futura los moderará antes de guardar.
- Revisar contenido ya guardado antes de esta versión (se puede reportar como hoy).
- Moderación de imágenes o texto con modelos propios o servicios de otras nubes.
- Detectar teléfonos o correos en descripciones para evitar contacto fuera de la app.

## 6. Contrato HTTP

Prefijo `/api/v1` (`ApiConventions.Prefix`). Cuerpos JSON en camelCase; fechas ISO 8601 en UTC; paginación `page`/`pageSize` (máximo 50) con el sobre `items`, `page`, `pageSize`, `totalItems`, `totalPages` de NV-01.

| Método | Ruta | Acceso | Descripción | Respuestas |
| --- | --- | --- | --- | --- |
| (varios) | `Rutas que guardan texto o fotos (NV-11, NV-12, NV-13, NV-16, NV-17, NV-19, NV-21, NV-22)` | Usuario | Cambio aditivo: 422 si el contenido es inapropiado; 503 si la revisión no está disponible | …, 422, 503 |
| GET | `/api/v1/admin/moderation/rejections` | Admin o moderador | Rechazos automáticos, paginados y filtrables por usuario o tipo | 200, 400, 401, 403, 429, 503 |

- Rechazo: `422 { type, title, status, detail, code: "content_rejected", field, categories: ["hate" | "sexual" | "violence" | "self_harm" | "blocklist"] }`; `detail` en español («El texto no cumple las reglas de la comunidad»). `field` indica qué campo se rechazó (por ejemplo `description` o `file`). Nada se guarda.
- Si Content Safety no responde o se agota el cupo gratuito → 503 `moderation_unavailable` con `Retry-After`; nada se guarda (falla cerrado, como pide el curso).
- Admin: `GET /admin/moderation/rejections?userId=&kind=&page=1&pageSize=20` → `{ items: [{ id, userId, kind, field, categories, maxSeverity, createdAt }], page, pageSize, totalItems, totalPages }`. Nunca incluye el texto ni la imagen rechazados.
- Las rutas existentes no cambian sus respuestas de éxito; solo agregan 422 y 503 a sus posibles errores.

## 6.1 Contrato para Mobile

Entregable acordado con Mobile: se envía junto con el aviso de despliegue. `DEPLOYED URL` y `TESTS` se completan en `evidence.md` al desplegar por el CD.

```text
ENDPOINT     Cualquier ruta que guarde texto o fotos (perfil, edificios, cuartos, fotos, solicitudes, chat, visitas, reseñas, compañero, reportes)
METHOD       POST / PUT / PATCH
AUTH         Bearer JWT (usuario tomado del token)
HEADERS      —
REQUEST      Igual que hoy
RESPONSE     422 { type, title, status, detail, code: "content_rejected", field, categories: ["hate" | "sexual" | "violence" | "self_harm" | "blocklist"] }
STATUS CODES Los de cada ruta + 422 (content_rejected) + 503 (moderation_unavailable)
PAGINATION   No
```

```text
ENDPOINT     /api/v1/admin/moderation/rejections?userId=&kind=&page=1&pageSize=20
METHOD       GET
AUTH         Bearer JWT con rol `admin` o `moderator` (NV-21)
HEADERS      —
REQUEST      userId?, kind?, page, pageSize
RESPONSE     200 { items: [{ id, userId, kind, field, categories, maxSeverity, createdAt }], page, pageSize, totalItems, totalPages }
STATUS CODES 200, 400, 401, 403, 429, 503
PAGINATION   Sí: createdAt desc
```

```text
DEPLOYED URL https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io (pendiente de despliegue)
TESTS        pendiente: unitarias, integración, seguridad y prueba HTTP real en Azure
```

## 7. Módulo, datos propios e integración

Módulo **Moderation** del monolito modular (constitution §6): es propietario exclusivo de sus casos de uso y de sus colecciones; otros módulos solo acceden a ellas por contratos (puertos de Application) o eventos de dominio en proceso. Sin dependencias circulares entre módulos.

| Aspecto | Detalle |
| --- | --- |
| Colecciones propias | `staff_roles`, `verification_reviews`, `reports`, `user_suspensions`, `audit_events`, `moderation_rejections` |
| Usa (puertos de otros módulos) | Listings `IBuildingRepository` e `IRoomRepository` (NV-12) para decisiones y suspensiones; Identity `IUserRepository` e `IRefreshTokenStore` (NV-10) para cuentas y revocación |
| Publica (eventos de dominio) | `Puerto compartido `IContentModerator` en `Application/Abstractions` (como `INotificationSink`), usado por Profile (NV-11), Listings (NV-12), Media (NV-13), Messaging (NV-16), Visits (NV-17), Reviews (NV-19) y Roommates (NV-22) sin depender del módulo Moderation; la implementación en Infrastructure llama a Content Safety y escribe `moderation_rejections`` |
| Consume (eventos de dominio) | — |

Los eventos se procesan en el mismo proceso y de forma idempotente; si una réplica falla a mitad del procesamiento, el evento se reintenta sin duplicar efectos (constitution §14 y §15). Ningún estado necesario para la corrección queda en memoria local.

## 8. Modelo de datos

- `moderation_rejections` (colección nueva de Moderation): `userId`, `kind` (`text` o `image`), `field`, `source` (ruta lógica, por ejemplo `room.description`), `categories`, `maxSeverity`, `createdAt`. Sin el contenido. TTL de 180 días.
- Índices: `{ userId, createdAt }` y `{ createdAt }`.
- Azure: `azurerm_cognitive_account` de tipo `ContentSafety`, SKU `F0`, `local_auth_enabled = false`; identidad administrada asignada por el sistema en `ca-thehouse-u2`; rol `Cognitive Services User` de esa identidad solo sobre el recurso.

## 9. Reglas de negocio

1. Orden en cada caso de uso: validación de formato (como hoy) → Content Safety → guardar. Si Content Safety rechaza o falla, no se guarda nada.
2. Texto: categorías odio, sexual, violencia y autolesión; se rechaza con severidad 4 o más (escala 0, 2, 4, 6) en cualquiera. Además una lista corta de términos prohibidos en español mantenida en el repositorio.
3. Imágenes: mismas categorías; se rechaza con severidad 4 o más; si la imagen pasa de 4 MB se envía una copia reducida (límite de la API).
4. Los textos de un mismo envío (por ejemplo nombre y descripción) se revisan juntos cuando caben en una sola llamada.
5. Tiempo máximo por llamada de 3 s con un reintento; después, 503.
6. Cada rechazo se registra (sin contenido) para que el administrador vea patrones de abuso; varios rechazos no suspenden la cuenta automáticamente.

## 10. Seguridad

Comunes a todas las specs funcionales:

- Autenticación con JWT Bearer de NV-05 y revocación de NV-06 en todo endpoint que no sea público; 401 sin detalles.
- Autorización por recurso (OWASP API1 BOLA): cada consulta filtra por el `sub` del token o por el rol requerido; un recurso ajeno responde 404, no 403, para no confirmar su existencia.
- Contratos de entrada estrictos (`JsonUnmappedMemberHandling.Disallow`) para impedir asignación masiva de campos (OWASP API3 BOPLA); proyecciones explícitas en las respuestas.
- Rate limit en Redis por usuario y por IP; si Redis no está disponible, falla cerrado con 503 (patrón NV-05/NV-07).
- Problem Details en español sin stack traces; `Cache-Control: no-store` en respuestas autenticadas y en errores.
- Sin PII, tokens ni contenido de mensajes en logs; identificadores con hash cuando se necesiten para correlación.

Específicas de esta spec:

- Sin llaves: la API usa la identidad administrada de la Container App y la autenticación local del recurso está desactivada.
- Nunca se envían documentos de verificación ni datos de otras personas; solo el texto o la foto que el usuario está subiendo.
- El contenido rechazado no se guarda ni se registra en logs; los logs llevan solo la categoría y la severidad.
- Rol mínimo (`Cognitive Services User`) limitado al recurso; la identidad del CD no cambia.
- La ruta de administración exige rol `admin` o `moderator` (NV-21) y no devuelve contenido.

## 11. Errores

Formato `application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

| Caso | Status | `title` |
| --- | --- | --- |
| Validación | 400 | Validación fallida |
| Sin autenticación o token revocado | 401 | No autorizado |
| Recurso inexistente o ajeno | 404 | Recurso no encontrado |
| Contenido inapropiado | 422 | Contenido no permitido (`content_rejected`) |
| Rate limit | 429 | Demasiadas solicitudes |
| Interno | 500 | Error interno del servidor |
| MongoDB o Redis no disponible | 503 | Servicio no disponible |
| Content Safety no disponible o cupo agotado | 503 | Revisión no disponible (`moderation_unavailable`) |

## 12. Requisitos verificables

| ID | Requisito | Criterio de aceptación | Fase | Tasks |
| --- | --- | --- | --- | --- |
| NV-21-R01 | Cliente de Content Safety | Llamadas de texto e imagen con identidad administrada, umbral 4, tiempo máximo y un reintento; falla cerrado con 503; probado contra un servidor falso. | F2 | T01 |
| NV-21-R02 | Moderación de texto antes de guardar | Cada campo de la lista del alcance pasa por la revisión antes de guardar; un texto inapropiado responde 422 y no se guarda; uno limpio se guarda como hoy (una prueba por módulo). | F1 | T02 |
| NV-21-R03 | Moderación de imágenes antes de guardar | Foto del edificio y de los cuartos revisadas antes de guardar; documentos nunca enviados; imagen grande reducida antes de enviarla. | F1 | T03 |
| NV-21-R04 | Registro de rechazos para el administrador | Rechazo registrado sin contenido; vista paginada y filtrable solo para admin o moderador; TTL de 180 días. | F2 | T04 |
| NV-21-R05 | Recurso de Azure con identidad administrada | Terraform del recurso F0, la identidad y el rol; plan revisado; apply con «OK PARA APPLY»; costo $0 verificado. | F3 | T05 |
| NV-21-R06 | Seguridad, privacidad y límites | Sin llaves ni contenido en logs; documentos excluidos; ruta admin protegida; Content Safety caído o sin cupo → 503 sin guardar. | F3 | T06 |
| NV-21-R07 | Contrato para Mobile y despliegue | Contrato §6.1 entregado a Mobile; desplegado por el CD; prueba real en Azure: texto y foto limpios se guardan, inapropiados se rechazan con 422 y aparecen en la vista del administrador. | F4 | T07 |
| NV-21-R08 | Pruebas, seguridad y evidencia | Pruebas unitarias del dominio, de integración de cada endpoint (casos felices, validación, 401, 404 ajeno, 429 y 503) y de seguridad (BOLA, asignación masiva, fuga de datos); `evidence.md` con comandos, resultados, hallazgos y revisión de coherencia con `constitution.md`, `AGENTS.md` y `memoria.md`. | F4 | T08, T09, T10 |

## 13. Decisiones pendientes

Se registran en `memoria.md` al aprobarse; mientras estén PENDIENTE, las tasks que dependen de ellas quedan BLOCKED.

| Tema | Propuesta | Estado |
| --- | --- | --- |
| Proveedor | Azure AI Content Safety (misma nube que el backend), plan F0 | APROBADO (M-BE-079) |
| Autenticación | Identidad administrada de la Container App con rol `Cognitive Services User`; sin llave ni secreto | APROBADO (M-BE-079) |
| Umbral | Severidad 4 o más en odio, sexual, violencia o autolesión, para texto e imágenes | APROBADO (M-BE-079) |
| Falla | Falla cerrado (503) si la API no responde o se agota el cupo, como pide el curso | APROBADO (M-BE-079) |
| Alcance | Todo texto que ven otras personas y las fotos de edificios y cuartos; documentos de verificación excluidos | APROBADO (M-BE-079) |
| Rechazos | Registro sin contenido con TTL de 180 días; sin suspensión automática | APROBADO (M-BE-079) |

## 14. Costo — COST-001

| Componente | Costo | Condición |
| --- | --- | --- |
| Azure AI Content Safety F0 | $0 | Plan gratuito; si se agota el cupo, Azure detiene el servicio sin cobrar (→ 503). Cupos a verificar en el portal antes del apply |
| MongoDB Atlas M0 | $0 | Una colección pequeña (`moderation_rejections`) con TTL |

Un plan gratuito, un crédito o una alerta de presupuesto no prueban por sí solos costo $0 (constitution §17): antes de usar cualquier servicio se exige evidencia vigente de elegibilidad, cuotas, duración, tarjeta y riesgo de cobro. Todo proveedor externo nuevo requiere además una decisión M-BE con análisis frente a constitution §30 (proveedores permitidos; precedente: M-BE-034). Si algún componente no puede demostrarse en $0, la task correspondiente queda **BLOCKED**.

## 15. Riesgos

| ID | Riesgo | Impacto | Mitigación |
| --- | --- | --- | --- |
| NV-21-RK01 | Cupo gratuito agotado | No se puede publicar ni chatear hasta el siguiente mes | Cupo verificado antes del apply, alerta de uso y revisión en lote de textos del mismo envío |
| NV-21-RK02 | Falsos positivos en español | Contenido válido rechazado | Umbral 4 (no 2) y mensaje claro; el usuario puede reformular |
| NV-21-RK03 | Latencia extra al guardar | Respuesta más lenta | Tiempo máximo de 3 s; solo en escrituras |
| NV-21-RK04 | Región sin el servicio | Apply fallido | Se verifica la región en el plan de Terraform antes del apply |

## 16. Dependencias

- NV-03 (Terraform) para el recurso, la identidad y el rol, con «OK PARA APPLY».
- NV-11, NV-12, NV-13, NV-16, NV-17, NV-19 y NV-22 (casos de uso que guardan contenido).

## 17. Trazabilidad y cobertura

| Métrica | Valor |
| --- | --- |
| Requisitos | 8 |
| Fases del plan | 4 |
| Tasks | 10 |
| Requisitos sin task | 0 |
| Tasks sin requisito | 0 |
| Tasks completadas | 6 |
| Estado documental | **✅ APROBADA** |
| Estado de ejecución | **🟡 EN EJECUCIÓN — 1.2.0: T01–T06 completas (recurso aplicado); T07–T10 esperan CI, despliegue y Azure** |

## 18. Aprobación pendiente

Para aprobar se requiere la revisión humana de esta versión y la resolución (o aceptación explícita como PENDIENTE) de las decisiones de la §13. Para ejecutar se requiere además «EJECUTA» con versión, tasks autorizadas y rutas permitidas. Antes de cada iteración de desarrollo se revisa la coherencia con `constitution.md`, `AGENTS.md` y `memoria.md` y se buscan brechas de seguridad.
