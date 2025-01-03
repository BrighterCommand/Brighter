#region Licence

/* The MIT License (MIT)
Copyright © 2025 Tim Salva <tim@jtsalva.dev>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System;
using System.Collections.Generic;

namespace Paramore.Brighter.Observability;

internal static class TagObjectsExtensions
{
    /// <summary>
    /// Avoids LINQ overhead when filtering activity tags
    /// </summary>
    /// <param name="tags">Tags to be filtered</param>
    /// <param name="allowedTags">Allowed tags to filter for</param>
    /// <returns></returns>
    internal static KeyValuePair<string, object?>[] Filter(
        this IEnumerable<KeyValuePair<string, object?>> tags,
#if NET8_0_OR_GREATER
        FrozenSet<string> allowedTags)
#else
        HashSet<string> allowedTags)
#endif
    {
        var buffer = new KeyValuePair<string, object?>[allowedTags.Count];
        var insertIndex = 0;

        foreach (var tag in tags)
        {
            if (allowedTags.Contains(tag.Key))
            {
                buffer[insertIndex++] = tag;
            }

            if (insertIndex == allowedTags.Count)
            {
                return buffer;
            }
        }

        var filtered = new KeyValuePair<string, object?>[insertIndex];
        Array.Copy(buffer, filtered, insertIndex);
        return filtered;
    }
}