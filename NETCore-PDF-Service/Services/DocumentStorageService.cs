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
        var id = Guid.NewGuid();
        var blobName = $"{id}{Path.GetExtension(fileName)}";

        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(pdfStream, overwrite: true, cancellationToken);

        var document = new Document
        {
            Id = id,
            FileName = fileName,
            BlobUrl = blobClient.Uri.ToString(),
            UploadedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return document;
    }
}
