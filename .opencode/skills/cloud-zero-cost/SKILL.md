---
name: cloud-zero-cost
description: Verifica COST-001, matriz AWS/Azure/GCP, dos instancias, Load Balancer, TLS, Terraform, creación y destrucción sin costo.
---

# Cloud Zero Cost

## Nombre

cloud-zero-cost

## Objetivo

Comprobar que toda arquitectura cloud académica pueda operar con costo obligatorio $0, incluidos dos instancias, Load Balancer, TLS, observabilidad y destrucción verificable, sin seleccionar un proveedor antes de la matriz.

## Cuándo utilizarlo

- Al revisar NV-03 o cualquier recurso cloud.
- Al comparar AWS, Azure y Google Cloud.
- Al evaluar free tiers, créditos, cuotas o riesgo de cobro.
- Al planificar Terraform, creación, operación y destrucción.
- Al revisar observabilidad, storage, Redis o conectividad MongoDB.

## Entradas

- Spec, plan y task autorizados.
- Matriz de proveedores y servicios propuestos.
- Evidencia vigente de precios, cuotas, regiones y elegibilidad.
- Requisitos de dos instancias, Load Balancer y TLS.
- Inventario de recursos y plan de destrucción.
- Restricciones de tarjeta y riesgo de cobro.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Enumerar todos los recursos del ciclo de vida, incluidos egress, logs, almacenamiento y registro.
3. Construir o revisar la matriz AWS/Azure/GCP con: compute, dos instancias, Load Balancer, HTTPS, container hosting, observabilidad, storage/CDN, Redis, conectividad MongoDB, Terraform, requiere tarjeta, riesgo de cobro y costo esperado.
4. Exigir evidencia vigente de cuotas, duración, región y elegibilidad.
5. Distinguir free tier, crédito temporal, alerta de presupuesto y costo realmente $0.
6. Revisar que dos instancias y Load Balancer estén incluidos, no asumidos.
7. Revisar creación, inventario, etiquetado y destrucción.
8. Revisar que la destrucción sea verificable y no dependa de recursos externos.
9. Si no se demuestra $0, emitir BLOCKED sin seleccionar proveedor ni provisionar.
10. Emitir hallazgos y resultado.

## Reglas

- Costo obligatorio = $0 para todo recurso académico.
- Solo se comparan AWS, Azure y Google Cloud.
- Ningún proveedor se selecciona mientras la matriz siga pendiente.
- Un free tier o una alerta no constituyen un límite duro de gasto.
- Si $0 y dos instancias/Load Balancer son incompatibles, se detiene y se revisa.
- No se confunde una demostración local con un despliegue cloud.
- La destrucción debe incluir evidencia, no una suposición.

## Restricciones

No escribir archivos, no ejecutar Terraform, cloud CLI, Docker ni shell. No crear recursos, workflows, Dockerfile ni infraestructura. No seleccionar proveedor. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Matriz completa o brecha declarada.
- Costo total y riesgos evaluados con evidencia o BLOCKED.
- Dos instancias, Load Balancer, TLS, observabilidad y destrucción revisados.
- Resultado y siguiente acción sin provisionar.
