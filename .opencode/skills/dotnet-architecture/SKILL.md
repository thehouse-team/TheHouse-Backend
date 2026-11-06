---
name: dotnet-architecture
description: Revisa Clean Architecture, Modular Monolith, DI, configuración, resiliencia, stateless y performance en .NET 10.
---

# .NET Architecture

## Nombre

dotnet-architecture

## Objetivo

Evaluar que el diseño .NET respete la Dependency Rule, los límites modulares, la composición explícita y las restricciones de operación sin escribir código.

## Cuándo utilizarlo

- Al revisar NV-01, NV-03 o cualquier spec que afecte la estructura.
- Al evaluar DI, configuración, middleware, resiliencia o rendimiento.
- Al comprobar si una propuesta introduce estado local o acoplamiento indebido.
- Al revisar la preparación para dos instancias y balanceo.

## Entradas

- `constitution.md`, `AGENTS.md` y `memoria.md`.
- Spec, plan y task con estado de autorización.
- Referencias actuales entre los siete proyectos.
- Diseño de módulos, puertos, adaptadores y composition root.
- Requisitos de configuración, resiliencia y performance.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Confirmar las referencias actuales: host → Application e Infrastructure; Infrastructure → Application y Domain; Application → Domain; Domain sin dependencias internas.
3. Revisar que Domain no dependa de HTTP, MongoDB, Redis, ASP.NET Core ni proveedores.
4. Revisar que Application defina casos de uso y puertos sin implementar adaptadores.
5. Revisar que Infrastructure implemente adaptadores y que el host sea composition root.
6. Evaluar lifetimes, registros duplicados, validación al inicio y opciones tipadas.
7. Revisar timeouts, cancelación, reintentos y circuit breaker con justificación.
8. Identificar estado en memoria que impida reemplazar o escalar instancias.
9. Revisar límites de módulo, contratos y ausencia de ciclos.
10. Evaluar paginación, I/O, asignaciones y límites de recursos.
11. Emitir hallazgos con severidad y evidencia.

## Reglas

- Las dependencias apuntan hacia el interior.
- El host compone; no concentra lógica de negocio en controllers.
- Los DTO de transporte no sustituyen modelos de dominio.
- Un monolito modular no se convierte en microservicios sin decisión y spec.
- Stateless significa estado compartido donde la corrección lo requiera, no ausencia de estado.
- No se aceptan fallbacks locales para decisiones de seguridad.
- No se afirma una mejora de performance sin medición.

## Restricciones

No modificar proyectos, referencias, código, `Program.cs`, configuración ni paquetes. No ejecutar `dotnet`, build, restore o tests. No crear módulos funcionales. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Arquitectura y dependencias evaluadas contra la constitución.
- Riesgos de DI, configuración, resiliencia, estado y performance identificados.
- Hallazgos con archivo y evidencia o ausencia declarada.
- Resultado y siguiente acción emitidos sin implementar.
