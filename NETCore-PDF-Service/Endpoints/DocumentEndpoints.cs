using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Data;
using NETCore_PDF_Service.Services;

namespace NETCore_PDF_Service.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/documents", PostDocumentAsync);
        app.MapGet("/documents", GetDocumentsAsync);
    }

    internal static string? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "A non-empty file is required.";
        }

        var isPdf = string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase);

        return isPdf ? null : "Only PDF files are supported.";
    }

    internal static async Task<IResult> PostDocumentAsync(
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DocumentEndpoints));
        // Read the form manually (rather than binding IFormFile as a parameter) so that a
        // request with no multipart body at all is handled as a clean 400 instead of an
        // unhandled BadHttpRequestException from the framework's automatic form binding.
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "A non-empty file is required." });
        }

        var form = await context.Request.ReadFormAsync();
        var file = form.Files.GetFile("file");

        var validationError = ValidateFile(file);
        if (validationError is not null)
        {
            return Results.BadRequest(new { error = validationError });
        }

        try
        {
            // Resolved lazily (instead of as a delegate parameter) so validation above can
            // reject bad requests without requiring blob storage to be reachable.
            var storageService = context.RequestServices.GetRequiredService<IDocumentStorageService>();
            await using var stream = file!.OpenReadStream();
            var document = await storageService.UploadAsync(stream, file.FileName);
            return Results.Created($"/documents/{document.Id}", document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload document {FileName}", file!.FileName);
            return Results.Problem("Failed to upload document.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    internal static async Task<IResult> GetDocumentsAsync(AppDbContext db)
    {
        // SQLite's EF Core provider cannot translate ORDER BY on DateTimeOffset to SQL,
        // so the ordering is applied client-side after materializing the (small) result set.
        var documents = (await db.Documents.ToListAsync())
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

        return Results.Ok(documents);
    }
}
