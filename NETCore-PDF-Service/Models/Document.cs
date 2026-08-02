namespace NETCore_PDF_Service.Models;

public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}
