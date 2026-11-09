namespace FileConverterService.Application.BusinessExceptions
{
    public abstract class BusinessException : Exception
    {
        private Dictionary<string, List<string>> _errors = new();
        public abstract string Code { get; }

        public Dictionary<string, List<string>> Errors
        {
            get =>
                _errors.ToDictionary(
                    error => error.Key,
                    error => error.Value.ToList()
                );
        }

        public void AddError(string key, string error)
        {
            bool isExist = _errors.TryGetValue(key, out List<string>? errorLists);
            if (isExist)
            {
                errorLists?.Add(error);
            }
            else
            {
                _errors.Add(key, new List<string>() { error });
            }
        }

        public BusinessException(string message = "", Dictionary<string, List<string>>? errors = null)
            : base(message)
        {
            if (errors != null)
            {
                _errors = errors;
            }
        }

    }

}
