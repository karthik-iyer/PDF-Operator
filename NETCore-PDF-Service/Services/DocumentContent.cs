namespace NETCore_PDF_Service.Services;

public sealed record DocumentContent(Stream Content, string ContentType, string FileName);
