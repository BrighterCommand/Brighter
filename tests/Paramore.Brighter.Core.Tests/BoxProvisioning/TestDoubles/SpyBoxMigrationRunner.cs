using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;

internal sealed class SpyBoxMigrationRunner : IAmABoxMigrationRunner
{
    public List<(BoxTableName TableName, SchemaName? SchemaName, BoxType BoxType)> Calls { get; } = [];

    public Task MigrateAsync(
        BoxTableName tableName,
        SchemaName? schemaName,
        BoxType boxType,
        BoxTableState tableState,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((tableName, schemaName, boxType));
        return Task.CompletedTask;
    }
}
