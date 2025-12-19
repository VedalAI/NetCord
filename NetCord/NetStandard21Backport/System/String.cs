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

        public static string Create(IFormatProvider? provider, StringFormatHandler handler)
        {
            return string.Format(provider, handler.String, handler.Parts);
        }
    }
}

[InterpolatedStringHandler]
public class StringFormatHandler
{
    public string String => _builder.ToString();
    public object[] Parts => _parts.Cast<object>().ToArray();
        
    private readonly StringBuilder _builder = new();
    private readonly List<string> _parts = [];
        
    public StringFormatHandler(int literalLength, int formattedCount)
    {
    }
        
    public void AppendLiteral(string s)
    {
        _builder.Append(s);
    }
        
    public void AppendFormatted<T>(T value)
    {
        _builder.Append($"{{{_parts.Count}}}");
        _parts.Add(value?.ToString() ?? string.Empty);
    }

    public void AppendFormatted<T>(T value, string? format)
    {
        _builder.Append($"{{{_parts.Count}:{format}}}");
        _parts.Add(value?.ToString() ?? string.Empty);
    }
        
    public void AppendFormatted<T>(T value, int alignment)
    {
        _builder.Append($"{{{_parts.Count},{alignment}}}");
        _parts.Add(value?.ToString() ?? string.Empty);
    }

    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        _builder.Append($"{{{_parts.Count},{alignment}:{format}}}");
        _parts.Add(value?.ToString() ?? string.Empty);
    }
}
