import { Injectable, signal } from '@angular/core';
import { PDFDocument, degrees } from 'pdf-lib';

/**
 * Owns the in-memory pdf-lib PDFDocument being edited plus the live-preview Blob derived
 * from it. Pure editing logic - no HTTP/Router dependencies, so it stays testable in
 * isolation and reusable regardless of which component drives it (see EditorPanelComponent
 * and Viewer).
 *
 * The PDFDocument instance itself is intentionally NOT a signal - pdf-lib mutates it in
 * place, and signals only notify on reassignment. Every mutating method finishes by calling
 * syncPreview(), which re-serializes the document and updates the public signals so
 * templates re-render.
 */
@Injectable({ providedIn: 'root' })
export class EditorStateService {
  private pdfDocument: PDFDocument | null = null;

  readonly currentBlob = signal<Blob | null>(null);
  readonly pageRotations = signal<number[]>([]);
  readonly isBusy = signal(false);
  readonly errorMessage = signal<string | null>(null);

  async loadFromBlob(blob: Blob): Promise<void> {
    this.isBusy.set(true);
    this.errorMessage.set(null);
    try {
      const bytes = await blob.arrayBuffer();
      this.pdfDocument = await PDFDocument.load(bytes);
      await this.syncPreview();
    } catch {
      this.errorMessage.set('Failed to load document for editing.');
    } finally {
      this.isBusy.set(false);
    }
  }

  async rotatePage(index: number, delta: 90 | -90): Promise<void> {
    if (!this.pdfDocument) {
      return;
    }
    const page = this.pdfDocument.getPages()[index];
    if (!page) {
      return;
    }
    const current = page.getRotation().angle;
    const next = ((current + delta) % 360 + 360) % 360;
    page.setRotation(degrees(next));
    await this.syncPreview();
  }

  /** Swaps two pages by index - the primitive behind the up/down reorder buttons. */
  async swapPages(indexA: number, indexB: number): Promise<void> {
    if (!this.pdfDocument || indexA === indexB) {
      return;
    }
    const pages = this.pdfDocument.getPages();
    const pageA = pages[indexA];
    const pageB = pages[indexB];
    if (!pageA || !pageB) {
      return;
    }

    const [lower, higher] = indexA < indexB ? [indexA, indexB] : [indexB, indexA];
    const lowerPage = pages[lower];
    const higherPage = pages[higher];

    // Remove the higher index first so removing the lower index isn't thrown off by a shift.
    this.pdfDocument.removePage(higher);
    this.pdfDocument.removePage(lower);
    this.pdfDocument.insertPage(lower, higherPage);
    this.pdfDocument.insertPage(higher, lowerPage);

    await this.syncPreview();
  }

  async deletePage(index: number): Promise<void> {
    if (!this.pdfDocument || this.pdfDocument.getPageCount() <= 1) {
      return;
    }
    this.pdfDocument.removePage(index);
    await this.syncPreview();
  }

  /** Exports a single page as its own standalone PDF Blob, without mutating the main document. */
  async extractPage(index: number): Promise<Blob> {
    if (!this.pdfDocument) {
      throw new Error('No document loaded to extract from.');
    }
    const extractedDoc = await PDFDocument.create();
    const [copiedPage] = await extractedDoc.copyPages(this.pdfDocument, [index]);
    extractedDoc.addPage(copiedPage);
    const bytes = await extractedDoc.save();
    return this.bytesToPdfBlob(bytes);
  }

  /** Appends every page from the given file to the end of the current document. */
  async mergePdf(file: File): Promise<void> {
    if (!this.pdfDocument) {
      return;
    }
    this.isBusy.set(true);
    this.errorMessage.set(null);
    try {
      const bytes = await file.arrayBuffer();
      const importedDoc = await PDFDocument.load(bytes);
      const copiedPages = await this.pdfDocument.copyPages(importedDoc, importedDoc.getPageIndices());
      copiedPages.forEach((page) => this.pdfDocument!.addPage(page));
      await this.syncPreview();
    } catch {
      this.errorMessage.set('Failed to merge PDF. Make sure the file is a valid PDF.');
    } finally {
      this.isBusy.set(false);
    }
  }

  /** Serializes the current state and wraps it in a File, ready for DocumentService.replaceDocument(). */
  async exportAsFile(fileName: string): Promise<File> {
    await this.syncPreview();
    const blob = this.currentBlob();
    if (!blob) {
      throw new Error('No document loaded to export.');
    }
    return new File([blob], fileName, { type: 'application/pdf' });
  }

  reset(): void {
    this.pdfDocument = null;
    this.currentBlob.set(null);
    this.pageRotations.set([]);
    this.errorMessage.set(null);
  }

  private async syncPreview(): Promise<void> {
    if (!this.pdfDocument) {
      return;
    }
    const bytes = await this.pdfDocument.save();
    this.currentBlob.set(this.bytesToPdfBlob(bytes));
    this.pageRotations.set(this.pdfDocument.getPages().map((page) => page.getRotation().angle));
  }

  /**
   * pdf-lib's save() returns a Uint8Array typed against ArrayBufferLike, which TS's DOM lib
   * doesn't accept directly as a BlobPart (it wants a plain ArrayBuffer). Slicing the
   * underlying buffer to the view's exact byte range sidesteps the type mismatch and is safe
   * regardless of whether the Uint8Array happens to span a larger buffer.
   */
  private bytesToPdfBlob(bytes: Uint8Array): Blob {
    const arrayBuffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
    return new Blob([arrayBuffer], { type: 'application/pdf' });
  }
}
