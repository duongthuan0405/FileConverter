using FluentValidation;
using FileConverterService.Application.Constants;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCInputValidator : AbstractValidator<ConverToPDF_UCInput>
    {
        private const long MaxFileBytes = 20L * 1024 * 1024;

        public ConverToPDF_UCInputValidator()
        {
            RuleFor(x => x.Files)
                .NotEmpty()
                .WithMessage(BussinessRuleMessage.FileValidator.MinimumFile)
                .Must(files => files != null && files.Count() <= 5)
                .WithMessage(BussinessRuleMessage.FileValidator.MaximumFile);

            RuleForEach(x => x.Files).Cascade(CascadeMode.Stop)
                .NotNull().WithMessage(BussinessRuleMessage.FileValidator.NullContent)
                .ChildRules(file =>
            {
                file.RuleFor(f => f.FileName)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BussinessRuleMessage.FileValidator.EmptyFileName)
                    .Must(name => Path.GetExtension(name).Equals(".doc", StringComparison.OrdinalIgnoreCase)
                        || Path.GetExtension(name).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                    .WithMessage(BussinessRuleMessage.FileValidator.InvalidType);

                file.RuleFor(f => f.Content)
                    .Cascade(CascadeMode.Stop)
                    .NotNull()
                    .WithMessage(BussinessRuleMessage.FileValidator.NullContent)
                    .Must(content => content.CanRead && content.CanSeek)
                    .WithMessage(BussinessRuleMessage.FileValidator.UnreadableContent)
                    .Must(content => content.Length > 0)
                    .WithMessage(BussinessRuleMessage.FileValidator.EmptyContent)
                    .Must(content => content.Length <= MaxFileBytes)
                    .WithMessage(BussinessRuleMessage.FileValidator.FileTooLarge);

                file.RuleFor(f => f.Type)
                    .Must((input, type) => Path.GetExtension(input.FileName ?? "").ToLowerInvariant() switch
                    {
                        ".doc" => string.Equals(type, "application/msword", StringComparison.OrdinalIgnoreCase),
                        ".docx" => string.Equals(type, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase),
                        _ => false
                    })
                    .WithMessage(BussinessRuleMessage.FileValidator.MimeMismatch);
            });
        }
    }
}
