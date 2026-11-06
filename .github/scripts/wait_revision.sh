#!/usr/bin/env bash
# NV-09 T11/T13: espera a que una revisión de la Container App quede
# aprovisionada y sea la revisión lista más reciente (la que recibe el 100 %
# del tráfico en revision_mode Single). Sin esto, el smoke podría estar
# probando la revisión anterior.
#
# Uso: wait_revision.sh NOMBRE_REVISION
# Requiere CONTAINER_APP y RESOURCE_GROUP en el entorno y una sesión de az.
set -euo pipefail

revision="${1:?revisión requerida}"
: "${CONTAINER_APP:?}" "${RESOURCE_GROUP:?}"

for i in $(seq 1 72); do
  state="$(az containerapp revision show -n "$CONTAINER_APP" -g "$RESOURCE_GROUP" --revision "$revision" \
    --query 'properties.provisioningState' -o tsv 2>/dev/null || echo Pending)"
  latest="$(az containerapp show -n "$CONTAINER_APP" -g "$RESOURCE_GROUP" \
    --query 'properties.latestReadyRevisionName' -o tsv 2>/dev/null || echo "")"
  echo "Intento $i: provisioningState=$state latestReadyRevisionName=$latest"
  if [ "$state" = "Failed" ]; then
    echo "::error::La revisión $revision falló al aprovisionarse."
    exit 1
  fi
  if [ "$state" = "Provisioned" ] && [ "$latest" = "$revision" ]; then
    echo "Revisión $revision lista y activa."
    exit 0
  fi
  sleep 5
done

echo "::error::Tiempo agotado (6 min) esperando que $revision quede activa."
exit 1
