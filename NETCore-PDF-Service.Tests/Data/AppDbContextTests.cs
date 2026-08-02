using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Models;
using NETCore_PDF_Service.Tests.Support;

namespace NETCore_PDF_Service.Tests.Data;

public class AppDbContextTests
{
    [Fact]
    public void Documents_MapsToDocumentsTable()
    {
        using var fixture = new SqliteInMemoryAppDbContext();

        var entityType = fixture.Context.Model.FindEntityType(typeof(Document));

        Assert.NotNull(entityType);
        Assert.Equal("Documents", entityType!.GetTableName());
    }

    [Fact]
    public void Id_IsThePrimaryKey()
    {
        using var fixture = new SqliteInMemoryAppDbContext();

        var entityType = fixture.Context.Model.FindEntityType(typeof(Document))!;
        var primaryKey = entityType.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal(new[] { nameof(Document.Id) }, primaryKey!.Properties.Select(p => p.Name));
    }

    [Theory]
    [InlineData(nameof(Document.FileName), 512)]
    [InlineData(nameof(Document.BlobUrl), 2048)]
    public void RequiredStringProperties_AreConfiguredWithExpectedMaxLength(string propertyName, int expectedMaxLength)
    {
        using var fixture = new SqliteInMemoryAppDbContext();

        var entityType = fixture.Context.Model.FindEntityType(typeof(Document))!;
        var property = entityType.FindProperty(propertyName)!;

        Assert.False(property.IsNullable);
        Assert.Equal(expectedMaxLength, property.GetMaxLength());
    }

    [Fact]
    public void UploadedAt_IsRequired()
    {
        using var fixture = new SqliteInMemoryAppDbContext();

        var entityType = fixture.Context.Model.FindEntityType(typeof(Document))!;
        var property = entityType.FindProperty(nameof(Document.UploadedAt))!;

        Assert.False(property.IsNullable);
    }

    [Fact]
    public void UploadedAt_HasAnIndex()
    {
        using var fixture = new SqliteInMemoryAppDbContext();

        var entityType = fixture.Context.Model.FindEntityType(typeof(Document))!;

        var hasIndexOnUploadedAt = entityType.GetIndexes()
            .Any(index => index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Document.UploadedAt) }));

        Assert.True(hasIndexOnUploadedAt);
    }

    [Fact]
    public async Task Documents_CanBePersistedAndRetrieved()
    {
        using var fixture = new SqliteInMemoryAppDbContext();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = "sample.pdf",
            BlobUrl = "http://localhost:10000/devstoreaccount1/documents/sample.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        };

        fixture.Context.Documents.Add(document);
        await fixture.Context.SaveChangesAsync();

        var persisted = await fixture.Context.Documents.SingleAsync(d => d.Id == document.Id);
        Assert.Equal(document.FileName, persisted.FileName);
        Assert.Equal(document.BlobUrl, persisted.BlobUrl);
    }
}
