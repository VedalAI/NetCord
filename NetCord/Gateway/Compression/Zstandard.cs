using System.Runtime.InteropServices;

namespace NetCord.Gateway.Compression;

internal static class Zstandard
{
    public static bool TryLoad()
    {
        try
        {
            // Try calling a simple function to see if the library loads
            _ = CreateDStream();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    [DllImport("libzstd", EntryPoint = "ZSTD_isError", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsError(nuint code);

    [DllImport("libzstd", EntryPoint = "ZSTD_getErrorName", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetErrorNamePtr(nuint code);

    public static string GetErrorName(nuint code)
    {
        var ptr = GetErrorNamePtr(code);
        return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
    }

    [DllImport("libzstd", EntryPoint = "ZSTD_createDStream", CallingConvention = CallingConvention.Cdecl)]
    public static extern DStreamHandle CreateDStream();

    [DllImport("libzstd", EntryPoint = "ZSTD_freeDStream", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint FreeDStream(nint zds);

    [DllImport("libzstd", EntryPoint = "ZSTD_initDStream", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint InitDStream(DStreamHandle zds);

    [DllImport("libzstd", EntryPoint = "ZSTD_decompressStream", CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint DecompressStream(DStreamHandle zds, ref Buffer output, ref Buffer input);

    public class DStreamHandle : SafeHandle
    {
        public DStreamHandle() : base(IntPtr.Zero, true) { }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            FreeDStream(handle);
            return true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Buffer
    {
        public byte* Ptr;
        public nuint Size;
        public nuint Pos;
    }
}
