using System.Buffers;

// ReSharper disable once CheckNamespace
namespace System.Text;

public static class EncodingExtensions
{
    extension(Encoding self)
    {
        public string GetString(ReadOnlySequence<byte> bytes)
        {
            return self.GetString(bytes.ToArray());
        }
    }
}
