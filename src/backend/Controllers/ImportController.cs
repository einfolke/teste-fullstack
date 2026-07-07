
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Models;
using Parking.Api.Services;
using System.Globalization;
using System.Text;

namespace Parking.Api.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private const int ColunasEsperadas = 9;

        private readonly AppDbContext _db;
        private readonly PlacaService _placa;
        public ImportController(AppDbContext db, PlacaService placa) { _db = db; _placa = placa; }

        [HttpPost("csv")]
        public async Task<IActionResult> ImportCsv()
        {
            if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
                return BadRequest("Envie um arquivo CSV no campo 'file'.");

            var file = Request.Form.Files[0];
            if (file.Length == 0)
                return BadRequest("O arquivo enviado está vazio.");

            using var s = file.OpenReadStream();
            using var r = new StreamReader(s, Encoding.UTF8);

            int numeroLinha = 0, processados = 0, inseridos = 0;
            var erros = new List<object>();
            var placasNoArquivo = new Dictionary<string, int>();

            // Linha 1 = cabeçalho
            numeroLinha++;
            var header = await r.ReadLineAsync();
            if (header == null)
                return BadRequest("O arquivo não possui conteúdo.");

            while (!r.EndOfStream)
            {
                numeroLinha++;
                var raw = await r.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                processados++;

                var cols = ParseCsvLine(raw);

                if (cols.Length != ColunasEsperadas)
                {
                    erros.Add(new
                    {
                        linha = numeroLinha,
                        coluna = (string?)null,
                        motivo = $"Número de colunas inválido: esperado {ColunasEsperadas}, encontrado {cols.Length}.",
                        conteudo = raw
                    });
                    continue;
                }

                var placaBruta = cols[0];
                var modelo = cols[1].Trim();
                var anoTexto = cols[2].Trim();
                var cliNome = cols[4].Trim();
                var cliTel = new string((cols[5] ?? "").Where(char.IsDigit).ToArray());
                var cliEnd = cols[6].Trim();
                var mensalistaTexto = cols[7].Trim();
                var valorTexto = cols[8].Trim();

                // Validações de campo com mensagens específicas
                var placa = _placa.Sanitizar(placaBruta);
                if (string.IsNullOrWhiteSpace(placa))
                {
                    erros.Add(NovoErro(numeroLinha, "placa", "Placa é obrigatória.", raw));
                    continue;
                }
                if (!_placa.EhValida(placa))
                {
                    erros.Add(NovoErro(numeroLinha, "placa", $"Placa inválida: '{placaBruta}'. Formato esperado: 3 letras + 4 caracteres (ex.: BRA1A23).", raw));
                    continue;
                }
                if (placasNoArquivo.TryGetValue(placa, out var linhaAnterior))
                {
                    erros.Add(NovoErro(numeroLinha, "placa", $"Placa duplicada no arquivo (já informada na linha {linhaAnterior}): '{placa}'.", raw));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cliNome))
                {
                    erros.Add(NovoErro(numeroLinha, "cliente_nome", "Nome do cliente é obrigatório.", raw));
                    continue;
                }

                int? ano = null;
                if (!string.IsNullOrEmpty(anoTexto))
                {
                    if (!int.TryParse(anoTexto, out var anoValor))
                    {
                        erros.Add(NovoErro(numeroLinha, "ano", $"Ano inválido: '{anoTexto}'. Informe um número inteiro.", raw));
                        continue;
                    }
                    if (anoValor < 1900 || anoValor > DateTime.UtcNow.Year + 1)
                    {
                        erros.Add(NovoErro(numeroLinha, "ano", $"Ano fora do intervalo permitido: '{anoTexto}'.", raw));
                        continue;
                    }
                    ano = anoValor;
                }

                bool mensalista = false;
                if (!string.IsNullOrEmpty(mensalistaTexto))
                {
                    if (!TryParseBool(mensalistaTexto, out mensalista))
                    {
                        erros.Add(NovoErro(numeroLinha, "mensalista", $"Valor de mensalista inválido: '{mensalistaTexto}'. Use true ou false.", raw));
                        continue;
                    }
                }

                decimal? valorMens = null;
                if (!string.IsNullOrEmpty(valorTexto))
                {
                    if (!decimal.TryParse(valorTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out var vm))
                    {
                        erros.Add(NovoErro(numeroLinha, "valor_mensalidade", $"Valor de mensalidade inválido: '{valorTexto}'. Use ponto como separador decimal (ex.: 189.90).", raw));
                        continue;
                    }
                    if (vm < 0)
                    {
                        erros.Add(NovoErro(numeroLinha, "valor_mensalidade", $"Valor de mensalidade não pode ser negativo: '{valorTexto}'.", raw));
                        continue;
                    }
                    valorMens = vm;
                }

                if (mensalista && (valorMens == null || valorMens <= 0))
                {
                    erros.Add(NovoErro(numeroLinha, "valor_mensalidade", "Cliente mensalista requer valor de mensalidade maior que zero.", raw));
                    continue;
                }

                if (await _db.Veiculos.AnyAsync(v => v.Placa == placa))
                {
                    erros.Add(NovoErro(numeroLinha, "placa", $"Placa já cadastrada no sistema: '{placa}'.", raw));
                    continue;
                }

                try
                {
                    var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Nome == cliNome && c.Telefone == cliTel);
                    if (cliente == null)
                    {
                        cliente = new Cliente
                        {
                            Nome = cliNome,
                            Telefone = string.IsNullOrEmpty(cliTel) ? null : cliTel,
                            Endereco = string.IsNullOrEmpty(cliEnd) ? null : cliEnd,
                            Mensalista = mensalista,
                            ValorMensalidade = mensalista ? valorMens : null
                        };
                        _db.Clientes.Add(cliente);
                        await _db.SaveChangesAsync();
                    }

                    var v = new Veiculo { Placa = placa, Modelo = string.IsNullOrEmpty(modelo) ? null : modelo, Ano = ano, ClienteId = cliente.Id };
                    _db.Veiculos.Add(v);
                    _db.Associacoes.Add(new VeiculoClienteAssociacao
                    {
                        VeiculoId = v.Id,
                        ClienteId = cliente.Id,
                        DataInicio = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();

                    placasNoArquivo[placa] = numeroLinha;
                    inseridos++;
                }
                catch (DbUpdateException ex)
                {
                    erros.Add(NovoErro(numeroLinha, null, $"Erro ao gravar no banco: {ex.InnerException?.Message ?? ex.Message}", raw));
                }
            }

            return Ok(new
            {
                processados,
                inseridos,
                falhas = erros.Count,
                erros
            });
        }

        private static object NovoErro(int linha, string? coluna, string motivo, string conteudo)
            => new { linha, coluna, motivo, conteudo };

        private static bool TryParseBool(string texto, out bool valor)
        {
            switch (texto.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "sim":
                case "s":
                    valor = true;
                    return true;
                case "false":
                case "0":
                case "nao":
                case "não":
                case "n":
                    valor = false;
                    return true;
                default:
                    valor = false;
                    return false;
            }
        }

        // Parser CSV que respeita campos entre aspas contendo vírgulas e aspas escapadas ("")
        private static string[] ParseCsvLine(string linha)
        {
            var campos = new List<string>();
            var sb = new StringBuilder();
            bool entreAspas = false;

            for (int i = 0; i < linha.Length; i++)
            {
                char c = linha[i];
                if (entreAspas)
                {
                    if (c == '"')
                    {
                        if (i + 1 < linha.Length && linha[i + 1] == '"') { sb.Append('"'); i++; }
                        else entreAspas = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') entreAspas = true;
                    else if (c == ',') { campos.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            campos.Add(sb.ToString());
            return campos.ToArray();
        }
    }
}
