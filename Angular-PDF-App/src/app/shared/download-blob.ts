/**
 * Triggers a browser download for the given Blob via a temporary object URL + anchor.
 * Used by Export (full document) and Extract (single page) - never hits the server.
 */
export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
