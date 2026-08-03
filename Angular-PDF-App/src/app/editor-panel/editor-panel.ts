import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';

import { Document } from '../models/document.model';
import { DocumentService } from '../services/document.service';
import { EditorStateService } from '../services/editor-state.service';
import { downloadBlob } from '../shared/download-blob';
import { extractErrorMessage } from '../shared/http-error';

interface PageRow {
  index: number;
  pageNumber: number;
  rotation: number;
}

/**
 * Toolbar + page list + Save button for Phase 4's editor mode. Injects EditorStateService
 * and DocumentService directly rather than routing everything through @Input/@Output,
 * consistent with UploadListComponent's existing style for shared singleton state.
 */
@Component({
  selector: 'app-editor-panel',
  imports: [CommonModule],
  templateUrl: './editor-panel.html',
  styleUrl: './editor-panel.scss'
})
export class EditorPanelComponent {
  @Input({ required: true }) documentId!: string;
  @Input() fileName = 'document.pdf';
  @Output() saved = new EventEmitter<Document>();

  protected readonly editorState = inject(EditorStateService);
  private readonly documentService = inject(DocumentService);

  protected readonly isSaving = signal(false);
  protected readonly isExporting = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);
  protected readonly exportError = signal<string | null>(null);

  protected readonly pages = computed<PageRow[]>(() =>
    this.editorState.pageRotations().map((rotation, index) => ({
      index,
      pageNumber: index + 1,
      rotation
    }))
  );

  protected rotate(index: number, delta: 90 | -90): void {
    this.editorState.rotatePage(index, delta);
  }

  protected moveUp(index: number): void {
    if (index <= 0) {
      return;
    }
    this.editorState.swapPages(index, index - 1);
  }

  protected moveDown(index: number): void {
    if (index >= this.pages().length - 1) {
      return;
    }
    this.editorState.swapPages(index, index + 1);
  }

  protected deletePage(index: number): void {
    if (this.pages().length <= 1) {
      return;
    }
    if (!confirm(`Delete page ${index + 1}? This cannot be undone.`)) {
      return;
    }
    this.editorState.deletePage(index);
  }

  protected async extractPage(index: number): Promise<void> {
    const blob = await this.editorState.extractPage(index);
    downloadBlob(blob, `page-${index + 1}.pdf`);
  }

  /**
   * Local-only download of the current in-memory PDFDocument. Does not call the API —
   * use Save to persist edits server-side.
   */
  protected async exportLocal(): Promise<void> {
    this.isExporting.set(true);
    this.exportError.set(null);
    try {
      const file = await this.editorState.exportAsFile(this.fileName);
      downloadBlob(file, this.fileName);
    } catch {
      this.exportError.set('Failed to export document.');
    } finally {
      this.isExporting.set(false);
    }
  }

  protected onMergeFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.editorState.mergePdf(file);
    input.value = '';
  }

  protected save(): void {
    this.isSaving.set(true);
    this.saveError.set(null);
    this.saveSuccess.set(false);

    this.editorState
      .exportAsFile(this.fileName)
      .then((file) =>
        this.documentService.replaceDocument(this.documentId, file).subscribe({
          next: (doc) => {
            this.isSaving.set(false);
            this.saveSuccess.set(true);
            this.saved.emit(doc);
          },
          error: (err: HttpErrorResponse) => {
            this.saveError.set(extractErrorMessage(err, 'Failed to save document.'));
            this.isSaving.set(false);
          }
        })
      )
      .catch(() => {
        this.saveError.set('Failed to prepare document for saving.');
        this.isSaving.set(false);
      });
  }
}
