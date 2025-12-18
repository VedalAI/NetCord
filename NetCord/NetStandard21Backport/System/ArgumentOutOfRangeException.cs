using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System;

public static class ArgumentOutOfRangeExceptionExtensions
{
    extension(ArgumentOutOfRangeException)
    {
        public static void ThrowIfNegative(int value, [CallerArgumentExpression("value")] string? paramName = null)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");
        }
        
        public static void ThrowIfLessThan(int value, int minValue, [CallerArgumentExpression("value")] string? paramName = null)
        {
            if (value < minValue)
                throw new ArgumentOutOfRangeException(paramName, $"Value must be at least {minValue}.");
        }
        
        public static void ThrowIfLessThanOrEqual(int value, int minValue, [CallerArgumentExpression("value")] string? paramName = null)
        {
            if (value <= minValue)
                throw new ArgumentOutOfRangeException(paramName, $"Value must be greater than {minValue}.");
        }
        
        public static void ThrowIfGreaterThan(int value, int maxValue, [CallerArgumentExpression("value")] string? paramName = null)
        {
            if (value > maxValue)
                throw new ArgumentOutOfRangeException(paramName, $"Value must be less than or equal to {maxValue}.");
        }
    }
}
