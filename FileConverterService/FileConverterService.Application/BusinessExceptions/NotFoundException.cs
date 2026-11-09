namespace FileConverterService.Application.BusinessExceptions
{
    public class NotFoundException : BusinessException
    {
        public override string Code => "NOT_FOUND";


        public NotFoundException(string message = "", Dictionary<string, List<string>>? errors = null) : base(message, errors)
        {

        }

    }
}
