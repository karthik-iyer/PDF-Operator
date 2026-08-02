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
- **Licensing**: `pdf.js` and `pdf-lib` were chosen deliberately over AGPL/commercial alternatives (e.g. iText7, Aspose) since they're permissively licensed and appropriate for an SDK-style deliverable.
- **Bonus WASM/annotation features (redaction, signatures, bookmarks) were deprioritized**: `pdf.js` already provides WASM-accelerated rendering, which satisfies the core viewing bonus. Redaction/signature/bookmark editing were left out of the MVP to protect the timebox and are called out explicitly in the roadmap below rather than partially implemented.

## 5. If I Had 1 More Day

- Move `pdf-lib` operations to a Web Worker so large-file rotate/merge doesn't block the UI thread
- Issue short-lived SAS tokens instead of proxying uploads through the API
- Add true content-stream redaction (not just a visual overlay rectangle)
- Add signature field placement (drag-to-place widget on a page)
- Add bookmark tree read/write in the editor toolbar
- Swap SQLite for Postgres with a real migration path, add optimistic concurrency on document metadata
- Add basic auth so documents aren't globally visible to any client
- Add a couple of automated tests: one for the upload/list API, one rendering a known PDF and asserting page count after an edit operation
