---
name: devsecops
description: Revisa SAST, SCA, SBOM, DAST, fuzzing, seguridad de contenedores, firma, secretos, CI/CD y quality gates.
---

# DevSecOps

## Nombre

devsecops

## Objetivo

Verificar que la seguridad se integre en todo el ciclo con controles ejecutables, evidencia reproducible y gates que bloqueen sin depender de afirmaciones o umbrales copiados.

## Cuándo utilizarlo

- Al revisar NV-02 o una cadena de CI/CD.
- Al evaluar SAST, SCA, SBOM, DAST, fuzzing o seguridad de contenedores.
- Al revisar gestión de secretos, firma o procedencia.
- Al definir quality gates, excepciones y evidencias.

## Entradas

- Spec, plan y task autorizados.
- Pipeline y workflows propuestos.
- Herramientas previstas y sus versiones.
- Inventario de dependencias, imágenes y artefactos.
- Política de severidades, excepciones y vencimientos.
- Restricciones COST-001 y permisos de CI.

## Procedimiento

1. Verificar autorización; sin ella, BLOCKED.
2. Enumerar los controles obligatorios y su evidencia esperada.
3. Revisar SAST: Semgrep y SonarCloud/SonarAnalyzer, reglas, baseline y supresión justificada.
4. Revisar SCA: auditoría NuGet, política de dependencias y respuesta a vulnerabilidades.
5. Revisar SBOM CycloneDX y su trazabilidad a la entrega o imagen.
6. Revisar DAST con OWASP ZAP y fuzzing con RESTler en entorno autorizado.
7. Revisar seguridad de contenedores: Trivy, Dockle, usuario no root y superficie mínima.
8. Revisar firma y verificación con Cosign cuando exista artefacto autorizado.
9. Revisar secretos: origen, alcance, rotación y ausencia en repositorio, logs e imágenes.
10. Revisar gates: criterio, bloqueo, excepción, responsable y vencimiento.
11. Revisar permisos de GitHub Actions y acciones fijadas por versión o digest.
12. Distinguir control planificado, ejecutado y aprobado.
13. Emitir hallazgos y resultado.

## Reglas

- Un scanner no ejecutado, fallido o sin reporte no equivale a PASS.
- Un hallazgo crítico o alto no se cierra sin corrección o excepción aprobada.
- Un free tier o una alerta de presupuesto no prueba costo cero.
- No se versionan secretos ni se imprimen en logs.
- No se copia código, workflow ni configuración de los repositorios de referencia.
- Las herramientas y umbrales de otro proyecto son referencia, no decisión de TheHouse.
- No se despliega ni firma sin autorización.

## Restricciones

No escribir archivos, no ejecutar scanners, Docker, Terraform, pipelines ni shell. No instalar herramientas ni modificar código o configuración. No fijar modelo o proveedor de IA.

## Criterios de finalización

- Controles y evidencia esperada enumerados.
- Brechas, hallazgos y excepciones identificados.
- Gates y permisos revisados.
- Resultado y siguiente acción sin ejecutar controles.
