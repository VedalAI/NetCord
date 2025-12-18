using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System.Threading;

public static class InterlockedExtensions
{
    extension(Interlocked)
    {
        public static byte Exchange(ref byte location, byte value)
        {
            return (byte)Interlocked.Exchange(ref Unsafe.As<byte, int>(ref location), value);
        }
    }
}
