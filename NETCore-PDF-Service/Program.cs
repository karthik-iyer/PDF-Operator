using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Data;
using NETCore_PDF_Service.Endpoints;
using NETCore_PDF_Service.Services;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => "Hello World!");

app.MapDocumentEndpoints();

app.Run();
