---
name: sdd-compliance
description: Verifica jerarquía normativa, autorización humana, trazabilidad requisito-fase-task y alcance antes de cualquier implementación.
---

# SDD Compliance

## Nombre

sdd-compliance

## Objetivo

Asegurar que toda revisión o implementación respete `constitution.md`, `AGENTS.md`, `memoria.md`, la spec activa, el plan activo y la task activa, con autorización humana verificable y trazabilidad completa.

## Cuándo utilizarlo

- Antes de iniciar cualquier implementación.
- Al revisar una spec, un plan o una task.
- Al comprobar si una aprobación cubre un alcance concreto.
- Al detectar contradicciones entre documentos.
- Al preparar un informe PASS, PASS WITH WARNINGS o BLOCKED.

## Entradas

- Ruta e ID de la spec, el plan y la task.
- Versiones y fechas de los tres documentos.
- Aprobación humana: aprobador, fecha, versión, IDs, rutas y restricciones.
- Matriz requisito → fase → task.
- Evidencia disponible y estado de las tasks.
- Decisiones `M-BE-NNN` aplicables y COST-001.

## Procedimiento

1. Leer en orden: constitución, AGENTS, memoria, spec, plan y task.
2. Declarar SPEC ACTIVA, PLAN ACTIVO, TASK ACTIVO y ESTADO AUTORIZADO.
3. Si falta autorización o existe `⛔ NO EJECUTAR`, responder BLOCKED.
4. Verificar que los tres documentos tengan IDs, versión, fecha y estado.
5. Comprobar que la aprobación identifique aprobador, fecha, versiones, tasks, rutas y restricciones.
6. Construir la matriz requisito → fase → task.
7. Detectar requisitos sin task, tasks sin requisito, fases sin trazabilidad y tasks fuera de alcance.
8. Comprobar que todas las tasks sigan `- [ ]` y que no exista `- [x]`.
9. Revisar dependencias entre specs y confirmar que una aprobación no se propaga.
10. Revisar COST-001 y la ausencia de proveedor cloud seleccionado sin evidencia.
11. Emitir resultado y hallazgos con archivo y evidencia.

## Reglas

- Ninguna implementación comienza sin spec, plan y task aprobados.
- Cada incremento funcional o arquitectónico relevante tiene spec independiente.
- Un plan aprobado no habilita tasks fuera de él.
- Un cambio material invalida la aprobación afectada.
- PASS es una revisión de cumplimiento, no una autorización humana.
- BLOCKED no se rebaja a PASS WITH WARNINGS por conveniencia.
- La evidencia ausente se reporta como ausente.

## Restricciones

No escribir archivos, no ejecutar shell, no modificar estados de autorización, no aprobar specs, no fijar modelo o proveedor de IA y no inventar evidencia. Las skills no amplían el alcance de la constitución.

## Criterios de finalización

- Alcance e IDs evaluados declarados.
- Matriz de trazabilidad completa o hallazgos bloqueantes identificados.
- Estado de autorización y de tasks verificado.
- Resultado emitido en el formato permitido.
- Pendientes y siguiente acción indicados sin iniciar implementación.
