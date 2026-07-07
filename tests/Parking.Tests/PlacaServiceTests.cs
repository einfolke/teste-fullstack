using Parking.Api.Services;
using Xunit;

namespace Parking.Tests;

public class PlacaServiceTests
{
    private readonly PlacaService _svc = new();

    [Theory]
    [InlineData("abc1d23", "ABC1D23")]
    [InlineData("ABC-1D23", "ABC1D23")]
    [InlineData(" abc 1d23 ", "ABC1D23")]
    [InlineData(null, "")]
    public void Sanitizar_remove_nao_alfanumericos_e_normaliza(string? entrada, string esperado)
    {
        Assert.Equal(esperado, _svc.Sanitizar(entrada));
    }

    [Theory]
    [InlineData("ABC1D23")] // Mercosul
    [InlineData("ABC1234")] // padrão antigo
    public void EhValida_aceita_placas_validas(string placa)
    {
        Assert.True(_svc.EhValida(placa));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB1D23")]   // só 2 letras iniciais
    [InlineData("ABCD123")]  // 4ª posição deveria ser dígito
    [InlineData("123456")]
    public void EhValida_rejeita_placas_invalidas(string placa)
    {
        Assert.False(_svc.EhValida(placa));
    }
}
