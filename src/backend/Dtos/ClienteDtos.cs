
namespace Parking.Api.Dtos
{
    public record ClienteCreateDto(string Nome, string? Telefone, string? Endereco, bool Mensalista, decimal? ValorMensalidade, bool Ativo = true);
    public record ClienteUpdateDto(string Nome, string? Telefone, string? Endereco, bool Mensalista, decimal? ValorMensalidade, bool Ativo = true);
}
