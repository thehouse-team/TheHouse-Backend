# Ciclo de vida de la infraestructura (NV-03 T20/T21)

Procedimiento repetible para **crear, verificar y destruir** infraestructura con el mismo Terraform de producción, sin tocar producción. Cada `apply` (crear y destruir) requiere «OK PARA APPLY».

## Por qué un workspace aparte

- `terraform workspace lifecycle` tiene su propio state (`terraform.tfstate.d/lifecycle/`, fuera de git): nunca ve los recursos de producción.
- `lifecycle.tfvars` cambia todos los nombres (`*-lifecycle`).
- La suscripción tiene dos límites que no se pueden ampliar en su oferta:
  - Un solo entorno de Container Apps (`MaxNumberOfRegionalEnvironmentsInSubExceeded`; la solicitud de cuota respondió `ResourceNotAvailableForOffer`).
  - Un solo Content Safety F0.

  Por eso el desechable crea su propia Container App **dentro** del entorno de producción, sin administrarlo (`existing_environment_id`), y desactiva Content Safety y la identidad del CD (`content_safety_enabled = false`, `cd_identity_enabled = false`). Así no otorga roles ni cambia nada compartido.

## Pasos

```powershell
cd infra/terraform
terraform workspace select lifecycle   # o: terraform workspace new lifecycle
$env_id = az containerapp env show -n cae-thehouse-u2 -g rg-thehouse-u2 --query id -o tsv

# 1. Crear (revisar el plan; esperar «OK PARA APPLY»)
terraform plan -var-file=lifecycle.tfvars -var "existing_environment_id=$env_id" -out=lifecycle.tfplan
terraform apply lifecycle.tfplan

# 2. Verificar
az resource list -g rg-thehouse-lifecycle -o table
az containerapp show -n ca-thehouse-lifecycle -g rg-thehouse-lifecycle --query properties.provisioningState

# 3. Destruir (revisar el plan; esperar «OK PARA APPLY»)
terraform plan -destroy -var-file=lifecycle.tfvars -var "existing_environment_id=$env_id" -out=lifecycle-destroy.tfplan
terraform apply lifecycle-destroy.tfplan

# 4. Comprobar que no queda nada
az group exists -n rg-thehouse-lifecycle      # false
terraform state list                           # vacío
terraform workspace select default
terraform plan                                 # producción: 0 to add, 0 to change, 0 to destroy
```

El id del entorno incluye el de la suscripción: se pasa con `-var` y no se guarda en el repositorio.
