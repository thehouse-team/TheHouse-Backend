---
name: API Contract
description: Revisa REST, OpenAPI, versionado, Problem Details, validación, paginación, códigos HTTP, idempotencia y correlation IDs.
---

# API Contract

## Nombre

api-contract

## Objetivo

Comprobar que el contrato HTTP sea coherente, explícito, versionable, seguro y verificable antes de implementar endpoints.

## Cuándo utilizarlo

- Al diseñar o revisar NV-01 y cualquier spec con endpoints.
- Al evaluar OpenAPI, errores, validación, paginación o versionado.
- Al revisar compatibilidad entre consumidores y proveedor.
- Al preparar pruebas de contrato o DAST.

## Entradas

- Spec, plan y task autorizados.
- Documento OpenAPI o descripción del contrato.
- Estrategia de versionado y deprecación.
- Catálogo de errores y formato Problem Details.
- Reglas de validación, paginación e idempotencia.
- Requisitos de correlation ID y observabilidad.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Identificar recursos, operaciones, métodos y códigos de estado.
3. Revisar que el diseño sea REST y no una colección de acciones RPC.
4. Comprobar que OpenAPI describa esquemas, parámetros, respuestas, errores y seguridad.
5. Revisar la estrategia de versión y el tratamiento de cambios incompatibles.
6. Revisar Problem Details: `application/problem+json`, campos estables, extensión de traza y ausencia de fugas.
7. Revisar validación de entrada y correspondencia 400/422.
8. Revisar paginación: orden estable, tamaño máximo, metadatos y ausencia de consultas ilimitadas.
9. Revisar idempotencia solo donde el contrato la requiera.
10. Revisar correlation ID: formato, límites, generación, propagación y eco.
11. Revisar ejemplos sin secretos ni datos personales.
12. Emitir hallazgos y resultado.

## Reglas

- Un endpoint sin contrato verificable no está listo.
- Los errores no exponen trazas, excepciones internas ni detalles de infraestructura.
- No se usa 200 para errores.
- No se promete idempotencia sin mecanismo verificable.
- No se inventan endpoints fuera de una spec aprobada.
- No se fija autenticación concreta sin la spec de seguridad correspondiente.
- La versión del contrato y la de la imagen deben ser trazables.

## Restricciones

No escribir archivos, no ejecutar shell, no modificar controladores, DTO, OpenAPI ni configuración. No crear endpoints. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Operaciones y estados revisados.
- Contrato OpenAPI coherente o brechas enumeradas.
- Errores, validación, paginación, idempotencia y correlación evaluados.
- Hallazgos con severidad y siguiente acción.
