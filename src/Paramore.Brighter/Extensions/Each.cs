#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions
{
    public static class EnumerationExtensions
    {
        /// <summary>
        /// Enumerate a range performing an action on each item
        /// </summary>
        /// <typeparam name="T">The type of the range to enumerate</typeparam>
        /// <param name="collection">The rage to enumerate</param>
        /// <param name="doThis">The action to perform on each action</param>
        public static void Each<T>(this IEnumerable<T> collection, Action<T> doThis)
        {
            foreach (var item in collection)
            {
                doThis(item);
            }
        }


        public static async Task EachAsync<T>(this IEnumerable<T> collection, Func<T, Task> doThis)
        {
            foreach (T item in collection)
            {
                await doThis(item);
            }
        }
    }
}
