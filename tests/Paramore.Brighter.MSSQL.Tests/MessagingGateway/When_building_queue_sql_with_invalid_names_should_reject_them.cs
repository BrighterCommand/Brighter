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

using System;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlQueueBuilderInvalidNamesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void When_building_queue_sql_with_invalid_names_should_reject_them(string? name)
    {
        //Arrange
        string table = name!;

        //Act
        var createError = Record.Exception(() => MsSqlQueueBuilder.GetDDL(table));
        var indexError = Record.Exception(() => MsSqlQueueBuilder.GetIndexDDL(table));
        var existsError = Record.Exception(() => MsSqlQueueBuilder.GetExistsQuery(table));

        //Assert
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(createError).ParamName);
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(indexError).ParamName);
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(existsError).ParamName);
    }

    [Fact]
    public void When_the_table_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string table = new('q', 129);

        //Act
        var createError = Record.Exception(() => MsSqlQueueBuilder.GetDDL(table));
        var existsError = Record.Exception(() => MsSqlQueueBuilder.GetExistsQuery(table));

        //Assert
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(createError).ParamName);
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(existsError).ParamName);
    }

    [Fact]
    public void When_the_derived_index_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string table = new('q', 120);

        //Act
        var exception = Record.Exception(() => MsSqlQueueBuilder.GetIndexDDL(table));

        //Assert
        Assert.Equal("queueTableName", Assert.IsType<ArgumentException>(exception).ParamName);
    }

    [Fact]
    public void When_the_schema_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string schema = new('s', 129);

        //Act
        var exception = Record.Exception(() => MsSqlQueueBuilder.GetExistsQuery("queue", schema));

        //Assert
        Assert.Equal("schemaName", Assert.IsType<ArgumentException>(exception).ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void When_the_schema_name_is_blank_should_reject_it(string schema)
    {
        //Arrange
        string table = "queue";

        //Act
        var exception = Record.Exception(() => MsSqlQueueBuilder.GetExistsQuery(table, schema));

        //Assert
        Assert.Equal("schemaName", Assert.IsType<ArgumentException>(exception).ParamName);
    }
}
