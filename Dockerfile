# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS build

WORKDIR /src

COPY TheHouse/TheHouse.csproj TheHouse/
COPY TheHouse.Application/TheHouse.Application.csproj TheHouse.Application/
COPY TheHouse.Domain/TheHouse.Domain.csproj TheHouse.Domain/
COPY TheHouse.Infrastructure/TheHouse.Infrastructure.csproj TheHouse.Infrastructure/

# NV-23 1.3.0: solo linux-x64. Sin RID, ONNX Runtime y SkiaSharp publican sus
# librerías nativas de todas las plataformas (722 MB en lugar de 58 MB).
RUN dotnet restore TheHouse/TheHouse.csproj --runtime linux-x64

COPY . .

RUN dotnet publish TheHouse/TheHouse.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble AS runtime

USER root

# Aplica los parches de seguridad del sistema operativo de la imagen base
# (NV-02-R08: Trivy bloquea Critical/High con corrección disponible).
RUN apt-get update \
    && apt-get upgrade -y --no-install-recommends \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER app

HEALTHCHECK \
    --interval=30s \
    --timeout=5s \
    --start-period=15s \
    --retries=3 \
    CMD curl --fail --silent --show-error --max-time 3 \
        http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "TheHouse.dll"]
