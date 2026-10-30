using FluentValidation;
using FileConverterService.Application.Constants;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCInputValidator : AbstractValidator<ConverToPDF_UCInput>
    {
        private static readonly string[] AllowedContentTypes =
        {
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };

        public ConverToPDF_UCInputValidator()
        {
            RuleFor(x => x.Files)
                .NotEmpty()
                .WithMessage(BussinessRuleMessage.FileValidator.MinimumFile)
                .Must(files => files != null && files.Count() <= 5)
                .WithMessage(BussinessRuleMessage.FileValidator.MaximumFile);

            RuleForEach(x => x.Files).ChildRules(file =>
            {
                file.RuleFor(f => f.FileName)
                    .NotEmpty()
                    .WithMessage(BussinessRuleMessage.FileValidator.EmptyFileName);

                file.RuleFor(f => f.Content)
                    .NotNull()
                    .WithMessage(BussinessRuleMessage.FileValidator.NullContent);

                file.RuleFor(f => f.Type)
                    .Must(type => AllowedContentTypes.Contains(type))
                    .WithMessage(BussinessRuleMessage.FileValidator.InvalidType);
            });
        }
    }
}
