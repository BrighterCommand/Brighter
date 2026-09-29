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


namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlQueueBuilderInvalidNamesTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async System.Threading.Tasks.Task When_building_queue_sql_with_invalid_names_should_reject_them(string? name)
    {
        //Arrange
        string table = name!;

        //Act
        Exception? createError = null;
        try
        {
            MsSqlQueueBuilder.GetDDL(table);
        }
        catch (Exception e)
        {
            createError = e;
        }
        Exception? indexError = null;
        try
        {
            MsSqlQueueBuilder.GetIndexDDL(table);
        }
        catch (Exception e)
        {
            indexError = e;
        }
        Exception? existsError = null;
        try
        {
            MsSqlQueueBuilder.GetExistsQuery(table);
        }
        catch (Exception e)
        {
            existsError = e;
        }

        //Assert
        await Assert.That((await Assert.That(createError).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
        await Assert.That((await Assert.That(indexError).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
        await Assert.That((await Assert.That(existsError).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_table_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string table = new('q', 129);

        //Act
        Exception? createError = null;
        try
        {
            MsSqlQueueBuilder.GetDDL(table);
        }
        catch (Exception e)
        {
            createError = e;
        }
        Exception? existsError = null;
        try
        {
            MsSqlQueueBuilder.GetExistsQuery(table);
        }
        catch (Exception e)
        {
            existsError = e;
        }

        //Assert
        await Assert.That((await Assert.That(createError).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
        await Assert.That((await Assert.That(existsError).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_derived_index_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string table = new('q', 120);

        //Act
        Exception? exception = null;
        try
        {
            MsSqlQueueBuilder.GetIndexDDL(table);
        }
        catch (Exception e)
        {
            exception = e;
        }

        //Assert
        await Assert.That((await Assert.That(exception).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("queueTableName");
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_schema_name_exceeds_the_identifier_limit_should_reject_it()
    {
        //Arrange
        string schema = new('s', 129);

        //Act
        Exception? exception = null;
        try
        {
            MsSqlQueueBuilder.GetExistsQuery("queue", schema);
        }
        catch (Exception e)
        {
            exception = e;
        }

        //Assert
        await Assert.That((await Assert.That(exception).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("schemaName");
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public async System.Threading.Tasks.Task When_the_schema_name_is_blank_should_reject_it(string schema)
    {
        //Arrange
        string table = "queue";

        //Act
        Exception? exception = null;
        try
        {
            MsSqlQueueBuilder.GetExistsQuery(table, schema);
        }
        catch (Exception e)
        {
            exception = e;
        }

        //Assert
        await Assert.That((await Assert.That(exception).IsTypeOf<ArgumentException>()).ParamName).IsEqualTo("schemaName");
    }
}
