using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Models;

namespace NETCore_PDF_Service.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(512);
            entity.Property(d => d.BlobUrl).IsRequired().HasMaxLength(2048);
            entity.Property(d => d.UploadedAt).IsRequired();
            entity.HasIndex(d => d.UploadedAt);
        });
    }
}
