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

public class MySqlMigrationLockNameTests
{
    // MySQL `GET_LOCK(name, timeout)` accepts a name of at most 64 characters; from MySQL 5.7.5
    // onward names exceeding 64 chars raise ER_USER_LOCK_WRONG_NAME, and earlier versions silently
    // truncated the name — which would let two distinct table names share a lock.
    private const int MySqlGetLockNameLimit = 64;

    [Fact]
    public void When_table_name_is_short_lock_name_should_use_simple_prefix_form()
    {
        //Arrange — a typical short table name well within the GET_LOCK limit (18 prefix + 12 = 30).
        const string tableName = "outbox_users";

        //Act
        var lockName = MySqlMigrationLockName.For(tableName);

        //Assert — preserves the historical lock-name format so existing deployments holding a lock
        //under the old name continue to interlock with the new code.
        Assert.Equal("BrighterMigration_outbox_users", lockName);
        Assert.True(lockName.Length <= MySqlGetLockNameLimit);
    }

    [Fact]
    public void When_table_name_is_long_lock_name_should_stay_within_64_chars()
    {
        //Arrange — a 60-char table name; with the simple prefix form (18 + 60 = 78) this would
        //blow MySQL's 64-char GET_LOCK limit.
        var tableName = new string('a', 60);

        //Act
        var lockName = MySqlMigrationLockName.For(tableName);

        //Assert
        Assert.True(
            lockName.Length <= MySqlGetLockNameLimit,
            $"Lock name was {lockName.Length} chars; MySQL GET_LOCK limit is {MySqlGetLockNameLimit}.");
    }

    [Fact]
    public void When_two_long_table_names_share_a_46_char_prefix_lock_names_should_differ()
    {
        //Arrange — two table names that are identical for the first 46 chars (the simple-form
        //budget after the 18-char `BrighterMigration_` prefix). A naïve truncation would collapse
        //both to the same lock name, allowing one runner to skip the lock another already holds.
        var sharedPrefix = new string('x', 46);
        var firstTableName = sharedPrefix + "_alpha_suffix_uniquely_distinguishes_first";
        var secondTableName = sharedPrefix + "_beta_suffix_uniquely_distinguishes_second";

        //Act
        var firstLockName = MySqlMigrationLockName.For(firstTableName);
        var secondLockName = MySqlMigrationLockName.For(secondTableName);

        //Assert — both within the limit, and distinct (collision-resistant via hashed suffix).
        Assert.True(firstLockName.Length <= MySqlGetLockNameLimit);
        Assert.True(secondLockName.Length <= MySqlGetLockNameLimit);
        Assert.NotEqual(firstLockName, secondLockName);
    }
}
