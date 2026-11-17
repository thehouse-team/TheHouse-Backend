using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Media;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

// NV-13: fotos y documentos. Las cargas suben su propio límite de cuerpo
// (Kestrel limita el resto a 64 KB); la ruta pública solo sirve fotos de
// edificios verificados con caché inmutable; los documentos solo salen al
// equipo de revisión, con `no-store`.
public static class MediaEndpoint
{
    public const string BuildingPhotoPath = LandlordEndpoint.BuildingPath + "/photo";

    public const string RoomPhotosPath = LandlordEndpoint.RoomPath + "/photos";

    public const string RoomPhotoPath = RoomPhotosPath + "/{photoId}";

    public const string DocumentsPath = LandlordEndpoint.BuildingPath + "/documents";

    public const string DocumentPath = DocumentsPath + "/{kind}";

    public const string PublicPath = ApiConventions.Prefix + "/media/{id}";

    public const string PrivatePath = PublicPath + "/private";

    public const string ReviewerDocumentPath = AdminEndpoint.BasePath + "/buildings/{id}/documents/{documentId}";

    // Holgura para los encabezados multipart sobre el tamaño del archivo.
    private const int MultipartOverhead = 16 * 1024;

    public static bool IsMedia(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return context.Request.Path.StartsWithSegments(ApiConventions.Prefix + "/media", StringComparison.OrdinalIgnoreCase) ||
               (path.StartsWith(LandlordEndpoint.BuildingsPath + "/", StringComparison.OrdinalIgnoreCase) &&
                (path.EndsWith("/photo", StringComparison.OrdinalIgnoreCase) || path.Contains("/documents", StringComparison.OrdinalIgnoreCase))) ||
               (path.StartsWith(LandlordEndpoint.BasePath + "/rooms/", StringComparison.OrdinalIgnoreCase) && path.Contains("/photos", StringComparison.OrdinalIgnoreCase)) ||
               (path.StartsWith(AdminEndpoint.BasePath + "/buildings/", StringComparison.OrdinalIgnoreCase) && path.Contains("/documents/", StringComparison.OrdinalIgnoreCase));
    }

    public static void MapMedia(this WebApplication app)
    {
        Upload(app.MapPut(BuildingPhotoPath, SetBuildingPhotoAsync), "SetBuildingPhoto", "Upload or replace the building photo (multipart field `file`)")
            .Produces<PhotoResponse>(200);
        Private(app.MapDelete(BuildingPhotoPath, DeleteBuildingPhotoAsync), "DeleteBuildingPhoto", "Remove the building photo")
            .Produces(204);
        Upload(app.MapPost(RoomPhotosPath, AddRoomPhotoAsync), "AddRoomPhoto", "Add a room photo (up to 4)")
            .Produces<PhotoResponse>(201);
        Private(app.MapDelete(RoomPhotoPath, DeleteRoomPhotoAsync), "DeleteRoomPhoto", "Remove a room photo")
            .Produces(204);
        Upload(app.MapPut(DocumentPath, UploadDocumentAsync), "UploadVerificationDocument", "Upload or replace `ine` or `property_proof` (multipart field `file`)")
            .Produces<DocumentResponse>(200);
        Private(app.MapGet(DocumentsPath, ListDocumentsAsync), "ListVerificationDocuments", "Metadata of my verification documents")
            .Produces<DocumentListResponse>(200);
        Private(app.MapGet(PrivatePath, PrivatePhotoAsync), "GetPrivatePhoto", "Photo for its owner or a reviewer (no-store)")
            .Produces(200, contentType: "image/webp");
        Private(app.MapGet(ReviewerDocumentPath, ReviewerDocumentAsync), "GetVerificationDocument", "Decrypted document for reviewers (audited)")
            .Produces(200, contentType: "application/octet-stream");
        app.MapGet(PublicPath, PublicPhotoAsync)
            .WithName("GetPublicPhoto").WithTags("Media").WithSummary("Photo of a verified building (immutable cache)")
            .Produces(200, contentType: "image/webp").Produces(304)
            .ProducesValidationProblem(400).ProducesProblem(404).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous()
            .DisableRateLimiting();
    }

    private static RouteHandlerBuilder Private(RouteHandlerBuilder builder, string name, string summary) =>
        builder.WithName(name).WithTags("Media").WithSummary(summary)
            .ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(429).ProducesProblem(503).ProducesProblem(500)
            .AllowAnonymous().RequireTokenNotRevoked().DisableRateLimiting();

    private static RouteHandlerBuilder Upload(RouteHandlerBuilder builder, string name, string summary) =>
        Private(builder, name, summary).Accepts<IFormFile>("multipart/form-data").ProducesProblem(413).ProducesProblem(415);

    private static Task<IResult> SetBuildingPhotoAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceUpload(context, store, limits, userId);
            var file = await ReadFileAsync(context, MediaRules.MaximumPhotoBytes);
            return Results.Json(await media.SetBuildingPhotoAsync(userId, id, file, context.RequestAborted));
        });

    private static Task<IResult> DeleteBuildingPhotoAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceUpload(context, store, limits, userId);
            await media.DeleteBuildingPhotoAsync(userId, id, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> AddRoomPhotoAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceUpload(context, store, limits, userId);
            var file = await ReadFileAsync(context, MediaRules.MaximumPhotoBytes);
            return Results.Json(await media.AddRoomPhotoAsync(userId, id, file, context.RequestAborted), statusCode: 201);
        });

    private static Task<IResult> DeleteRoomPhotoAsync(string id, string photoId, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceUpload(context, store, limits, userId);
            await media.DeleteRoomPhotoAsync(userId, id, photoId, context.RequestAborted);
            return Results.NoContent();
        });

    private static Task<IResult> UploadDocumentAsync(string id, string kind, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceUpload(context, store, limits, userId);
            var file = await ReadFileAsync(context, MediaRules.MaximumDocumentBytes);
            return Results.Json(await media.UploadDocumentAsync(userId, id, kind, file, context.RequestAborted));
        });

    private static Task<IResult> ListDocumentsAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceRead(context, store, limits, userId);
            return Results.Json(await media.ListDocumentsAsync(userId, id, context.RequestAborted));
        });

    private static Task<IResult> PrivatePhotoAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceRead(context, store, limits, userId);
            var bytes = await media.PrivatePhotoAsync(userId, id, Thumb(context), context.RequestAborted);
            return Results.Bytes(bytes, "image/webp");
        });

    private static Task<IResult> ReviewerDocumentAsync(string id, string documentId, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits) =>
        UserEndpointSupport.RunAsync(context, async userId =>
        {
            await EnforceRead(context, store, limits, userId);
            var (content, contentType, kind) = await media.ReviewerDocumentAsync(userId, id, documentId, context.RequestAborted);
            var extension = contentType switch { "application/pdf" => ".pdf", "image/png" => ".png", _ => ".jpg" };
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(content, contentType, kind + extension);
        });

    // Pública: sin sesión; 404 si el edificio no está verificado.
    private static async Task<IResult> PublicPhotoAsync(string id, HttpContext context, MediaService media, IRateLimitStore store, UserRateLimitOptions limits)
    {
        try
        {
            await UserEndpointSupport.EnforceAsync(
                context,
                store,
                RateLimitKeys.MediaPublicIp(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                limits.MediaReadsPerWindow,
                limits.Window,
                limits.RetryAfterSeconds);
            var thumb = Thumb(context);
            var bytes = await media.PublicPhotoAsync(id, thumb, context.RequestAborted);
            var etag = new EntityTagHeaderValue($"\"{id.ToLowerInvariant()}-{(thumb ? "thumb" : "full")}\"", isWeak: true);
            var headers = context.Response.GetTypedHeaders();
            headers.ETag = etag;
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return HttpCaching.Matches(context.Request, etag)
                ? Results.StatusCode(StatusCodes.Status304NotModified)
                : Results.Bytes(bytes, "image/webp");
        }
        catch (Exception exception) when (exception is RequestValidationException)
        {
            return UserEndpointSupport.Error(context, 400);
        }
        catch (NotFoundException)
        {
            return UserEndpointSupport.Error(context, 404);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return UserEndpointSupport.Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return UserEndpointSupport.Error(context, 503);
        }
    }

    private static bool Thumb(HttpContext context) =>
        UserEndpointSupport.QueryText(context, "size") switch
        {
            null or "" or "full" => false,
            "thumb" => true,
            _ => throw new RequestValidationException("size", "size must be full or thumb."),
        };

    // Lee el campo `file` de un multipart acotado a `maximumBytes`.
    private static async Task<UploadedFile> ReadFileAsync(HttpContext context, int maximumBytes)
    {
        if (!context.Request.HasFormContentType ||
            !(context.Request.ContentType ?? string.Empty).StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserEndpointSupport.UnsupportedBodyException(415);
        }

        var limit = (long)maximumBytes + MultipartOverhead;
        if (context.Request.ContentLength > limit)
        {
            throw new UserEndpointSupport.UnsupportedBodyException(413);
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
        {
            size.MaxRequestBodySize = limit;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(
                new FormOptions { MultipartBodyLengthLimit = limit, ValueCountLimit = 4, BufferBodyLengthLimit = limit },
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            throw new UserEndpointSupport.UnsupportedBodyException(413);
        }

        var file = form.Files.GetFile("file");
        if (file is null || form.Files.Count != 1 || file.Length == 0)
        {
            throw new RequestValidationException("file", "Send exactly one file in the field `file`.");
        }

        if (file.Length > maximumBytes)
        {
            throw new UserEndpointSupport.UnsupportedBodyException(413);
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, context.RequestAborted);
        return new UploadedFile(buffer.ToArray());
    }

    private static Task EnforceUpload(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.MediaUploadsUser(userId), limits.MediaUploadsPerHour, limits.GenerateWindow, 3600);

    private static Task EnforceRead(HttpContext context, IRateLimitStore store, UserRateLimitOptions limits, string userId) =>
        UserEndpointSupport.EnforceAsync(
            context, store, RateLimitKeys.MediaPrivateUser(userId), limits.MediaReadsPerWindow, limits.Window, limits.RetryAfterSeconds);
}
