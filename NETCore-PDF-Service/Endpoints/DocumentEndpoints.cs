using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Data;
using NETCore_PDF_Service.Models;
using NETCore_PDF_Service.Services;

namespace NETCore_PDF_Service.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        // .Accepts<IFormFile>(...) documents the multipart file upload for Swagger even though
        // the handlers read the form manually from HttpContext rather than binding IFormFile as
        // a parameter (see ReadAndValidateFileAsync) - without it, Swagger has no way to know
        // these endpoints expect a file and won't render a file-picker in "Try it out".
        app.MapPost("/documents", PostDocumentAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<Document>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("UploadDocument")
            .WithSummary("Upload a new PDF document")
            .WithTags("Documents");

        app.MapPut("/documents/{id:guid}", PutDocumentAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<Document>(StatusCodes.Status200OK)
            .Produces<Document>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("UpsertDocument")
            .WithSummary("Create a document at the given id, or replace its file if one already exists")
            .WithTags("Documents");

        app.MapGet("/documents", GetDocumentsAsync)
            .Produces<List<Document>>(StatusCodes.Status200OK)
            .WithName("ListDocuments")
            .WithSummary("List all documents, most recently uploaded first")
            .WithTags("Documents");

        app.MapGet("/documents/{id:guid}/download", DownloadDocumentAsync)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithName("DownloadDocument")
            .WithSummary("Download a document's PDF file content")
            .WithTags("Documents");
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

    // Reads the form manually (rather than binding IFormFile as a parameter) so that a
    // request with no multipart body at all is handled as a clean 400 instead of an
    // unhandled BadHttpRequestException from the framework's automatic form binding. Shared
    // by POST and PUT since both accept the file the same way.
    private static async Task<(IFormFile? File, IResult? Error)> ReadAndValidateFileAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            return (null, Results.BadRequest(new { error = "A non-empty file is required." }));
        }

        var form = await context.Request.ReadFormAsync();
        var file = form.Files.GetFile("file");

        var validationError = ValidateFile(file);
        return validationError is not null
            ? (null, Results.BadRequest(new { error = validationError }))
            : (file, null);
    }

    internal static async Task<IResult> PostDocumentAsync(
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DocumentEndpoints));
        var (file, error) = await ReadAndValidateFileAsync(context);
        if (error is not null)
        {
            return error;
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

    internal static async Task<IResult> PutDocumentAsync(
        Guid id,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DocumentEndpoints));
        var (file, error) = await ReadAndValidateFileAsync(context);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var storageService = context.RequestServices.GetRequiredService<IDocumentStorageService>();
            await using var stream = file!.OpenReadStream();
            var (document, created) = await storageService.UpsertAsync(id, stream, file.FileName);

            return created
                ? Results.Created($"/documents/{document.Id}", document)
                : Results.Ok(document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save document {DocumentId}", id);
            return Results.Problem("Failed to save document.", statusCode: StatusCodes.Status500InternalServerError);
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

    internal static async Task<IResult> DownloadDocumentAsync(
        Guid id,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(DocumentEndpoints));

        try
        {
            var storageService = context.RequestServices.GetRequiredService<IDocumentStorageService>();
            var content = await storageService.DownloadAsync(id);

            return content is null
                ? Results.NotFound()
                : Results.Stream(content.Content, content.ContentType, content.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download document {DocumentId}", id);
            return Results.Problem("Failed to download document.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
