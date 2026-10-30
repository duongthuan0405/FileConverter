using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.UseCases.ConvertToPDF
{
    public class ConverToPDF_UCHandler : IRequestHandler<ConverToPDF_UCInput, ConverToPDF_UCOutput>
    {
        public Task<ConverToPDF_UCOutput> Handle(ConverToPDF_UCInput request, CancellationToken cancellationToken)
        {
            List<string> test = request.Files.Select(f => $"{f.FileName}.{f.Type}").ToList();
            return Task.FromResult(new ConverToPDF_UCOutput() { JobIds = test });
        }
    }
}
