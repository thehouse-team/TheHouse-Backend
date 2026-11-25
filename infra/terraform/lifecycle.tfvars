# NV-03 T20/T21: entorno DESECHABLE para demostrar el ciclo de vida
# (crear → verificar → destruir) con el mismo Terraform de producción.
# Se usa solo en el workspace `lifecycle`, que tiene su propio state.
# Todos los nombres son distintos de producción; no contiene secretos.
#
# La suscripción admite un solo entorno de Container Apps (la cuota no se
# puede ampliar en esta oferta: ResourceNotAvailableForOffer), así que la
# app desechable se crea dentro del entorno de producción SIN administrarlo:
# Terraform no lo crea, no lo cambia y no lo destruye. Su id (que incluye el
# de la suscripción) se pasa al planear y no se guarda en el repositorio:
#
#   terraform workspace select lifecycle
#   $env_id = az containerapp env show -n cae-thehouse-u2 -g rg-thehouse-u2 --query id -o tsv
#   terraform plan  -var-file=lifecycle.tfvars -var "existing_environment_id=$env_id" -out=lifecycle.tfplan
#   terraform apply lifecycle.tfplan              (con «OK PARA APPLY»)
#   terraform plan  -destroy -var-file=lifecycle.tfvars -var "existing_environment_id=$env_id" -out=lifecycle-destroy.tfplan
#   terraform apply lifecycle-destroy.tfplan      (con «OK PARA APPLY»)

location                       = "eastus2"
resource_group_name            = "rg-thehouse-lifecycle"
container_app_environment_name = "cae-thehouse-lifecycle"
container_app_name             = "ca-thehouse-lifecycle"
log_analytics_workspace_name   = "log-thehouse-lifecycle"
cd_identity_name               = "id-thehouse-lifecycle-deploy"
app_identity_name              = "id-thehouse-lifecycle-app"
content_safety_name            = "cs-thehouse-lifecycle"
application_insights_name      = "appi-thehouse-lifecycle"

# Un solo F0 de Content Safety por suscripción: el de producción.
content_safety_enabled = false

# Sin identidad del CD: el desechable no otorga roles sobre recursos compartidos.
cd_identity_enabled = false

# Nadie usa este entorno: escala a cero y sin modo de evaluación.
evaluation_mode = false
min_replicas    = 0
max_replicas    = 1

tags = {
  purpose = "nv-03-lifecycle-test"
  delete  = "after-verification"
}
