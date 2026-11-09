using System;

namespace FileConverterService.Application.UseCases.GetJob
{
    public class GetJob_UCOutput
    {
        public string JobId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }
        public string? Error { get; set; }
    }
}

