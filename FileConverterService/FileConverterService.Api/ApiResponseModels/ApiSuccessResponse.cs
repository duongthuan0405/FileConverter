using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileConverterService.Api.ApiResponseModels
{
    public class ApiSuccessResponse<T> : ApiResponse<T>
    {
        public override bool Success => true;
        public ApiSuccessResponse(T? data, string message = "Request is executed successful")
        {
            Message = message;
            Data = data;
        }
    }
}