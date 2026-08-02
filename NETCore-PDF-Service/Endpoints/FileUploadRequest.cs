namespace NETCore_PDF_Service.Endpoints;

/// <summary>
/// Shape-only DTO used purely so Swagger/OpenAPI generates an object schema with a named
/// "file" property for multipart/form-data bodies (see <see cref="DocumentEndpoints"/>).
/// The endpoints themselves never bind to this type - they read the file manually from
/// <c>HttpContext.Request.Form</c> - but without it Swashbuckle describes the body as a bare
/// <c>string/binary</c> schema, which gives Swagger UI's file picker no field name to send the
/// upload under, so the server-side <c>form.Files.GetFile("file")</c> lookup never finds it.
/// </summary>
internal sealed class FileUploadRequest
{
    public IFormFile? File { get; set; }
}
