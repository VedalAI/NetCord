// ReSharper disable once CheckNamespace
namespace System;

public static class CharExtensions
{
    extension(char)
    {
        public static bool IsAsciiDigit(char c)
        {
            return c is >= '0' and <= '9';
        }
        
        public static bool IsAsciiLetterOrDigit(char c)
        {
            return (c is >= 'A' and <= 'Z') || (c is >= 'a' and <= 'z') || IsAsciiDigit(c);
        }
    }
}
