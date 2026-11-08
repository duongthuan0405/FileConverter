using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.Constants
{
    internal static class BussinessRuleMessage
    {
        internal static class JobValidator
        {
            public const string InvalidJobId = "JobId must be a valid, non-empty GUID.";
        }

        internal static class FileValidator
        {
            public static string InvalidType = "Only .doc and .docx files are supported.";
            public static string MinimumFile = "Number of files has to be greater than 0!";
            public static string MaximumFile = "Number of files has to be less than or equal to 5!";
            public static string EmptyFileName = "File name cannot be empty!";
            public static string NullContent = "File content cannot be null!";
            public const string UnreadableContent = "File content must be readable and support length inspection.";
            public const string EmptyContent = "File must not be empty.";
            public const string FileTooLarge = "File size must not exceed 20 MiB.";
            public const string MimeMismatch = "Content type must match the .doc or .docx file extension.";
        }
    }
}
