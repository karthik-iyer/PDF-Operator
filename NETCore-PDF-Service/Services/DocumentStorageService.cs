using Azure;
using Azure.Storage.Blobs;
using NETCore_PDF_Service.Data;
using NETCore_PDF_Service.Models;

namespace NETCore_PDF_Service.Services;

public class DocumentStorageService : IDocumentStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly AppDbContext _dbContext;

    public DocumentStorageService(BlobContainerClient containerClient, AppDbContext dbContext)
    {
        _containerClient = containerClient;
        _dbContext = dbContext;
    }

    public async Task<Document> UploadAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        // A freshly generated id can never collide with an existing row, so this is always
        // the "create" branch of UpsertAsync - reusing it avoids duplicating the
        // blob-upload/save logic between the two entry points.
        var (document, _) = await UpsertAsync(Guid.NewGuid(), pdfStream, fileName, cancellationToken);
        return document;
    }

    public async Task<(Document Document, bool Created)> UpsertAsync(
        Guid id, Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        // Blob name depends only on id + extension (always ".pdf"), so replacing a document's
        // file overwrites the same blob in place rather than orphaning the previous one.
        var blobClient = _containerClient.GetBlobClient(GetBlobName(id, fileName));
        await blobClient.UploadAsync(pdfStream, overwrite: true, cancellationToken);

        var document = await _dbContext.Documents.FindAsync(new object?[] { id }, cancellationToken);
        var created = document is null;

        if (document is null)
        {
            document = new Document { Id = id };
            _dbContext.Documents.Add(document);
        }

        document.FileName = fileName;
        document.BlobUrl = blobClient.Uri.ToString();
        document.UploadedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (document, created);
    }

    public async Task<DocumentContent?> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents.FindAsync(new object?[] { id }, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var blobClient = _containerClient.GetBlobClient(GetBlobName(document.Id, document.FileName));

        try
        {
            var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            return new DocumentContent(stream, "application/pdf", document.FileName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Orphaned metadata row (blob was deleted/never written) - treat as not found.
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents.FindAsync(new object?[] { id }, cancellationToken);
        if (document is null)
        {
            return false;
        }

        // Deleted before the DB row so a failure here leaves the document "still existing"
        // (consistent, retryable) rather than a DB row pointing at nothing. DeleteIfExistsAsync
        // (not DeleteAsync) so an already-orphaned blob doesn't turn a cleanup delete into a 500.
        var blobClient = _containerClient.GetBlobClient(GetBlobName(document.Id, document.FileName));
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    // Blob name is derived from the id + original extension rather than parsed back out of
    // the stored BlobUrl, so it stays correct even if the storage backend/URL format changes.
    private static string GetBlobName(Guid id, string fileName) => $"{id}{Path.GetExtension(fileName)}";
}
