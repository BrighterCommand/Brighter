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

#nullable enable

namespace Paramore.Brighter;

/// <summary>
/// Supplies relational outbox inserts that persist the claim-check DataRef header.
/// </summary>
/// <remarks>
/// Optional companion to <see cref="IRelationDatabaseOutboxQueries"/>. The outbox probes the live table
/// before choosing these inserts; tables without DataRef retain the existing insert commands.
/// Schema support is cached per outbox instance, so recreate the outbox after a schema migration.
/// Table-name resolution follows the provider's existing runtime queries and default schema conventions.
/// </remarks>
public interface IRelationalDatabaseOutboxDataRefQueries
{
    /// <summary>
    /// Inserts the standard message columns followed by DataRef. Uses {0} for the table name.
    /// </summary>
    string AddDataRefCommand { get; }

    /// <summary>
    /// Inserts the standard message columns followed by DataRef and CausationId. Uses {0} for the table name.
    /// </summary>
    string AddDataRefCausationCommand { get; }

    /// <summary>
    /// Inserts rows with the standard columns followed by DataRef. Uses {0} for the table and {1} for values.
    /// </summary>
    string BulkAddDataRefCommand { get; }

    /// <summary>
    /// Inserts rows with the standard columns followed by DataRef and CausationId.
    /// Uses {0} for the table name and {1} for the generated values tuples.
    /// </summary>
    string BulkAddDataRefCausationCommand { get; }

    /// <summary>
    /// Returns a row when the live table contains DataRef. Uses {0} for the table name.
    /// </summary>
    string DataRefColumnExistsCommand { get; }
}
