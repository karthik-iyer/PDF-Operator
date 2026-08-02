using NETCore_PDF_Service.Models;

namespace NETCore_PDF_Service.Tests.Models;

public class DocumentTests
{
    [Fact]
    public void NewDocument_HasEmptyStringDefaults_NotNull()
    {
        var document = new Document();

        Assert.Equal(string.Empty, document.FileName);
        Assert.Equal(string.Empty, document.BlobUrl);
    }

    [Fact]
    public void NewDocument_HasDefaultId_UntilExplicitlyAssigned()
    {
        var document = new Document();

        Assert.Equal(Guid.Empty, document.Id);
    }

    [Fact]
    public void Document_PropertiesRoundTrip_WhenAssigned()
    {
        var id = Guid.NewGuid();
        var uploadedAt = DateTimeOffset.UtcNow;

        var document = new Document
        {
            Id = id,
            FileName = "report.pdf",
            BlobUrl = "http://localhost:10000/devstoreaccount1/documents/report.pdf",
            UploadedAt = uploadedAt
        };

        Assert.Equal(id, document.Id);
        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal("http://localhost:10000/devstoreaccount1/documents/report.pdf", document.BlobUrl);
        Assert.Equal(uploadedAt, document.UploadedAt);
    }
}
