using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NETCore_PDF_Service.Data;
using NETCore_PDF_Service.Endpoints;
using NETCore_PDF_Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NETCore PDF Service API",
        Version = "v1",
        Description = "Upload, replace, list, and download PDF document metadata + content."
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? "UseDevelopmentStorage=true";
    var containerName = builder.Configuration["BlobStorage:ContainerName"] ?? "documents";

    var serviceClient = new BlobServiceClient(connectionString);
    var containerClient = serviceClient.GetBlobContainerClient(containerName);
    containerClient.CreateIfNotExists(PublicAccessType.None);

    return containerClient;
});

builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();

// Origins come from config (empty by default) rather than being hardcoded, so a real frontend
// origin can be added per-environment later without touching this file.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NETCore PDF Service API v1");
    });

    // Convenience only: point browsers hitting the bare root at the docs instead of a 404.
    // Scoped to Development since Swagger UI itself isn't mapped in other environments.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseCors("Frontend");

app.MapDocumentEndpoints();

app.Run();
