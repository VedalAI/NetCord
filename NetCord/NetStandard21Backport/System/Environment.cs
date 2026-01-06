using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace System;

public static class EnvironmentExtensions
{
    extension(Environment)
    {
        public static long TickCount64 => unchecked((long) GetTickCount64());
    }
    
    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
