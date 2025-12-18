// ReSharper disable once CheckNamespace
namespace System.Collections.Concurrent;

public static class ConcurrentDictionaryExtensions
{
    extension<TKey, TValue>(ConcurrentDictionary<TKey, TValue> concurrent) where TKey : notnull
    {
        public Dictionary<TKey, TValue> ToDictionary()
        {
            return concurrent.ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
