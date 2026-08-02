using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Moq;
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
}
