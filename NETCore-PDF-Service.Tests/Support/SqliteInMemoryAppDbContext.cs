using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NETCore_PDF_Service.Data;

namespace NETCore_PDF_Service.Tests.Support;

/// <summary>
/// Wraps a real <see cref="AppDbContext"/> backed by an open SQLite ":memory:" connection.
/// Using the same provider as production (rather than the EF Core InMemory provider) means
/// tests exercise real SQLite behavior/quirks - which already caught a real bug
/// (DateTimeOffset ORDER BY is not supported server-side).
/// </summary>
public sealed class SqliteInMemoryAppDbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Context { get; }

    public SqliteInMemoryAppDbContext()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
