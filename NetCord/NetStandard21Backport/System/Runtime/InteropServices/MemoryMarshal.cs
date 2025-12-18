// ReSharper disable once CheckNamespace
namespace System.Runtime.InteropServices;

public static class MemoryMarshalExtensions
{
    extension(MemoryMarshal)
    {
        public static ref T GetArrayDataReference<T>(T[] array)
        {
            return ref MemoryMarshal.GetReference(array.AsSpan());
        }
    }
}
