// ReSharper disable once CheckNamespace
namespace System;

public static class MemoryExtensionsExtensions
{
    extension(ReadOnlySpan<byte> self)
    {
        public ReadOnlySpan<byte> TrimEnd(byte value)
        {
            int end = self.Length;
            while (end > 0 && self[end - 1] == value)
                end--;
            return self[..end];
        }
    }
}
