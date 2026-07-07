using FluentValidation;
using Parking.Api.Dtos;

namespace Parking.Api.Validators
{
    public class VeiculoCreateDtoValidator : AbstractValidator<VeiculoCreateDto>
    {
        public VeiculoCreateDtoValidator()
        {
            RuleFor(x => x.Placa)
                .NotEmpty().WithMessage("A placa é obrigatória.");

            RuleFor(x => x.ClienteId)
                .NotEmpty().WithMessage("O cliente é obrigatório.");

            RuleFor(x => x.Ano)
                .Must(a => a >= 1900 && a <= DateTime.UtcNow.Year + 1)
                .When(x => x.Ano.HasValue)
                .WithMessage("Ano fora do intervalo permitido.");
        }
    }

    public class VeiculoUpdateDtoValidator : AbstractValidator<VeiculoUpdateDto>
    {
        public VeiculoUpdateDtoValidator()
        {
            RuleFor(x => x.Placa)
                .NotEmpty().WithMessage("A placa é obrigatória.");

            RuleFor(x => x.ClienteId)
                .NotEmpty().WithMessage("O cliente é obrigatório.");

            RuleFor(x => x.Ano)
                .Must(a => a >= 1900 && a <= DateTime.UtcNow.Year + 1)
                .When(x => x.Ano.HasValue)
                .WithMessage("Ano fora do intervalo permitido.");
        }
    }
}
