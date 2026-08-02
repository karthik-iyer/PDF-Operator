using NETCore_PDF_Service.Models;

namespace NETCore_PDF_Service.Services;

public interface IDocumentStorageService
{
    Task<Document> UploadAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);
}
