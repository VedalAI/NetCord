// ReSharper disable all

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text
{
    /// <summary>Represents a parsed composite format string.</summary>
    [DebuggerDisplay("{Format}")]
    public sealed class CompositeFormat
    {
        /// <summary>The parsed segments that make up the composite format string.</summary>
        /// <remarks>
        /// Every segment represents either a literal or a format hole, based on whether Literal
        /// is non-null or ArgIndex is non-negative.
        /// </remarks>
        internal readonly (string? Literal, int ArgIndex, int Alignment, string? Format)[] _segments;
        /// <summary>The sum of the lengths of all of the literals in <see cref="_segments"/>.</summary>
        internal readonly int _literalLength;
        /// <summary>The number of segments in <see cref="_segments"/> that represent format holes.</summary>
        internal readonly int _formattedCount;
        /// <summary>The number of args required to satisfy the format holes.</summary>
        /// <remarks>This is equal to one more than the largest index required by any format hole.</remarks>
        internal readonly int _argsRequired;

        /// <summary>Initializes the instance.</summary>
        /// <param name="format">The composite format string that was parsed.</param>
        /// <param name="segments">The parsed segments.</param>
        private CompositeFormat(string format, (string? Literal, int ArgIndex, int Alignment, string? Format)[] segments)
        {
            // Store the format.
            Debug.Assert(format is not null);
            Format = format;

            // Store the segments.
            Debug.Assert(segments is not null);
            _segments = segments;

            // Compute derivative information from the segments.
            int literalLength = 0, formattedCount = 0, argsRequired = 0;
            foreach ((string? Literal, int ArgIndex, int Alignment, string? Format) segment in segments)
            {
                Debug.Assert((segment.Literal is not null) ^ (segment.ArgIndex >= 0), "The segment should represent a literal or a format hole, but not both.");

                if (segment.Literal is string literal)
                {
                    literalLength += literal.Length; // no concern about overflow as these were parsed out of a single string
                }
                else if (segment.ArgIndex >= 0)
                {
                    formattedCount++;
                    argsRequired = Math.Max(argsRequired, segment.ArgIndex + 1);
                }
            }

            // Store the derivative information.
            Debug.Assert(literalLength >= 0);
            Debug.Assert(formattedCount >= 0);
            Debug.Assert(formattedCount == 0 || argsRequired > 0);
            _literalLength = literalLength;
            _formattedCount = formattedCount;
            _argsRequired = argsRequired;
        }

        /// <summary>Parse the composite format string <paramref name="format"/>.</summary>
        /// <param name="format">The string to parse.</param>
        /// <returns>The parsed <see cref="CompositeFormat"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="format"/> is null.</exception>
        /// <exception cref="FormatException">A format item in <paramref name="format"/> is invalid.</exception>
        public static CompositeFormat Parse([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));

            var segments = new List<(string? Literal, int ArgIndex, int Alignment, string? Format)>();
            int failureOffset = default;
            string failureReason = default;
            if (!TryParseLiterals(format, segments, ref failureOffset, ref failureReason))
            {
                throw new FormatException($"Invalid format at offset {failureOffset}, reason: " + failureReason);
            }

            return new CompositeFormat(format, segments.ToArray());
        }

        /// <summary>Gets the original composite format string used to create this <see cref="CompositeFormat"/> instance.</summary>
        public string Format { get; }

        /// <summary>Gets the minimum number of arguments that must be passed to a formatting operation using this <see cref="CompositeFormat"/>.</summary>
        /// <remarks>It's permissible to supply more arguments than this value, but it's an error to pass fewer.</remarks>
        public int MinimumArgumentCount => _argsRequired;

        /// <summary>Throws an exception if the specified number of arguments is fewer than the number required.</summary>
        /// <param name="numArgs">The number of arguments provided by the caller.</param>
        /// <exception cref="FormatException">An insufficient number of arguments were provided.</exception>
        internal void ValidateNumberOfArgs(int numArgs)
        {
            if (numArgs < _argsRequired)
            {
                throw new FormatException("Index out of range in format string.");
            }
        }

        /// <summary>Parse the composite format string into segments.</summary>
        /// <param name="format">The format string.</param>
        /// <param name="segments">The list into which to store the segments.</param>
        /// <param name="failureOffset">The offset at which a parsing error occured if <see langword="false"/> is returned.</param>
        /// <param name="failureReason">The reason for a parsing failure if <see langword="false"/> is returned.</param>
        /// <returns>true if the format string can be parsed successfully; otherwise, false.</returns>
        private static bool TryParseLiterals(ReadOnlySpan<char> format, List<(string? Literal, int ArgIndex, int Alignment, string? Format)> segments, ref int failureOffset, ref string failureReason)
        {
            // This parsing logic is copied from string.Format.  It's the same code modified to not format
            // as part of parsing and instead store the parsed literals and argument specifiers (alignment
            // and format) for later use.

            // Rather than parsing directly into the segments list, literals are parsed into a reusable builder.
            // Due to the nature of the parsing logic copied from string.Format, and our desire not to veer from
            // it significantly in order to maintain compatibility and accidental regression, multiple literals
            // next to each other might be parsed separately due to braces in between them.  This builder then
            // allows us to merge those segments back together easily prior to their being appended to the list.
            var vsb = new ValueStringBuilder(stackalloc char[256]);

            // Repeatedly find the next hole and process it.
            int pos = 0;
            char ch;
            while (true)
            {
                // Skip until either the end of the input or the first unescaped opening brace, whichever comes first.
                // Along the way we need to also unescape escaped closing braces.
                while (true)
                {
                    // Find the next brace.  If there isn't one, the remainder of the input is text to be appended, and we're done.
                    ReadOnlySpan<char> remainder = format.Slice(pos);
                    int countUntilNextBrace = remainder.IndexOfAny('{', '}');
                    if (countUntilNextBrace < 0)
                    {
                        vsb.Append(remainder);
                        segments.Add((vsb.ToString(), -1, 0, null));
                        return true;
                    }

                    // Append the text until the brace.
                    vsb.Append(remainder.Slice(0, countUntilNextBrace));
                    pos += countUntilNextBrace;

                    // Get the brace.  It must be followed by another character, either a copy of itself in the case of being
                    // escaped, or an arbitrary character that's part of the hole in the case of an opening brace.
                    char brace = format[pos];
                    if (!TryMoveNext(format, ref pos, out ch))
                    {
                        goto FailureUnclosedFormatItem;
                    }
                    if (brace == ch)
                    {
                        vsb.Append(ch);
                        pos++;
                        continue;
                    }

                    // This wasn't an escape, so it must be an opening brace.
                    if (brace != '{')
                    {
                        goto FailureUnexpectedClosingBrace;
                    }

                    // Proceed to parse the hole.
                    segments.Add((vsb.ToString(), -1, 0, null));
                    vsb.Length = 0;
                    break;
                }

                // We're now positioned just after the opening brace of an argument hole, which consists of
                // an opening brace, an index, an optional width preceded by a comma, and an optional format
                // preceded by a colon, with arbitrary amounts of spaces throughout.
                int width = 0;
                string? itemFormat = null; // used if itemFormat is null

                // First up is the index parameter, which is of the form:
                //     at least on digit
                //     optional any number of spaces
                // We've already read the first digit into ch.
                Debug.Assert(format[pos - 1] == '{');
                Debug.Assert(ch != '{');
                int index = ch - '0';
                if ((uint)index >= 10u)
                {
                    goto FailureExpectedAsciiDigit;
                }

                // Common case is a single digit index followed by a closing brace.  If it's not a closing brace,
                // proceed to finish parsing the full hole format.
                if (!TryMoveNext(format, ref pos, out ch))
                {
                    goto FailureUnclosedFormatItem;
                }
                if (ch != '}')
                {
                    // Continue consuming optional additional digits.
                    while (char.IsAsciiDigit(ch))
                    {
                        index = index * 10 + ch - '0';
                        if (!TryMoveNext(format, ref pos, out ch))
                        {
                            goto FailureUnclosedFormatItem;
                        }
                    }

                    // Consume optional whitespace.
                    while (ch == ' ')
                    {
                        if (!TryMoveNext(format, ref pos, out ch))
                        {
                            goto FailureUnclosedFormatItem;
                        }
                    }

                    // Parse the optional alignment, which is of the form:
                    //     comma
                    //     optional any number of spaces
                    //     optional -
                    //     at least one digit
                    //     optional any number of spaces
                    if (ch == ',')
                    {
                        // Consume optional whitespace.
                        do
                        {
                            if (!TryMoveNext(format, ref pos, out ch))
                            {
                                goto FailureUnclosedFormatItem;
                            }
                        }
                        while (ch == ' ');

                        // Consume an optional minus sign indicating left alignment.
                        int leftJustify = 1;
                        if (ch == '-')
                        {
                            leftJustify = -1;
                            if (!TryMoveNext(format, ref pos, out ch))
                            {
                                goto FailureUnclosedFormatItem;
                            }
                        }

                        // Parse alignment digits. The read character must be a digit.
                        width = ch - '0';
                        if ((uint)width >= 10u)
                        {
                            goto FailureExpectedAsciiDigit;
                        }
                        if (!TryMoveNext(format, ref pos, out ch))
                        {
                            goto FailureUnclosedFormatItem;
                        }
                        while (char.IsAsciiDigit(ch))
                        {
                            width = width * 10 + ch - '0';
                            if (!TryMoveNext(format, ref pos, out ch))
                            {
                                goto FailureUnclosedFormatItem;
                            }
                        }
                        width *= leftJustify;

                        // Consume optional whitespace
                        while (ch == ' ')
                        {
                            if (!TryMoveNext(format, ref pos, out ch))
                            {
                                goto FailureUnclosedFormatItem;
                            }
                        }
                    }

                    // The next character needs to either be a closing brace for the end of the hole,
                    // or a colon indicating the start of the format.
                    if (ch != '}')
                    {
                        if (ch != ':')
                        {
                            // Unexpected character
                            goto FailureUnclosedFormatItem;
                        }

                        // Search for the closing brace; everything in between is the format,
                        // but opening braces aren't allowed.
                        int startingPos = pos;
                        while (true)
                        {
                            if (!TryMoveNext(format, ref pos, out ch))
                            {
                                goto FailureUnclosedFormatItem;
                            }

                            if (ch == '}')
                            {
                                // Argument hole closed
                                break;
                            }

                            if (ch == '{')
                            {
                                // Braces inside the argument hole are not supported
                                goto FailureUnclosedFormatItem;
                            }
                        }

                        startingPos++;
                        itemFormat = format.Slice(startingPos, pos - startingPos).ToString();
                    }
                }

                Debug.Assert(format[pos] == '}');
                pos++;

                segments.Add((null, index, width, itemFormat));

                // Continue parsing the rest of the format string.
            }

            FailureUnexpectedClosingBrace:
            failureReason = "Unexpected closing brace";
            failureOffset = pos;
            return false;

            FailureUnclosedFormatItem:
            failureReason = "Unclosed format item";
            failureOffset = pos;
            return false;

            FailureExpectedAsciiDigit:
            failureReason = "Expected ASCII digit";
            failureOffset = pos;
            return false;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool TryMoveNext(ReadOnlySpan<char> format, ref int pos, out char nextChar)
            {
                pos++;
                if ((uint)pos >= (uint)format.Length)
                {
                    nextChar = '\0';
                    return false;
                }

                nextChar = format[pos];
                return true;
            }
        }
    }
}

internal ref struct ValueStringBuilder
{
  private char[] _arrayToReturnToPool;
  private Span<char> _chars;
  private int _pos;

  public ValueStringBuilder(Span<char> initialBuffer)
  {
    this._arrayToReturnToPool = (char[]) null;
    this._chars = initialBuffer;
    this._pos = 0;
  }

  public ValueStringBuilder(int initialCapacity)
  {
    this._arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
    this._chars = (Span<char>) this._arrayToReturnToPool;
    this._pos = 0;
  }

  public int Length
  {
    get => this._pos;
    set => this._pos = value;
  }

  public int Capacity => this._chars.Length;

  public void EnsureCapacity(int capacity)
  {
    if ((uint) capacity <= (uint) this._chars.Length)
      return;
    this.Grow(capacity - this._pos);
  }

  public ref char GetPinnableReference() => ref MemoryMarshal.GetReference<char>(this._chars);

  public ref char GetPinnableReference(bool terminate)
  {
    if (terminate)
    {
      this.EnsureCapacity(this.Length + 1);
      this._chars[this.Length] = char.MinValue;
    }
    return ref MemoryMarshal.GetReference<char>(this._chars);
  }

  public ref char this[int index] => ref this._chars[index];

  public override string ToString()
  {
    string str = this._chars.Slice(0, this._pos).ToString();
    this.Dispose();
    return str;
  }

  public Span<char> RawChars => this._chars;

  public ReadOnlySpan<char> AsSpan(bool terminate)
  {
    if (terminate)
    {
      this.EnsureCapacity(this.Length + 1);
      this._chars[this.Length] = char.MinValue;
    }
    return (ReadOnlySpan<char>) this._chars.Slice(0, this._pos);
  }

  public ReadOnlySpan<char> AsSpan() => (ReadOnlySpan<char>) this._chars.Slice(0, this._pos);

  public ReadOnlySpan<char> AsSpan(int start)
  {
    return (ReadOnlySpan<char>) this._chars.Slice(start, this._pos - start);
  }

  public ReadOnlySpan<char> AsSpan(int start, int length)
  {
    return (ReadOnlySpan<char>) this._chars.Slice(start, length);
  }

  public bool TryCopyTo(Span<char> destination, out int charsWritten)
  {
    if (this._chars.Slice(0, this._pos).TryCopyTo(destination))
    {
      charsWritten = this._pos;
      this.Dispose();
      return true;
    }
    charsWritten = 0;
    this.Dispose();
    return false;
  }

  public void Insert(int index, char value, int count)
  {
    if (this._pos > this._chars.Length - count)
      this.Grow(count);
    int length = this._pos - index;
    this._chars.Slice(index, length).CopyTo(this._chars.Slice(index + count));
    this._chars.Slice(index, count).Fill(value);
    this._pos += count;
  }

  public void Insert(int index, string s)
  {
    if (s == null)
      return;
    int length1 = s.Length;
    if (this._pos > this._chars.Length - length1)
      this.Grow(length1);
    int length2 = this._pos - index;
    this._chars.Slice(index, length2).CopyTo(this._chars.Slice(index + length1));
    s.AsSpan().CopyTo(this._chars.Slice(index));
    this._pos += length1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Append(char c)
  {
    int pos = this._pos;
    Span<char> chars = this._chars;
    if ((uint) pos < (uint) chars.Length)
    {
      chars[pos] = c;
      this._pos = pos + 1;
    }
    else
      this.GrowAndAppend(c);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Append(string s)
  {
    if (s == null)
      return;
    int pos = this._pos;
    if (s.Length == 1 && (uint) pos < (uint) this._chars.Length)
    {
      this._chars[pos] = s[0];
      this._pos = pos + 1;
    }
    else
      this.AppendSlow(s);
  }

  private void AppendSlow(string s)
  {
    int pos = this._pos;
    if (pos > this._chars.Length - s.Length)
      this.Grow(s.Length);
    s.AsSpan().CopyTo(this._chars.Slice(pos));
    this._pos += s.Length;
  }

  public void Append(char c, int count)
  {
    if (this._pos > this._chars.Length - count)
      this.Grow(count);
    Span<char> span = this._chars.Slice(this._pos, count);
    for (int index = 0; index < span.Length; ++index)
      span[index] = c;
    this._pos += count;
  }

  public unsafe void Append(char* value, int length)
  {
    if (this._pos > this._chars.Length - length)
      this.Grow(length);
    Span<char> span = this._chars.Slice(this._pos, length);
    for (int index = 0; index < span.Length; ++index)
      span[index] = *value++;
    this._pos += length;
  }

  public void Append(scoped ReadOnlySpan<char> value)
  {
    if (this._pos > this._chars.Length - value.Length)
      this.Grow(value.Length);
    value.CopyTo(this._chars.Slice(this._pos));
    this._pos += value.Length;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public Span<char> AppendSpan(int length)
  {
    int pos = this._pos;
    if (pos > this._chars.Length - length)
      this.Grow(length);
    this._pos = pos + length;
    return this._chars.Slice(pos, length);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private void GrowAndAppend(char c)
  {
    this.Grow(1);
    this.Append(c);
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  private void Grow(int additionalCapacityBeyondPos)
  {
    char[] destination = ArrayPool<char>.Shared.Rent((int) Math.Max((uint) (this._pos + additionalCapacityBeyondPos), Math.Min((uint) (this._chars.Length * 2), 2147483591U)));
    this._chars.Slice(0, this._pos).CopyTo((Span<char>) destination);
    char[] arrayToReturnToPool = this._arrayToReturnToPool;
    this._chars = (Span<char>) (this._arrayToReturnToPool = destination);
    if (arrayToReturnToPool == null)
      return;
    ArrayPool<char>.Shared.Return(arrayToReturnToPool);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public unsafe void Dispose()
  {
    char[] arrayToReturnToPool = this._arrayToReturnToPool;
    this = new ValueStringBuilder();
    if (arrayToReturnToPool == null)
      return;
    ArrayPool<char>.Shared.Return(arrayToReturnToPool);
  }
}
