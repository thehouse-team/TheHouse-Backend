# NV-03 1.3.0-u2 (T11/T12, §7.2): Application Insights basado en el workspace
# de Log Analytics existente. Sus datos cuentan en el mismo workspace (tope de
# 0.1 GB/día y 30 días de retención) y además tiene su propio tope, para que
# la telemetría nunca deje sin espacio a los logs de consola. Sin llaves: la
# autenticación local está desactivada y la API envía con su identidad
# administrada y el rol mínimo de publicación sobre este recurso.
resource "azurerm_application_insights" "this" {
  name                = var.application_insights_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  retention_in_days   = 30

  daily_data_cap_in_gb                 = var.application_insights_daily_cap_gb
  daily_data_cap_notifications_enabled = false
  local_authentication_enabled         = false
  ip_masking_enabled                   = true
  internet_ingestion_enabled           = true
  internet_query_enabled               = true

  tags = local.common_tags
}

resource "azurerm_role_assignment" "app_monitoring_publisher" {
  scope                = azurerm_application_insights.this.id
  role_definition_name = "Monitoring Metrics Publisher"
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
  description          = "NV-03 1.3.0-u2: la API envía telemetría con su identidad administrada."
}
