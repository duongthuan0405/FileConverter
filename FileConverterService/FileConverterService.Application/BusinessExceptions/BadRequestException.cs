namespace FileConverterService.Application.BusinessExceptions
{

    public class BadRequestException : BusinessException
    {
        public BadRequestException(string message = "", Dictionary<string, List<string>>? errors = null) : base(message, errors)
        {
        }

        public override string Code => "BAD_REQUEST";

    }
}
