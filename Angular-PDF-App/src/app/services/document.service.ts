import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { Document } from '../models/document.model';

/**
 * Thin HTTP data-access layer over NETCore-PDF-Service's /documents endpoints
 * (see NETCore-PDF-Service/Endpoints/DocumentEndpoints.cs). No error handling/retry
 * logic lives here by design - callers decide how to react to failures.
 */
@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly baseUrl = `${environment.apiBaseUrl}/documents`;

  constructor(private readonly http: HttpClient) {}

  uploadDocument(file: File): Observable<Document> {
    return this.http.post<Document>(this.baseUrl, this.toFormData(file));
  }

  listDocuments(): Observable<Document[]> {
    return this.http.get<Document[]>(this.baseUrl);
  }

  getDocument(id: string): Observable<Document> {
    return this.http.get<Document>(`${this.baseUrl}/${id}`);
  }

  replaceDocument(id: string, file: File): Observable<Document> {
    // Backend upserts: 200 OK if id already existed, 201 Created if it didn't - both return
    // the Document body, so no special-casing is needed here.
    return this.http.put<Document>(`${this.baseUrl}/${id}`, this.toFormData(file));
  }

  deleteDocument(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  downloadDocument(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  // Field name must be exactly "file" - it has to match the backend's
  // form.Files.GetFile("file") lookup in DocumentEndpoints.ReadAndValidateFileAsync.
  private toFormData(file: File): FormData {
    const formData = new FormData();
    formData.append('file', file);
    return formData;
  }
}
