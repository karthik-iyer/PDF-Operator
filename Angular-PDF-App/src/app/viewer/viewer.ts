import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { LucideLayers, LucideSave } from '@lucide/angular';
import {
  NgxExtendedPdfViewerModule,
  NgxExtendedPdfViewerService,
  PagesLoadedEvent,
  PdfSidebarView,
  ZoomType
} from 'ngx-extended-pdf-viewer';
import { map } from 'rxjs';

import { EditorPanelComponent } from '../editor-panel/editor-panel';
import { Document } from '../models/document.model';
import { DocumentService } from '../services/document.service';
import { EditorStateService } from '../services/editor-state.service';
import { extractErrorMessage } from '../shared/http-error';

/**
 * Loads the PDF via DocumentService.downloadDocument() as a Blob and passes it to
 * ngx-extended-pdf-viewer. Marking/highlight/draw/text/stamp tools come from the
 * library toolbar. Save exports the current viewer document (including annotations)
 * via getCurrentDocumentAsBlob() and PUTs it back through DocumentService.
 *
 * "Page tools" opens the pdf-lib editor panel (rotate/reorder/merge/export) separately.
 */
@Component({
  selector: 'app-viewer',
  imports: [NgxExtendedPdfViewerModule, EditorPanelComponent, LucideSave, LucideLayers],
  templateUrl: './viewer.html',
  styleUrl: './viewer.scss'
})
export class Viewer {
  private readonly route = inject(ActivatedRoute);
  private readonly documentService = inject(DocumentService);
  private readonly pdfViewerService = inject(NgxExtendedPdfViewerService);
  protected readonly editorState = inject(EditorStateService);

  protected readonly id = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('id'))),
    { initialValue: null }
  );

  protected readonly pdfSrc = signal<Blob | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly documentMeta = signal<Document | null>(null);
  protected readonly editMode = signal(false);

  protected readonly page = signal<number | undefined>(1);
  protected readonly pageCount = signal(0);
  protected readonly zoom = signal<ZoomType>('page-width');
  protected readonly findbarVisible = signal(false);
  protected readonly sidebarVisible = signal(true);
  protected readonly activeSidebarView = signal(PdfSidebarView.THUMBS);

  protected readonly isSaving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);

  /** Live preview from pdf-lib page tools when active; otherwise the downloaded/saved Blob. */
  protected readonly displaySrc = computed(() => this.editorState.currentBlob() ?? this.pdfSrc());

  constructor() {
    effect(() => {
      const id = this.id();
      if (!id) {
        return;
      }
      this.editorState.reset();
      this.editMode.set(false);
      this.page.set(1);
      this.pageCount.set(0);
      this.zoom.set('page-width');
      this.findbarVisible.set(false);
      this.sidebarVisible.set(true);
      this.activeSidebarView.set(PdfSidebarView.THUMBS);
      this.saveError.set(null);
      this.saveSuccess.set(false);
      this.loadDocument(id);
    });
  }

  protected async toggleEditMode(): Promise<void> {
    const enteringEditMode = !this.editMode();
    this.editMode.set(enteringEditMode);

    if (enteringEditMode) {
      // Capture any in-viewer annotations before handing bytes to pdf-lib.
      const annotated = await this.pdfViewerService.getCurrentDocumentAsBlob();
      const source = annotated ?? this.pdfSrc();
      if (source) {
        await this.editorState.loadFromBlob(source);
      }
    }
  }

  protected onSaved(doc: Document): void {
    this.documentMeta.set(doc);
    // Page-tools Save already PUTed; keep the local download source in sync so leaving
    // page-tools mode doesn't flash stale pre-edit bytes.
    const edited = this.editorState.currentBlob();
    if (edited) {
      this.pdfSrc.set(edited);
    }
  }

  /**
   * Persist the currently rendered PDF — including highlight/draw/text/stamp/comment
   * annotations — back to the same document id via PUT.
   */
  protected async saveToServer(): Promise<void> {
    const id = this.id();
    if (!id) {
      return;
    }

    this.isSaving.set(true);
    this.saveError.set(null);
    this.saveSuccess.set(false);

    try {
      // Prefer pdf-lib bytes if page tools have mutated the doc; otherwise export the
      // viewer document so annotations are included.
      let blob: Blob | null = null;
      if (this.editorState.currentBlob()) {
        blob = this.editorState.currentBlob();
      } else {
        blob = (await this.pdfViewerService.getCurrentDocumentAsBlob()) ?? null;
      }

      if (!blob) {
        this.saveError.set('Nothing to save yet — wait for the PDF to finish loading.');
        this.isSaving.set(false);
        return;
      }

      const fileName = this.documentMeta()?.fileName ?? 'document.pdf';
      const file = new File([blob], fileName, { type: 'application/pdf' });

      this.documentService.replaceDocument(id, file).subscribe({
        next: (doc) => {
          this.documentMeta.set(doc);
          this.pdfSrc.set(blob);
          // Drop the pdf-lib working copy so displaySrc reads the saved Blob; next page-tools
          // session reloads from the annotated/saved bytes.
          if (!this.editMode()) {
            this.editorState.reset();
          }
          this.isSaving.set(false);
          this.saveSuccess.set(true);
          window.setTimeout(() => this.saveSuccess.set(false), 2500);
        },
        error: (err: HttpErrorResponse) => {
          this.saveError.set(extractErrorMessage(err, 'Failed to save document.'));
          this.isSaving.set(false);
        }
      });
    } catch {
      this.saveError.set('Failed to prepare the PDF for saving.');
      this.isSaving.set(false);
    }
  }

  protected onPagesLoaded(event: PagesLoadedEvent): void {
    this.pageCount.set(event.pagesCount);
  }

  private loadDocument(id: string): void {
    this.pdfSrc.set(null);
    this.errorMessage.set(null);
    this.isLoading.set(true);

    this.documentService.downloadDocument(id).subscribe({
      next: (blob) => {
        this.pdfSrc.set(blob);
        this.isLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to load document.'));
        this.isLoading.set(false);
      }
    });

    this.documentService.getDocument(id).subscribe({
      next: (doc) => this.documentMeta.set(doc),
      error: () => this.documentMeta.set(null)
    });
  }
}
