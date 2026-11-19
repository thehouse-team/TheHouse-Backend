# NV-09 T01 (NV-09-R07): identidad del CD con OIDC y mínimo privilegio.
#
# GitHub Actions obtiene un token de Azure solo desde el Environment
# `production` del repositorio, sin client secret. El rol personalizado
# permite actualizar la imagen de la Container App y leer sus revisiones,
# pero no borrarla, ni leer sus secretos (`listSecrets`), ni tocar otros
# recursos del grupo.

data "azurerm_client_config" "current" {}

resource "azurerm_user_assigned_identity" "cd" {
  count = var.cd_identity_enabled ? 1 : 0

  name                = var.cd_identity_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name

  tags = local.common_tags
}

resource "azurerm_federated_identity_credential" "github_production" {
  count = var.cd_identity_enabled ? 1 : 0

  name                      = "github-${var.github_environment}"
  user_assigned_identity_id = azurerm_user_assigned_identity.cd[0].id
  issuer                    = "https://token.actions.githubusercontent.com"
  audience                  = ["api://AzureADTokenExchange"]
  # El repositorio usa el subject inmutable de GitHub OIDC
  # (`use_immutable_subject: true`): incluye los IDs numéricos de la
  # organización y del repositorio, así que un repositorio borrado y
  # recreado con el mismo nombre no hereda el acceso a Azure.
  subject = "${var.github_oidc_subject_prefix}:environment:${var.github_environment}"
}

resource "azurerm_role_definition" "cd_deployer" {
  count = var.cd_identity_enabled ? 1 : 0

  name        = "TheHouse CD Deployer (${var.resource_group_name})"
  scope       = azurerm_resource_group.this.id
  description = "NV-09: actualizar la imagen de la Container App y leer sus revisiones. Sin delete ni listSecrets."

  permissions {
    actions = [
      "Microsoft.App/containerApps/read",
      "Microsoft.App/containerApps/write",
      "Microsoft.App/containerApps/revisions/read",
      "Microsoft.App/containerApps/revisions/replicas/read",
      "Microsoft.App/managedEnvironments/read",
      "Microsoft.App/managedEnvironments/join/action",
      "Microsoft.App/locations/containerappOperationResults/read",
      "Microsoft.App/locations/containerappOperationStatuses/read",
    ]
    not_actions = []
  }

  assignable_scopes = [
    azurerm_resource_group.this.id,
  ]
}

# Actualizar la app exige `join` sobre su entorno: dos asignaciones con
# alcance a cada recurso, nunca al grupo completo ni a la suscripción.
resource "azurerm_role_assignment" "cd_container_app" {
  count = var.cd_identity_enabled ? 1 : 0

  scope              = azurerm_container_app.this.id
  role_definition_id = azurerm_role_definition.cd_deployer[0].role_definition_resource_id
  principal_id       = azurerm_user_assigned_identity.cd[0].principal_id
  principal_type     = "ServicePrincipal"
  description        = "NV-09: despliegue de imagen por digest desde GitHub Actions."
}

resource "azurerm_role_assignment" "cd_environment" {
  count = var.cd_identity_enabled ? 1 : 0

  scope              = local.environment_id
  role_definition_id = azurerm_role_definition.cd_deployer[0].role_definition_resource_id
  principal_id       = azurerm_user_assigned_identity.cd[0].principal_id
  principal_type     = "ServicePrincipal"
  description        = "NV-09: join al entorno requerido al actualizar la app."
}

# El estado de producción conserva sus recursos (sin destruir ni recrear).
moved {
  from = azurerm_user_assigned_identity.cd
  to   = azurerm_user_assigned_identity.cd[0]
}

moved {
  from = azurerm_federated_identity_credential.github_production
  to   = azurerm_federated_identity_credential.github_production[0]
}

moved {
  from = azurerm_role_definition.cd_deployer
  to   = azurerm_role_definition.cd_deployer[0]
}

moved {
  from = azurerm_role_assignment.cd_container_app
  to   = azurerm_role_assignment.cd_container_app[0]
}

moved {
  from = azurerm_role_assignment.cd_environment
  to   = azurerm_role_assignment.cd_environment[0]
}
