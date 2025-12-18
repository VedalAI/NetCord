using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace System;

public static class StringExtensions
{
    extension(string source)
    {
        public void CopyTo(Span<char> destination)
        {
            source.AsSpan().CopyTo(destination);
        }

        public bool TryCopyTo(Span<char> destination)
        {
            return source.AsSpan().TryCopyTo(destination);
        }
        
        public static string Format(IFormatProvider? provider, CompositeFormat format, ReadOnlySpan<object?> args)
        {
            return string.Format(provider, format.Format, args.ToArray());
        }

        public static string Create(IFormatProvider? provider, [InterpolatedStringHandlerArgument("provider")] DefaultInterpolatedStringHandler handler)
        {
            return handler.ToString();
        }
    }
}
