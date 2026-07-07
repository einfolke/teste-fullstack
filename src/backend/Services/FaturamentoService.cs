
using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Models;

namespace Parking.Api.Services
{
    public class FaturamentoService
    {
        private readonly AppDbContext _db;
        private readonly IDistributedLock _lock;

        // O lock é opcional: sem Redis configurado, usa NoOp (não quebra os testes nem o uso local).
        public FaturamentoService(AppDbContext db, IDistributedLock? distributedLock = null)
        {
            _db = db;
            _lock = distributedLock ?? new NoOpDistributedLock();
        }

        // Gera faturas da competência (yyyy-MM) aplicando faturamento proporcional:
        // cada veículo é cobrado apenas pelos dias em que esteve associado ao cliente no mês.
        public async Task<List<Fatura>> GerarAsync(string competencia, CancellationToken ct = default)
        {
            // Lock distribuído por competência: evita que duas instâncias gerem as mesmas faturas em paralelo.
            await using var trava = await _lock.AdquirirAsync($"faturamento:{competencia}", TimeSpan.FromMinutes(5), ct);
            if (trava == null)
                return new List<Fatura>(); // outra instância já está gerando esta competência

            var part = competencia.Split('-');
            var ano = int.Parse(part[0]);
            var mes = int.Parse(part[1]);
            var diasNoMes = DateTime.DaysInMonth(ano, mes);
            // Limites UTC para o filtro no banco (Npgsql exige Kind=Utc)
            var inicioMes = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Utc);
            var fimMes = new DateTime(ano, mes, diasNoMes, 23, 59, 59, DateTimeKind.Utc);
            // Limites por data (dia) para o cálculo proporcional em memória
            var primeiroDia = new DateTime(ano, mes, 1);
            var ultimoDia = new DateTime(ano, mes, diasNoMes);

            var mensalistas = await _db.Clientes
                .Where(c => c.Mensalista && c.Ativo)
                .AsNoTracking()
                .ToListAsync(ct);

            // Associações que tocam a competência (início <= fim do mês e (sem fim ou fim >= início do mês))
            var associacoes = await _db.Associacoes
                .Where(a => a.DataInicio <= fimMes && (a.DataFim == null || a.DataFim >= inicioMes))
                .AsNoTracking()
                .ToListAsync(ct);

            // Mapa de placas para exibir no detalhe da fatura
            var idsVeiculos = associacoes.Select(a => a.VeiculoId).Distinct().ToList();
            var veiculos = await _db.Veiculos
                .Where(v => idsVeiculos.Contains(v.Id))
                .ToListAsync(ct);
            var placas = veiculos.ToDictionary(v => v.Id, v => v.Placa);
            // Veículos inativos não geram cobrança
            var veiculosAtivos = veiculos.Where(v => v.Ativo).Select(v => v.Id).ToHashSet();

            var criadas = new List<Fatura>();

            foreach (var cli in mensalistas)
            {
                var existente = await _db.Faturas
                    .FirstOrDefaultAsync(f => f.ClienteId == cli.Id && f.Competencia == competencia, ct);
                if (existente != null) continue; // idempotência simples

                var mensalidade = cli.ValorMensalidade ?? 0m;
                var valorPorDia = mensalidade / diasNoMes;

                var periodosDoCliente = associacoes.Where(a => a.ClienteId == cli.Id);

                decimal total = 0m;
                var veiculosNaFatura = new HashSet<Guid>();
                var detalhes = new List<string>();

                foreach (var a in periodosDoCliente)
                {
                    if (!veiculosAtivos.Contains(a.VeiculoId)) continue;

                    var inicio = a.DataInicio.Date > primeiroDia ? a.DataInicio.Date : primeiroDia;
                    var fim = (a.DataFim?.Date ?? ultimoDia) < ultimoDia ? (a.DataFim?.Date ?? ultimoDia) : ultimoDia;
                    if (fim < inicio) continue;

                    var dias = (fim - inicio).Days + 1;
                    total += valorPorDia * dias;
                    veiculosNaFatura.Add(a.VeiculoId);
                    var placa = placas.TryGetValue(a.VeiculoId, out var p) ? p : a.VeiculoId.ToString().Substring(0, 8);
                    detalhes.Add($"{placa}: {dias}/{diasNoMes} dias");
                }

                if (veiculosNaFatura.Count == 0) continue; // cliente sem veículos no mês

                var fat = new Fatura
                {
                    Competencia = competencia,
                    ClienteId = cli.Id,
                    Valor = Math.Round(total, 2, MidpointRounding.AwayFromZero),
                    Observacao = $"Proporcional por dias — {string.Join("; ", detalhes)}"
                };

                foreach (var vid in veiculosNaFatura)
                    fat.Veiculos.Add(new FaturaVeiculo { FaturaId = fat.Id, VeiculoId = vid });

                _db.Faturas.Add(fat);
                criadas.Add(fat);
            }

            await _db.SaveChangesAsync(ct);
            return criadas;
        }
    }
}
