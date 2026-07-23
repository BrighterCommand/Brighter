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
    /// Role: Supplies the SQL a relational outbox uses to track and replay the causation id of the messages it stores.
    /// Responsibility: Knowing the backend-specific statements for writing the <c>CausationId</c> column, clearing the
    /// dispatched state of a causation's messages, and probing that the column exists.
    /// </summary>
    /// <remarks>
    /// This is an optional companion to <see cref="IRelationDatabaseOutboxQueries"/>. When a backend's query set
    /// implements it, <see cref="RelationDatabaseOutbox"/> exposes <see cref="IAmACausationTrackingOutbox"/> behaviour;
    /// when it does not, the outbox reports that it does not support causation tracking. This keeps causation tracking
    /// opt-in and per-backend, so backends that have not yet added the column continue to work.
    /// </remarks>
    public interface IRelationalDatabaseOutboxCausationQueries
    {
        /// <summary>
        /// An <c>INSERT</c> that also writes the <c>CausationId</c> column (in addition to the columns written by
        /// <see cref="IRelationDatabaseOutboxQueries.AddCommand"/>). Takes a leading <c>{0}</c> table-name placeholder.
        /// </summary>
        string AddCausationCommand { get; }

        /// <summary>
        /// A bulk <c>INSERT</c> that also writes the <c>CausationId</c> column for every row (in addition to the columns
        /// written by <see cref="IRelationDatabaseOutboxQueries.BulkAddCommand"/>). Takes a leading <c>{0}</c> table-name
        /// placeholder and a trailing <c>{1}</c> placeholder for the generated per-row <c>VALUES</c> tuples.
        /// </summary>
        string BulkAddCausationCommand { get; }

        /// <summary>
        /// An <c>UPDATE</c> that clears the dispatched state of every message stored under a given <c>CausationId</c>,
        /// so the sweeper resends them. Takes a leading <c>{0}</c> table-name placeholder.
        /// </summary>
        string ReplayCausationCommand { get; }

        /// <summary>
        /// A statement that returns a row when the <c>CausationId</c> column exists on the outbox table, used as the
        /// runtime schema-support probe. Takes a leading <c>{0}</c> table-name placeholder.
        /// </summary>
        /// <remarks>
        /// The backend probes (MsSql <c>OBJECT_ID</c>, Postgres <c>to_regclass</c>, MySql <c>information_schema.
        /// table_name</c>) resolve the table against the connection's default schema/database, so a box table in a
        /// non-default schema can be reported as "column absent" even when it is present — silently degrading to no
        /// causation tracking. This mirrors the existing unqualified <c>FROM {0}</c> runtime queries (it is a shared
        /// limitation of the box SQL, not specific to this probe) rather than a regression.
        /// </remarks>
        string CausationColumnExistsCommand { get; }
    }
}
