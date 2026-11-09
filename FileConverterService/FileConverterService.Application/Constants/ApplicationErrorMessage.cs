using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.Constants
{
    internal static class ApplicationErrorMessage
    {
        public static string BadInput = "Input data is invalid!";
        public const string JobNotFound = "Conversion job was not found.";
        public const string JobNotCompleted = "The conversion job has not completed successfully.";
        public const string JobFileNotFound = "The converted PDF file was not found.";
        public const string JobIsActive = "Cannot delete resources while the job is pending or processing.";
    }
}
