namespace FileConverterService.Application.BusinessExceptions
{
    public class ConflictUniqueValueException : BusinessException
    {
        public ConflictUniqueValueException(string message = "", Dictionary<string, List<string>>? errors = null) : base(message, errors)
        {
        }

        public override string Code => "CONFLICT";



    }

}
