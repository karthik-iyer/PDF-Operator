# If I Had 1 More Day — Roadmap

Stretch goals beyond the current MVP. Ordered roughly by leverage for a real multi-user product. Not a commitment — a shortlist of what would matter next.

## Roadmap

Today the MVP covers upload / list / download / delete, view + annotate + save, and page tools (rotate, reorder, delete page, merge, export/print). Everything below is stretch.

1. **Authentication & login**  
   Add real identity (e.g. ASP.NET Core Identity, Auth0, or Entra ID): login/logout, JWT or cookie sessions, protect all `/documents` routes. Stop treating every anonymous browser as a full admin of the shared library.

2. **Authorization, roles & admin**  
   Per-user document ownership (`OwnerUserId`), roles such as `User` / `Admin`, and policies so users only list/download/edit their own files. Admin UI (or Swagger-gated ops) for cross-user list, force-delete, and quarantine review.

3. **Stronger validation & abuse limits**  
   Stricter PDF sniffing (magic bytes + structure checks), max size / page-count caps, rate limiting, antivirus-friendly content-type enforcement, and clearer API error contracts. Reject truncated or non-PDF masquerading uploads before they hit blob storage.

4. **Virus / malware scanning on upload**  
   Async scan pipeline (e.g. ClamAV, Defender for Storage, or a cloud scanning API): accept upload → quarantine state → scan → mark `Clean` / `Blocked`. Block download and viewer open until clean; surface status in the Angular library.

5. **Multi-file & parallel uploads (incl. large / background)**  
   Library UI: multi-select / drag-drop many PDFs at once; fire **parallel** `POST /documents` uploads with a concurrency cap, per-file progress, and partial failure (retry failed files without redoing successes). For very large files: chunked or resumable uploads (TUS / SAS + block blobs), optional job/queue worker for big batches, and size limits that no longer require holding the entire file in one API request.

6. **Richer library & document management**  
   Rename, duplicate/copy, soft-delete (trash + restore). Folders, tags, favorites; search by filename (later full-text). Batch actions: multi-select delete, download selection as ZIP, merge selected library docs into one. Replace file in place (keep id / history) vs upload-as-new. Import from URL; time-limited share links. Optional server-rendered thumbnails so the library isn’t only a flat filename table.

7. **More structural PDF operations**  
   Beyond current page tools: split into multiple PDFs; extract pages → new document; insert blank pages; replace a page with an image; crop / resize / normalize page size; page numbers / headers / footers; flatten annotations into static content before share.

8. **Security & packaging operations**  
   Password-protect / remove password; restrict print/copy permissions; text or image watermarks (all pages); compress / linearize for smaller downloads.

9. **Interop & conversion**  
   Images → PDF; PDF → PNG/JPEG (per page or ZIP). OCR for scanned PDFs (searchable / selectable text). Form fill (AcroForm); extract embedded attachments. Compare two document versions (page diff).

10. **Deeper PDF product features (beyond today’s markup toolbar)**  
    The viewer already ships pdf.js annotation tools (highlight/color text, draw, free text, stamp, comment). Next would be capabilities that are *not* just overlays: true content-stream redaction (permanent removal), AcroForm-style signature field placement, bookmark/outline *editing* (we only show thumbnails today), and server-side version history / audit log of saves.

11. **Hardening storage & viewing at scale**  
    Short-lived SAS (or equivalent) so the browser talks to blob storage directly; HTTP range / linearized PDF loading for large docs; swap SQLite for Postgres (or similar) with concurrency on metadata. Move heavy `pdf-lib` work off the UI thread (Web Worker) where needed.

12. **Ops, tests & observability**  
    CI for API + Angular, smoke e2e (upload → open → save), structured logging/metrics/tracing, health checks, and a minimal deploy story (real Azure Storage + hosted API + static SPA) with secrets out of `appsettings`.

---

Related context and earlier tradeoffs: [`ARCHITECTURE-And-Design.md`](ARCHITECTURE-And-Design.md).  
How to run the MVP today: root [`README.md`](../README.md).
