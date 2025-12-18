// ReSharper disable once CheckNamespace
namespace System.Numerics;

public interface IMinMaxValue<TSelf> where TSelf : IMinMaxValue<TSelf>
{
    TSelf MaxValue { get; }
    TSelf MinValue { get; }
}
