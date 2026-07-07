namespace Parking.Api.Models
{
    // Histórico de associação de um veículo a um cliente, com período de vigência.
    // DataFim nula indica associação vigente (em aberto).
    public class VeiculoClienteAssociacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid VeiculoId { get; set; }
        public Guid ClienteId { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
    }
}
