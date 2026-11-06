#!/usr/bin/env bash
# NV-09 T09/T10: entorno efímero en el runner.
#
# Ejecuta la imagen en ASPNETCORE_ENVIRONMENT=Testing contra MongoDB y Redis
# levantados como `services` del job (expuestos en 127.0.0.1). La llave JWT
# llega por la variable de entorno JWT_SIGNING_KEY (aleatoria por run) y se
# pasa a Docker por nombre, sin escribirla en la línea de comandos.
#
# Uso:
#   ephemeral_env.sh start IMAGEN [smoke|scan]
#   ephemeral_env.sh seed  IMAGEN
#   ephemeral_env.sh logs
#   ephemeral_env.sh stop
set -euo pipefail

NAME="thehouse-ephemeral"
cmd="${1:?comando requerido}"

common_env() {
  : "${JWT_SIGNING_KEY:?JWT_SIGNING_KEY requerido}"
  export JWT_SIGNING_KEY
  ENV_ARGS=(
    -e ASPNETCORE_ENVIRONMENT=Testing
    -e ASPNETCORE_HTTP_PORTS=8080
    -e MongoDB__Enabled=true
    -e MongoDB__ConnectionString=mongodb://127.0.0.1:27017
    -e MongoDB__Database=thehouse_ephemeral
    -e Redis__Enabled=true
    -e Redis__ConnectionString=127.0.0.1:6379
    -e Jwt__Enabled=true
    -e JWT_SIGNING_KEY
  )
}

case "$cmd" in
  start)
    image="${2:?imagen requerida}"
    profile="${3:-smoke}"
    common_env
    extra=()
    if [ "$profile" = "scan" ]; then
      # Solo para el escaneo activo: límites globales holgados para que ZAP
      # cubra la API en lugar de recibir 429 (los límites por endpoint de
      # propiedades y universidades son fijos y siguen aplicando).
      extra=(
        -e Foundation__RateLimitPermitLimit=10000
        -e LoginRateLimit__LoginEmailPerWindow=1000
        -e LoginRateLimit__LoginIpPerWindow=1000
      )
    fi
    docker rm -f "$NAME" >/dev/null 2>&1 || true
    docker run -d --name "$NAME" --network host "${ENV_ARGS[@]}" "${extra[@]}" "$image" >/dev/null
    for i in $(seq 1 60); do
      if curl -fsS --max-time 2 http://127.0.0.1:8080/health/ready >/dev/null 2>&1; then
        echo "API efímera lista ($profile) tras ${i}s."
        exit 0
      fi
      if [ "$(docker inspect -f '{{.State.Running}}' "$NAME")" != "true" ]; then
        echo "::error::El contenedor terminó antes de estar listo."
        docker logs --tail 80 "$NAME" || true
        exit 1
      fi
      sleep 1
    done
    echo "::error::La API efímera no respondió /health/ready en 60s."
    docker logs --tail 80 "$NAME" || true
    exit 1
    ;;
  seed)
    image="${2:?imagen requerida}"
    common_env
    docker run --rm --network host "${ENV_ARGS[@]}" \
      -e UniversitySeed__Enabled=true \
      -e PropertySeed__Enabled=true \
      "$image" --seed-universities --seed-properties
    ;;
  logs)
    docker logs --tail 120 "$NAME" || true
    ;;
  stop)
    docker rm -f "$NAME" >/dev/null 2>&1 || true
    ;;
  *)
    echo "Comando desconocido: $cmd" >&2
    exit 2
    ;;
esac
