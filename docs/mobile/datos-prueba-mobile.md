# Datos de prueba para validar Mobile en producción

Datos sintéticos cargados en el backend productivo (`ca-thehouse-u2`) el 2026-11-27 para que Mobile termine de validar búsqueda, detalle con fotos, Smart Match y los flujos de solicitud, mensajes, visitas, convenio y renta. No cambian contratos ni agregan servicios. Los registros DEMO (NV-07 y NV-08) no se tocaron.

**Autorización:** «Autorizo ambos» — Victor Manuel Jose Jose, 2026-11-27. Cubre la carga de datos de prueba en producción y el rol `reviewer` temporal para aprobar los edificios (M-BE-094).

## Qué se cargó

| Dato | Detalle |
| --- | --- |
| Cuentas | 8 cuentas `@example.com` con contraseña aleatoria: 5 arrendadores (modo `individual`, sin RFC, teléfono con lada `00`, que no existe), 2 estudiantes y 1 revisora. Las credenciales están solo en un archivo local fuera de git; a Mobile se le pasan landlord1, student1 y student2 por canal privado |
| Edificios | 10, todos con el nombre `[PRUEBA Mobile] …` y reglas que dicen que son datos de prueba; dirección «Calle de Prueba» en el municipio real; coordenadas a 0.5–2.4 km de su universidad (la más cercana en los 10 casos) |
| Universidades | UTTT (3 edificios), UAEH-ESTE en Tepeji (3), UAEH-ESTL, UAEH-ESAT, ITSOEH y TESJI (1 cada una) |
| Cuartos | 26: precios de 1,800 a 5,500 MXN; `single`, `double` y `shared`; baño privado o compartido; muebles, servicios, contratos (`four_months`, `six_months`, `annual`) y fechas de disponibilidad variados |
| Fotos | 3–4 por cuarto (89; el máximo del backend es 4, `MediaRules.MaximumRoomPhotos`) y una fachada por edificio. Son ilustraciones generadas por el script, sin fotos de terceros; el backend las vuelve a codificar a WebP (1600 px y miniatura de 400 px) |
| Documentos | `ine` y `property_proof` por edificio: PDF que dicen «DOCUMENTO DE PRUEBA - NO ES REAL», sin datos personales |
| Costo | ~120 subidas moderadas por Azure AI Content Safety F0 (dentro del cupo gratuito) y unos 5 MB en GridFS (Atlas M0, límite de 512 MB) |

## Ejecución

El script `.azure-check/mobile_testdata.py` (fuera de git) lo corrió Victor en cuatro fases. El script puede retomar una fase si se interrumpe.

| Fase | Resultado |
| --- | --- |
| `prepare` | Cuentas, perfiles, 10 edificios, 26 cuartos, fotos, fachadas y documentos; los 10 edificios enviados a revisión (202) |
| `--grant-role … reviewer --allow-production-role-change` | `Role granted.`; la bitácora registró «Rol reviewer asignado para el usuario 6b09b1d1… desde la línea de comandos» (actor `cli`) |
| `approve` | 10/10 edificios aprobados (`verified`) y flujos creados (ver abajo) |
| `--revoke-role … reviewer` | `Role revoked.`, también en la bitácora. La cuenta revisora queda sin rol |
| `verify` | **93/93 PASS** (la primera corrida tuvo 32 fallas del propio script: llamaba al detalle sin sesión y comparaba el encabezado `content-type` con mayúsculas; se corrigió el script, no los datos) |

### Lo que comprueba `verify`

| Bloque | Casos | Resultado |
| --- | --- | --- |
| Detalle | `GET /rooms/{id}` con sesión: 25 cuartos con `photos` (3–4, con `url` y `thumbUrl`), `thumbnailUrl` y `buildingId` correcto; el cuarto rentado da 404 | 26/26 |
| Fotos por HTTPS | Foto y miniatura del primer cuarto de cada uno: 200 `image/webp` | 25/25 |
| Búsqueda | `GET /rooms/search` en las 6 universidades: todos los cuartos de prueba con `thumbnailUrl`, y la miniatura carga por HTTPS | 12/12 |
| Smart Match | `POST /smart-match/recommendations/query` en UTTT (9 cuartos de prueba) y UAEH-ESTE (11), todos con `thumbnailUrl` | 2/2 |
| Propiedad | Cada arrendador ve exactamente sus cuartos; otro arrendador recibe 404 en un edificio ajeno | 11/11 |
| Ubicación | `GET /universities/search?lat&lng` desde cada edificio pone su universidad entre las 3 más cercanas | 10/10 |
| Flujos | Renta activa de student1; conversaciones y notificaciones de student1, student2 y landlord1 | 7/7 |

## Ids

### Universidades

| Código | universityId |
| --- | --- |
| UTTT | `6aeecc11f3c9f3f76294628f` |
| UAEH-ESTE | `6aeecc11f3c9f3f762946290` |
| UAEH-ESTL | `6aeecc11f3c9f3f762946291` |
| UAEH-ESAT | `6aeecc11f3c9f3f762946292` |
| ITSOEH | `6aeecc11f3c9f3f762946293` |
| TESJI | `6aeecc11f3c9f3f762946294` |

### Edificios y cuartos

| Edificio | buildingId | Dueño | Universidad | roomId | Precio | Tipo | Baño | Fotos |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| B01 | `6b09b1e6aa1c1664f8f18d40` | landlord1 | UTTT | `6b09b1e7aa1c1664f8f18d41` | 2800 | single | shared | 0 |
| B01 | `6b09b1e6aa1c1664f8f18d40` | landlord1 | UTTT | `6b09b1e7aa1c1664f8f18d42` | 3600 | single | private | 3 |
| B01 | `6b09b1e6aa1c1664f8f18d40` | landlord1 | UTTT | `6b09b1e7aa1c1664f8f18d43` | 2200 | shared | shared | 4 |
| B02 | `6b09b207aa1c1664f8f18d78` | landlord1 | UAEH-ESTE | `6b09b208aa1c1664f8f18d79` | 2500 | single | shared | 3 |
| B02 | `6b09b207aa1c1664f8f18d78` | landlord1 | UAEH-ESTE | `6b09b208aa1c1664f8f18d7a` | 3200 | single | private | 4 |
| B02 | `6b09b207aa1c1664f8f18d78` | landlord1 | UAEH-ESTE | `6b09b208aa1c1664f8f18d7b` | 2000 | double | shared | 3 |
| B03 | `6b09b220aa1c1664f8f18dac` | landlord2 | UAEH-ESTE | `6b09b221aa1c1664f8f18dad` | 4200 | single | private | 4 |
| B03 | `6b09b220aa1c1664f8f18dac` | landlord2 | UAEH-ESTE | `6b09b221aa1c1664f8f18dae` | 3900 | single | private | 3 |
| B03 | `6b09b220aa1c1664f8f18dac` | landlord2 | UAEH-ESTE | `6b09b221aa1c1664f8f18daf` | 2600 | single | shared | 3 |
| B04 | `6b09b23caa1c1664f8f18de0` | landlord2 | UTTT | `6b09b23daa1c1664f8f18de1` | 2100 | single | shared | 3 |
| B04 | `6b09b23caa1c1664f8f18de0` | landlord2 | UTTT | `6b09b23daa1c1664f8f18de2` | 1900 | shared | shared | 4 |
| B05 | `6b09b24daa1c1664f8f18e07` | landlord3 | UTTT | `6b09b24eaa1c1664f8f18e08` | 5500 | single | private | 4 |
| B05 | `6b09b24daa1c1664f8f18e07` | landlord3 | UTTT | `6b09b24eaa1c1664f8f18e09` | 4800 | double | private | 3 |
| B06 | `6b09b260aa1c1664f8f18e2e` | landlord3 | UAEH-ESTE | `6b09b261aa1c1664f8f18e2f` | 3000 | single | shared | 3 |
| B06 | `6b09b260aa1c1664f8f18e2e` | landlord3 | UAEH-ESTE | `6b09b261aa1c1664f8f18e30` | 3400 | single | private | 4 |
| B06 | `6b09b260aa1c1664f8f18e2e` | landlord3 | UAEH-ESTE | `6b09b261aa1c1664f8f18e31` | 2300 | shared | shared | 3 |
| B07 | `6b09b275aa1c1664f8f18e62` | landlord4 | UAEH-ESAT | `6b09b276aa1c1664f8f18e63` | 2400 | single | shared | 4 |
| B07 | `6b09b275aa1c1664f8f18e62` | landlord4 | UAEH-ESAT | `6b09b276aa1c1664f8f18e64` | 2900 | single | private | 3 |
| B08 | `6b09b288aa1c1664f8f18e89` | landlord4 | ITSOEH | `6b09b288aa1c1664f8f18e8a` | 2700 | single | shared | 3 |
| B08 | `6b09b288aa1c1664f8f18e89` | landlord4 | ITSOEH | `6b09b288aa1c1664f8f18e8b` | 3300 | single | private | 4 |
| B08 | `6b09b288aa1c1664f8f18e89` | landlord4 | ITSOEH | `6b09b288aa1c1664f8f18e8c` | 1800 | shared | shared | 3 |
| B09 | `6b09b29daa1c1664f8f18ebd` | landlord5 | UAEH-ESTL | `6b09b29eaa1c1664f8f18ebe` | 2200 | single | shared | 3 |
| B09 | `6b09b29daa1c1664f8f18ebd` | landlord5 | UAEH-ESTL | `6b09b29eaa1c1664f8f18ebf` | 2600 | single | shared | 4 |
| B10 | `6b09b2adaa1c1664f8f18ee4` | landlord5 | TESJI | `6b09b2aeaa1c1664f8f18ee5` | 3100 | single | private | 4 |
| B10 | `6b09b2adaa1c1664f8f18ee4` | landlord5 | TESJI | `6b09b2aeaa1c1664f8f18ee6` | 2500 | single | shared | 3 |
| B10 | `6b09b2adaa1c1664f8f18ee4` | landlord5 | TESJI | `6b09b2aeaa1c1664f8f18ee7` | 2000 | shared | shared | 3 |

El cuarto `6b09b1e7aa1c1664f8f18d41` (B01) está rentado: no aparece en la búsqueda y su detalle responde 404.

### Flujos

| Flujo | Estado | Ids |
| --- | --- | --- |
| Renta activa | student1 en B01 (landlord1): solicitud aceptada, 2 mensajes, visita confirmada, convenio aceptado | solicitud `6b09b4770066857d554ada84`, conversación `6b09b477fd1974ba89c4975e`, visita `6b09b47aaa1c1664f8f18f39`, convenio `6b09b47bc1e12022228bfd82` |
| Convenio pendiente | student2 en B02 (landlord1): solicitud aceptada, 2 mensajes, visita por confirmar; landlord1 emite el código (`POST /landlord/agreements/{id}/code`) y student2 lo acepta desde la app | solicitud `6b09b47e25fab0a73597f89b`, conversación `6b09b47ec5d9ade23602e410`, visita `6b09b481aa1c1664f8f18f40`, convenio `6b09b48217922ded24aae8fe` |
| Solicitud por responder | student2 en B01: landlord1 la acepta o rechaza desde la app | `6b09b482132e9634e0eaa63c` |
| Solicitud en espera | student1 en B03 (landlord2) | `6b09b4833da62f95026c36c3` |

## Qué puede probar Mobile sin afectar usuarios reales

Búsqueda, detalle, Smart Match, favoritos, solicitudes (crear, aceptar, rechazar), mensajes, visitas, aceptar el convenio con el código, ver la renta y las notificaciones, siempre con las 3 cuentas de prueba sobre cuartos `[PRUEBA Mobile]`. No se deben enviar solicitudes, mensajes ni reportes a cuartos que no sean de prueba (les llegan a arrendadores reales), ni eliminar las cuentas de prueba antes de la limpieza.

Notas para Mobile:

- `GET /rooms/{id}` exige sesión (401 sin token).
- Las URLs de fotos son relativas (`/api/v1/media/{id}` y `?size=thumb`); se antepone la URL base del API.
- Los cuartos de prueba traen `demo: false` porque son publicaciones normales.

## Limpieza (pendiente)

Mientras existan, los 10 edificios se ven en la búsqueda pública de esas universidades. Cuando Mobile termine, Victor corre `python .azure-check/mobile_testdata.py cleanup <url>`. La limpieza:

1. Termina la renta.
2. Cancela el convenio pendiente.
3. Elimina las 8 cuentas. NV-10 borra sus edificios, cuartos y fotos.

El resultado se registra aquí.
