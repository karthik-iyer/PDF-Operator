# PDF-Operator

A web app to view, manipulate, and perform other operations on PDF documents.

> Living document — update it alongside [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md) (and `Angular-PDF-App/README.md`, once it exists) whenever setup steps or endpoints change.

## Repo Structure

| Path | What it is |
|---|---|
| [`NETCore-PDF-Service/`](NETCore-PDF-Service/) | .NET 10 Minimal API backend — PDF metadata (SQLite) + file storage (Azure Blob Storage / Azurite). **See its [README](NETCore-PDF-Service/README.md) for full setup, all endpoints, and sample payloads.** |
| `NETCore-PDF-Service.Tests/` | xUnit + Moq unit tests for the backend |
| `Angular-PDF-App/` | Angular frontend (viewer/editor) — not yet built out |
| `ArchitectureAndDesignDocument/` | High-level architecture & design notes for the overall app |
| `CursorLog/` | Implementation plans / design decisions for each feature, in chronological order |

## Backend Quick Start (NETCore-PDF-Service)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js/npm (for Azurite).

```bash
# 1. Restore local tools (dotnet-ef) and NuGet packages
cd NETCore-PDF-Service
dotnet tool restore
dotnet restore

# 2. Install & start the local Azure Storage emulator (from repo root, separate terminal)
npm install -g azurite
azurite --skipApiVersionCheck --location .azurite --silent

# 3. Run the API (applies EF Core migrations automatically on startup)
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092
```

Then open **http://localhost:5092/swagger** for interactive API docs, or use the endpoints directly:

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/documents` | Upload a new PDF (server generates the id) |
| `PUT` | `/documents/{id}` | Create at a specific id, or replace the file if it already exists (upsert) |
| `GET` | `/documents` | List all documents, most recent first |
| `GET` | `/documents/{id}/download` | Download a document's PDF bytes |
| `DELETE` | `/documents/{id}` | Delete a document and its underlying file |

Full request/response samples, Postman notes, configuration reference, and troubleshooting tips live in [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md).

### Running Tests

```bash
dotnet test NETCore-PDF-Service.Tests
```

## Frontend Quick Start (Angular-PDF-App)

*Not yet implemented — this section will be filled in the same way as the backend one above (prerequisites, install/run commands, endpoint summary table) once the Angular app is scaffolded.*

Intended design: Angular + [`ngx-extended-pdf-viewer`](https://www.npmjs.com/package/ngx-extended-pdf-viewer) for viewing, [`pdf-lib`](https://pdf-lib.js.org/) for client-side editing/export. See [`ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md`](ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md) for the full design.

## Running the Full Stack

Once the frontend exists, this section will describe running both the backend (`NETCore-PDF-Service` + Azurite) and the frontend (`Angular-PDF-App`) together for local end-to-end development.
