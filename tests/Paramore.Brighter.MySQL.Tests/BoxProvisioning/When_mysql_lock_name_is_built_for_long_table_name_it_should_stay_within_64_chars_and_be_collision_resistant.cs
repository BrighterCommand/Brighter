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

using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// <see cref="MySqlMigrationLockName"/> folds the schema into the lock name and keeps it within
/// <c>GET_LOCK</c>'s 64-character limit, hashing a long composite so that distinct long names, or the
/// same table in two schemas, never share a lock (ADR 0057 §5b).
/// </summary>
public class MySqlMigrationLockNameTests
{
    // MySQL `GET_LOCK(name, timeout)` accepts a name of at most 64 characters; from MySQL 5.7.5
    // onward names exceeding 64 chars raise ER_USER_LOCK_WRONG_NAME, and earlier versions silently
    // truncated the name — which would let two distinct table names share a lock.
    private const int MySqlGetLockNameLimit = 64;

    [Fact]
    public void When_schema_and_table_name_are_short_lock_name_should_use_simple_composite_form()
    {
        //Arrange
        const string schema = "public";
        const string tableName = "outbox_users";

        //Act
        var lockName = MySqlMigrationLockName.For(schema, tableName);

        //Assert
        Assert.Equal("BrighterMigration_public.outbox_users", lockName);
        Assert.True(lockName.Length <= MySqlGetLockNameLimit);
    }

    [Fact]
    public void When_schema_is_null_lock_name_should_omit_schema_segment()
    {
        //Arrange
        const string tableName = "outbox_users";

        //Act
        var lockName = MySqlMigrationLockName.For(schema: null, tableName);

        //Assert
        Assert.Equal("BrighterMigration_outbox_users", lockName);
        Assert.True(lockName.Length <= MySqlGetLockNameLimit);
    }

    [Fact]
    public void When_composite_is_long_lock_name_should_stay_within_64_chars()
    {
        //Arrange
        const string schema = "public";
        var tableName = new string('a', 60);

        //Act
        var lockName = MySqlMigrationLockName.For(schema, tableName);

        //Assert
        Assert.True(
            lockName.Length <= MySqlGetLockNameLimit,
            $"Lock name was {lockName.Length} chars; MySQL GET_LOCK limit is {MySqlGetLockNameLimit}.");
    }

    [Fact]
    public void When_two_long_table_names_share_a_long_prefix_lock_names_should_differ()
    {
        //Arrange
        const string schema = "public";
        var sharedPrefix = new string('x', 46);
        var firstTableName = sharedPrefix + "_alpha_suffix_uniquely_distinguishes_first";
        var secondTableName = sharedPrefix + "_beta_suffix_uniquely_distinguishes_second";

        //Act
        var firstLockName = MySqlMigrationLockName.For(schema, firstTableName);
        var secondLockName = MySqlMigrationLockName.For(schema, secondTableName);

        //Assert
        Assert.True(firstLockName.Length <= MySqlGetLockNameLimit);
        Assert.True(secondLockName.Length <= MySqlGetLockNameLimit);
        Assert.NotEqual(firstLockName, secondLockName);
    }

    [Fact]
    public void When_two_distinct_schemas_share_a_table_name_lock_names_should_differ()
    {
        //Arrange
        const string firstSchema = "public";
        const string secondSchema = "billing";
        const string sharedTableName = "outbox_users";

        //Act
        var firstLockName = MySqlMigrationLockName.For(firstSchema, sharedTableName);
        var secondLockName = MySqlMigrationLockName.For(secondSchema, sharedTableName);

        //Assert
        Assert.NotEqual(firstLockName, secondLockName);
    }
}
