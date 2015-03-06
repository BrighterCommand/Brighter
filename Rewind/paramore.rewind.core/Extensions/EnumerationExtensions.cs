using System;
using System.Collections.Generic;

namespace Paramore.Rewind.Core.Extensions
{
    public static class EnumerationExtensions
    {
        public static void Each<T>(this IEnumerable<T> collection, Action<T> doThis)
        {
            foreach (T item in collection)
            {
                doThis(item);
            }
        }
    }
}