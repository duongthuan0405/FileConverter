namespace FileConverterService.Application.BusinessExceptions
{
    public class UnauthorizedException : BusinessException
    {
        public UnauthorizedException(string message = "", Dictionary<string, List<string>>? errors = null) : base(message, errors)
        {
        }

        public override string Code => "UNAUTHORIZED";

    }
}
