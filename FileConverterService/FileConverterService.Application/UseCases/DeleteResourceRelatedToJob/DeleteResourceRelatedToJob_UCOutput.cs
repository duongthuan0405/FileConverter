using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.UseCases.DeleteResourceRelatedToJob
{
    public class DeleteResourceRelatedToJob_UCOutput
    {
        public string JobId { get; set; } = string.Empty;
        // Indicates whether the cleanup was successful
        public bool Success { get; set; }
    }
}
