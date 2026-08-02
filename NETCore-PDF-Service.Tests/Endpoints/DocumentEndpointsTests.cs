using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using NETCore_PDF_Service.Endpoints;
using NETCore_PDF_Service.Models;
using NETCore_PDF_Service.Services;
using NETCore_PDF_Service.Tests.Support;

namespace NETCore_PDF_Service.Tests.Endpoints;

public class DocumentEndpointsTests
{
    private static FormFile CreateFormFile(string fileName, string contentType, byte[]? content = null)
    {
        var stream = new MemoryStream(content ?? new byte[] { 1, 2, 3 });
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    #region ValidateFile

    [Fact]
    public void ValidateFile_ReturnsError_WhenFileIsNull()
    {
        var error = DocumentEndpoints.ValidateFile(null);

        Assert.Equal("A non-empty file is required.", error);
    }

    [Fact]
    public void ValidateFile_ReturnsError_WhenFileIsEmpty()
    {
        var file = CreateFormFile("empty.pdf", "application/pdf", content: Array.Empty<byte>());

        var error = DocumentEndpoints.ValidateFile(file);

        Assert.Equal("A non-empty file is required.", error);
    }

    [Fact]
    public void ValidateFile_ReturnsError_WhenNotPdf()
    {
        var file = CreateFormFile("notes.txt", "text/plain");

        var error = DocumentEndpoints.ValidateFile(file);

        Assert.Equal("Only PDF files are supported.", error);
    }

    [Fact]
    public void ValidateFile_ReturnsNull_WhenContentTypeIsApplicationPdf()
    {
        var file = CreateFormFile("report.pdf", "application/pdf");

        var error = DocumentEndpoints.ValidateFile(file);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateFile_ReturnsNull_WhenExtensionIsPdfEvenWithWrongContentType()
    {
        var file = CreateFormFile("report.pdf", "application/octet-stream");

        var error = DocumentEndpoints.ValidateFile(file);

        Assert.Null(error);
    }

    #endregion

    #region GetDocumentsAsync

    [Fact]
    public async Task GetDocumentsAsync_ReturnsDocuments_OrderedByUploadedAtDescending()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var now = DateTimeOffset.UtcNow;

        var oldest = new Document { Id = Guid.NewGuid(), FileName = "a.pdf", BlobUrl = "http://a", UploadedAt = now.AddMinutes(-10) };
        var newest = new Document { Id = Guid.NewGuid(), FileName = "b.pdf", BlobUrl = "http://b", UploadedAt = now };
        var middle = new Document { Id = Guid.NewGuid(), FileName = "c.pdf", BlobUrl = "http://c", UploadedAt = now.AddMinutes(-5) };

        dbFixture.Context.Documents.AddRange(oldest, newest, middle);
        await dbFixture.Context.SaveChangesAsync();

        var result = await DocumentEndpoints.GetDocumentsAsync(dbFixture.Context);

        var okResult = Assert.IsType<Ok<List<Document>>>(result);
        Assert.Equal(
            new[] { newest.Id, middle.Id, oldest.Id },
            okResult.Value!.Select(d => d.Id));
    }

    [Fact]
    public async Task GetDocumentsAsync_ReturnsEmptyList_WhenNoDocumentsExist()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();

        var result = await DocumentEndpoints.GetDocumentsAsync(dbFixture.Context);

        var okResult = Assert.IsType<Ok<List<Document>>>(result);
        Assert.Empty(okResult.Value!);
    }

    #endregion

    #region PostDocumentAsync

    private static DefaultHttpContext CreateHttpContext(IDocumentStorageService? storageService = null)
    {
        var services = new ServiceCollection();
        if (storageService is not null)
        {
            services.AddSingleton(storageService);
        }

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
    }

    [Fact]
    public async Task PostDocumentAsync_ReturnsBadRequest_WhenRequestHasNoFormContentType()
    {
        var storageServiceMock = new Mock<IDocumentStorageService>();
        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.PostDocumentAsync(context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        storageServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostDocumentAsync_ReturnsBadRequest_WhenNoFileFieldPresent()
    {
        var storageServiceMock = new Mock<IDocumentStorageService>();
        var context = CreateHttpContext(storageServiceMock.Object);
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>());

        var result = await DocumentEndpoints.PostDocumentAsync(context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        storageServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostDocumentAsync_ReturnsBadRequest_WhenFileIsNotPdf()
    {
        var storageServiceMock = new Mock<IDocumentStorageService>();
        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("notes.txt", "text/plain");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PostDocumentAsync(context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        storageServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostDocumentAsync_ReturnsCreated_WhenFileIsValidPdf()
    {
        var expectedDocument = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "report.pdf",
            BlobUrl = "http://127.0.0.1:10000/devstoreaccount1/documents/generated.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };

        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "report.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDocument);

        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("report.pdf", "application/pdf");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PostDocumentAsync(context, NullLoggerFactory.Instance);

        var createdResult = Assert.IsType<Created<Document>>(result);
        Assert.Equal($"/documents/{expectedDocument.Id}", createdResult.Location);
        Assert.Equal(expectedDocument, createdResult.Value);
        storageServiceMock.Verify(
            s => s.UploadAsync(It.IsAny<Stream>(), "report.pdf", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PostDocumentAsync_ReturnsProblem_WhenStorageServiceThrows()
    {
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob storage unreachable"));

        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("report.pdf", "application/pdf");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PostDocumentAsync(context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    #endregion

    #region GetDocumentByIdAsync

    [Fact]
    public async Task GetDocumentByIdAsync_ReturnsDocument_WhenItExists()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "report.pdf",
            BlobUrl = "http://a",
            UploadedAt = DateTimeOffset.UtcNow
        };
        dbFixture.Context.Documents.Add(document);
        await dbFixture.Context.SaveChangesAsync();

        var result = await DocumentEndpoints.GetDocumentByIdAsync(document.Id, dbFixture.Context);

        var okResult = Assert.IsType<Ok<Document>>(result);
        Assert.Equal(document.Id, okResult.Value!.Id);
        Assert.Equal(document.FileName, okResult.Value.FileName);
    }

    [Fact]
    public async Task GetDocumentByIdAsync_ReturnsNotFound_WhenItDoesNotExist()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();

        var result = await DocumentEndpoints.GetDocumentByIdAsync(Guid.NewGuid(), dbFixture.Context);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region PutDocumentAsync

    [Fact]
    public async Task PutDocumentAsync_ReturnsBadRequest_WhenRequestHasNoFormContentType()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.PutDocumentAsync(id, context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        storageServiceMock.Verify(
            s => s.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PutDocumentAsync_ReturnsBadRequest_WhenFileIsNotPdf()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("notes.txt", "text/plain");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PutDocumentAsync(id, context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        storageServiceMock.Verify(
            s => s.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PutDocumentAsync_ReturnsCreated_WhenServiceReportsNewDocument()
    {
        var id = Guid.NewGuid();
        var expectedDocument = new Document
        {
            Id = id,
            FileName = "report.pdf",
            BlobUrl = $"http://127.0.0.1:10000/devstoreaccount1/documents/{id}.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };

        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.UpsertAsync(id, It.IsAny<Stream>(), "report.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedDocument, true));

        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("report.pdf", "application/pdf");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PutDocumentAsync(id, context, NullLoggerFactory.Instance);

        var createdResult = Assert.IsType<Created<Document>>(result);
        Assert.Equal($"/documents/{id}", createdResult.Location);
        Assert.Equal(expectedDocument, createdResult.Value);
    }

    [Fact]
    public async Task PutDocumentAsync_ReturnsOk_WhenServiceReportsReplacedDocument()
    {
        var id = Guid.NewGuid();
        var expectedDocument = new Document
        {
            Id = id,
            FileName = "replacement.pdf",
            BlobUrl = $"http://127.0.0.1:10000/devstoreaccount1/documents/{id}.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };

        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.UpsertAsync(id, It.IsAny<Stream>(), "replacement.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((expectedDocument, false));

        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("replacement.pdf", "application/pdf");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PutDocumentAsync(id, context, NullLoggerFactory.Instance);

        var okResult = Assert.IsType<Ok<Document>>(result);
        Assert.Equal(expectedDocument, okResult.Value);
    }

    [Fact]
    public async Task PutDocumentAsync_ReturnsProblem_WhenStorageServiceThrows()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob storage unreachable"));

        var context = CreateHttpContext(storageServiceMock.Object);
        var file = CreateFormFile("report.pdf", "application/pdf");
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = await DocumentEndpoints.PutDocumentAsync(id, context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    #endregion

    #region DownloadDocumentAsync

    [Fact]
    public async Task DownloadDocumentAsync_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DownloadAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentContent?)null);

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DownloadDocumentAsync(id, context, NullLoggerFactory.Instance);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DownloadDocumentAsync_ReturnsFileStream_WhenDocumentExists()
    {
        var id = Guid.NewGuid();
        using var pdfStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var content = new DocumentContent(pdfStream, "application/pdf", "report.pdf");

        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DownloadAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DownloadDocumentAsync(id, context, NullLoggerFactory.Instance);

        var fileResult = Assert.IsType<FileStreamHttpResult>(result);
        Assert.Same(pdfStream, fileResult.FileStream);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("report.pdf", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task DownloadDocumentAsync_ReturnsProblem_WhenServiceThrows()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DownloadAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob storage unreachable"));

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DownloadDocumentAsync(id, context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    #endregion

    #region DeleteDocumentAsync

    [Fact]
    public async Task DeleteDocumentAsync_ReturnsNoContent_WhenServiceReportsDeleted()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DeleteDocumentAsync(id, context, NullLoggerFactory.Instance);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ReturnsNotFound_WhenServiceReportsNoDocument()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DeleteDocumentAsync(id, context, NullLoggerFactory.Instance);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ReturnsProblem_WhenServiceThrows()
    {
        var id = Guid.NewGuid();
        var storageServiceMock = new Mock<IDocumentStorageService>();
        storageServiceMock
            .Setup(s => s.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob storage unreachable"));

        var context = CreateHttpContext(storageServiceMock.Object);

        var result = await DocumentEndpoints.DeleteDocumentAsync(id, context, NullLoggerFactory.Instance);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    #endregion
}
