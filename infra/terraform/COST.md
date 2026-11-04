# COST-001 — TheHouse NV-03 / U2

Fecha de revisión: 2026-11-04

## Objetivo

Demostrar que el despliegue de verificación NV-03 puede permanecer en costo efectivo de $0 durante su ventana controlada.

## Arquitectura evaluada

Azure Container Apps con perfil Consumption.

Configuración por réplica:

- 0.25 vCPU
- 0.5 GiB RAM

Configuración de evaluación:

- min_replicas = 2
- max_replicas = 2

No se aprovisionan:

- Azure Container Registry
- Log Analytics Workspace
- Application Insights
- VNet personalizada
- NAT Gateway
- IP pública dedicada
- dominio personalizado
- Key Vault

Los logs del Container Apps Environment permanecen en Streaming Only.

## Concesión gratuita de Azure Container Apps

Concesión mensual por suscripción:

- 180000 vCPU-seconds
- 360000 GiB-seconds
- 2000000 requests HTTP

Los health probes no cuentan como requests facturables.

## Cálculo conservador para dos réplicas

CPU total:

2 replicas * 0.25 vCPU = 0.5 vCPU

180000 / 0.5 = 360000 segundos

360000 / 3600 = 100 horas

Memoria total:

2 replicas * 0.5 GiB = 1 GiB

360000 / 1 = 360000 segundos

360000 / 3600 = 100 horas

Ventana teórica conservadora máxima si la concesión mensual está completamente disponible:

100 horas

Este cálculo no sustituye la comprobación del consumo real de la suscripción.

## GHCR

Imagen:

ghcr.io/thehouse-team/thehouse-backend@sha256:15f196af9c32fc843cf1723b4615f896668b0368b292e1246807ab0d03194775

Commit asociado:

c6518862788407fe678c1a524ad4d4b764dc6a71

El almacenamiento y ancho de banda de Container Registry se documentan actualmente como gratuitos.

Estado de acceso:

BLOCKED

La prueba de pull anónimo devolvió HTTP 401.

Mientras el paquete permanezca privado, Azure Container Apps necesitaría autenticación para GHCR.

No se permite introducir PAT, GITHUB_TOKEN ni otras credenciales persistentes en Terraform, terraform.tfvars o Terraform state.

## Dependencias externas

MongoDB, Redis y JWT permanecen desactivados en el bootstrap Terraform.

No se crean recursos de MongoDB ni Redis mediante Terraform.

## Riesgos COST-001 pendientes

Debe verificarse antes de apply:

1. Disponibilidad real de la concesión gratuita mensual de la suscripción Azure.
2. Resultado completo de terraform plan.
3. Ausencia de recursos Azure inesperados.
4. Acceso a la imagen OCI sin introducir secretos en Terraform.
5. Ventana de despliegue controlada.
6. Destrucción posterior autorizada y verificada.
7. Evidencia de costo observado.

## Estado

COST-001: BLOCKED

No existe autorización para terraform apply.
