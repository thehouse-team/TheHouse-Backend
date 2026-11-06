output "resource_group_name" {
  description = "Azure resource group."
  value       = azurerm_resource_group.this.name
}

output "container_app_environment_name" {
  description = "Container Apps environment."
  value       = azurerm_container_app_environment.this.name
}

output "container_app_name" {
  description = "Container App name."
  value       = azurerm_container_app.this.name
}

output "container_app_fqdn" {
  description = "Latest Container App revision FQDN."
  value       = azurerm_container_app.this.latest_revision_fqdn
}

output "container_app_url" {
  description = "Public HTTPS endpoint."
  value       = "https://${azurerm_container_app.this.ingress[0].fqdn}"
}

output "image" {
  description = "Immutable image deployed by Terraform."
  value       = var.image
}

output "min_replicas" {
  value = var.min_replicas
}

output "max_replicas" {
  value = var.max_replicas
}

output "estimated_max_free_window_hours" {
  description = "Theoretical maximum at two 0.25 vCPU / 0.5Gi replicas if the monthly Container Apps free grant is entirely unused."
  value       = 100
}

output "log_analytics_workspace_name" {
  description = "Log Analytics workspace receiving Container Apps logs."
  value       = azurerm_log_analytics_workspace.this.name
}

# NV-09 T03: valores no sensibles para las variables del Environment
# `production` de GitHub (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID).
output "cd_azure_client_id" {
  description = "Client ID of the CD managed identity (not a secret)."
  value       = azurerm_user_assigned_identity.cd.client_id
}

output "cd_azure_tenant_id" {
  description = "Tenant ID for azure/login (not a secret)."
  value       = data.azurerm_client_config.current.tenant_id
}

output "cd_azure_subscription_id" {
  description = "Subscription ID for azure/login (not a secret)."
  value       = data.azurerm_client_config.current.subscription_id
}
