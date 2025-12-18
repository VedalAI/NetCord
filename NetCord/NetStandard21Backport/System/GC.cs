// ReSharper disable once CheckNamespace
namespace System;

// ReSharper disable once InconsistentNaming
public static class GCExtensions
{
    extension(GC)
    {
        public static T[] AllocateUninitializedArray<T>(int length)
        {
            return new T[length];
        }
    }
}
