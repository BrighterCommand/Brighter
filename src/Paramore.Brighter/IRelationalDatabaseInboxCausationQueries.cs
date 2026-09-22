#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    /// <summary>
    /// Role: Supplies the SQL a relational inbox uses to track the causation id of the requests it stores.
    /// Responsibility: Knowing the backend-specific statements for writing, reading, and probing the
    /// <c>CausationId</c> column.
    /// </summary>
    /// <remarks>
    /// This is an optional companion to <see cref="IRelationalDatabaseInboxQueries"/>. When a backend's query
    /// set implements it, <see cref="RelationalDatabaseInbox"/> exposes <see cref="IAmACausationTrackingInbox"/>
    /// behaviour; when it does not, the inbox reports that it does not support causation tracking. This keeps
    /// causation tracking opt-in and per-backend, so backends that have not yet added the column continue to work.
    /// </remarks>
    public interface IRelationalDatabaseInboxCausationQueries
    {
        /// <summary>
        /// An <c>INSERT</c> that also writes the <c>CausationId</c> column (in addition to the columns written by
        /// <see cref="IRelationalDatabaseInboxQueries.AddCommand"/>). Takes a leading <c>{0}</c> table-name placeholder.
        /// </summary>
        string AddCausationCommand { get; }

        /// <summary>
        /// A <c>SELECT</c> that returns the <c>CausationId</c> for a given command id and context key. Takes a
        /// leading <c>{0}</c> table-name placeholder.
        /// </summary>
        string GetCausationIdCommand { get; }

        /// <summary>
        /// A statement that returns a row when the <c>CausationId</c> column exists on the inbox table, used as the
        /// runtime schema-support probe. Takes a leading <c>{0}</c> table-name placeholder.
        /// </summary>
        string CausationColumnExistsCommand { get; }
    }
}
