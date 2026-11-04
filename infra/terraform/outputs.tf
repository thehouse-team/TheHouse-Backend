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
  value       = "https://${azurerm_container_app.this.latest_revision_fqdn}"
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
