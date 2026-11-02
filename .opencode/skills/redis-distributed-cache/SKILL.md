---
name: Redis Distributed Cache
description: Revisa claves, TTL, cache-aside, invalidación, stampede, estado compartido, fallos y conexiones Redis.
---

# Redis Distributed Cache

## Nombre

redis-distributed-cache

## Objetivo

Evaluar que Redis se use como estado compartido explícito, con claves, TTL, invalidación y degradación definidos, sin convertirlo en fuente de verdad ni en un punto único de fallo silencioso.

## Cuándo utilizarlo

- Al revisar NV-03 o una spec que requiera caché o estado compartido.
- Al diseñar claves, TTL o invalidación.
- Al evaluar revocación, bloqueos o cuotas distribuidas.
- Al revisar fallos, reconexión o degradación.

## Entradas

- Spec, plan y task autorizados.
- Casos de uso y datos a cachear.
- Requisitos de consistencia y seguridad.
- Topología, timeouts y presupuesto de latencia.
- COST-001 y alternativas de servicio.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Identificar qué estado debe compartirse y por qué no puede ser local.
3. Revisar namespace de claves, versionado y separación por usuario o tenant.
4. Revisar TTL, tamaño, expiración y prevención de claves huérfanas.
5. Revisar cache-aside: lectura, escritura, invalidación y carrera entre ambas.
6. Revisar stampede, hot keys y reintentos.
7. Revisar atomicidad cuando se actualicen contadores o bloqueos.
8. Revisar fallos: timeout, reconexión, degradación y salud.
9. Revisar que ningún fallback local abra un control de seguridad.
10. Revisar que Redis no sustituya la fuente de verdad persistente.
11. Revisar costo y alternativas compatibles con $0.
12. Emitir hallazgos y resultado.

## Reglas

- Toda clave tiene namespace, propósito y TTL o justificación de persistencia.
- La invalidación ocurre después de una escritura confirmada.
- No se cachean secretos, tokens en claro ni respuestas privadas sin aislamiento.
- No se usa memoria local para revocación o cuotas que deban ser globales.
- Un fallo de Redis no debe producir decisiones de seguridad divergentes.
- No se copian TTL, claves ni fallbacks de otros proyectos.

## Restricciones

No escribir archivos, no ejecutar Redis, contenedores ni shell. No instalar clientes, no crear caché, middlewares ni repositorios. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Estado compartido y claves evaluados.
- TTL, invalidación, fallos y seguridad revisados.
- Riesgos de consistencia y costo declarados.
- Resultado y siguiente acción sin implementar.
