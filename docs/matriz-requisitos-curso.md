# Matriz de requisitos del curso — Backend TheHouse

| Campo | Valor |
| --- | --- |
| Fuente | «Requerimientos para el desarrollo del proyecto Móvil» v3 (cuatrimestre septiembre–diciembre 2026) |
| Fecha | 2026-11-20 |
| Evaluación | 3 y 4 de diciembre de 2026 (último día para registrar calificaciones: 4 de diciembre) |
| Avance del backend | 240/276 tasks (87 %); 95 % sin Smart Match (NV-23/NV-24) |
| URL | https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io |

Estados: ✅ cumplido y desplegado · 🟡 parcial o con fecha · ❌ brecha · ➖ fuera del backend (Mobile, Landing o ML/GenAI).

## 1. Requisitos generales (puntos 1–12)

| # | Requisito | Estado | Evidencia en el backend | Cómo demostrarlo |
| --- | --- | --- | --- | --- |
| 2 | Evaluación por prompts de que todo está implementado | ✅ | Cada spec tiene `spec.md`, `plan.md`, `task.md` y `evidence.md` con trazabilidad requisito → task → prueba (`specs/README.md`) | Abrir `specs/README.md` y una `evidence.md` |
| 3 | Nube, base de datos y backend | ✅ | Azure Container Apps, MongoDB Atlas M0 y Upstash Redis | URL en vivo y `/health/ready` |
| 5 | Spec-Driven Development y DevSecOps | ✅ | `constitution.md` → `AGENTS.md` → `memoria.md` (decisiones M-BE) → `specs/` (NV-01 a NV-25); cada cambio con aprobación humana registrada | Mostrar una spec y su M-BE |
| 6 | Desarrollo con agentes de IA | ✅ | `.opencode/` y PRs con «Generated with Claude Code»; aprobaciones humanas en cada paso | Historial de PRs |
| 7 | Pruebas unitarias, de seguridad y de integración | ✅ | 1058 pruebas en el CI (unitarias 408, integración 496, seguridad 154), Semgrep, Gitleaks, SBOM, Trivy, Checkov y tfsec | Run del CI en GitHub Actions |
| 8 | CI/CD | ✅ | `ci.yml` (7 checks obligatorios) y `cd.yml` (publicar → firma → verificación efímera con smoke y ZAP → aprobación → despliegue → DAST); NV-02 y NV-09 | Un run del CD con su aprobación |
| 9 | Caché en base de datos, backend y frontend | 🟡 | **Backend/HTTP:** `ETag`, 304 y `Cache-Control` en catálogos y fotos (`public, max-age=31536000, immutable`). **Base de datos:** índices por consulta y la caché propia de MongoDB. **Frontend:** la app usa los `ETag` y `Cache-Control`. No hay caché de datos en Redis (Redis se usa para seguridad y límites) | `curl -I` mostrando `ETag` y luego 304 con `If-None-Match` |
| 10 | Logout en backend y móvil | ✅ | `POST /api/v1/auth/logout` revoca el JWT (`jti`) y la sesión en Redis (NV-06, NV-10) | Logout y reuso del token → 401 |
| 11 | Matriz de versionamiento aplicada en cada despliegue | ✅ | `VERSIONING.md` §11 (commit, digest, run, aprobador, revisión) y `deployment-record.json` en cada CD | `VERSIONING.md` |
| 12 | MCP | ➖ | Opcional | — |

## 2. Nube (punto 13)

| Requisito | Estado | Evidencia | Cómo demostrarlo |
| --- | --- | --- | --- |
| Todos los servicios en la nube (BD externa permitida) | ✅ | Azure (cómputo, logs, Content Safety); Atlas y Upstash como servicios externos | Portal de Azure |
| Load balancer o similar | 🟡 | Ingress de Container Apps (balanceo administrado, probes `/health/live` y `/health/ready`, sin sesiones fijas). **Dos réplicas: ventana del 3 de diciembre** (NV-03 T08/T09/T19) | `az containerapp replica list` con 2 réplicas |
| IaC con Terraform para crear y eliminar | ✅ | `infra/terraform/` y `LIFECYCLE.md`: crear (4), verificar y destruir (4) demostrado el 2026-11-19 (NV-03 T20) | `terraform plan` y `LIFECYCLE.md` |
| Salida por SSL/HTTPS | ✅ | TLS 1.3 con certificado administrado por Azure, HTTP→301 y HSTS de 30 días (NV-03 T10) | `curl -I http://…` → 301 |

## 3. Base de datos (punto 14)

| Requisito | Estado | Evidencia |
| --- | --- | --- |
| NoSQL | ✅ | MongoDB Atlas M0 (`thehouse_u2`, Azure eastus2) y Redis |

## 4. Backend (punto 15)

| Requisito | Estado | Evidencia | Cómo demostrarlo |
| --- | --- | --- | --- |
| Escalable, al menos 2 instancias | 🟡 | Sin estado local de seguridad (NV-03 T01–T05); escala 0–2. **Dos réplicas fijas el 3 de diciembre con «OK PARA APPLY»** | 2 réplicas Healthy |
| Solo AWS, Azure o GCP, con costo cero | ✅ | Azure; Cost Management 0.00 USD (1 al 19 de noviembre); presupuesto de 1 USD con alertas | Cost Management |
| Todas las funciones de la API en la nube | ✅ | 122 rutas en Container Apps | `/openapi` en el entorno efímero o la lista de rutas |
| Endpoints eficientes | ✅ | Paginación, índices, caché HTTP, compresión y proyecciones | Tiempo de respuesta en vivo |
| API o microservicios | ✅ | Monolito modular (constitution §6) más servicios auxiliares (TheHouse-GenAI y TheHouse-ML) | Diagrama de módulos |
| **Imágenes en el CDN de la nube** | ❌ | Las fotos se guardan en MongoDB (GridFS) y se sirven por la API con `Cache-Control: public, max-age=31536000, immutable` (NV-13, por COST-001). **No hay Azure CDN/Front Door** | Ver §8 |
| Observabilidad (logs de eventos y errores) | 🟡 | Log Analytics con logs de consola correlacionados (NV-03 T24/T25); trazas y métricas OTel sin exportador (T11/T12 abiertas) | Consulta KQL en Log Analytics |
| JWT y Redis distribuido | ✅ | JWT con revocación por `jti` y `sid` en Redis; rate limit, candados y sal en Redis (NV-05, NV-06, NV-10) | Login → token; logout → 401 |
| API performance: caching, rate limit, middleware, paginación, indexing, serializers, compresión | ✅ | `ETag`/304; rate limit en Redis (429 con `Retry-After`); middlewares de correlación, seguridad, revocación y límites de cuerpo; paginación `page`/`pageSize`; índices por repositorio; System.Text.Json en camelCase; `PublicResponseCompressionMiddleware` | `curl` con `Accept-Encoding: gzip` y 429 |
| Integración con patrones | ✅ | Puertos y adaptadores (Clean Architecture), circuit breaker y degradación (GenAI), cola de push, fallo cerrado | Código de `Application/Abstractions` |
| Paso de creación de infraestructura y despliegue, y su eliminación | ✅ | Terraform más CD; ciclo de vida demostrado (NV-03 T20/T21) | `LIFECYCLE.md` |
| Versionamiento | ✅ | `VERSIONING.md`, imágenes `sha-<commit>` por digest firmado | `VERSIONING.md` |

## 5. Móvil (punto 16), lo que depende del backend

| Requisito | Estado | Evidencia en el backend | Pendiente |
| --- | --- | --- | --- |
| Moderación de texto, imágenes y videos antes de guardar | ✅ | NV-21 1.2.0: Azure AI Content Safety con identidad administrada y lista de términos; 422 `content_rejected`; prueba real 10/10 | Videos: la app no los sube |
| Push en los procesos principales | 🟡 | NV-20 1.2.0: FCM con los 16 tipos de la bandeja; dispositivos 14/14 en Azure | Prueba en un teléfono real (T05) |
| Aviso de privacidad vinculado a la landing | 🟡 | NV-10 1.2.0: `GET /api/v1/legal` y aceptación registrada | URLs de la landing (`Legal__*`) |
| Eliminar cuenta | ✅ | NV-10 1.2.0: vista previa y eliminación; prueba real 27/27 | Google Play pide además la URL web |
| Sesión siempre viva salvo logout | ✅ | NV-10 1.2.0: sesión deslizante de 30 días sin tope | — |
| Al menos una función de ML y de IA generativa | 🟡 | NV-24 1.2.0 aprobada (explicaciones con GenAI); GenAI ya desplegado en el entorno | ML (NV-23) pendiente; la búsqueda en lenguaje natural es alternativa si ML se retrasa |
| Permisos, SQLite y despliegue en Google Play | ➖ | — | Mobile |

## 6. Landing, GitHub y ML/GenAI (puntos 17–19)

| Requisito | Estado | Nota |
| --- | --- | --- |
| Landing con enlace a la tienda, aviso de privacidad y Google Analytics | ➖ | Equipo de Landing; el backend solo necesita sus URLs |
| Una sola cuenta de GitHub | ✅ | Organización `thehouse-team` |
| ML e IA generativa con las mismas prácticas | 🟡 | TheHouse-GenAI desplegado en `cae-thehouse-u2` (pendiente: `minReplicas = 0` y evidencia de costo); TheHouse-ML pendiente |

## 7. Entrega (punto 20)

| Requisito | Estado | Nota |
| --- | --- | --- |
| Proyectos en `.rar` | 🟡 | Se arma al final desde `main` (sin `bin/`, `obj/`, `.azure-check/` ni archivos de Terraform con state o variables) |
| **Script de la base de datos** | ❌ | Hoy las colecciones e índices se crean desde el código y los catálogos con `--seed-universities --seed-properties`. **Falta un script entregable** (ver §8) |

## 8. Brechas y propuestas

| Brecha | Riesgo en la evaluación | Propuesta |
| --- | --- | --- |
| Imágenes sin CDN de la nube | Medio: el PDF lo pide explícito | Opción A: documentar la decisión por COST-001 (caché HTTP `immutable` de un año que sirve como caché en el cliente y en proxies). Opción B: spec para Azure Blob Storage con CDN, si se demuestra costo $0 |
| Script de la base de datos | Alto: es parte de la entrega | Spec corta: un script de `mongosh` (`scripts/db/`) que cree las colecciones, todos los índices (incluidos TTL y únicos) y cargue los catálogos de universidades y propiedades; generado a partir de los repositorios para que coincida con el código |
| Dos instancias | Bajo: ventana agendada | 3 de diciembre con «OK PARA APPLY» (NV-03 T08/T09/T19) |
| ML (NV-23) y GenAI (NV-24) | Alto: el PDF pide ambas funciones | Seguir con los compañeros; conectar `parse-search` si ML se retrasa |
| Caché de datos del backend | Bajo | La caché HTTP cumple el punto 9; opcional: caché en Redis para catálogos |
| Seguridad de Atlas (usuario `atlasAdmin`) | Medio | En curso: usuario `thehouse-backend` con `readWrite` en `thehouse_u2` (NV-03 T07) |
| Trazas y métricas OTel | Bajo | NV-03 T11/T12 con decisión de exportador |

## 9. Guion de demostración del backend

1. **Nube y HTTPS:** la URL, `/health/ready` 200, `http://` → 301, certificado TLS 1.3.
2. **Dos réplicas** (3 de diciembre): `az containerapp replica list` con 2 Healthy y el balanceo del ingress.
3. **Cuenta:** registro → login (JWT y refresh) → `/users/me` → logout → el mismo token responde 401.
4. **Rendimiento:** catálogo con `ETag` → 304; respuesta comprimida; login repetido → 429 con `Retry-After`.
5. **Moderación:** un texto con un término prohibido → 422 `content_rejected`; una foto limpia se guarda.
6. **Push:** una solicitud de otra cuenta → llega el push al teléfono (con Mobile).
7. **Eliminar cuenta:** vista previa → eliminación → login 401.
8. **CI/CD y versionamiento:** un run del CI (7 checks) y del CD (firma, ZAP, aprobación, despliegue y DAST); `VERSIONING.md`.
9. **Infraestructura como código:** `terraform plan` sin cambios y `LIFECYCLE.md` (crear y destruir).
10. **SDD:** `specs/README.md`, una spec con su evidencia y `memoria.md` (decisiones M-BE).
11. **Costo cero:** Cost Management en 0.00 USD.
