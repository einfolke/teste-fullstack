using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Models;
using Testcontainers.PostgreSql;
using Xunit;

namespace Parking.Tests;

public class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool Disponivel { get; private set; }
    public string? MotivoIndisponivel { get; private set; }
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:17-alpine")
                .Build();

            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();

            using var db = NewContext();
            await db.Database.EnsureCreatedAsync();

            Disponivel = true;
        }
        catch (Exception ex)
        {
            Disponivel = false;
            MotivoIndisponivel = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}

public class IntegracaoPostgresTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public IntegracaoPostgresTests(PostgresFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Indice_unico_nome_telefone_e_aplicado_pelo_banco()
    {
        Skip.IfNot(_fixture.Disponivel, $"Docker indisponível: {_fixture.MotivoIndisponivel}");

        using var db = _fixture.NewContext();
        var telefone = "319" + new Random().Next(10000000, 99999999);

        db.Clientes.Add(new Cliente { Nome = "Duplicado", Telefone = telefone });
        await db.SaveChangesAsync();

        db.Clientes.Add(new Cliente { Nome = "Duplicado", Telefone = telefone });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task DateTime_unspecified_lanca_e_utc_funciona()
    {
        Skip.IfNot(_fixture.Disponivel, $"Docker indisponível: {_fixture.MotivoIndisponivel}");

        using (var db = _fixture.NewContext())
        {
            db.Associacoes.Add(new VeiculoClienteAssociacao
            {
                VeiculoId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                DataInicio = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Unspecified)
            });

            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        using (var db = _fixture.NewContext())
        {
            db.Associacoes.Add(new VeiculoClienteAssociacao
            {
                VeiculoId = Guid.NewGuid(),
                ClienteId = Guid.NewGuid(),
                DataInicio = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            var afetados = await db.SaveChangesAsync();
            Assert.Equal(1, afetados);
        }
    }
}
