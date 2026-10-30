using System;
using System.Collections.Generic;
using System.Text;

namespace FileConverterService.Application.Constants
{
    internal static class BussinessRuleMessage
    {
        internal static class FileValidator
        {
            public static string InvalidType = "Only .doc and .docx files are supported.";
            public static string MinimumFile = "Number of files has to be greater than 0!";
            public static string MaximumFile = "Number of files has to be less than or equal to 5!";
            public static string EmptyFileName = "File name cannot be empty!";
            public static string NullContent = "File content cannot be null!";
        }
    }
}
