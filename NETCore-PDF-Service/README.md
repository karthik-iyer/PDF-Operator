# NETCore-PDF-Service

A minimal ASP.NET Core (.NET 10) Minimal API backend for the PDF-Operator app. It stores PDF document **metadata** in SQLite (via EF Core) and the actual PDF **file bytes** in Azure Blob Storage (Azurite locally).

> This README is a living document — update it whenever endpoints, setup steps, or configuration change.

## Tech Stack

| Concern | Technology |
|---|---|
| API framework | ASP.NET Core Minimal APIs (.NET 10) |
| Metadata storage | SQLite, via EF Core Code First migrations |
| File storage | Azure Blob Storage (`Azure.Storage.Blobs`), emulated locally with **Azurite** |
| API docs | Swagger / OpenAPI via `Swashbuckle.AspNetCore` |
| Testing | xUnit + Moq (unit tests in `NETCore-PDF-Service.Tests`) |

## Prerequisites

Install these before running the project:

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download)** — currently developed against `10.0.201`. Check with `dotnet --version`.
2. **Node.js + npm** — only needed to install Azurite (the local Azure Storage emulator).
3. **Azurite**, installed globally:

   ```bash
   npm install -g azurite
   ```

4. **dotnet-ef CLI tool** — pinned as a local tool (see `dotnet-tools.json`), restored per-project (step 1 below), not installed globally.

## Project Structure

```
NETCore-PDF-Service/
├── Data/                 # AppDbContext (EF Core)
├── Endpoints/            # Minimal API route handlers (DocumentEndpoints.cs)
├── Migrations/           # EF Core Code First migrations
├── Models/               # Document entity
├── Services/             # IDocumentStorageService / DocumentStorageService (blob + db logic)
├── data/                 # app.db (SQLite file) - tracked in git, see "Data & Storage" below
├── Program.cs            # App startup / DI / middleware wiring
└── appsettings*.json     # Configuration (connection strings, blob container name)

NETCore-PDF-Service.Tests/
├── Data/                 # AppDbContext tests
├── Endpoints/            # DocumentEndpoints tests
├── Models/               # Document model tests
├── Services/             # DocumentStorageService tests
└── Support/              # SqliteInMemoryAppDbContext test fixture
```

## Getting Started

Run these from the repository root (`PDF-Operator/`) unless noted otherwise.

### 1. Restore .NET local tools

```bash
cd NETCore-PDF-Service
dotnet tool restore
```

This installs `dotnet-ef` at the version pinned in `dotnet-tools.json` (must match the installed SDK's ASP.NET Core runtime version).

### 2. Restore NuGet packages

```bash
dotnet restore
```

(Also restores the test project if you build/test from the solution level.)

### 3. Start Azurite (local Azure Blob Storage emulator)

From the repository root:

```bash
azurite --skipApiVersionCheck --location .azurite --silent
```

- `--location .azurite` keeps Azurite's emulated storage data out of the way in a gitignored folder.
- `--skipApiVersionCheck` avoids version-mismatch errors between the installed Azurite and the `Azure.Storage.Blobs` SDK version used here.
- Leave this running in its own terminal — it serves Blob (10000), Queue (10001), and Table (10002) endpoints.

### 4. Run the API

```bash
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092
```

- On startup, EF Core automatically applies any pending migrations (`db.Database.Migrate()` in `Program.cs`) — no manual `dotnet ef database update` needed for normal runs.
- The SQLite file lives at `NETCore-PDF-Service/data/app.db` (relative to the project's content root).
- Default URL (from `Properties/launchSettings.json`): `http://localhost:5092` (or `https://localhost:7230` for the `https` profile).

### 5. Open the API docs (Swagger)

With the API running, browse to:

```
http://localhost:5092/swagger
```

This gives you an interactive UI listing every endpoint, its request/response schema, and a "Try it out" button (including file-upload pickers for `POST`/`PUT`). Swagger UI is only enabled in the `Development` environment.

## Configuration Reference

`appsettings.Development.json`:

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

| Key | Purpose | Production override |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite file location | Point to a persistent volume/path |
| `ConnectionStrings:AzureStorage` | Blob storage connection string | Real Azure Storage account connection string |
| `BlobStorage:ContainerName` | Blob container name | Any valid container name (auto-created if missing) |

## Endpoints

All routes are grouped under `/documents`. Full interactive docs live at `/swagger` (see above) — the samples below are for quick reference.

### `POST /documents` — Upload a new document

Server generates the `Id`. Not idempotent — retrying after a failed/timed-out request creates a new document each time.

```bash
curl -X POST http://localhost:5092/documents \
  -F "file=@report.pdf;type=application/pdf"
```

**Response `201 Created`:**

```json
{
  "id": "bb1f25dc-34bd-465a-bf13-e629b81dd094",
  "fileName": "report.pdf",
  "blobUrl": "http://127.0.0.1:10000/devstoreaccount1/documents/bb1f25dc-34bd-465a-bf13-e629b81dd094.pdf",
  "uploadedAt": "2026-08-02T14:40:15.1434673+00:00"
}
```

**Errors:** `400` (missing file / not a PDF), `500` (storage failure).

### `PUT /documents/{id}` — Create-or-replace a document at a known id

Client supplies the `id` (a GUID it generates). Creates the document if that id doesn't exist yet, or replaces the existing file + metadata if it does. Idempotent — safe to retry.

```bash
curl -X PUT http://localhost:5092/documents/93707d9a-e1c1-4cf0-82ac-2a6831de197a \
  -F "file=@report-v2.pdf;type=application/pdf"
```

**Response:** `201 Created` (new document) or `200 OK` (existing document replaced) — same body shape as `POST`.

**Errors:** `400` (missing file / not a PDF), `500` (storage failure).

### `GET /documents` — List all documents

Returns all documents, most recently uploaded/updated first.

```bash
curl http://localhost:5092/documents
```

**Response `200 OK`:**

```json
[
  {
    "id": "bb1f25dc-34bd-465a-bf13-e629b81dd094",
    "fileName": "report.pdf",
    "blobUrl": "http://127.0.0.1:10000/devstoreaccount1/documents/bb1f25dc-34bd-465a-bf13-e629b81dd094.pdf",
    "uploadedAt": "2026-08-02T14:40:15.1434673+00:00"
  }
]
```

### `GET /documents/{id}` — Get a single document's metadata

```bash
curl http://localhost:5092/documents/bb1f25dc-34bd-465a-bf13-e629b81dd094
```

**Response:** `200 OK` with the same document shape as `GET /documents`, or `404 Not Found` if the id doesn't exist.

### `GET /documents/{id}/download` — Download a document's PDF content

Streams the actual PDF bytes back with `Content-Type: application/pdf` and a `Content-Disposition: attachment` header carrying the original filename.

```bash
curl -o downloaded.pdf http://localhost:5092/documents/bb1f25dc-34bd-465a-bf13-e629b81dd094/download
```

**Response:** `200 OK` with the raw PDF bytes, or `404 Not Found` if the id doesn't exist (or its blob is missing).

### `DELETE /documents/{id}` — Delete a document

Deletes both the metadata row and the underlying blob.

```bash
curl -X DELETE http://localhost:5092/documents/bb1f25dc-34bd-465a-bf13-e629b81dd094
```

**Response:** `204 No Content` on success, `404 Not Found` if the id doesn't exist.

## Testing from Postman

- `POST`/`PUT`: set the body type to **form-data**, add a key named `file`, set its type to **File**, and choose a PDF from disk.
- `GET`/`GET .../download`/`DELETE`: no body needed.

## Running Tests

```bash
dotnet test NETCore-PDF-Service.Tests
```

Tests use an in-memory SQLite database (same provider as production, just `:memory:`) and Moq for `BlobContainerClient`/`BlobClient`/`IDocumentStorageService` — no running Azurite instance required to run the test suite.

## Data & Storage Notes

- `NETCore-PDF-Service/data/app.db` **is** committed to git (so the schema/sample data is directly consumable by other contributors without running migrations first). Its `*.db-wal`/`*.db-shm` sidecar files are gitignored — always let the app shut down cleanly (or checkpoint manually) before committing so the WAL is flushed into `app.db`.
- Blob content itself is **not** committed — it only lives in Azurite locally (or real Azure Storage in higher environments). If you restore this repo fresh and the `data/app.db` metadata references blobs that don't exist in your local Azurite instance yet, `GET /documents` will still work (metadata only), but `GET /documents/{id}/download` will `404` until you re-upload.
- `.azurite/` (Azurite's own local storage data) is gitignored.

## Adding a New EF Core Migration

```bash
cd NETCore-PDF-Service
dotnet ef migrations add <MigrationName>
```

Migrations apply automatically on next `dotnet run` (see `Program.cs`). To apply without running the app:

```bash
dotnet ef database update
```

## Troubleshooting

- **`EADDRINUSE` starting Azurite**: a previous Azurite instance is still running.
  Find and stop it (Windows):
  ```powershell
  Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -eq 10000 }
  Stop-Process -Id <OwningProcess> -Force
  ```
- **`MSB3026`/`MSB3027` file locked during `dotnet build`**: a previous `dotnet run` process is still holding the built `.dll`/`.exe`. Stop it first:
  ```powershell
  Get-Process -Name "NETCore-PDF-Service" | Stop-Process -Force
  ```
- **`GET /documents/{id}/download` returns 404 for a document you can see in `GET /documents`**: Azurite's data was reset/recreated since that document was uploaded (its blob no longer exists) — re-upload via `PUT /documents/{id}` to fix it in place.
