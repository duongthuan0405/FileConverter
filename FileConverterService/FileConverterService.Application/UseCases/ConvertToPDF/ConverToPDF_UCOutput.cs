using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCOutput
    {
        public IEnumerable<string> JobIds { get; set; } = new List<string>();
    }
}
