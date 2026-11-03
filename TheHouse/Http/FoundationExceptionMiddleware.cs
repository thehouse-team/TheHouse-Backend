using System.ComponentModel.DataAnnotations;

namespace TheHouse.Http;

public sealed class FoundationExceptionMiddleware(RequestDelegate next, ILogger<FoundationExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente ya cerró la conexión: no intentar escribir una respuesta.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var status = exception switch
            {
                BadHttpRequestException badRequest when badRequest.StatusCode is >= 400 and < 500 => badRequest.StatusCode,
                ValidationException => 400,
                TimeoutException => 504,
                OperationCanceledException => 503,
                _ => 500
            };
            // No pasar la excepción al logger: su mensaje/stack puede contener entradas o secretos.
            logger.LogWarning("Solicitud rechazada: HTTP {StatusCode}; correlación {CorrelationId}", status, context.TraceIdentifier);
            context.Response.Clear();
            await ProblemResponses.WriteAsync(context, status, validation: status == 400);
        }
    }
}
