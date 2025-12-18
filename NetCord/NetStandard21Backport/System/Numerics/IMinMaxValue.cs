// ReSharper disable once CheckNamespace
namespace System.Numerics;

public interface IMinMaxValue<TSelf> where TSelf : IMinMaxValue<TSelf>
{
    static TSelf MaxValue { get; }
    static TSelf MinValue { get; }
}
