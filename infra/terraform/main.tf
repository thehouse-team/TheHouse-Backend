locals {
  common_tags = merge(
    {
      application = "thehouse-backend"
      unit        = "u2"
      managed_by  = "terraform"
      environment = "verification"
    },
    var.tags
  )
}

resource "azurerm_resource_group" "this" {
  name     = var.resource_group_name
  location = var.location

  tags = local.common_tags
}

resource "azurerm_container_app_environment" "this" {
  name                = var.container_app_environment_name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  # Intentionally no Log Analytics workspace.
  #
  # AzureRM 5.x leaves logs in Streaming Only mode when
  # logs_destination is omitted. This avoids provisioning
  # an additional billable observability resource for U2.

  tags = local.common_tags
}

resource "azurerm_container_app" "this" {
  name                         = var.container_app_name
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = azurerm_resource_group.this.name
  workload_profile_name        = "Consumption"
  revision_mode                = "Single"

  # Los valores de secretos de Container Apps se administran fuera de
  # Terraform para impedir que credenciales MongoDB, Redis o JWT entren
  # en archivos .tf, tfvars o en el flujo normal de IaC.

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = "thehouse-backend"
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "ASPNETCORE_HTTP_PORTS"
        value = "8080"
      }

      env {
        name  = "Proxy__Enabled"
        value = "true"
      }

      env {
        name  = "Proxy__TrustAzureContainerAppsIngress"
        value = "true"
      }

      env {
        name  = "Proxy__ForwardLimit"
        value = "1"
      }

      env {
        name  = "Observability__ServiceVersion"
        value = var.service_version
      }

      env {
        name  = "Observability__OtlpEnabled"
        value = "false"
      }

      # Bootstrap U2: Terraform crea el recurso sin secretos.
      # Secretos y configuracion runtime final se administran
      # posteriormente mediante Azure Container Apps.
      env {
        name  = "MongoDB__Enabled"
        value = "false"
      }

      env {
        name  = "Redis__Enabled"
        value = "false"
      }

      env {
        name  = "Jwt__Enabled"
        value = "false"
      }

      startup_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/live"
        interval_seconds        = 3
        timeout                 = 2
        failure_count_threshold = 10
      }

      liveness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/live"
        initial_delay           = 15
        interval_seconds        = 30
        timeout                 = 5
        failure_count_threshold = 3
      }

      readiness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/ready"
        interval_seconds        = 10
        timeout                 = 5
        failure_count_threshold = 3
        success_count_threshold = 1
      }
    }
  }

  ingress {
    external_enabled           = true
    allow_insecure_connections = false
    target_port                = 8080
    transport                  = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  tags = local.common_tags

  lifecycle {
    # Secretos y variables runtime son administrados fuera de Terraform.
    # CREATE usa el bootstrap sin secretos; actualizaciones posteriores
    # no deben revertir la configuracion runtime validada en Azure.
    ignore_changes = [
      secret,
      template[0].container[0].env
    ]
    precondition {
      condition     = var.min_replicas <= var.max_replicas
      error_message = "min_replicas cannot be greater than max_replicas."
    }

    precondition {
      condition = (
        !var.evaluation_mode ||
        (
          var.min_replicas == 2 &&
          var.max_replicas == 2
        )
      )

      error_message = "U2 evaluation mode requires exactly two replicas."
    }

    precondition {
      condition = startswith(
        var.image,
        "ghcr.io/thehouse-team/thehouse-backend@sha256:"
      )

      error_message = "Mutable container image references are forbidden."
    }
  }
}
