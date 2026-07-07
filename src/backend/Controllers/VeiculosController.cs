
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Api.Data;
using Parking.Api.Dtos;
using Parking.Api.Models;
using Parking.Api.Services;

namespace Parking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VeiculosController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PlacaService _placa;
        private readonly IValidator<VeiculoCreateDto> _createValidator;
        private readonly IValidator<VeiculoUpdateDto> _updateValidator;

        public VeiculosController(
            AppDbContext db,
            PlacaService placa,
            IValidator<VeiculoCreateDto> createValidator,
            IValidator<VeiculoUpdateDto> updateValidator)
        {
            _db = db;
            _placa = placa;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] Guid? clienteId = null)
        {
            var q = _db.Veiculos.AsQueryable();
            if (clienteId.HasValue) q = q.Where(v => v.ClienteId == clienteId.Value);
            var list = await q.OrderBy(v => v.Placa).ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VeiculoCreateDto dto)
        {
            var validacao = await _createValidator.ValidateAsync(dto);
            if (!validacao.IsValid)
                return BadRequest(validacao.Errors.Select(e => e.ErrorMessage));

            var placa = _placa.Sanitizar(dto.Placa);
            if (!_placa.EhValida(placa)) return BadRequest("Placa inválida.");
            if (await _db.Veiculos.AnyAsync(v => v.Placa == placa)) return Conflict("Placa já existe.");

            var v = new Veiculo { Placa = placa, Modelo = dto.Modelo, Ano = dto.Ano, ClienteId = dto.ClienteId, Ativo = dto.Ativo };
            _db.Veiculos.Add(v);
            _db.Associacoes.Add(new VeiculoClienteAssociacao
            {
                VeiculoId = v.Id,
                ClienteId = v.ClienteId,
                DataInicio = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = v.Id }, v);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var v = await _db.Veiculos.FindAsync(id);
            return v == null ? NotFound() : Ok(v);
        }

        // BUG propositado: não invalida/atualiza nada no front; candidato deve ajustar no front (React Query) ou aqui (retornar entidade e orientar)
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] VeiculoUpdateDto dto)
        {
            var v = await _db.Veiculos.FindAsync(id);
            if (v == null) return NotFound();

            var validacao = await _updateValidator.ValidateAsync(dto);
            if (!validacao.IsValid)
                return BadRequest(validacao.Errors.Select(e => e.ErrorMessage));

            var placa = _placa.Sanitizar(dto.Placa);
            if (!_placa.EhValida(placa)) return BadRequest("Placa inválida.");
            if (await _db.Veiculos.AnyAsync(x => x.Placa == placa && x.Id != id)) return Conflict("Placa já existe.");

            v.Placa = placa;
            v.Modelo = dto.Modelo;
            v.Ano = dto.Ano;
            v.Ativo = dto.Ativo;

            if (v.ClienteId != dto.ClienteId)
            {
                var agora = DateTime.UtcNow;
                var vigente = await _db.Associacoes
                    .Where(a => a.VeiculoId == id && a.DataFim == null)
                    .OrderByDescending(a => a.DataInicio)
                    .FirstOrDefaultAsync();
                if (vigente != null) vigente.DataFim = agora;

                _db.Associacoes.Add(new VeiculoClienteAssociacao
                {
                    VeiculoId = id,
                    ClienteId = dto.ClienteId,
                    DataInicio = agora
                });

                v.ClienteId = dto.ClienteId; // troca de cliente permitida
            }

            await _db.SaveChangesAsync();
            return Ok(v);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var v = await _db.Veiculos.FindAsync(id);
            if (v == null) return NotFound();
            _db.Veiculos.Remove(v);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
