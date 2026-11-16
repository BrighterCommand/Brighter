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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.Core.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.BoxProvisioning;

public class TrippedTopicsProvisionerRunsTests
{
    [Fact]
    public async Task When_the_provisioner_runs_should_call_the_runner_once_with_the_trip_table_the_schema_and_the_tripped_topics_box_type()
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner("BrighterTrippedTopics", new SchemaName("brighter"), runner);

        //Act
        await provisioner.ProvisionAsync(CancellationToken.None);

        //Assert
        Assert.Equal(BoxType.TrippedTopics, provisioner.BoxType);
        Assert.Equal("BrighterTrippedTopics", provisioner.BoxTableName.Value);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("BrighterTrippedTopics", call.TableName.Value);
        Assert.Equal("brighter", call.SchemaName?.Value);
        Assert.Equal(BoxType.TrippedTopics, call.BoxType);
    }

    [Fact]
    public async Task When_no_schema_is_configured_should_call_the_runner_with_no_schema()
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner("BrighterTrippedTopics", null, runner);

        //Act
        await provisioner.ProvisionAsync(CancellationToken.None);

        //Assert
        var call = Assert.Single(runner.Calls);
        Assert.Null(call.SchemaName);
    }

    [Theory]
    [InlineData("Trips; DROP TABLE Outbox", null)]
    [InlineData("1Trips", null)]
    [InlineData("BrighterTrippedTopics", "dbo; DROP TABLE Outbox")]
    public async Task When_a_table_or_schema_name_is_unsafe_should_reject_it_before_calling_the_runner(string tableName, string? schemaName)
    {
        //Arrange
        var runner = new SpyBoxMigrationRunner();
        var provisioner = new TrippedTopicsProvisioner(
            tableName, schemaName is null ? null : new SchemaName(schemaName), runner);

        //Act
        await Assert.ThrowsAsync<ConfigurationException>(() => provisioner.ProvisionAsync(CancellationToken.None));

        //Assert
        Assert.Empty(runner.Calls);
    }
}
