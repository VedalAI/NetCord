using System.Runtime.InteropServices;

namespace NetCord.Gateway.Voice;

internal class OpusDecoderHandle() : SafeHandle(IntPtr.Zero, true)
{
    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Opus.OpusDecoderDestroy(handle);
        return true;
    }
}
