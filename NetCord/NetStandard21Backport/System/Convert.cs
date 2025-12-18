using System.Globalization;

// ReSharper disable once CheckNamespace
namespace System;

public static class ConvertExtensions
{
    extension(Convert)
    {
        public static byte[] FromHexString(ReadOnlySpan<char> chars)
        {
            if (chars.Length % 2 != 0)
                throw new FormatException("The input is not a valid hex string as its length is not a multiple of 2.");

            var bytes = new byte[chars.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(chars.Slice(i * 2, 2), NumberStyles.HexNumber);

            return bytes;
        }
    }
}
