using FluentValidation;
using Parking.Api.Dtos;

namespace Parking.Api.Validators
{
    public class ClienteCreateDtoValidator : AbstractValidator<ClienteCreateDto>
    {
        public ClienteCreateDtoValidator()
        {
            RuleFor(x => x.Nome)
                .NotEmpty().WithMessage("O nome é obrigatório.");

            When(x => x.Mensalista, () =>
            {
                RuleFor(x => x.ValorMensalidade)
                    .Must(v => v.HasValue && v.Value > 0)
                    .WithMessage("Informe um valor de mensalidade maior que zero para clientes mensalistas.");
            });
        }
    }

    public class ClienteUpdateDtoValidator : AbstractValidator<ClienteUpdateDto>
    {
        public ClienteUpdateDtoValidator()
        {
            RuleFor(x => x.Nome)
                .NotEmpty().WithMessage("O nome é obrigatório.");

            When(x => x.Mensalista, () =>
            {
                RuleFor(x => x.ValorMensalidade)
                    .Must(v => v.HasValue && v.Value > 0)
                    .WithMessage("Informe um valor de mensalidade maior que zero para clientes mensalistas.");
            });
        }
    }
}
