# Unit Tests for Each Layer (NETCore-PDF-Service)

## Goal

Add a dedicated xUnit test project covering each architectural layer of `NETCore-PDF-Service`:

- **Models** — `Document`
- **Data** — `AppDbContext` (EF Core model configuration)
- **Services** — `DocumentStorageService` (blob upload + metadata persistence)
- **Endpoints** — `POST /documents`, `GET /documents` (validation + routing logic)

This requires two small, SOLID-motivated refactors first, since the current code isn't unit-testable in isolation:

1. `DocumentStorageService` is a concrete class injected directly into the `POST /documents` lambda in `Program.cs`. To unit test the endpoint layer without touching Azure Blob Storage, extract an `IDocumentStorageService` interface (Dependency Inversion) so endpoint tests can mock it.
2. The endpoint delegates are anonymous lambdas inside top-level statements in `Program.cs`, so they aren't reachable from a test project. Extract them into a testable `DocumentEndpoints` static class (Single Responsibility — keeps `Program.cs` as pure composition-root wiring).

No behavior changes to the running app — this is a test-focused addition plus the minimal refactor needed to make it possible.

## Current state

- [`Models/Document.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Models\Document.cs) — POCO entity.
- [`Data/AppDbContext.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Data\AppDbContext.cs) — configures `Documents` table (key, required fields, max lengths, index on `UploadedAt`).
- [`Services/DocumentStorageService.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Services\DocumentStorageService.cs) — concrete class, depends on `BlobContainerClient` + `AppDbContext`.
- [`Program.cs`](D:\Projects\Learn\PDF-Operator\NETCore-PDF-Service\Program.cs) — DI setup, migration-on-startup, and both endpoint lambdas inline.
- No test project exists yet.

## Refactor for testability

### 1. Extract `IDocumentStorageService`

New file `Services/IDocumentStorageService.cs`:

```csharp
namespace NETCore_PDF_Service.Services;

public interface IDocumentStorageService
{
    Task<Document> UploadAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);
}
```

`DocumentStorageService` implements it; `Program.cs` DI registration changes to:

```csharp
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();
```

### 2. Extract `Endpoints/DocumentEndpoints.cs`

Move both route handlers out of `Program.cs` into a static class with an extension method, splitting validation into a small pure function so it's testable without any HTTP plumbing:

```csharp
public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        app.MapPost("/documents", PostDocumentAsync);
        app.MapGet("/documents", GetDocumentsAsync);
    }

    internal static string? ValidateFile(IFormFile? file) { /* same rules as today */ }

    internal static async Task<IResult> PostDocumentAsync(
        HttpContext context, IDocumentStorageService storageService, ILogger<>>...) { /* same logic as today, using ValidateFile */ }

    internal static async Task<IResult> GetDocumentsAsync(AppDbContext db) { /* same ordering logic as today */ }
}
```

`Program.cs` shrinks to `app.MapDocumentEndpoints();`. Behavior (status codes, error shapes, ordering, lazy service resolution) is unchanged from the current implementation — this is a pure move + extraction.

## Test project

New project: `NETCore-PDF-Service.Tests/NETCore-PDF-Service.Tests.csproj`

- Target framework: `net10.0` (matches main project)
- `ProjectReference` to `NETCore-PDF-Service.csproj`
- Packages: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Moq`, `coverlet.collector`
- **No new EF Core provider package.** Tests use SQLite's `:memory:` mode (via `Microsoft.Data.Sqlite`, already a transitive dependency through `Microsoft.EntityFrameworkCore.Sqlite`) with an open `SqliteConnection` kept alive for the test's lifetime. This keeps tests exercising the *real* provider the app uses in production (relevant — we already hit one SQLite-specific quirk with `DateTimeOffset` ordering) instead of introducing `Microsoft.EntityFrameworkCore.InMemory` as a second, different-behaving provider.

## Layer-by-layer test plan

### `Models/DocumentTests.cs`

- New `Document` has expected defaults (`FileName`/`BlobUrl` empty string, not null).

### `Data/AppDbContextTests.cs`

Build an `AppDbContext` against an open in-memory SQLite connection and assert on `dbContext.Model` metadata (no need to actually persist rows):

- `Documents` entity maps to table `Documents`.
- `Id` is the primary key.
- `FileName`, `BlobUrl`, `UploadedAt` are required (`IsNullable == false`).
- `FileName` max length 512, `BlobUrl` max length 2048.
- An index exists on `UploadedAt`.

### `Services/DocumentStorageServiceTests.cs`

- Mock `BlobContainerClient` / `BlobClient` (Azure SDK clients are designed to be mockable: protected parameterless ctor + virtual members) so no real Azurite dependency.
- Real `AppDbContext` backed by a fresh in-memory SQLite connection per test (so `SaveChangesAsync` is genuinely exercised, matching what caught the `DateTimeOffset` ordering issue earlier).
- Cases:
  - `UploadAsync` calls `containerClient.GetBlobClient(...)` with a name ending in the source file's extension.
  - `UploadAsync` calls `blobClient.UploadAsync(stream, overwrite: true, ...)`.
  - Returned `Document` has `FileName` set from the input, `BlobUrl` set from `blobClient.Uri`, `UploadedAt` within a small tolerance of `DateTimeOffset.UtcNow`, and a new `Id`.
  - The row is actually persisted (`dbContext.Documents.CountAsync() == 1` after the call, matching the returned `Document`).

### `Endpoints/DocumentEndpointsTests.cs`

- `ValidateFile`: null file → error; zero-length file → error; wrong content-type/extension → error; valid `.pdf` → `null` (no error).
- `GetDocumentsAsync`: seed 3 `Document` rows with different `UploadedAt` values into an in-memory-SQLite-backed `AppDbContext`, call the handler directly, assert the returned `Ok<List<Document>>.Value` is ordered newest-first.
- `PostDocumentAsync`:
  - No form content-type on the request → `BadRequest` result, `IDocumentStorageService` never invoked (`Mock.Verify(..., Times.Never)`).
  - Form present but no `file` field / empty file → `BadRequest`.
  - Non-PDF file → `BadRequest`.
  - Valid PDF → mocked `IDocumentStorageService.UploadAsync` returns a canned `Document`; assert `Created<Document>` result with matching `Location` and `Value`.
  - Requests are built with a `DefaultHttpContext` whose `Request.Form` is set directly to a `FormCollection` containing a real `Microsoft.AspNetCore.Http.FormFile` (constructed from an in-memory `MemoryStream`) — no actual multipart-body parsing needed for a unit test.

Typed `Results.*` return values (`BadRequest<T>`, `Ok<T>`, `Created<T>`) are asserted directly via `Assert.IsType<...>()` and their `StatusCode`/`Value` properties — no need to spin up a `WebApplicationFactory`/real HTTP server for these to qualify as true unit tests.

## Verification

- [ ] `dotnet build` succeeds for both projects
- [ ] `dotnet test` passes for all four layers
- [ ] Running app behavior (`dotnet run` + manual smoke test) is unchanged after the `IDocumentStorageService` / `DocumentEndpoints` refactor

## Out of scope

- Integration tests against a real/emulated Azurite instance (kept as manual smoke testing, as done previously)
- Code coverage thresholds/CI wiring
- Testing `Program.cs` startup/migration logic itself
