---
name: mongodb
description: Revisa modelado documental, índices, consultas, conexiones, consistencia y seguridad de MongoDB.
---

# MongoDB

## Nombre

mongodb

## Objetivo

Evaluar que el diseño MongoDB responda a patrones de acceso reales, con índices justificados, límites explícitos y sin filtrar detalles de persistencia hacia Domain.

## Cuándo utilizarlo

- Al revisar NV-03 o una spec de persistencia.
- Al diseñar colecciones, agregados, embedding o referencias.
- Al evaluar índices, TTL, consistencia o transacciones.
- Al revisar conexiones, timeouts y recuperación.

## Entradas

- Spec, plan y task autorizados.
- Consultas y patrones de acceso previstos.
- Volumen, crecimiento y requisitos de consistencia.
- Modelo de dominio y límites de módulo.
- Requisitos de seguridad y COST-001.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Identificar consultas y patrones de acceso antes de elegir el modelo.
3. Revisar agregados, embedding, referencias y límites de documento.
4. Revisar índices únicos, compuestos y TTL contra consultas concretas.
5. Revisar consistencia, atomicidad y necesidad real de transacciones.
6. Revisar que `MongoDB.Driver` y los detalles BSON queden en Infrastructure.
7. Revisar reutilización de conexiones, timeouts, cancelación y reconexión.
8. Revisar credenciales, TLS, mínimo privilegio y separación por entorno.
9. Revisar crecimiento, paginación y consultas sin índices.
10. Revisar si la conectividad y el servicio pueden cumplir COST-001.
11. Emitir hallazgos y resultado.

## Reglas

- El modelo documental se deriva de consultas, no de tablas SQL.
- No se copian entidades, índices ni cadenas de conexión de otros repositorios.
- No se exponen tipos BSON o del driver en Domain/Application.
- No se declara una conexión o base de datos existente sin evidencia.
- No se cachean datos privados sin aislamiento.
- No se asume gratuidad del servicio de MongoDB.

## Restricciones

No escribir archivos, no ejecutar MongoDB, contenedores ni shell. No instalar drivers, no crear repositorios, entidades, índices ni migraciones. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Patrones de acceso y modelo evaluados.
- Índices, consistencia, conexiones y seguridad revisados.
- Riesgos de costo y evidencia faltante declarados.
- Resultado y siguiente acción sin implementar.
