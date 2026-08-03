# PDF-Operator

A web app to view, manipulate, and perform other operations on PDF documents.

> Living document — update it alongside [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md) and [`Angular-PDF-App/README.md`](Angular-PDF-App/README.md) whenever setup steps, endpoints, or frontend run details change.

If you are **new to this repo**, start with [New here? Start here](#new-here-start-here), then [How to use the app](#how-to-use-the-app-step-by-step). Prefer to let [Cursor](https://cursor.com/) drive setup? Jump to [Prompts for Cursor](#prompts-for-cursor).

## Repo Structure

| Path | What it is |
|---|---|
| [`NETCore-PDF-Service/`](NETCore-PDF-Service/) | .NET 10 Minimal API backend — PDF metadata (SQLite) + file storage (Azure Blob Storage / Azurite). **See its [README](NETCore-PDF-Service/README.md) for full setup, all endpoints, and sample payloads.** |
| `NETCore-PDF-Service.Tests/` | xUnit + Moq unit tests for the backend |
| [`Angular-PDF-App/`](Angular-PDF-App/) | Angular 21 SPA — document library, pdf.js viewer/annotations (`ngx-extended-pdf-viewer`), client-side page edits (`pdf-lib`). **See its [README](Angular-PDF-App/README.md) for full frontend docs.** |
| `ArchitectureAndDesignDocument/` | High-level architecture & design notes for the overall app |
| `CursorLog/` | Implementation plans / design decisions for each feature, in chronological order |

---

## New here? Start here

This section is the full path from an empty machine to a working local stack and a first PDF edit.

### What you will run

You need **three processes** at once:

| # | Process | Why |
|---|---|---|
| 1 | **Azurite** | Local Azure Blob Storage emulator — stores PDF file bytes |
| 2 | **NETCore-PDF-Service** | API on `http://localhost:5092` — metadata (SQLite) + blob access |
| 3 | **Angular-PDF-App** | UI on `http://localhost:4200` — upload, view, annotate, page tools |

### Prerequisites (install once)

Install these on your machine before the project-specific steps:

| Tool | Why | Check |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Run / build the API | `dotnet --version` (expect 10.x) |
| [Node.js LTS](https://nodejs.org/) + npm | Azurite + Angular app | `node -v` / `npm -v` |
| Git | Clone the repo | `git --version` |
| A browser | Use the UI | Chrome / Edge / Firefox / etc. |

Optional but useful: [Cursor](https://cursor.com/) (or VS Code) with the C# / Angular extensions you prefer.

### Step 1 — Clone the repo

```bash
git clone <your-remote-url> PDF-Operator
cd PDF-Operator
```

Open this folder as the workspace root in Cursor/VS Code.

### Step 2 — Install Azurite (global, once)

```bash
npm install -g azurite
```

### Step 3 — Restore the backend

```bash
cd NETCore-PDF-Service
dotnet tool restore
dotnet restore
cd ..
```

`dotnet tool restore` installs the pinned `dotnet-ef` local tool. Migrations apply automatically when the API starts.

### Step 4 — Install frontend packages

```bash
cd Angular-PDF-App
npm install
cd ..
```

### Step 5 — Start Azurite (terminal 1)

From the **repo root** (`PDF-Operator/`):

```bash
azurite --skipApiVersionCheck --location .azurite --silent
```

Leave this running. Data lands under `.azurite/` (local only).

### Step 6 — Start the API (terminal 2)

From the **repo root**:

```bash
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092
```

Confirm:

- Console shows the app listening
- **http://localhost:5092/swagger** opens in the browser

### Step 7 — Start the Angular app (terminal 3)

```bash
cd Angular-PDF-App
npm start
```

Open **http://localhost:4200/**. Dev config points the UI at `http://localhost:5092` and CORS allows `http://localhost:4200`.

### Step 8 — Smoke-check

1. UI loads at http://localhost:4200 (document library).
2. Swagger loads at http://localhost:5092/swagger.
3. Upload a small PDF from the UI (next section).

If something fails, see [Troubleshooting](#troubleshooting-newcomers) or the project READMEs.

---

## How to use the app (step by step)

Do this after the three processes from above are running.

### A. Document library (`/`)

1. Open **http://localhost:4200/**.
2. You should see the **Documents** list (empty the first time).
3. Click **Upload PDF** and choose a `.pdf` file.
4. When upload finishes, the file appears in the table (name + upload time).
5. Optional row actions:
   - **Download** (icon) — save the stored PDF to your machine
   - **Delete** (trash icon) — remove metadata + blob
6. Click the **file name** to open the viewer.

### B. Viewer (`/viewer/:id`)

1. The PDF loads in the center; **thumbnails** appear in the left strip.
2. Use the **ngx / pdf.js toolbar** for everyday viewing:
   - zoom, page navigation, find
   - print / download
   - annotation tools: highlight, draw, text, stamp, comment (and related editor buttons)
3. Mark up the document as needed.
4. Click **Save** in the top app bar to persist the current PDF (including annotations) to the server via `PUT /documents/{id}`.
5. Confirm a brief “Saved” toast (or an error message if the API is down).

### C. Page tools (structural edits)

These use **pdf-lib** in the browser (rotate / reorder / delete / merge), separate from ink-style annotations.

1. In the viewer, click **Page tools**.
2. A side panel lists pages with controls to:
   - rotate ±90°
   - move pages up / down
   - delete a page (at least one page remains)
   - merge another PDF into this document
   - export / download the edited file locally
   - save the edited bytes to the server
3. The main viewer refreshes from the in-memory preview as you edit.
4. When finished, save from Page tools and/or the app-bar **Save**, then use the rail’s Documents icon (or go to `/`) to return to the library.

### D. Optional: call the API directly

Open **http://localhost:5092/swagger** to try upload / list / download / delete without the UI. Full curl samples: [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md).

### E. Stop everything

In each terminal: `Ctrl+C`. Order does not matter. Restart later from Steps 5–7 (skip install/restore unless dependencies changed).

---

## Prompts for Cursor

Open the **PDF-Operator** repo root in Cursor Agent/Chat and paste prompts like these. Ask them **in order** the first time you set up. Adjust paths if your clone lives elsewhere.

### 1) Check what is already installed

```text
I'm new to this PDF-Operator repo. Check whether this machine has the .NET 10 SDK, Node.js/npm, and Azurite available. Report versions (or what's missing) and point me to the exact install links or commands from the root README. Do not change project files yet.
```

### 2) Install / restore project dependencies

```text
Following the root README "New here? Start here" section: restore NETCore-PDF-Service tools and NuGet packages, install Angular-PDF-App npm dependencies, and install Azurite globally if it's missing. Run the commands yourself, show me the results, and stop before starting any long-running servers.
```

### 3) Start the full local stack

```text
Start the full PDF-Operator local stack from the repo root using three terminals (or background processes): (1) azurite --skipApiVersionCheck --location .azurite --silent, (2) dotnet run --project NETCore-PDF-Service --urls http://localhost:5092, (3) npm start inside Angular-PDF-App. Confirm each is healthy (API swagger reachable, Angular on http://localhost:4200). If something fails, diagnose from the READMEs and fix it.
```

### 4) Verify build

```text
Build both sides of PDF-Operator: run backend tests with `dotnet test NETCore-PDF-Service.Tests`, then `npm run build` in Angular-PDF-App. If the Angular build OOMs, retry with NODE_OPTIONS=--max-old-space-size=8192. Summarize pass/fail only.
```

### 5) Explain how to use the UI

```text
Using the root README "How to use the app" section, walk me through uploading a PDF, opening the viewer, adding an annotation, saving, and using Page tools. Keep it step-by-step for a first-time user; don't modify code.
```

### 6) Something is broken — debug

```text
The PDF-Operator stack isn't working as expected: [describe symptom — e.g. CORS error, empty viewer, upload 400, Azurite not running]. Read the root README troubleshooting plus Angular-PDF-App/README.md and NETCore-PDF-Service/README.md, inspect running terminals, and fix or tell me the exact next command.
```

### Tips when using Cursor on this repo

- Keep the workspace root at **`PDF-Operator/`** (not only the frontend or backend folder) so Agent can see both projects and these READMEs.
- Prefer **Agent mode** for install/run commands; use Ask mode if you only want explanations.
- Durable plans for this repo live under [`CursorLog/`](CursorLog/) — say “follow CursorLog / the README” if you want Agent to stick to existing design.
- Do not ask Cursor to commit or push unless you explicitly want that.

---

## Troubleshooting (newcomers)

| Symptom | What to try |
|---|---|
| `dotnet` not found / wrong major version | Install [.NET 10 SDK](https://dotnet.microsoft.com/download); reopen the terminal |
| `azurite` not found | `npm install -g azurite` |
| Angular `npm install` fails | Use Node LTS; delete `node_modules` + lock issues only if needed, then reinstall |
| CORS errors in the browser | API must allow `http://localhost:4200` (dev `appsettings`); both UI and API must be the URLs above |
| UI loads but list/upload fails | Azurite + API must both be running |
| Viewer blank / worker 404 | See [`Angular-PDF-App/README.md`](Angular-PDF-App/README.md) (`bleeding-edge` assets + `pdfDefaultOptions.assetsFolder`) |
| Port already in use | Stop the other process on 5092 / 4200, or change URLs consistently (API URL must match `environment.development.ts`) |
| `ng build` out of memory | `$env:NODE_OPTIONS='--max-old-space-size=8192'` then `npx ng build` (PowerShell) |

More detail: [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md) · [`Angular-PDF-App/README.md`](Angular-PDF-App/README.md).

---

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
| `GET` | `/documents/{id}` | Get a single document's metadata |
| `GET` | `/documents/{id}/download` | Download a document's PDF bytes |
| `DELETE` | `/documents/{id}` | Delete a document and its underlying file |

Full request/response samples, Postman notes, configuration reference, and troubleshooting tips live in [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md).

### Running Tests

```bash
dotnet test NETCore-PDF-Service.Tests
```

## Frontend Quick Start (Angular-PDF-App)

Prerequisites: [Node.js](https://nodejs.org/) + npm. The API (+ Azurite) should be running for uploads/list/viewer to work — see above. Dev CORS allows `http://localhost:4200`.

```bash
cd Angular-PDF-App
npm install
npm start
# opens http://localhost:4200/  (ng serve, development env → API at http://localhost:5092)
```

| Area | Library / detail |
|---|---|
| Framework | Angular 21 (standalone + signals), SCSS shell |
| Viewing & annotations | [`ngx-extended-pdf-viewer`](https://www.npmjs.com/package/ngx-extended-pdf-viewer) (Mozilla **pdf.js**) — highlight, draw, text, stamp, comment, zoom, find, print, download |
| Page editing | [`pdf-lib`](https://pdf-lib.js.org/) — rotate, reorder, delete page, merge, local export (Page tools panel) |
| API client | `DocumentService` → `/documents*` (upload, list, download Blob into viewer, PUT save, delete) |
| Routes | `/` library · `/viewer/:id` viewer + Save + Page tools |

**How PDF loading works:** the viewer downloads bytes via `GET /documents/{id}/download` as a `Blob` and passes that to ngx (not the Azurite `blobUrl`, which is not browser-readable). **Save** exports the current viewer document (including annotations) and `PUT`s it back.

Full structure, env config, asset/`bleeding-edge` setup, feature checklist, and troubleshooting: [`Angular-PDF-App/README.md`](Angular-PDF-App/README.md). Design tradeoffs: [`ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md`](ArchitectureAndDesignDocument/ARCHITECTURE-And-Design.md).

### Frontend tests / build

```bash
cd Angular-PDF-App
npm test          # Vitest via ng test
npm run build     # production build → dist/
```

## Running the Full Stack

Use three terminals from the repo root (`PDF-Operator/`):

```bash
# Terminal 1 — blob emulator
azurite --skipApiVersionCheck --location .azurite --silent

# Terminal 2 — API
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092

# Terminal 3 — Angular
cd Angular-PDF-App
npm install   # first time only
npm start
```

Then open **http://localhost:4200/**. Swagger remains at **http://localhost:5092/swagger**.

| Service | URL |
|---|---|
| Angular UI | http://localhost:4200 |
| API | http://localhost:5092 |
| Swagger | http://localhost:5092/swagger |
| Azurite | local emulator (connection string in backend `appsettings`) |
