using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using NETCore_PDF_Service.Models;
using NETCore_PDF_Service.Services;
using NETCore_PDF_Service.Tests.Support;

namespace NETCore_PDF_Service.Tests.Services;

public class DocumentStorageServiceTests
{
    private static Mock<BlobClient> CreateBlobClientMock(string blobName)
    {
        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Azure.Response<Azure.Storage.Blobs.Models.BlobContentInfo>)null!);
        blobClientMock
            .Setup(c => c.Uri)
            .Returns(new Uri($"http://127.0.0.1:10000/devstoreaccount1/documents/{blobName}"));

        return blobClientMock;
    }

    [Fact]
    public async Task UploadAsync_UploadsToBlobWithNameEndingInSourceExtension()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var containerClientMock = new Mock<BlobContainerClient>();
        Mock<BlobClient>? capturedBlobClientMock = null;
        string? capturedBlobName = null;

        containerClientMock
            .Setup(c => c.GetBlobClient(It.Is<string>(name => name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))))
            .Returns((string name) =>
            {
                capturedBlobName = name;
                capturedBlobClientMock = CreateBlobClientMock(name);
                return capturedBlobClientMock.Object;
            });

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        await sut.UploadAsync(stream, "report.pdf");

        Assert.NotNull(capturedBlobName);
        Assert.EndsWith(".pdf", capturedBlobName);
        capturedBlobClientMock!.Verify(
            c => c.UploadAsync(stream, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadAsync_ReturnsDocumentWithExpectedMetadata()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var containerClientMock = new Mock<BlobContainerClient>();
        var blobClientMock = CreateBlobClientMock("generated-name.pdf");

        containerClientMock
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var before = DateTimeOffset.UtcNow;

        var result = await sut.UploadAsync(stream, "report.pdf");

        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("report.pdf", result.FileName);
        Assert.Equal(blobClientMock.Object.Uri.ToString(), result.BlobUrl);
        Assert.InRange(result.UploadedAt, before, after);
    }

    [Fact]
    public async Task UploadAsync_PersistsDocumentToDbContext()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var containerClientMock = new Mock<BlobContainerClient>();
        var blobClientMock = CreateBlobClientMock("generated-name.pdf");

        containerClientMock
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.UploadAsync(stream, "report.pdf");

        Assert.Equal(1, await dbFixture.Context.Documents.CountAsync());
        var persisted = await dbFixture.Context.Documents.SingleAsync();
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(result.FileName, persisted.FileName);
        Assert.Equal(result.BlobUrl, persisted.BlobUrl);
    }

    #region DownloadAsync

    [Fact]
    public async Task DownloadAsync_ReturnsNull_WhenDocumentRowDoesNotExist()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var containerClientMock = new Mock<BlobContainerClient>();
        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);

        var result = await sut.DownloadAsync(Guid.NewGuid());

        Assert.Null(result);
        containerClientMock.Verify(c => c.GetBlobClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsContent_WhenDocumentAndBlobExist()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "report.pdf",
            BlobUrl = "http://127.0.0.1:10000/devstoreaccount1/documents/whatever.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };
        dbFixture.Context.Documents.Add(document);
        await dbFixture.Context.SaveChangesAsync();

        var pdfBytes = new byte[] { 1, 2, 3 };
        var blobStream = new MemoryStream(pdfBytes);
        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(c => c.OpenReadAsync(It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blobStream);

        var containerClientMock = new Mock<BlobContainerClient>();
        containerClientMock
            .Setup(c => c.GetBlobClient($"{document.Id}.pdf"))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);

        var result = await sut.DownloadAsync(document.Id);

        Assert.NotNull(result);
        Assert.Same(blobStream, result!.Content);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("report.pdf", result.FileName);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsNull_WhenBlobIsMissing()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "report.pdf",
            BlobUrl = "http://127.0.0.1:10000/devstoreaccount1/documents/whatever.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };
        dbFixture.Context.Documents.Add(document);
        await dbFixture.Context.SaveChangesAsync();

        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(c => c.OpenReadAsync(It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        var containerClientMock = new Mock<BlobContainerClient>();
        containerClientMock
            .Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);

        var result = await sut.DownloadAsync(document.Id);

        Assert.Null(result);
    }

    #endregion

    #region UpsertAsync

    [Fact]
    public async Task UpsertAsync_CreatesNewDocument_WhenIdDoesNotExist()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var id = Guid.NewGuid();
        var containerClientMock = new Mock<BlobContainerClient>();
        var blobClientMock = CreateBlobClientMock($"{id}.pdf");

        containerClientMock
            .Setup(c => c.GetBlobClient($"{id}.pdf"))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var (document, created) = await sut.UpsertAsync(id, stream, "report.pdf");

        Assert.True(created);
        Assert.Equal(id, document.Id);
        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal(1, await dbFixture.Context.Documents.CountAsync());
    }

    [Fact]
    public async Task UpsertAsync_ReplacesFileNameAndUploadedAt_WhenIdAlreadyExists()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var id = Guid.NewGuid();
        var originalUploadedAt = DateTimeOffset.UtcNow.AddDays(-1);
        dbFixture.Context.Documents.Add(new Document
        {
            Id = id,
            FileName = "original.pdf",
            BlobUrl = $"http://127.0.0.1:10000/devstoreaccount1/documents/{id}.pdf",
            UploadedAt = originalUploadedAt
        });
        await dbFixture.Context.SaveChangesAsync();

        var containerClientMock = new Mock<BlobContainerClient>();
        var blobClientMock = CreateBlobClientMock($"{id}.pdf");
        containerClientMock
            .Setup(c => c.GetBlobClient($"{id}.pdf"))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);
        using var stream = new MemoryStream(new byte[] { 4, 5, 6 });

        var (document, created) = await sut.UpsertAsync(id, stream, "replacement.pdf");

        Assert.False(created);
        Assert.Equal(id, document.Id);
        Assert.Equal("replacement.pdf", document.FileName);
        Assert.True(document.UploadedAt > originalUploadedAt);
        Assert.Equal(1, await dbFixture.Context.Documents.CountAsync());
        blobClientMock.Verify(
            c => c.UploadAsync(stream, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_UsesSameBlobName_RegardlessOfCreateOrReplace()
    {
        using var dbFixture = new SqliteInMemoryAppDbContext();
        var id = Guid.NewGuid();
        var containerClientMock = new Mock<BlobContainerClient>();
        var blobClientMock = CreateBlobClientMock($"{id}.pdf");
        containerClientMock
            .Setup(c => c.GetBlobClient($"{id}.pdf"))
            .Returns(blobClientMock.Object);

        var sut = new DocumentStorageService(containerClientMock.Object, dbFixture.Context);

        using (var firstStream = new MemoryStream(new byte[] { 1 }))
        {
            await sut.UpsertAsync(id, firstStream, "first.pdf");
        }

        using (var secondStream = new MemoryStream(new byte[] { 2 }))
        {
            await sut.UpsertAsync(id, secondStream, "second.pdf");
        }

        containerClientMock.Verify(c => c.GetBlobClient($"{id}.pdf"), Times.Exactly(2));
    }

    #endregion
}
