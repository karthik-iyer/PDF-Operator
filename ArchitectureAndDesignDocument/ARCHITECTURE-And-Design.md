# Architecture & Design — PDF Viewer SDK Web App

## 1. Overview

A minimal PDF viewer/editor web app with:
- **Frontend**: Angular, using `ngx-extended-pdf-viewer` (wraps Mozilla `pdf.js`, WASM-accelerated rendering) for viewing, and `pdf-lib` for client-side editing/export.
- **Backend**: .NET Core Minimal API — two endpoints, upload and list.
- **Storage**: Azurite (local Azure Blob Storage emulator) for raw PDF bytes.
- **Database**: SQLite (single table), EF Core Code First, migration checked into source control.

The design goal was to keep every layer as thin as possible given a 2–3 hour timebox, by leaning on mature libraries (`pdf.js`, `pdf-lib`) instead of writing PDF-manipulation code by hand.

## 2. Component Diagram

```
Angular SPA
 ├─ Viewer (ngx-extended-pdf-viewer)
 ├─ Editor (pdf-lib: rotate/reorder/merge/extract → export)
 └─ Upload/List screen
        │  HTTP JSON + file upload
        ▼
.NET Minimal API (Program.cs)
 ├─ POST /documents  → save file to Azurite, save row to SQLite
 └─ GET  /documents  → list from SQLite
        │
        ▼
SQLite (1 table)      Azurite (raw PDF blobs)
```

**Flow:**
1. User uploads a PDF → Angular sends it to `POST /documents`.
2. API streams the file to Azurite blob storage, writes a metadata row to SQLite, returns the blob URL.
3. User selects a document from the list → Angular fetches the blob URL directly and renders it in the viewer.
4. In Editor mode, the PDF bytes are loaded into `pdf-lib` client-side; all page operations (rotate, reorder, delete, merge, extract) happen in memory in the browser.
5. Export/Save writes the modified bytes back out as a browser download — no server round trip required for editing.

## 3. State Management Approach

State is intentionally minimal and split by concern:

| State | Where it lives | Lifecycle |
|---|---|---|
| Document list (metadata) | Fetched from `GET /documents`, held in a simple Angular service (`DocumentService`) as an observable/array | Refreshed after each upload |
| Active document bytes | In-memory `ArrayBuffer` inside a single `DocumentStateService`, loaded when a document is opened | Cleared when a different document is opened |
| Viewer state (zoom, page, scroll mode) | Owned by `ngx-extended-pdf-viewer`'s own internal state/inputs | Lives only while the viewer component is mounted |
| Editor state (page order, rotation, deletions) | Held as a `PDFDocument` object (from `pdf-lib`) in `DocumentStateService`, mutated in place until Export | Discarded on Export or navigating away (no autosave/draft persistence) |

There is no global store (NgRx, etc.) — a single service with a couple of observables is sufficient for the scope of this app and avoids adding a state-management library the timebox doesn't justify.

The server is the source of truth only for **persisted metadata** (filename, blob URL, upload timestamp). It is never the source of truth for in-progress edits — those exist purely client-side until export.

## 4. Key Tradeoffs

- **Client-side editing only (no server-side PDF library)**: `pdf-lib` running entirely in the browser is much faster to build than any server-side approach and keeps the API surface tiny (two endpoints). The tradeoff is that very large PDFs are bounded by the browser tab's memory, since the whole document is held as bytes in memory during editing. Acceptable for the scope of this MVP; noted as a scaling risk below.
- **SQLite instead of a hosted DB**: SQLite is a single file, so it can be checked into source control and the app runs with zero external setup (`npm run dev` and `dotnet run` just work). The tradeoff is SQLite is not suitable for concurrent multi-user writes in production — this is a deliberate MVP-only choice, not a production recommendation.
- **Azurite instead of a real Azure Storage account**: keeps setup free and local, with identical SDK code (`Azure.Storage.Blobs`) — swapping in a real connection string later requires no code changes, only config.
- **No SAS token issuance**: uploads/downloads are proxied through the API rather than using short-lived SAS URLs issued to the client. This is simpler to build but means the API is in the request path for every upload; a production version would issue SAS tokens so the client talks to blob storage directly.
- **Blob-based viewer loading loses native HTTP range requests**: the viewer (`ngx-extended-pdf-viewer`) loads a document by calling `GET /documents/{id}/download`, buffering the entire response as a `Blob` in Angular, and passing that `Blob` directly to the viewer's `[src]` input. This is a direct consequence of deliberately *not* binding `[src]` to the `Document.blobUrl` field returned by the API — that URL points at Azurite's private container, which only the API's `BlobContainerClient` credentials can read, so the browser can't fetch it directly. The tradeoff: pdf.js's normal behavior of streaming a PDF via chained HTTP range requests (rendering page 1 while later pages are still downloading, "linearized loading") is lost, since the whole file must download before rendering starts. For the MVP's expected file sizes this is imperceptible, so it wasn't chased further. A partial mitigation exists for later: `ngx-extended-pdf-viewer`'s `[src]` also accepts a URL plus `[httpHeaders]`/`[authorization]` instead of a pre-fetched `Blob`, which would let pdf.js's own range-request logic hit `/download` directly (still API-mediated, never `blobUrl`). Adopting that would require the backend to opt into range processing (e.g. `Results.Stream(..., enableRangeProcessing: true)`) and confirming the Azure Blob SDK's read stream actually supports seeking — deferred as a roadmap item below rather than implemented speculatively.
- **Licensing**: `pdf.js` and `pdf-lib` were chosen deliberately over AGPL/commercial alternatives (e.g. iText7, Aspose) since they're permissively licensed and appropriate for an SDK-style deliverable.
- **Basic pdf.js markup is in; deeper PDF product features were deprioritized**: `ngx-extended-pdf-viewer` already exposes highlight/draw/text/stamp/comment (and WASM-accelerated rendering). True content-stream redaction, AcroForm signature fields, bookmark *editing*, and save audit history were left out of the MVP and are called out in [`IF-I-HAD-1-MORE-DAY.md`](IF-I-HAD-1-MORE-DAY.md).

## 5. If I Had 1 More Day

See the dedicated roadmap (auth/login, roles/admin, validation, virus scan, bulk upload, scale, deeper PDF features, ops):

→ **[`IF-I-HAD-1-MORE-DAY.md`](IF-I-HAD-1-MORE-DAY.md)**
