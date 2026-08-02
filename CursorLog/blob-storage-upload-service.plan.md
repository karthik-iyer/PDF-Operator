# Azure.Storage.Blobs Upload Service (Azurite-backed)

## Goal

Add a single service class to [`NETCore-PDF-Service`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service) that:

1. Uploads a PDF stream to Azure Blob Storage (Azurite locally) and returns the blob URL.
2. Saves a `Document` row (via `AppDbContext`) after a successful upload.

Both responsibilities live in **one service class** — no `IDocumentStorageService` interface, no separate repository layer. The only DI ceremony is what's required to construct the service and inject it into Minimal API endpoint delegates later.

This plan does **not** add the `POST /documents` / `GET /documents` endpoints themselves — those will call this service in a follow-up step. It also does not touch the existing `Document` entity or `AppDbContext` schema (already in place from the EF Core setup).

## Current state

- [`Data/AppDbContext.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Data\AppDbContext.cs) and [`Models/Document.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Models\Document.cs) already exist with `Id`, `FileName`, `BlobUrl`, `UploadedAt`.
- [`Program.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Program.cs) registers `AppDbContext` and runs `Database.Migrate()` on startup.
- No blob storage package or service exists yet.

## Design

```mermaid
sequenceDiagram
  participant Endpoint as Minimal API endpoint (future)
  participant Service as DocumentStorageService
  participant Azurite as Azurite (BlobContainerClient)
  participant Db as AppDbContext (SQLite)

  Endpoint->>Service: UploadAsync(pdfStream, fileName)
  Service->>Azurite: UploadAsync(blobName, stream)
  Azurite-->>Service: BlobClient.Uri
  Service->>Db: Documents.Add(document); SaveChangesAsync()
  Db-->>Service: OK
  Service-->>Endpoint: Document
```

### 1. NuGet package

```bash
dotnet add package Azure.Storage.Blobs
```

### 2. Configuration

`appsettings.Development.json` gains an Azurite connection string and container name:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/app.db",
    "AzureStorage": "UseDevelopmentStorage=true"
  },
  "BlobStorage": {
    "ContainerName": "documents"
  }
}
```

`UseDevelopmentStorage=true` is the standard shorthand connection string Azure Storage SDKs resolve to Azurite's default local emulator endpoints. Running Azurite locally is a prerequisite for this to work; not part of this plan.

### 3. Service class

New file `Services/DocumentStorageService.cs`:

```csharp
namespace NETCore_PDF_Service.Services;

public class DocumentStorageService
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
```

Design notes:

- **Blob naming**: `{Guid}{extension}` avoids collisions and keeps the original `FileName` only in the metadata row, not the blob path.
- **`overwrite: true`**: safe since the blob name is a fresh GUID every call; never actually overwrites an existing blob.
- **Single method does both steps** (upload, then persist metadata) per your requirement — no separate repository class. If the DB save fails after a successful blob upload, the blob is orphaned; acceptable for this MVP scope (no compensating transaction/cleanup).
- **Constructor takes concrete `BlobContainerClient`**, not `BlobServiceClient` — the service only ever operates on one container, so there's no need to resolve the container client itself in every call.

### 4. Program.cs wiring

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NETCore_PDF_Service.Services;

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? "UseDevelopmentStorage=true";
    var containerName = builder.Configuration["BlobStorage:ContainerName"] ?? "documents";

    var serviceClient = new BlobServiceClient(connectionString);
    var containerClient = serviceClient.GetBlobContainerClient(containerName);
    containerClient.CreateIfNotExists(PublicAccessType.None);

    return containerClient;
});

builder.Services.AddScoped<DocumentStorageService>();
```

- `BlobContainerClient` registered as a singleton (thread-safe, cheap to reuse); container is created once at startup if missing.
- `DocumentStorageService` registered as scoped, matching `AppDbContext`'s scoped lifetime — avoids captive-dependency issues when it's injected into Minimal API endpoint handlers later (endpoint delegate parameters are resolved per-request from DI automatically).
- No interface registration — endpoints will take `DocumentStorageService` directly as a parameter.

## Verification

- [ ] `dotnet build` succeeds with the new package and service
- [ ] With Azurite running locally (`azurite` CLI or Docker), a small manual test (temporary throwaway endpoint or unit test) can call `UploadAsync` and confirm:
  - A blob appears in the `documents` container
  - A row appears in the `Documents` SQLite table with matching `BlobUrl`
- [ ] No interface or repository abstraction was introduced beyond `DocumentStorageService` itself

## Out of scope (follow-up work, not this plan)

- `POST /documents` / `GET /documents` Minimal API endpoints
- Request validation (file type/size limits)
- Azurite startup/orchestration (docker-compose, etc.)
- Error handling/rollback if DB save fails after blob upload succeeds
