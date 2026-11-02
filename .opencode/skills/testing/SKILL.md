---
name: Testing Quality
description: Planifica y revisa pruebas unitarias, integración, seguridad, contrato, mutación, performance y evidencia de calidad.
---

# Testing Quality

## Nombre

testing

## Objetivo

Evaluar que la estrategia de pruebas cubra los riesgos relevantes con suites reproducibles, criterios medibles y evidencia real, sin confundir cobertura con corrección.

## Cuándo utilizarlo

- Al revisar NV-02 o cualquier spec con requisitos verificables.
- Al definir o auditar suites unitarias, de integración o de seguridad.
- Al planificar mutación, contrato, performance, DAST o fuzzing.
- Al revisar umbrales, quality gates o evidencia de CI.

## Entradas

- Spec, plan y task autorizados.
- Riesgos y requisitos verificables.
- Suites existentes y su propósito.
- Herramientas previstas: xUnit, WebApplicationFactory, Stryker.NET, PactNet, NBomber, OWASP ZAP y RESTler.
- Entorno, datos, SLO y restricciones de costo.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Mapear cada riesgo relevante a una suite y a un criterio de aceptación.
3. Revisar que las pruebas unitarias aíslen Domain/Application.
4. Revisar que las de integración usen composición real y `WebApplicationFactory` cuando corresponda.
5. Revisar que las de seguridad cubran autorización, abuso, entrada, cabeceras y límites.
6. Revisar contrato con consumidores identificados y PactNet si aplica.
7. Revisar mutación con Stryker.NET y distinguir score de cobertura.
8. Revisar performance con NBomber: escenario, datos, concurrencia, SLO y entorno.
9. Revisar DAST y fuzzing en entorno aislado y autorizado.
10. Comprobar que los umbrales se midan antes de ser gates.
11. Revisar evidencia: reporte, versión, comando reproducible y retención.
12. Detectar tests vacíos, omitidos, inestables o no ejecutados.
13. Emitir hallazgos y resultado.

## Reglas

- La existencia de un proyecto de tests no prueba cobertura.
- Un test omitido o no ejecutado no es PASS.
- No se inventan cantidades, porcentajes, latencias ni resultados.
- Los umbrales se proponen con datos, no se copian de otro repositorio.
- Performance y DAST requieren entorno autorizado y aislado.
- La cobertura de líneas no sustituye mutación ni pruebas de comportamiento.
- Las pruebas deben ser deterministas y limpiar sus datos.

## Restricciones

No escribir archivos, no ejecutar tests, scanners ni shell. No instalar paquetes ni crear proyectos. No modificar código para hacer pasar una prueba. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Riesgos mapeados a suites y criterios.
- Brechas y pruebas inexistentes declaradas.
- Umbrales y evidencia evaluados como propuesta o hecho comprobado.
- Resultado y siguiente acción emitidos sin ejecutar.
