# NV-13 — Fotografías y documentos — Plan

| Campo | Valor |
| --- | --- |
| ID | NV-13 |
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

La decisión de almacenamiento y cifrado bloquea todo lo demás; se resuelve primero y después se implementan fotos (riesgo menor) antes que documentos.

Principios:

1. Contrato primero: DTOs, códigos y errores se fijan antes de la persistencia.
2. Autorización por recurso desde la primera task que expone datos de un usuario.
3. Cada fase deja pruebas verdes en CI (NV-02) antes de avanzar.
4. Nada se despliega fuera del CD de NV-09 ni sin aprobación en `production`.
5. Un control no ejecutado no se reporta como PASS.

## 2. Fases

| Fase | Nombre | Requisitos | Tasks | Salida |
| --- | --- | --- | --- | --- |
| F1 | Contrato y dominio | R01 | T01 | Carga de fotos |
| F2 | Persistencia e integración | R02, R03, R04 | T02, T03, T04 | Procesamiento seguro de imágenes; Carga de documentos cifrados; Entrega de fotos |
| F3 | Seguridad, autorización y límites | R05, R06 | T05, T06 | Acceso restringido a documentos; Retención y borrado |
| F4 | Pruebas y evidencia | R07 | T07, T08, T09 | Pruebas, seguridad y evidencia |

## 3. Detalle de fases

### F1 — Contrato y dominio

- **T01 · Carga de fotos** (R01): Acepta solo JPEG/PNG/WebP ≤ 5 MB validados por contenido; respeta los límites por edificio y cuarto; devuelve id y URL de miniatura.

**Criterio de salida:** tasks de F1 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F2 — Persistencia e integración

- **T02 · Procesamiento seguro de imágenes** (R02): Re-codificación a WebP, miniatura y eliminación de EXIF/GPS verificadas con una imagen de prueba que contiene coordenadas.
- **T03 · Carga de documentos cifrados** (R03): INE y comprobante ≤ 10 MB cifrados con AES-256-GCM; la base guarda solo metadatos y la llave de datos cifrada.
- **T04 · Entrega de fotos** (R04): `GET /media/{id}` sirve fotos de edificios Verificados con `ETag` y caché pública; 404 para no verificados.

**Criterio de salida:** tasks de F2 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F3 — Seguridad, autorización y límites

- **T05 · Acceso restringido a documentos** (R05): Ningún endpoint público o de dueño devuelve el contenido; solo NV-21 genera enlaces temporales; pruebas de acceso denegado.
- **T06 · Retención y borrado** (R06): Borrar una foto o un cuarto elimina sus archivos; documentos con `retentionUntil` y tarea de limpieza.

**Criterio de salida:** tasks de F3 con aceptación cumplida, pruebas verdes en CI y sin hallazgos de seguridad abiertos de severidad media o mayor.

### F4 — Pruebas y evidencia

- **T07 · Pruebas unitarias y de integración** (R07): Casos felices y de error de cada endpoint y regla de negocio, con MongoDB y Redis aislados; ninguna prueba depende de servicios externos de pago.
- **T08 · Pruebas de seguridad** (R07): BOLA entre dos usuarios, asignación masiva, validación de límites, rate limit, fallo cerrado de Redis y ausencia de datos sensibles en respuestas y logs.
- **T09 · Evidencia y revisión final** (R07): `evidence.md` con commit, comandos, resultados, hallazgos, costo y revisión de coherencia y seguridad; resultado PASS, PASS WITH WARNINGS o BLOCKED.

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
- T02 depende de: T01.
- T03 depende de: T01.
- T04 depende de: T01.
- T05 depende de: T01, T02, T03, T04.
- T06 depende de: T01, T02, T03, T04.
- T07 depende de: T01, T02, T03, T04, T05, T06.
- T08 depende de: T01, T02, T03, T04, T05, T06.
- T09 depende de: T07, T08.

## 5. Estrategia de verificación

| Requisito | Control | Verificación |
| --- | --- | --- |
| R01 | Carga de fotos | Pruebas automatizadas en CI |
| R02 | Procesamiento seguro de imágenes | Pruebas automatizadas en CI |
| R03 | Carga de documentos cifrados | Pruebas automatizadas en CI |
| R04 | Entrega de fotos | Pruebas automatizadas en CI |
| R05 | Acceso restringido a documentos | Pruebas automatizadas en CI |
| R06 | Retención y borrado | Pruebas automatizadas en CI |
| R07 | Pruebas, seguridad y evidencia | Revisión de reportes y `evidence.md` |

## 6. Seguridad

- Límite de tamaño aplicado en Kestrel y en el endpoint (413) para evitar consumo de memoria.
- Sin procesamiento de imágenes con librerías vulnerables: versión fijada y escaneo SCA en CI (NV-02).
- Documentos: cifrado en reposo, acceso solo por rol revisor, auditoría de cada lectura (NV-21) y nunca en logs.
- Nombres de archivo del cliente ignorados; claves de almacenamiento aleatorias.
- Fotos de edificios no verificados no se sirven públicamente.
- Revisión de seguridad (OWASP API Top 10) al cierre de cada fase.

## 7. Costo

| Componente | Costo | Condición |
| --- | --- | --- |
| Almacenamiento de fotos y documentos | $0 solo con la opción elegida dentro de su plan gratuito | PENDIENTE DE VERIFICAR |
| Ancho de banda de imágenes | $0 dentro de la concesión de Container Apps | Miniaturas y caché HTTP para reducirlo |

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
| R02 | F2 | T02 |
| R03 | F2 | T03 |
| R04 | F2 | T04 |
| R05 | F3 | T05 |
| R06 | F3 | T06 |
| R07 | F4 | T07, T08, T09 |

## 10. No ejecución

Este plan no autoriza código, paquetes, colecciones, índices, workflows ni cambios en Azure. Estado de ejecución: **⛔ NO EJECUTAR HASTA «EJECUTA»**.
