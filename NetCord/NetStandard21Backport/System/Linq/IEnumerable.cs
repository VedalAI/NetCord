// ReSharper disable once CheckNamespace
namespace System.Linq;

public static class EnumerableExtensions
{
    extension<TSource>(IEnumerable<TSource> self)
    {
        public IEnumerable<TSource> DistinctBy<TKey>(Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            foreach (var element in self)
            {
                if (seenKeys.Add(keySelector(element))) 
                    yield return element;
            }
        }
    }
}
