using Parking.Api.Models;
using Parking.Api.Services;
using Xunit;

namespace Parking.Tests;

public class FaturamentoServiceTests
{
    private const string Competencia = "2025-08"; // 31 dias

    private static Guid AddCliente(Parking.Api.Data.AppDbContext db, bool mensalista, decimal? valor, bool ativo = true)
    {
        var c = new Cliente
        {
            Nome = "Cliente " + Guid.NewGuid().ToString("N")[..6],
            Mensalista = mensalista,
            ValorMensalidade = valor,
            Ativo = ativo
        };
        db.Clientes.Add(c);
        return c.Id;
    }

    private static Guid AddVeiculo(Parking.Api.Data.AppDbContext db, Guid clienteId, bool ativo = true)
    {
        var v = new Veiculo
        {
            Placa = "ABC" + new Random().Next(1000, 9999),
            ClienteId = clienteId,
            Ativo = ativo
        };
        db.Veiculos.Add(v);
        return v.Id;
    }

    private static void AddAssociacao(Parking.Api.Data.AppDbContext db, Guid veiculoId, Guid clienteId, DateTime inicio, DateTime? fim)
    {
        db.Associacoes.Add(new VeiculoClienteAssociacao
        {
            VeiculoId = veiculoId,
            ClienteId = clienteId,
            DataInicio = inicio,
            DataFim = fim
        });
    }

    [Fact]
    public async Task Mes_completo_cobra_valor_integral()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: true, valor: 310m); // 310/31 = 10/dia
        var veic = AddVeiculo(db, cli);
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), null);
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        var fatura = Assert.Single(criadas);
        Assert.Equal(310m, fatura.Valor); // 31 dias * 10
    }

    [Fact]
    public async Task Periodo_parcial_cobra_proporcional_aos_dias()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: true, valor: 310m); // 10/dia
        var veic = AddVeiculo(db, cli);
        // Associado de 01 a 17/08 = 17 dias
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), new DateTime(2025, 8, 17));
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        var fatura = Assert.Single(criadas);
        Assert.Equal(170m, fatura.Valor); // 17 dias * 10
    }

    [Fact]
    public async Task Troca_de_cliente_no_meio_do_mes_divide_os_dias_entre_os_clientes()
    {
        using var db = TestDb.Create();
        var joao = AddCliente(db, mensalista: true, valor: 310m);   // 10/dia
        var maria = AddCliente(db, mensalista: true, valor: 620m);  // 20/dia
        var veic = AddVeiculo(db, joao);
        AddAssociacao(db, veic, joao, new DateTime(2025, 8, 1), new DateTime(2025, 8, 17));  // 17 dias
        AddAssociacao(db, veic, maria, new DateTime(2025, 8, 18), null);                     // 14 dias
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        var fJoao = Assert.Single(criadas, f => f.ClienteId == joao);
        var fMaria = Assert.Single(criadas, f => f.ClienteId == maria);
        Assert.Equal(170m, fJoao.Valor);  // 17 * 10
        Assert.Equal(280m, fMaria.Valor); // 14 * 20
    }

    [Fact]
    public async Task Cliente_inativo_nao_gera_fatura()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: true, valor: 310m, ativo: false);
        var veic = AddVeiculo(db, cli);
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), null);
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        Assert.Empty(criadas);
    }

    [Fact]
    public async Task Veiculo_inativo_nao_e_cobrado()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: true, valor: 310m);
        var veic = AddVeiculo(db, cli, ativo: false);
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), null);
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        Assert.Empty(criadas); // único veículo do cliente está inativo
    }

    [Fact]
    public async Task Cliente_nao_mensalista_nao_gera_fatura()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: false, valor: null);
        var veic = AddVeiculo(db, cli);
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), null);
        await db.SaveChangesAsync();

        var criadas = await new FaturamentoService(db).GerarAsync(Competencia);

        Assert.Empty(criadas);
    }

    [Fact]
    public async Task Geracao_e_idempotente_nao_duplica_faturas()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, mensalista: true, valor: 310m);
        var veic = AddVeiculo(db, cli);
        AddAssociacao(db, veic, cli, new DateTime(2025, 8, 1), null);
        await db.SaveChangesAsync();

        var service = new FaturamentoService(db);
        await service.GerarAsync(Competencia);
        var segunda = await service.GerarAsync(Competencia);

        Assert.Empty(segunda); // já existe fatura para a competência
        Assert.Single(db.Faturas.Where(f => f.ClienteId == cli));
    }
}
