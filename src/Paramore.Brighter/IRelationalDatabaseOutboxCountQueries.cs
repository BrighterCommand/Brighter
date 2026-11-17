#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

namespace Paramore.Brighter;

/// <summary>
/// Supplies a server-side count of outstanding messages with an age filter and a ceiling.
/// </summary>
/// <remarks>
/// Optional companion to <see cref="IRelationDatabaseOutboxQueries"/>. Query providers without
/// this capability retain the paged-read count. Uses the provider's outstanding-page parameters
/// with a page size of maxCount and page number one, and {0} for the table name.
/// </remarks>
public interface IRelationalDatabaseOutboxCountQueries
{
    /// <summary>
    /// Counts at most the requested page size of undispatched messages older than the cutoff.
    /// Returns a single integer column without selecting message payloads.
    /// </summary>
    string GetOutstandingMessageCountCommand { get; }
}
