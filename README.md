# PDF-Operator

A web app to view, manipulate, and perform other operations on PDF documents.

> Living document — update it alongside [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md) and [`Angular-PDF-App/README.md`](Angular-PDF-App/README.md) whenever setup steps, endpoints, or frontend run details change.

If you are **new to this repo** — including a **fresh machine with no Git, .NET, or Node** — start with [New here? Start here](#new-here-start-here) (Phase 0 installs the tools), then [How to use the app](#how-to-use-the-app-step-by-step). Prefer to let [Cursor](https://cursor.com/) drive setup? Jump to [Prompts for Cursor](#prompts-for-cursor).

## Repo Structure

| Path | What it is |
|---|---|
| [`NETCore-PDF-Service/`](NETCore-PDF-Service/) | .NET 10 Minimal API backend — PDF metadata (SQLite) + file storage (Azure Blob Storage / Azurite). **See its [README](NETCore-PDF-Service/README.md) for full setup, all endpoints, and sample payloads.** |
| `NETCore-PDF-Service.Tests/` | xUnit + Moq unit tests for the backend |
| [`Angular-PDF-App/`](Angular-PDF-App/) | Angular 21 SPA — document library, pdf.js viewer/annotations (`ngx-extended-pdf-viewer`), client-side page edits (`pdf-lib`). **See its [README](Angular-PDF-App/README.md) for full frontend docs.** |
| `ArchitectureAndDesignDocument/` | High-level architecture & design notes, plus [`IF-I-HAD-1-MORE-DAY.md`](ArchitectureAndDesignDocument/IF-I-HAD-1-MORE-DAY.md) stretch roadmap |
| `CursorLog/` | Implementation plans / design decisions for each feature, in chronological order |

---

## New here? Start here

This is the full path from a **fresh machine** (no .NET, no Node, no Git assumed) to a working local stack and a first PDF edit.

You need admin rights (or permission to install software) for Phase 0. After each installer, **close and reopen** the terminal (and Cursor) so `PATH` updates take effect.

### What you will run (after tools are installed)

| # | Process | Why |
|---|---|---|
| 1 | **Azurite** | Local Azure Blob Storage emulator — stores PDF file bytes |
| 2 | **NETCore-PDF-Service** | API on `http://localhost:5092` — metadata (SQLite) + blob access |
| 3 | **Angular-PDF-App** | UI on `http://localhost:4200` — upload, view, annotate, page tools |

### Phase 0 — Install system tools (once per machine)

Do these **in order**. Pick your OS. You only need a browser for the downloaders below.

#### 0a. Git

Needed to clone the repository.

| OS | Install |
|---|---|
| **Windows** | Download [Git for Windows](https://git-scm.com/download/win) → run installer → accept defaults. Or in an elevated PowerShell: `winget install --id Git.Git -e --source winget` |
| **macOS** | Install [Xcode Command Line Tools](https://developer.apple.com/xcode/resources/) (`xcode-select --install`) or [Git](https://git-scm.com/download/mac). Or: `brew install git` if you use Homebrew |
| **Linux** | e.g. `sudo apt update && sudo apt install -y git` (Debian/Ubuntu) |

Verify in a **new** terminal:

```bash
git --version
```

#### 0b. .NET 10 SDK

Needed to restore, build, and run the API.

1. Open [.NET downloads](https://dotnet.microsoft.com/download).
2. Install the **.NET 10 SDK** (not only the runtime).
3. Windows alternative (elevated PowerShell / Terminal):

   ```powershell
   winget install Microsoft.DotNet.SDK.10
   ```

4. Close the terminal completely, open a new one, verify:

   ```bash
   dotnet --version
   ```

   You want a **10.x** version (this repo has been developed against `10.0.201`). If `dotnet` is not found, reboot once or confirm the SDK’s install path is on `PATH`.

#### 0c. Node.js (includes npm)

Needed for Azurite and the Angular app.

1. Open [Node.js downloads](https://nodejs.org/) and install the current **LTS** build (includes npm).
2. Windows alternative:

   ```powershell
   winget install OpenJS.NodeJS.LTS
   ```

3. New terminal, verify:

   ```bash
   node -v
   npm -v
   ```

#### 0d. Editor (optional but recommended)

- [Cursor](https://cursor.com/) — AI-assisted IDE; see [Prompts for Cursor](#prompts-for-cursor)
- Or [VS Code](https://code.visualstudio.com/)

#### 0e. Browser

Any modern browser (Edge, Chrome, Firefox, Safari) to open the UI and Swagger.

#### 0f. Sanity check (everything missing → everything present)

In a fresh terminal, all of these should print versions (not “command not found”):

```bash
git --version
dotnet --version
node -v
npm -v
```

If any fail, finish that install and open a **new** terminal before continuing.

---

### Phase 1 — Get the code

```bash
git clone <your-remote-url> PDF-Operator
cd PDF-Operator
```

Open the **`PDF-Operator`** folder as the workspace root in Cursor/VS Code (File → Open Folder).

If you already have a zip of the repo, extract it and `cd` into that folder instead of cloning.

---

### Phase 2 — Install project tooling & packages

#### 2a. Azurite (global npm package, once)

Azurite is the local blob storage emulator. It is **not** bundled with Node; install it after Node works:

```bash
npm install -g azurite
azurite --version
```

If `azurite` is not found after install, close/reopen the terminal or ensure npm’s global bin directory is on `PATH`.

#### 2b. Backend restore

```bash
cd NETCore-PDF-Service
dotnet tool restore
dotnet restore
cd ..
```

`dotnet tool restore` installs the pinned `dotnet-ef` local tool. EF Core migrations apply automatically when the API starts — you do not need to run them by hand for a normal first run.

#### 2c. Frontend packages

```bash
cd Angular-PDF-App
npm install
cd ..
```

This can take a few minutes the first time.

---

### Phase 3 — Run the stack (three terminals)

Keep all three running while you use the app. From the **repo root** (`PDF-Operator/`) unless noted.

#### Terminal 1 — Azurite

```bash
azurite --skipApiVersionCheck --location .azurite --silent
```

Leave it running. Local blob data goes under `.azurite/`.

#### Terminal 2 — API

```bash
dotnet run --project NETCore-PDF-Service --urls http://localhost:5092
```

Confirm:

- Console shows the app listening
- **http://localhost:5092/swagger** opens in the browser

First run may download NuGet packages and take longer.

#### Terminal 3 — Angular UI

```bash
cd Angular-PDF-App
npm start
```

Open **http://localhost:4200/**. Dev config points the UI at `http://localhost:5092`; CORS allows `http://localhost:4200`.

#### Smoke-check

1. UI loads at http://localhost:4200 (document library).
2. Swagger loads at http://localhost:5092/swagger.
3. Upload a small PDF from the UI — see [How to use the app](#how-to-use-the-app-step-by-step).

If something fails, see [Troubleshooting](#troubleshooting-newcomers) or the project READMEs.

**Next day shortcut:** Phase 0 and Phase 2 are already done — only Phase 3 (three terminals).

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

### B. Viewer page (`/viewer/:id`) — full feature map

Open a document from the library. Everything below is available on this page.

#### App chrome (above the PDF)

| Control | What it does |
|---|---|
| **Documents** (coral back button) | Returns to the library (`/`). Always visible, including while loading or on error. |
| File name | Shows the current document’s name (from API metadata). |
| Status toast | Brief **Saved** confirmation, or an error message after a failed save. |
| **Save** (app bar) | Exports the current viewer PDF **including annotations** (`getCurrentDocumentAsBlob`) and uploads it with `PUT /documents/{id}`. Prefers pdf-lib bytes if Page tools have already mutated the file. |
| **Page tools** | Toggles the right-hand structural editor panel (pdf-lib). Active state is highlighted. |

The left **icon rail** (shell) also has a Documents home icon — same destination as the back button.

#### Thumbnail strip (left of the canvas)

- Always forced to **thumbnail** view (outline/attachments/layers chrome is hidden).
- Click a thumbnail to jump to that page; selected page is highlighted.

#### ngx-extended-pdf-viewer / pdf.js toolbar (enabled options)

These come from the PDF plugin toolbar (not our custom app bar):

| Area | Features turned on in this app |
|---|---|
| **Annotations / editors** | Highlight (color text), Draw (ink), Text (free text), Stamp, Comment — via editor buttons |
| **Zoom** | Zoom in / out / presets (`showZoomButtons`) |
| **Paging** | Previous / next page, page number field (`showPagingButtons`, `showPreviousAndNextPageButtons`, `showPageNumber`) |
| **Find** | In-document text search (`showFindButton`) |
| **Print** | Browser print of the PDF (`showPrintButton`) |
| **Download** | Download the current viewer file locally (`showDownloadButton`) — does **not** write to the server; use app-bar **Save** to persist |
| **Scroll / layout** | Scrolling mode controls (`showScrollingButtons`) |
| **Spread** | Single / two-page spread (`showSpreadButton`) |
| **Book mode** | Book-style presentation (`showBookModeButton`) |

Sidebar toggle from the plugin is **off** (`showSidebarButton=false`); we keep our own thumbnail strip open instead.

Typical flow: mark up with highlight/draw/text/stamp/comment → click app-bar **Save** → confirm the toast.

#### Page tools panel (structural edits — pdf-lib)

Click **Page tools**. A side panel opens. Edits happen in the browser; the main viewer live-previews the working copy.

| Control | What it does |
|---|---|
| **Merge PDF…** | Pick another PDF; its pages are appended into the working document |
| **⟲ / ⟳** | Rotate that page left / right by 90° |
| **↑ / ↓** | Reorder — move page up or down |
| **⇩ Extract** | Download that single page as its own PDF |
| **✕ Delete** | Remove the page (disabled when only one page remains) |
| Rotation label | Shows current rotation angle when non-zero |
| **Export** | Download the **entire** edited PDF to your machine (local only; no API write) |
| **Save** (panel) | Upload the pdf-lib working copy to the server (`PUT /documents/{id}`) |

When finished, use panel **Save** and/or app-bar **Save**, then **Documents** to return to the library.

**Mental model:** plugin toolbar = view + markup; Page tools = page structure; **Export** = local file; **Save** = persist to the API.

### D. Optional: call the API directly

Open **http://localhost:5092/swagger** to try upload / list / download / delete without the UI. Full curl samples: [`NETCore-PDF-Service/README.md`](NETCore-PDF-Service/README.md).

### E. Stop everything

In each terminal: `Ctrl+C`. Order does not matter. Restart later with Phase 3 only (skip Phase 0–2 unless you changed dependencies or wiped `node_modules`).

---

## Prompts for Cursor

Assume a **fresh machine** unless you know tools are already installed. Open the **PDF-Operator** repo root in Cursor Agent/Chat and paste these **in order**.

Cursor can run many terminal commands for you, but **OS installers** (.NET / Node / Git) often need you to approve a `winget`/installer prompt, enter an admin password, or finish a GUI wizard — then **reload the window** so Agent sees the new `PATH`.

### 0) Fresh machine — install Git, .NET 10 SDK, and Node LTS

```text
This is a fresh machine for PDF-Operator: I may have no Git, no .NET, and no Node. Follow the root README section "Phase 0 — Install system tools". Detect my OS. Check git / dotnet / node / npm. For anything missing, give me the exact install steps (prefer winget on Windows, otherwise official download links). If you can run winget/install commands safely, do that and tell me when I must approve UAC or reopen Cursor. After installs, verify all four commands print versions. Do not clone or restore the project until those checks pass.
```

### 1) Confirm toolchain, then clone/open if needed

```text
Verify git, dotnet (--version should be 10.x), node, and npm work in this environment. If the PDF-Operator repo is not already the workspace root, tell me exactly how to clone it and open the folder in Cursor per the root README Phase 1. Do not start servers yet.
```

### 2) Install Azurite + restore backend + npm install frontend

```text
Following root README Phase 2: install Azurite globally with npm if missing, run dotnet tool restore and dotnet restore in NETCore-PDF-Service, then npm install in Angular-PDF-App. Run the commands yourself, show results, and stop before starting any long-running servers.
```

### 3) Start the full local stack

```text
Start the full PDF-Operator local stack from the repo root (README Phase 3): (1) azurite --skipApiVersionCheck --location .azurite --silent, (2) dotnet run --project NETCore-PDF-Service --urls http://localhost:5092, (3) npm start inside Angular-PDF-App. Confirm each is healthy (Swagger at http://localhost:5092/swagger, UI at http://localhost:4200). If something fails, diagnose from the READMEs and fix it.
```

### 4) Verify build / tests

```text
Build both sides of PDF-Operator: run `dotnet test NETCore-PDF-Service.Tests`, then `npm run build` in Angular-PDF-App. If the Angular build OOMs, retry with NODE_OPTIONS=--max-old-space-size=8192. Summarize pass/fail only.
```

### 5) Explain how to use the UI

```text
Using the root README "How to use the app" section, walk me through uploading a PDF, opening the viewer, adding an annotation, saving, and using Page tools. Keep it step-by-step for a first-time user; don't modify code.
```

### 6) Something is broken — debug

```text
The PDF-Operator stack isn't working as expected: [describe symptom — e.g. dotnet not found, CORS error, empty viewer, upload 400, Azurite not running]. Read the root README troubleshooting plus Angular-PDF-App/README.md and NETCore-PDF-Service/README.md, inspect running terminals, and fix or tell me the exact next command. If a tool is missing from PATH, walk me through Phase 0 again.
```

### One-shot prompt (optional)

If you want a single message after opening an empty folder / the repo:

```text
I have a fresh machine and the PDF-Operator repo (or will clone it). Treat me as having no Git/.NET/Node unless checks prove otherwise. Follow the root README from Phase 0 through Phase 3: install system tools, restore dependencies, start Azurite + API + Angular, and tell me when to open http://localhost:4200. Pause and ask me whenever an installer needs manual approval.
```

### Tips when using Cursor on this repo

- Keep the workspace root at **`PDF-Operator/`** (not only the frontend or backend folder) so Agent can see both projects and these READMEs.
- After installing Git / .NET / Node, use **Developer: Reload Window** (or restart Cursor) before asking Agent to continue — otherwise it may still not see `dotnet` / `node`.
- Prefer **Agent mode** for install/run commands; use Ask mode if you only want explanations.
- Durable plans live under [`CursorLog/`](CursorLog/) — say “follow CursorLog / the README” if you want Agent to stick to existing design.
- Do not ask Cursor to commit or push unless you explicitly want that.

---

## Troubleshooting (newcomers)

| Symptom | What to try |
|---|---|
| `git` / `dotnet` / `node` / `npm` not found | Finish [Phase 0](#phase-0--install-system-tools-once-per-machine); **new** terminal or reload Cursor; reboot if PATH still stale |
| `dotnet` exists but not 10.x | Install [.NET 10 SDK](https://dotnet.microsoft.com/download) (SDK, not runtime-only). Multiple SDKs can coexist |
| `winget` not found (Windows) | Use the official installer `.exe` links in Phase 0 instead |
| Installer finished but command still missing | Close all terminals + Cursor, reopen; on Windows check “Add to PATH” was enabled |
| `azurite` not found | Need Node first, then `npm install -g azurite`; reopen terminal |
| Angular `npm install` fails | Confirm Node LTS with `node -v`; retry; corporate proxy may need npm config |
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
