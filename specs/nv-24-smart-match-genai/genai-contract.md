# NV-24 — Contrato con TheHouse-GenAI (referencia)

| Campo | Valor |
| --- | --- |
| Fuente | Guía de integración del equipo de GenAI, 2026-11-18 (contrato congelado) |
| Servicio | `TheHouse-GenAI`, repositorio aparte; solo lo llama el backend, nunca la app |
| Usado por | NV-24 1.2.0 (`explain-match`); `parse-search` queda para una spec futura |
| Credencial | Encabezado `X-Service-Key`; en el backend, secreto de Container Apps `genai-service-key` como `GenAI__ServiceKey`. Se entrega por canal privado: **nunca** en el repositorio, en chats ni en `appsettings.json` |

## 1. Reglas comunes

| Regla | Valor |
| --- | --- |
| Encabezados | `X-Service-Key` (obligatorio), `Content-Type: application/json`, `X-Request-ID` (correlación; GenAI lo devuelve y lo registra) |
| JSON | camelCase; números como números; un campo extra → 422 |
| Timeout del cliente | 8 s (GenAI corta su llamada a la IA a ~6 s y responde con plantilla) |
| Reintentos | Uno, solo ante error de red o 5xx; nunca ante 4xx ni timeout |
| Límite | 60 solicitudes por minuto → 429 con `Retry-After` |
| Cuerpo máximo | 64 KB → 413 |
| Salud | `GET /health/live` y `GET /health/ready`, sin clave |
| Errores | `{ "error": { "code", "message", "request_id" } }`: 401 `UNAUTHORIZED`, 413 `PAYLOAD_TOO_LARGE`, 422 `VALIDATION_ERROR`, 429 `TOO_MANY_REQUESTS`, 500 `INTERNAL_ERROR` |

Si GenAI falla por cualquier motivo, el backend sigue funcionando: Smart Match responde con la explicación por plantilla (`generatedBy: "template"`).

## 2. `POST /api/v1/generative/explain-match`

Request (sin campos extra):

```json
{
  "matchScore": 91,
  "locale": "es-MX",
  "university": "UTTT",
  "property": { "title": "Habitación cerca de UTTT", "price": 2800, "currency": "MXN", "distanceKm": 1.3, "services": ["internet", "baño privado"] },
  "preferences": { "budgetMax": 3000, "currency": "MXN", "maxDistanceKm": 3, "essentialServices": ["internet"] }
}
```

| Campo | Reglas |
| --- | --- |
| `matchScore` | Entero 0–100, calculado por TheHouse-ML (NV-23) |
| `university` | Texto ≤ 120 (nombre corto, por ejemplo `UTTT`) |
| `property.title` | ≤ 120; título público del cuarto |
| `property.price`, `preferences.budgetMax` | ≥ 0, máximo 2 decimales |
| `property.currency`, `preferences.currency` | `MXN`, iguales entre sí (si no, 422) |
| `property.distanceKm`, `preferences.maxDistanceKm` | ≥ 0 |
| `property.services`, `preferences.essentialServices` | Hasta 20, cada uno ≤ 40 caracteres, en español |

Nunca se envían: id del cuarto, dirección, coordenadas, datos del arrendador o del estudiante, descripción libre, tokens ni contraseñas.

Response 200: `{ "explanation": "...", "generated": true, "promptVersion": "explain_match_v3" }`. Con `generated: false` el texto es la plantilla de GenAI; el backend usa **su propia** plantilla para que `pros` y `cons` y el resumen sean coherentes.

### Mapeo de servicios del backend

| Backend (`PreferenceCatalogs`) | Texto para GenAI |
| --- | --- |
| `wifi` | internet |
| `water` | agua |
| `electricity` | luz |
| `gas` | gas |
| `parking` | estacionamiento |
| `laundry` | lavadora |
| `furnished` | amueblado |
| `hot_water` | agua caliente |
| `kitchen` | cocina |
| `security` | seguridad |
| `cleaning` | limpieza |
| Cuarto con baño propio (`bathroom = private`, NV-12) | baño privado |

## 3. `POST /api/v1/generative/parse-search` (spec futura)

Convierte una frase libre en filtros `{ maxBudget, currency, maxDistanceKm, university, essentialServices, petsAllowed }` con `confidence` y `generated`; nunca devuelve propiedades. Mobile acordó dejar la búsqueda en lenguaje natural fuera de esta etapa, así que el backend no la integra todavía. Respuestas a las preguntas abiertas del equipo de GenAI, para cuando se haga esa spec:

| Pregunta | Respuesta del backend |
| --- | --- |
| «no acepto mascotas» o «sin mascotas»: ¿qué significa el filtro? | `null` está bien. Hoy los cuartos de NV-12 **no guardan** si aceptan mascotas, así que el backend ignorará `petsAllowed` hasta que exista ese dato |
| `maxDistanceKm`: ¿desde la universidad mencionada o la del perfil? | Desde la universidad mencionada en la frase si existe en el catálogo de NV-08; si no se menciona o no existe, desde la universidad del perfil del estudiante (NV-11); sin ninguna, no se aplica la distancia |
| ¿Los servicios usan los mismos nombres? | No: el backend usa códigos en inglés. El backend mapea (tabla de la §2, en sentido inverso); «baño privado» se convierte en el filtro de cuarto con baño propio, no en un servicio |

El backend siempre valida universidad y servicios contra sus catálogos; con `confidence: low` o `generated: false` usa la búsqueda normal.
