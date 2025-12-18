// ReSharper disable once CheckNamespace
namespace System;

public static class HashCodeExtensions
{
    extension(HashCode self)
    {
        public void AddBytes(ReadOnlySpan<byte> bytes)
        {
            foreach (byte t in bytes)
            {
                self.Add(t);
            }
        }
    }
}
