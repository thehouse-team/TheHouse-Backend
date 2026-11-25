# Guion de demostración del backend — evaluación del 3 y 4 de diciembre de 2026

Guion paso a paso para mostrar el backend de TheHouse en vivo. Cada paso dice **qué se muestra**, **el comando** (PowerShell en Windows) y **qué debe verse**, y a qué punto del PDF del curso responde. La matriz completa está en `docs/matriz-requisitos-curso.md`.

Duración estimada: 20–25 minutos. Los pasos marcados con ⏱ se pueden saltar si falta tiempo.

## 0. Preparación (antes de la evaluación)

```powershell
$U = "https://ca-thehouse-u2.grayisland-cb722b5f.eastus2.azurecontainerapps.io"
# La app escala a cero: despertarla unos minutos antes.
curl.exe -s -o NUL -w "%{http_code}`n" "$U/health/ready"
$uttt = (Invoke-RestMethod "$U/api/v1/universities/search?q=uttt").items | Where-Object code -eq "UTTT" | Select-Object -ExpandProperty id
$email = "demo-$(Get-Random)@example.com"; $pass = "Demo-Evaluacion-2026!"
```

- Abrir en pestañas: GitHub Actions (último CI y CD), el Portal de Azure (`rg-thehouse-u2`), Cost Management y `specs/README.md`.
- Tener el teléfono con la app de Mobile para el paso 7.

## 1. Nube, HTTPS y salud (puntos 3 y 13)

```powershell
curl.exe -s -i "$U/health/ready" | Select-Object -First 1
curl.exe -s -o NUL -w "%{http_code} -> %{redirect_url}`n" ("http://" + $U.Substring(8) + "/health/live")
```

Debe verse: `HTTP/1.1 200` y la redirección `301` de `http://` a `https://`. Mencionar: Azure Container Apps (eastus2), MongoDB Atlas M0 y Upstash Redis.

## 2. Dos instancias y balanceo (puntos 13 y 15) — solo el 3 de diciembre

```powershell
az containerapp replica list -n ca-thehouse-u2 -g rg-thehouse-u2 --query "[].{name:name, state:properties.runningState}" -o table
```

Debe verse: 2 réplicas `Running` (ventana de NV-03 T08/T09/T18/T19 con «OK PARA APPLY»). Explicar: sin estado en memoria para la seguridad (JWT, revocación y límites en Redis), probes `/health/live` y `/health/ready`, sin sesiones fijas.

## 3. Cuenta: registro, sesión y revocación (punto 15)

```powershell
Invoke-RestMethod -Method Post "$U/api/v1/auth/register" -ContentType "application/json" -Body (@{ email = $email; password = $pass } | ConvertTo-Json)
$login = Invoke-RestMethod -Method Post "$U/api/v1/auth/login" -ContentType "application/json" -Body (@{ email = $email; password = $pass } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)" }
Invoke-RestMethod "$U/api/v1/users/me" -Headers $h
Invoke-RestMethod -Method Post "$U/api/v1/auth/logout" -Headers $h
curl.exe -s -o NUL -w "%{http_code}`n" -H "Authorization: Bearer $($login.accessToken)" "$U/api/v1/users/me"
```

Debe verse: 201, el JWT y el refresh, el perfil y **401** con el mismo token después del logout (revocación en Redis, NV-06).

## 4. Búsqueda y Smart Match con ML (puntos 14 y 16)

```powershell
$login = Invoke-RestMethod -Method Post "$U/api/v1/auth/login" -ContentType "application/json" -Body (@{ email = $email; password = $pass } | ConvertTo-Json)
$h = @{ Authorization = "Bearer $($login.accessToken)" }
(Invoke-RestMethod "$U/api/v1/rooms/search?universityId=$uttt&maxDistanceKm=30&pageSize=5").items | Select-Object title, price, distanceKm
$prefs = @{ budget = @{ min = 1000; max = 10000 }; maxDistanceKm = 30; essentialServices = @("wifi","furnished"); livingWith = "any"; placeType = "room" }
$body = @{ universityId = $uttt; preferences = $prefs } | ConvertTo-Json -Depth 5
(Invoke-RestMethod -Method Post "$U/api/v1/smart-match/recommendations/query?pageSize=5" -ContentType "application/json" -Body $body).items | Select-Object @{n="cuarto";e={$_.room.title}}, matchScore, modelVersion
```

Debe verse: cuartos cerca de la UTTT con distancia aproximada (nunca la dirección) y las recomendaciones ordenadas por `matchScore` con `baseline-1.0.0`. Explicar: es el modelo base de **TheHouse-ML (ML-04)**, el mismo cálculo que su Python (paridad bit a bit en 2,000 casos, NV-23); el desglose por factor y la regla de convivencia estricta.

```powershell
$room = (Invoke-RestMethod "$U/api/v1/rooms/search?universityId=$uttt&maxDistanceKm=30&pageSize=1").items[0].id
Invoke-RestMethod -Method Post "$U/api/v1/smart-match/scores" -ContentType "application/json" -Body (@{ roomIds = @($room); universityId = $uttt; preferences = $prefs } | ConvertTo-Json -Depth 5) | ConvertTo-Json -Depth 6
```

Debe verse: `matchScore` y el `breakdown` de 6 factores (presupuesto, distancia, servicios, tipo de lugar, tipo de inmueble y extras).

## 5. Explicación con IA generativa (punto 16)

```powershell
Invoke-RestMethod -Method Post "$U/api/v1/smart-match/explanations/query" -ContentType "application/json" -Body (@{ roomId = $room; universityId = $uttt; preferences = $prefs } | ConvertTo-Json -Depth 5) | ConvertTo-Json -Depth 4
```

Debe verse: `summary`, ventajas, desventajas y `generatedBy`. Con TheHouse-GenAI respondiendo es `genai`; si GenAI está apagado o devuelve plantilla, `template` (RNF-16: la app nunca falla por la IA). Explicar: el backend solo manda hechos públicos, valida el texto de la IA y usa caché de 24 h (NV-24). ⏱ Si GenAI tarda al despertar, repetir el comando a los 30 s.

## 6. Rendimiento, caché y límites (puntos 9 y 15)

```powershell
$e = (curl.exe -s -i "$U/api/v1/universities?page=1&pageSize=10" | Select-String -Pattern '^ETag:').ToString().Split(' ',2)[1].Trim()
curl.exe -s -o NUL -w "%{http_code}`n" -H "If-None-Match: $e" "$U/api/v1/universities?page=1&pageSize=10"
$otro = "intruso-$(Get-Random)@example.com"   # otro correo: no bloquea la cuenta de la demo
1..6 | ForEach-Object { curl.exe -s -o NUL -w "%{http_code} " -X POST "$U/api/v1/auth/login" -H "Content-Type: application/json" -d "{\`"email\`":\`"$otro\`",\`"password\`":\`"incorrecta\`"}" }
```

Debe verse: **304** con el `ETag` (caché HTTP) y, al repetir el login fallido, `401 401 401 401 401 429` (límite de 5 por correo por minuto en Redis, con `Retry-After`). Mencionar: fotos con `Cache-Control: public, max-age=31536000, immutable` como alternativa al CDN por costo cero (M-BE-084).

## 7. Push al teléfono (punto 16) ⏱ con Mobile

Con la app del teléfono abierta en otra cuenta: enviar una solicitud o un mensaje desde la app → debe llegar la notificación push (FCM, NV-20).

## 8. Moderación de contenido (punto 15) ⏱

```powershell
Invoke-RestMethod -Method Post "$U/api/v1/landlord/profile" -Headers $h -ContentType "application/json" -Body (@{ fullName = "Eres un pendejo"; phone = "+527731234567"; rentalMode = "individual"; acceptedTermsVersion = "2026-11" } | ConvertTo-Json)
```

Debe verse: **422** `content_rejected` con el campo y la categoría (Azure AI Content Safety y lista propia, NV-21). No se guarda nada.

## 9. Eliminar la cuenta (punto 16, privacidad)

```powershell
Invoke-RestMethod "$U/api/v1/users/me/deletion" -Headers $h
Invoke-RestMethod -Method Post "$U/api/v1/users/me/deletion" -Headers $h -ContentType "application/json" -Body (@{ password = $pass; confirmation = "ELIMINAR" } | ConvertTo-Json)
curl.exe -s -o NUL -w "%{http_code}`n" -X POST "$U/api/v1/auth/login" -H "Content-Type: application/json" -d "{\`"email\`":\`"$email\`",\`"password\`":\`"$pass\`"}"
```

Debe verse: la vista previa de lo que se borra, **204** y **401** al intentar entrar otra vez (NV-10; también borra los datos de Smart Match).

## 10. Base de datos reproducible (puntos 14 y 20)

Mostrar `scripts/db/README.md` y, si hay tiempo, correr el script en un MongoDB local:

```powershell
docker run -d --name thehouse-db -p 127.0.0.1:27017:27017 mongo:7.0.45
docker cp scripts/db thehouse-db:/db
docker exec thehouse-db mongosh --quiet --file /db/01-schema.mongodb.js | Select-Object -Last 1
docker exec thehouse-db mongosh --quiet --file /db/02-catalogs.mongodb.js
docker rm -f thehouse-db
```

Debe verse: «Esquema listo … 35 colecciones, 72 índices y 2 buckets de GridFS» y los catálogos (10 universidades, 12 propiedades, 60 lugares). Explicar: el script se genera desde el código y una prueba del CI falla si no coinciden (NV-26).

## 11. CI/CD, seguridad y versionamiento (puntos 18 y 19)

En GitHub Actions:
- **CI:** build con advertencias como error, ~1,170 pruebas, Semgrep, SCA/SBOM, Gitleaks, Trivy y Terraform.
- **CD:** imagen por digest firmada con Cosign, verificación en un entorno efímero, ZAP, aprobación humana del Environment `production` (sin auto-aprobación), despliegue, smoke y rollback probado; omite despliegues de solo documentación comparando con el último commit desplegado (NV-09-R17).
- `VERSIONING.md`.

## 12. Infraestructura como código (punto 13)

```powershell
cd infra/terraform
terraform plan
```

Debe verse: `No changes` (producción igual al código). Mostrar `LIFECYCLE.md`: crear (4 recursos), verificar y destruir (4) con el mismo Terraform, demostrado el 2026-11-19.

## 13. SDD y documentación (punto 18)

Mostrar `specs/README.md` (26 specs, avance), una spec con su `evidence.md` (por ejemplo NV-23) y `memoria.md` (decisiones M-BE aprobadas por el responsable).

## 14. Costo cero (COST-001)

Cost Management de `rg-thehouse-u2`: 0.00 USD por día. Mencionar: Container Apps escala a cero, Log Analytics con tope de ingesta, Atlas M0, Upstash gratuito, Content Safety F0 y Gemini en nivel gratuito sin facturación.

## Si algo falla en vivo

| Síntoma | Qué hacer |
| --- | --- |
| La primera llamada tarda o da 503 | La app estaba en cero: esperar 20–30 s y repetir |
| `generatedBy: template` | Es el comportamiento seguro (RNF-16); explicarlo y seguir |
| 429 inesperado | Esperar el `Retry-After` (60 s) o usar otro correo |
| `terraform plan` pide credenciales | `az login` antes de la demo |
