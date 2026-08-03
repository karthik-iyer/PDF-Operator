import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { LucideDownload, LucideTrash2, LucideUpload } from '@lucide/angular';

import { Document } from '../models/document.model';
import { DocumentService } from '../services/document.service';
import { downloadBlob } from '../shared/download-blob';
import { extractErrorMessage } from '../shared/http-error';

@Component({
  selector: 'app-upload-list',
  imports: [CommonModule, LucideUpload, LucideDownload, LucideTrash2],
  templateUrl: './upload-list.html',
  styleUrl: './upload-list.scss'
})
export class UploadListComponent implements OnInit {
  private readonly documentService = inject(DocumentService);
  private readonly router = inject(Router);

  protected readonly documents = signal<Document[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly isUploading = signal(false);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly downloadingId = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.refreshList();
  }

  protected refreshList(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.documentService.listDocuments().subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.isLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to load documents.'));
        this.isLoading.set(false);
      }
    });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.isUploading.set(true);
    this.errorMessage.set(null);
    this.documentService.uploadDocument(file).subscribe({
      next: (created) => {
        this.documents.update((docs) => [created, ...docs]);
        this.isUploading.set(false);
        input.value = '';
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to upload document.'));
        this.isUploading.set(false);
        input.value = '';
      }
    });
  }

  protected onDownload(doc: Document): void {
    this.downloadingId.set(doc.id);
    this.errorMessage.set(null);
    this.documentService.downloadDocument(doc.id).subscribe({
      next: (blob) => {
        downloadBlob(blob, doc.fileName);
        this.downloadingId.set(null);
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to download document.'));
        this.downloadingId.set(null);
      }
    });
  }

  protected onDelete(doc: Document): void {
    if (!confirm(`Delete "${doc.fileName}"? This cannot be undone.`)) {
      return;
    }

    this.deletingId.set(doc.id);
    this.errorMessage.set(null);
    this.documentService.deleteDocument(doc.id).subscribe({
      next: () => {
        this.documents.update((docs) => docs.filter((d) => d.id !== doc.id));
        this.deletingId.set(null);
      },
      error: (err: HttpErrorResponse) => {
        this.errorMessage.set(extractErrorMessage(err, 'Failed to delete document.'));
        this.deletingId.set(null);
      }
    });
  }

  protected onOpen(doc: Document): void {
    this.router.navigate(['/viewer', doc.id]);
  }
}
