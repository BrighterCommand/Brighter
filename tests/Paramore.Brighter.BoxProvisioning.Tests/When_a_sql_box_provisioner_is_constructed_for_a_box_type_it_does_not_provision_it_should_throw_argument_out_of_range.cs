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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// A <see cref="SqlBoxProvisioner{TConnection, TTransaction}"/> provisions an outbox or an inbox table. A box type it has
/// no table for must fail when the provisioner is built, not be treated as an inbox.
/// </summary>
public class SqlBoxProvisionerUnsupportedBoxTypeTests
{
    [Fact]
    public void When_a_sql_box_provisioner_is_constructed_for_a_box_type_it_does_not_provision_it_should_throw_argument_out_of_range()
    {
        //Arrange
        const BoxType unsupportedBoxType = BoxType.TrippedTopics;

        //Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ProbeProvisioner(unsupportedBoxType));

        //Assert
        Assert.Contains(nameof(BoxType.TrippedTopics), ex.Message, StringComparison.Ordinal);
    }

    private sealed class ProbeProvisioner : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public ProbeProvisioner(BoxType boxType)
            : base(
                new StubBoxDetectionHelper(),
                new StubBoxMigrationCatalog(),
                new UnusedPayloadValidator(),
                new StubRelationalDatabaseConfiguration(),
                new UnusedMigrationRunner(),
                boxType)
        {
        }

        protected override FakeDbConnection CreateConnection(string connectionString) => new FakeDbConnection();

        protected override string PayloadColumnName => "Body";
    }

    private sealed class UnusedPayloadValidator : IAmABoxPayloadModeValidator<FakeDbConnection>
    {
        public Task ValidateAsync(
            FakeDbConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class UnusedMigrationRunner : IAmABoxMigrationRunner
    {
        public Task MigrateAsync(
            BoxTableName tableName,
            SchemaName? schemaName,
            BoxType boxType,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
