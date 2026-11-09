using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCOutput
    {
        public int TotalFile { get; set; } = 0;
        public int SuccessfulUploadedFile { get; set; } = 0;
        public IEnumerable<string> JobIds { get; set; } = new List<string>();
    }
}
