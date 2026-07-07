using Microsoft.AspNetCore.Mvc;
using Parking.Api.Controllers;
using Parking.Api.Dtos;
using Parking.Api.Models;
using Parking.Api.Services;
using Xunit;

namespace Parking.Tests;

public class VeiculosControllerTests
{
    private static VeiculosController NewController(Parking.Api.Data.AppDbContext db)
        => new(db, new PlacaService());

    private static Guid AddCliente(Parking.Api.Data.AppDbContext db, string nome)
    {
        var c = new Cliente { Nome = nome };
        db.Clientes.Add(c);
        return c.Id;
    }

    [Fact]
    public async Task Create_valido_cria_veiculo_e_associacao_vigente()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, "João");
        await db.SaveChangesAsync();
        var ctrl = NewController(db);

        var res = await ctrl.Create(new VeiculoCreateDto("ABC1D23", "Gol", 2019, cli));

        Assert.IsType<CreatedAtActionResult>(res);
        var veic = Assert.Single(db.Veiculos);
        Assert.True(veic.Ativo);
        var assoc = Assert.Single(db.Associacoes);
        Assert.Equal(cli, assoc.ClienteId);
        Assert.Null(assoc.DataFim); // vigente
    }

    [Fact]
    public async Task Create_placa_invalida_retorna_badrequest()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, "João");
        await db.SaveChangesAsync();
        var ctrl = NewController(db);

        var res = await ctrl.Create(new VeiculoCreateDto("123", null, null, cli));

        Assert.IsType<BadRequestObjectResult>(res);
    }

    [Fact]
    public async Task Create_placa_duplicada_retorna_conflict()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, "João");
        db.Veiculos.Add(new Veiculo { Placa = "ABC1D23", ClienteId = cli });
        await db.SaveChangesAsync();
        var ctrl = NewController(db);

        var res = await ctrl.Create(new VeiculoCreateDto("ABC1D23", null, null, cli));

        Assert.IsType<ConflictObjectResult>(res);
    }

    [Fact]
    public async Task Update_troca_de_cliente_fecha_associacao_antiga_e_abre_nova()
    {
        using var db = TestDb.Create();
        var joao = AddCliente(db, "João");
        var maria = AddCliente(db, "Maria");
        var veic = new Veiculo { Placa = "ABC1D23", ClienteId = joao };
        db.Veiculos.Add(veic);
        db.Associacoes.Add(new VeiculoClienteAssociacao { VeiculoId = veic.Id, ClienteId = joao, DataInicio = DateTime.UtcNow.AddDays(-10) });
        await db.SaveChangesAsync();
        var ctrl = NewController(db);

        var res = await ctrl.Update(veic.Id, new VeiculoUpdateDto("ABC1D23", "Gol", 2019, maria));

        Assert.IsType<OkObjectResult>(res);
        Assert.Equal(maria, db.Veiculos.Single().ClienteId);

        var antiga = Assert.Single(db.Associacoes, a => a.ClienteId == joao);
        Assert.NotNull(antiga.DataFim); // fechada

        var nova = Assert.Single(db.Associacoes, a => a.ClienteId == maria);
        Assert.Null(nova.DataFim); // vigente
    }

    [Fact]
    public async Task Update_altera_status_para_inativo()
    {
        using var db = TestDb.Create();
        var cli = AddCliente(db, "João");
        var veic = new Veiculo { Placa = "ABC1D23", ClienteId = cli, Ativo = true };
        db.Veiculos.Add(veic);
        await db.SaveChangesAsync();
        var ctrl = NewController(db);

        var res = await ctrl.Update(veic.Id, new VeiculoUpdateDto("ABC1D23", "Gol", 2019, cli, Ativo: false));

        Assert.IsType<OkObjectResult>(res);
        Assert.False(db.Veiculos.Single().Ativo);
    }
}
