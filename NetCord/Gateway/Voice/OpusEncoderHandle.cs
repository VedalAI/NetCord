using System.Runtime.InteropServices;

namespace NetCord.Gateway.Voice;

internal class OpusEncoderHandle() : SafeHandle(IntPtr.Zero, true)
{
    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Opus.OpusEncoderDestroy(handle);
        return true;
    }
}
