# NV-21 1.2.0: Azure AI Content Safety para revisar texto e imágenes antes
# de guardarlos. Plan gratuito F0 (sin cobro: al llegar al cupo se detiene),
# sin llaves: la autenticación local está desactivada y la Container App usa
# su identidad administrada con el rol mínimo sobre este recurso.
resource "azurerm_cognitive_account" "content_safety" {
  # NV-03 T20: Azure permite un solo F0 por suscripción; el entorno
  # desechable del ciclo de vida lo desactiva (producción siempre lo tiene).
  count = var.content_safety_enabled ? 1 : 0

  name                  = var.content_safety_name
  location              = azurerm_resource_group.this.location
  resource_group_name   = azurerm_resource_group.this.name
  kind                  = "ContentSafety"
  sku_name              = var.content_safety_sku
  custom_subdomain_name = var.content_safety_name
  local_auth_enabled    = false

  tags = local.common_tags
}

# Identidad de la app (no la del CD): solo puede usar este recurso.
resource "azurerm_user_assigned_identity" "app" {
  name                = var.app_identity_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name

  tags = local.common_tags
}

resource "azurerm_role_assignment" "app_content_safety" {
  count = var.content_safety_enabled ? 1 : 0

  scope                = azurerm_cognitive_account.content_safety[0].id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
  description          = "NV-21: la API revisa contenido con su identidad administrada."
}

# El estado de producción conserva sus recursos (sin destruir ni recrear).
moved {
  from = azurerm_cognitive_account.content_safety
  to   = azurerm_cognitive_account.content_safety[0]
}

moved {
  from = azurerm_role_assignment.app_content_safety
  to   = azurerm_role_assignment.app_content_safety[0]
}
