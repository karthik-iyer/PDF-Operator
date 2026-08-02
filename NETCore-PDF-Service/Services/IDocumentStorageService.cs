using NETCore_PDF_Service.Models;

namespace NETCore_PDF_Service.Services;

public interface IDocumentStorageService
{
    Task<Document> UploadAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the PDF content for <paramref name="id"/>, or <see langword="null"/> if no
    /// document row exists for that id, or if the underlying blob is missing.
    /// </summary>
    Task<DocumentContent?> DownloadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a document at <paramref name="id"/> if none exists, or replaces the file and
    /// metadata of the existing one. <c>Created</c> is <see langword="true"/> when a new row
    /// was inserted, <see langword="false"/> when an existing one was replaced.
    /// </summary>
    Task<(Document Document, bool Created)> UpsertAsync(
        Guid id, Stream pdfStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the document and its underlying blob (if any). Returns <see langword="true"/>
    /// if a document was found and deleted, <see langword="false"/> if no document exists for
    /// <paramref name="id"/>.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
