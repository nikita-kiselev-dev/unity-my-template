using System.Collections.Generic;
using System.Collections;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is ICollection<T> collection)
            {
                return collection.Count == 0;
            }

            if (enumerable is ICollection nonGeneric)
            {
                return nonGeneric.Count == 0;
            }

            using var enumerator = enumerable.GetEnumerator();
            return !enumerator.MoveNext();
        }
    }
}
