namespace Parking.Api.Services
{
    public class FaturamentoJob
    {
        private readonly FaturamentoService _faturamento;
        public FaturamentoJob(FaturamentoService faturamento) => _faturamento = faturamento;

        public Task GerarCompetenciaAsync(string competencia, CancellationToken ct = default)
            => _faturamento.GerarAsync(competencia, ct);

        public Task GerarMesAnteriorAsync(CancellationToken ct = default)
        {
            var competencia = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM");
            return _faturamento.GerarAsync(competencia, ct);
        }
    }
}
