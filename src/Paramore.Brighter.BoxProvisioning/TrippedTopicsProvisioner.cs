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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Provisions the tripped topics table that distributed circuit-breaker instances share. It guards
/// the identifiers and calls the backend's <see cref="IAmABoxMigrationRunner"/>, which re-detects the
/// table state under its own lock, so the provisioner does no detection and no payload validation.
/// </summary>
public class TrippedTopicsProvisioner : IAmABoxProvisioner
{
    private readonly SchemaName? _schemaName;
    private readonly IAmABoxMigrationRunner _migrationRunner;

    /// <summary>
    /// Initialises a provisioner for one trip table.
    /// </summary>
    /// <param name="tableName">The trip table name; must be a safe SQL identifier.</param>
    /// <param name="schemaName">The database schema, or null when the backend has none.</param>
    /// <param name="migrationRunner">The backend's migration runner, bound to the trip table's catalog.</param>
    public TrippedTopicsProvisioner(
        BoxTableName tableName,
        SchemaName? schemaName,
        IAmABoxMigrationRunner migrationRunner)
    {
        BoxTableName = tableName;
        _schemaName = schemaName;
        _migrationRunner = migrationRunner;
    }

    /// <inheritdoc />
    public BoxType BoxType => BoxType.TrippedTopics;

    /// <inheritdoc />
    public BoxTableName BoxTableName { get; }

    /// <inheritdoc />
    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        GuardIdentifiers();

        await _migrationRunner.MigrateAsync(
            BoxTableName,
            _schemaName,
            BoxType.TrippedTopics,
            new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0),
            cancellationToken);
    }

    private void GuardIdentifiers()
    {
        Identifiers.AssertSafe(BoxTableName.Value, nameof(BoxTableName));
        if (_schemaName is not null)
            Identifiers.AssertSafe(_schemaName.Value, nameof(SchemaName));
    }
}
