# GET /documents/{id}/download — Download the Actual PDF

## Goal

Add an endpoint that streams the actual PDF bytes back to the client for a given document `Id`, since today the API only ever returns metadata (`POST`/`GET /documents`) — there's no way to retrieve the file content itself.

## Design

```mermaid
sequenceDiagram
  participant Client
  participant API as DocumentEndpoints
  participant Storage as DocumentStorageService
  participant Db as AppDbContext
  participant Blob as BlobContainerClient

  Client->>API: GET /documents/{id}/download
  API->>Storage: DownloadAsync(id)
  Storage->>Db: Documents.FindAsync(id)
  alt no row for id
    Db-->>Storage: null
    Storage-->>API: null
    API-->>Client: 404 Not Found
  else row found
    Db-->>Storage: Document
    Storage->>Blob: GetBlobClient(blobName).OpenReadAsync()
    alt blob missing (orphaned row)
      Blob-->>Storage: 404 RequestFailedException
      Storage-->>API: null
      API-->>Client: 404 Not Found
    else blob found
      Blob-->>Storage: Stream
      Storage-->>API: DocumentContent(Stream, "application/pdf", FileName)
      API-->>Client: 200, application/pdf, Content-Disposition: attachment; filename="{FileName}"
    end
  end
```

### Route

`GET /documents/{id:guid}/download` — a dedicated sub-path (not `GET /documents/{id}` returning binary) so a future JSON metadata-by-id endpoint can still use the plain `/documents/{id}` path without conflicting.

### `IDocumentStorageService` — new method

```csharp
public sealed record DocumentContent(Stream Content, string ContentType, string FileName);

Task<DocumentContent?> DownloadAsync(Guid id, CancellationToken cancellationToken = default);
```

Returns `null` when the document row doesn't exist *or* when the underlying blob is missing (treated as equivalent "not found" cases from the endpoint's perspective — no need for the endpoint to know which one happened).

### `DocumentStorageService.DownloadAsync` implementation notes

- Reconstructs the blob name the same way `UploadAsync` does (`{id}{extension-of-original-filename}`) via a small shared private helper — avoids depending on parsing `Document.BlobUrl` back apart, and stays consistent if the storage backend/URL format ever changes.
- Catches `Azure.RequestFailedException` with `Status == 404` from the blob open call and returns `null` (orphaned-row edge case) instead of letting it bubble up as a 500.
- Content-Type is always `application/pdf` (the API only ever accepts PDFs on upload).

### Endpoint handler

```csharp
internal static async Task<IResult> DownloadDocumentAsync(Guid id, HttpContext context)
{
    var storageService = context.RequestServices.GetRequiredService<IDocumentStorageService>();
    var content = await storageService.DownloadAsync(id);

    return content is null
        ? Results.NotFound()
        : Results.Stream(content.Content, content.ContentType, content.FileName);
}
```

Resolved lazily via `RequestServices` for the same reason as `PostDocumentAsync` — keeps the pattern consistent across the file.

## Tests to add

- **Services**: `DownloadAsync` returns the correct stream/content-type/filename for an existing document; returns `null` for a non-existent `Id`; returns `null` when the blob itself is missing (mocked 404 from `BlobClient`).
- **Endpoints**: `DownloadDocumentAsync` returns `Results.NotFound()` when the service returns `null`; returns a stream result with the expected content-type/filename when the service returns content.

## Verification

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] `GET /documents/{id}/download` for a real uploaded document returns `200` with the original PDF bytes intact
- [ ] `GET /documents/{random-guid}/download` returns `404`

## Out of scope

- Range requests / partial content (`Accept-Ranges`, `206`) for large-file streaming
- Caching headers (`ETag`, `Last-Modified`)
- Authorization on the download path (matches the rest of the API — no auth currently)
