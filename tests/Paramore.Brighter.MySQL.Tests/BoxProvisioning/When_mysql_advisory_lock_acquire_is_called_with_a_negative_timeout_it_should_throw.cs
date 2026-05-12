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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlAdvisoryLockTimeoutValidationTests
{
    // GET_LOCK takes its timeout in whole seconds via an INT bind parameter. The MySql adapter
    // computes `(int)Math.Max(1, Math.Ceiling(timeout.TotalSeconds))`; for a negative TimeSpan
    // the Ceiling result is <= 0 but Math.Max floors at 1, so the call would silently SUCCEED
    // with a 1-second wait — a meaningless interpretation of "negative timeout" and a footgun
    // for callers expecting the input to be rejected as invalid (the same shape PR #4039 review
    // item #7 calls out). Per the cross-backend validation contract pinned by
    // `MsSqlAdvisoryLockTimeoutValidationTests` (ADR 0057 §5b), the validation must live inside
    // the abstraction's acquire path so any caller of MySqlAdvisoryLock — including but not
    // limited to MySqlBoxMigrationRunner — is protected with an actionable
    // ArgumentOutOfRangeException rather than a deadlocked deployment.

    private readonly string _connectionString = Const.DefaultConnectingString;

    [Fact]
    public async Task When_lock_timeout_is_negative_acquire_should_throw()
    {
        //Arrange
        var negativeTimeout = TimeSpan.FromMilliseconds(-1);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, "any_resource", negativeTimeout, default));
    }
}
