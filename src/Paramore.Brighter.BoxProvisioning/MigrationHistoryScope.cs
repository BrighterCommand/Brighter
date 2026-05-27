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

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Controls where the box migration-history table (<c>__BrighterMigrationHistory</c>) is
/// physically placed relative to the configured
/// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>.
/// </summary>
public enum MigrationHistoryScope
{
    /// <summary>
    /// History lives in the backend default schema (MSSQL <c>dbo</c> / PostgreSQL <c>public</c> /
    /// the connection-bound <c>DATABASE()</c> on MySQL). This is the default and is identical to
    /// the behaviour prior to this feature — even for operators who configure a non-null
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>.
    /// </summary>
    Global = 0,

    /// <summary>
    /// On MSSQL and PostgreSQL, history is created in the configured
    /// <see cref="IAmARelationalDatabaseConfiguration.SchemaName"/>, co-located with the tenant's
    /// box tables inside its isolation/backup boundary. It is a no-op on backends without a
    /// distinct schema concept (MySQL, SQLite, Spanner), where history stays in the default
    /// location and no exception is thrown.
    /// </summary>
    PerSchema = 1
}
