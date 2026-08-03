/**
 * Mirrors NETCore-PDF-Service's Models/Document.cs and its camelCase JSON serialization.
 */
export interface Document {
  id: string;
  fileName: string;
  blobUrl: string;
  /** ISO 8601 string (serialized from a .NET DateTimeOffset). */
  uploadedAt: string;
}
