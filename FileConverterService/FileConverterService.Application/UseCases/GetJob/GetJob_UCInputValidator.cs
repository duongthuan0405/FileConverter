using FileConverterService.Application.Constants;
using FluentValidation;

namespace FileConverterService.Application.UseCases.GetJob;

public class GetJob_UCInputValidator : AbstractValidator<GetJob_UCInput>
{
    public GetJob_UCInputValidator()
    {
        RuleFor(x => x.JobId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithMessage(BussinessRuleMessage.JobValidator.InvalidJobId);
    }
}
