# NV-03 — Pre-Apply Gate

Fecha: 2026-11-04

## Regla

terraform apply está prohibido hasta que todos los controles de este documento estén en PASS y exista autorización humana explícita:

OK PARA APPLY

## Estado actual

T15 Terraform estáticamente validado: PASS

Terraform 1.17.1: PASS

AzureRM 5.12.0 fijado en lockfile: PASS

terraform validate: PASS

terraform fmt -check: PASS

Escaneo de secretos fuente: PASS

Imagen OCI por digest: PASS

Publicación GHCR real: PASS

Pull anónimo GHCR: FAIL — HTTP 401

COST-001 demostrado: PENDING

Terraform plan revisado: PENDING

Suscripción Azure verificada: PENDING

Autorización OK PARA APPLY: MISSING

## Resultado

GATE: BLOCKED

No ejecutar terraform apply.
