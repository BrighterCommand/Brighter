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

#nullable enable

using System;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.MSSQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// The runner's lock resource is <c>BrighterMigration_&lt;schema&gt;.&lt;table&gt;</c>, with no schema
/// meaning <c>dbo</c>, so same-named tables in different schemas take different locks. The fake lock
/// throws once it has recorded the resource, so neither the schema nor the table need exist.
/// </summary>
public class MsSqlRunnerLockResourceSchemaQualificationTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;

    [Theory]
    [InlineData(null, "dbo")]
    [InlineData("dbo", "dbo")]
    [InlineData("billing", "billing")]
    public async Task When_mssql_runner_acquires_lock_resource_should_be_qualified_by_schema(
        string? configuredSchema, string expectedSchemaInLockResource)
    {
        //Arrange
        await Configuration.EnsureDatabaseExistsAsync(_connectionString);
        var tableName = $"test_outbox_{Guid.NewGuid():N}";
        var config = new RelationalDatabaseConfiguration(
            _connectionString, outBoxTableName: tableName, schemaName: configuredSchema);

        var fakeLock = new FakeMsSqlAdvisoryLock(
            throwOnAcquire: new InvalidOperationException("acquire-then-stop probe for lock-resource assertion"));

        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30), fakeLock);
        var freshHint = new BoxTableState(TableExists: false, HistoryExists: false, CurrentVersion: 0);

        //Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.MigrateAsync(
                tableName, configuredSchema, BoxType.Outbox, freshHint));

        //Assert
        Assert.Equal(
            $"BrighterMigration_{expectedSchemaInLockResource}.{tableName}",
            fakeLock.AcquiredResource);
    }
}
