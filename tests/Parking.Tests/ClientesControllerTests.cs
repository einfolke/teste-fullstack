using Microsoft.AspNetCore.Mvc;
using Parking.Api.Controllers;
using Parking.Api.Dtos;
using Parking.Api.Models;
using Xunit;

namespace Parking.Tests;

public class ClientesControllerTests
{
    [Fact]
    public async Task Create_valido_retorna_created_e_persiste_ativo()
    {
        using var db = TestDb.Create();
        var ctrl = new ClientesController(db);

        var res = await ctrl.Create(new ClienteCreateDto("João", "31999990001", "Rua A", true, 189.90m, Ativo: true));

        Assert.IsType<CreatedAtActionResult>(res);
        var cli = Assert.Single(db.Clientes);
        Assert.True(cli.Ativo);
    }

    [Fact]
    public async Task Create_mensalista_sem_valor_retorna_badrequest()
    {
        using var db = TestDb.Create();
        var ctrl = new ClientesController(db);

        var res = await ctrl.Create(new ClienteCreateDto("João", null, null, true, null));

        Assert.IsType<BadRequestObjectResult>(res);
    }

    [Fact]
    public async Task Create_nome_e_telefone_duplicados_retorna_conflict()
    {
        using var db = TestDb.Create();
        db.Clientes.Add(new Cliente { Nome = "João", Telefone = "31999990001" });
        await db.SaveChangesAsync();
        var ctrl = new ClientesController(db);

        var res = await ctrl.Create(new ClienteCreateDto("João", "31999990001", null, false, null));

        Assert.IsType<ConflictObjectResult>(res);
    }

    [Fact]
    public async Task Update_altera_status_para_inativo()
    {
        using var db = TestDb.Create();
        var cli = new Cliente { Nome = "João", Telefone = "31999990001", Mensalista = true, ValorMensalidade = 100m, Ativo = true };
        db.Clientes.Add(cli);
        await db.SaveChangesAsync();
        var ctrl = new ClientesController(db);

        var res = await ctrl.Update(cli.Id, new ClienteUpdateDto("João", "31999990001", null, true, 100m, Ativo: false));

        Assert.IsType<OkObjectResult>(res);
        Assert.False(db.Clientes.Single().Ativo);
    }

    [Fact]
    public async Task Update_para_combinacao_existente_retorna_conflict()
    {
        using var db = TestDb.Create();
        db.Clientes.Add(new Cliente { Nome = "Maria", Telefone = "31988880002" });
        var joao = new Cliente { Nome = "João", Telefone = "31999990001" };
        db.Clientes.Add(joao);
        await db.SaveChangesAsync();
        var ctrl = new ClientesController(db);

        // Tenta renomear João para a mesma combinação de Maria
        var res = await ctrl.Update(joao.Id, new ClienteUpdateDto("Maria", "31988880002", null, false, null));

        Assert.IsType<ConflictObjectResult>(res);
    }

    [Fact]
    public async Task Update_cliente_inexistente_retorna_notfound()
    {
        using var db = TestDb.Create();
        var ctrl = new ClientesController(db);

        var res = await ctrl.Update(Guid.NewGuid(), new ClienteUpdateDto("X", null, null, false, null));

        Assert.IsType<NotFoundObjectResult>(res);
    }
}
