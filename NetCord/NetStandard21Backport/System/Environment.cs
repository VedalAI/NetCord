// ReSharper disable once CheckNamespace
namespace System;

public static class EnvironmentExtensions
{
    extension(Environment)
    {
        public static long TickCount64 => Environment.TickCount;
    }
}
