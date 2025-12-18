using System.Runtime.InteropServices;

namespace NetCord.Gateway.Voice.Encryption;

internal static class XSalsa20Poly1305
{
    public const int MacBytes = 16;
    public const int NonceBytes = 24;

    [DllImport("libsodium", EntryPoint = "crypto_secretbox_easy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CryptoSecretboxEasy(ref byte c, ref byte m, ulong mlen, ref byte n, ref byte k);

    [DllImport("libsodium", EntryPoint = "crypto_secretbox_open_easy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CryptoSecretboxOpenEasy(ref byte m, ref byte c, ulong clen, ref byte n, ref byte k);
}

internal static class XChaCha20Poly1305
{
    public const int ABytes = 16;
    public const int NPubBytes = 24;

    [DllImport("libsodium", EntryPoint = "crypto_aead_xchacha20poly1305_ietf_encrypt", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CryptoAeadXChaCha20Poly1305IetfEncrypt(ref byte c, ref ulong clen_p, ref byte m, ulong mlen, ref byte ad, ulong adlen, ref byte nsec, ref byte npub, ref byte k);

    [DllImport("libsodium", EntryPoint = "crypto_aead_xchacha20poly1305_ietf_decrypt", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int CryptoAeadXChaCha20Poly1305IetfDecrypt(ref byte m, ref ulong mlen_p, ref byte nsec, ref byte c, ulong clen, ref byte ad, ulong adlen, ref byte npub, ref byte k);
}
