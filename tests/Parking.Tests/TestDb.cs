using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;

namespace Parking.Tests;

// Helper para criar um AppDbContext usando o provedor InMemory,
// isolando cada teste em um banco próprio (nome único por instância).
public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // O InMemory não suporta índices/extensões do Postgres; ignoramos os avisos.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
