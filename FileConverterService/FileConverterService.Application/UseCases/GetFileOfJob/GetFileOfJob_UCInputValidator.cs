using FileConverterService.Application.Constants;
using FluentValidation;

namespace FileConverterService.Application.UseCases.GetFileOfJob;

public class GetFileOfJob_UCInputValidator : AbstractValidator<GetFileOfJob_UCInput>
{
    public GetFileOfJob_UCInputValidator()
    {
        RuleFor(x => x.JobId)
            .Must(value => Guid.TryParse(value, out var id) && id != Guid.Empty)
            .WithMessage(BussinessRuleMessage.JobValidator.InvalidJobId);
    }
}
