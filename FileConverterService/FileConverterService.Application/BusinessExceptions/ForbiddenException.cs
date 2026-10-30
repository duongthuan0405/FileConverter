namespace FileConverterService.Application.BusinessExceptions
{
    public class ForbiddenException : BusinessException
    {
        public ForbiddenException(string message = "", Dictionary<string, List<string>>? errors = null) : base(message, errors)
        {
        }

        public override string Code => "FORBIDDEN";

    }
}
