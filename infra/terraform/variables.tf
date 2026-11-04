variable "location" {
  description = "Azure region for the U2 Container Apps deployment."
  type        = string
  default     = "eastus2"
}

variable "resource_group_name" {
  description = "Resource group dedicated to TheHouse U2."
  type        = string
  default     = "rg-thehouse-u2"
}

variable "container_app_environment_name" {
  description = "Azure Container Apps environment name."
  type        = string
  default     = "cae-thehouse-u2"
}

variable "container_app_name" {
  description = "Azure Container App name."
  type        = string
  default     = "ca-thehouse-u2"
}

variable "image" {
  description = "Immutable OCI image reference. Digest is mandatory."
  type        = string

  default = "ghcr.io/thehouse-team/thehouse-backend@sha256:15f196af9c32fc843cf1723b4615f896668b0368b292e1246807ab0d03194775"

  validation {
    condition = startswith(
      var.image,
      "ghcr.io/thehouse-team/thehouse-backend@sha256:"
    )

    error_message = "image must use the immutable TheHouse GHCR sha256 digest."
  }
}

variable "service_version" {
  description = "Version reported by OpenTelemetry."
  type        = string
  default     = "sha-c6518862788407fe678c1a524ad4d4b764dc6a71"
}

variable "min_replicas" {
  description = "Minimum replicas during U2 evaluation."
  type        = number
  default     = 2

  validation {
    condition     = var.min_replicas >= 0 && var.min_replicas <= 10
    error_message = "min_replicas must be between 0 and 10."
  }
}

variable "max_replicas" {
  description = "Maximum replicas during U2 evaluation."
  type        = number
  default     = 2

  validation {
    condition     = var.max_replicas >= 1 && var.max_replicas <= 10
    error_message = "max_replicas must be between 1 and 10."
  }
}

variable "cpu" {
  description = "vCPU assigned to each replica."
  type        = number
  default     = 0.25

  validation {
    condition     = var.cpu == 0.25
    error_message = "U2 COST-001 requires 0.25 vCPU per replica."
  }
}

variable "memory" {
  description = "Memory assigned to each replica."
  type        = string
  default     = "0.5Gi"

  validation {
    condition     = var.memory == "0.5Gi"
    error_message = "U2 COST-001 requires 0.5Gi memory per replica."
  }
}

variable "evaluation_mode" {
  description = "Enforces exactly two replicas for the U2 evaluation."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Additional Azure tags."
  type        = map(string)
  default     = {}
}
