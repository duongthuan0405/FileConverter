using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileConverterService.Api.ApiResponseModels
{
    public abstract class ApiResponse<T>
    {
        public abstract bool Success { get; }
        public string Message { get; set; } = string.Empty;
        public T? Data {get; set;} = default(T);
    }
}