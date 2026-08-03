# Angular-PDF-App

Angular SPA for the PDF-Operator app: upload/list documents against **NETCore-PDF-Service**, view and annotate PDFs in the browser, and run client-side page edits (rotate, reorder, delete, merge, export).

> Living document — update this whenever routes, libraries, env config, or run steps change. Pair with the root [`README.md`](../README.md) and [`NETCore-PDF-Service/README.md`](../NETCore-PDF-Service/README.md).

**New to the whole repo?** Use the root guide first:

- [New here? Start here](../README.md#new-here-start-here) — fresh machine: install Git / .NET 10 / Node, then Azurite + API + this app
- [How to use the app (step by step)](../README.md#how-to-use-the-app-step-by-step) — library + full viewer feature map (plugin toolbar, Save, Page tools, Export)
- [Prompts for Cursor](../README.md#prompts-for-cursor) — copy-paste Agent prompts (including “no tools installed yet”)

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | [Angular](https://angular.dev/) **21** (standalone components, signals) |
| Language / tooling | TypeScript ~5.9, Angular CLI 21, SCSS |
| PDF **viewing** + in-viewer annotations | [`ngx-extended-pdf-viewer`](https://www.npmjs.com/package/ngx-extended-pdf-viewer) **^29** (wraps Mozilla **pdf.js**) |
| PDF **page editing** (client-side) | [`pdf-lib`](https://pdf-lib.js.org/) **^1.17** |
| Icons | [`@lucide/angular`](https://lucide.dev/) |
| HTTP | Angular `HttpClient` → `DocumentService` |
| Unit tests | Vitest via `ng test` |
| Backend API | [`NETCore-PDF-Service`](../NETCore-PDF-Service/) at `http://localhost:5092` (dev) |

There is **no** Angular Material (or similar) UI kit — layout and chrome are custom SCSS with shared tokens under `src/styles/`.

## PDF libraries (what each one does)

The app deliberately splits PDF work across two libraries:

### 1. `ngx-extended-pdf-viewer` (view + annotate)

- Angular wrapper around Mozilla **pdf.js**.
- Used on `/viewer/:id` to render the PDF, show thumbnails, zoom, find, print, and download.
- Provides the **annotation / markup toolbar**: highlight, draw, text, stamp, comment, signature (via the library’s editor buttons).
- The viewer is fed a **`Blob`** from `GET /documents/{id}/download` (not the raw Azurite `blobUrl`, which the browser cannot read).
- **Save** in the app bar calls `NgxExtendedPdfViewerService.getCurrentDocumentAsBlob()` so annotations are included, then `PUT`s the file back through `DocumentService.replaceDocument`.

**Asset path note:** pdf.js assets are copied from `node_modules/ngx-extended-pdf-viewer/bleeding-edge/` into the app’s `bleeding-edge/` output (see `angular.json`). `src/main.ts` sets:

```ts
pdfDefaultOptions.assetsFolder = 'bleeding-edge';
```

Without that, the viewer worker/scripts 404 at runtime.

### 2. `pdf-lib` (structural page tools)

- Pure client-side PDF manipulation (no server-side PDF engine).
- Owned by `EditorStateService`: loads bytes into a `PDFDocument`, mutates in memory, re-serializes a preview `Blob` for the viewer.
- Driven by the **Page tools** side panel (`EditorPanelComponent`):
  - rotate pages (±90°)
  - reorder (swap up/down)
  - delete page (keeps at least one)
  - merge another PDF into the working copy
  - export/download the edited file locally
  - save edited bytes to the server (same `PUT` path)

Annotations (ngx) and page structure edits (pdf-lib) are complementary: open Page tools when you need rotate/reorder/merge; use the ngx toolbar for markup; Save persists the current viewer document (preferring pdf-lib bytes when page tools have mutated the file).

## Prerequisites

1. **[Node.js](https://nodejs.org/)** (LTS recommended) + npm — project pins `packageManager: npm@11.11.0`.
2. **Backend stack running** for real data: Azurite + `NETCore-PDF-Service` on **http://localhost:5092**. See [backend README](../NETCore-PDF-Service/README.md).
3. CORS: Development API allows `http://localhost:4200` (`Cors:AllowedOrigins` in `appsettings.Development.json`).

## Getting Started

From the repository root (`PDF-Operator/`):

### 1. Install dependencies

```bash
cd Angular-PDF-App
npm install
```

### 2. Start the API (separate terminals)

```bash
# Terminal A — Azurite (repo root)
azurite --skipApiVersionCheck --location .azurite --silent

# Terminal B — API
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092
```

### 3. Start the Angular dev server

```bash
cd Angular-PDF-App
npm start
# equivalent: npx ng serve
```

Open **http://localhost:4200/**. The app hot-reloads on source changes.

`ng serve` uses the **development** configuration by default, which file-replaces `environment.ts` with `environment.development.ts` (`apiBaseUrl: 'http://localhost:5092'`).

## Scripts

| Command | Purpose |
|---|---|
| `npm start` / `ng serve` | Dev server at http://localhost:4200 |
| `npm run build` / `ng build` | Production build → `dist/` |
| `npm run watch` | Rebuild on change (development config) |
| `npm test` / `ng test` | Unit tests (Vitest) |

If a production `ng build` runs out of memory on Windows, raise the Node heap and retry:

```bash
# PowerShell
$env:NODE_OPTIONS='--max-old-space-size=8192'
npx ng build
```

## Configuration

| File | Role |
|---|---|
| `src/environments/environment.development.ts` | Dev: `apiBaseUrl: 'http://localhost:5092'` |
| `src/environments/environment.ts` | Prod: `apiBaseUrl: ''` (same-origin; expects reverse proxy or co-hosted API) |
| `angular.json` | SCSS `includePaths` → `src/styles`; copies ngx `bleeding-edge` assets |
| `src/main.ts` | Sets `pdfDefaultOptions.assetsFolder` |

To point the UI at a different API host in development, change `apiBaseUrl` in `environment.development.ts` and ensure that host’s CORS allows `http://localhost:4200`.

## Routes & UI map

| Route | Component | What you can do |
|---|---|---|
| `/` | `UploadListComponent` | List documents, upload PDF, download, delete, open viewer |
| `/viewer/:id` | `Viewer` | View PDF, annotate (ngx toolbar), Save to server, Page tools (pdf-lib panel) |

Shell: `AppShellComponent` wraps child routes with the left icon rail (home → Documents).

## Project structure

```
Angular-PDF-App/
├── public/                      # Static assets (favicon, etc.)
├── src/
│   ├── app/
│   │   ├── app-shell/           # Icon rail + router outlet
│   │   ├── upload-list/         # Document library (upload / list / actions)
│   │   ├── viewer/              # ngx-extended-pdf-viewer + app Save / Page tools
│   │   ├── editor-panel/        # pdf-lib page tools sidebar
│   │   ├── services/
│   │   │   ├── document.service.ts      # HTTP → /documents*
│   │   │   └── editor-state.service.ts  # pdf-lib working copy + preview Blob
│   │   ├── models/document.model.ts     # Mirrors backend Document JSON
│   │   ├── shared/              # http-error helpers, download-blob util
│   │   ├── app.routes.ts
│   │   └── app.config.ts        # provideRouter, provideHttpClient
│   ├── environments/
│   ├── styles/
│   │   ├── _variables.scss      # Design tokens
│   │   └── _pdf-viewer.scss     # ngx / pdf.js chrome overrides
│   ├── styles.scss
│   ├── index.html
│   └── main.ts                  # Bootstrap + pdf.js assetsFolder
├── angular.json
└── package.json
```

## API usage from the frontend

`DocumentService` (`src/app/services/document.service.ts`) is a thin client over the backend:

| UI action | HTTP |
|---|---|
| Upload | `POST /documents` (`multipart/form-data`, field name **`file`**) |
| List | `GET /documents` |
| Open metadata | `GET /documents/{id}` |
| Load PDF into viewer | `GET /documents/{id}/download` → `Blob` |
| Save (annotations and/or page edits) | `PUT /documents/{id}` with the new PDF file |
| Delete | `DELETE /documents/{id}` |

Full payload samples and status codes: [`NETCore-PDF-Service/README.md`](../NETCore-PDF-Service/README.md).

## Features checklist

### Library (`/`)

- Upload PDF, list by upload time, download, delete, open viewer

### Viewer page (`/viewer/:id`) — detailed

Full walkthrough also lives in the root README: [How to use the app → Viewer page](../README.md#b-viewer-page-viewerid--full-feature-map).

**App chrome**

| Control | Purpose |
|---|---|
| **Documents** back button | Return to library (`/`) — always shown |
| File name | Current document title |
| Saved / error toast | Feedback after app-bar Save |
| **Save** | Persist current PDF + annotations to API (`PUT`) |
| **Page tools** | Toggle pdf-lib structural side panel |

**Thumbnail strip**

- Thumbnail navigation (outline/attachments/layers UI hidden); click to jump pages

**ngx-extended-pdf-viewer toolbar (enabled)**

| Group | Options |
|---|---|
| Editors / annotations | Highlight, Draw, Text, Stamp, Comment (`showEditorButtons` + each editor flag) |
| Zoom | Zoom buttons |
| Paging | Prev/next, page number |
| Find | Text search |
| Print | Print |
| Download | Local download from the plugin (not a server save) |
| Layout | Scrolling modes, spread, book mode |

Plugin sidebar button is disabled; our strip stays on thumbs.

**Page tools panel (pdf-lib)**

| Control | Purpose |
|---|---|
| Merge PDF… | Append another PDF’s pages |
| ⟲ / ⟳ | Rotate page ±90° |
| ↑ / ↓ | Reorder pages |
| ⇩ Extract | Download one page as a PDF |
| ✕ Delete | Remove page (min 1 page) |
| **Export** | Download full edited PDF locally |
| **Save** | Upload edited PDF to the API |

**Shell / styling:** custom SCSS tokens, icon rail, restyled list + viewer chrome

## Troubleshooting

| Symptom | Likely fix |
|---|---|
| CORS errors in the browser console | API not running, or origin not in `Cors:AllowedOrigins` (must include `http://localhost:4200`) |
| Empty list / network failures | Confirm API at http://localhost:5092 and Azurite is up |
| Viewer blank / worker 404 | Confirm `bleeding-edge` assets in `angular.json` and `pdfDefaultOptions.assetsFolder` in `main.ts` |
| Upload rejected | Only PDF (`application/pdf` / `.pdf`); field name must be `file` |
| `ng build` OOM | Set `NODE_OPTIONS=--max-old-space-size=8192` (see Scripts) |

## Further reading

- Root overview & full-stack run order: [`../README.md`](../README.md)
- Backend setup & endpoints: [`../NETCore-PDF-Service/README.md`](../NETCore-PDF-Service/README.md)
- Design notes / tradeoffs: [`../ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md`](../ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md)
- Feature plans: [`../CursorLog/`](../CursorLog/)
- [ngx-extended-pdf-viewer docs](https://pdfviewer.net/)
- [pdf-lib docs](https://pdf-lib.js.org/)
- [Angular CLI reference](https://angular.dev/tools/cli)
