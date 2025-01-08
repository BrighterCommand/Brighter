using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Locking.MySql;
using Paramore.Brighter.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.LockingProvider;

[Trait("Category", "MySql")]
public class MySqlLockingProviderTests
{
    private readonly MySqlTestHelper _msSqlTestHelper;

    public MySqlLockingProviderTests()
    {
        _msSqlTestHelper = new MySqlTestHelper();
        _msSqlTestHelper.SetupMessageDb();
    }


    [Fact]
    public async Task GivenAMySqlLockingProvider_WhenLockIsCalled_LockCanBeObtainedAndThenReleased()
    {
        var provider = new MySqlLockingProvider((MySqlConnectionProvider)_msSqlTestHelper.ConnectionProvider);
        const string resource = "Sweeper";

        var result = await provider.ObtainLockAsync(resource, CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(resource, result);

        await provider.ReleaseLockAsync(resource, result, CancellationToken.None);
    }

    [Fact]
    public async Task GivenTwoLockingProviders_WhenLockIsCalledOnBoth_OneFailsUntilTheFirstLockIsReleased()
    {
        var provider1 = new MySqlLockingProvider((MySqlConnectionProvider)_msSqlTestHelper.ConnectionProvider);
        var provider2 = new MySqlLockingProvider((MySqlConnectionProvider)_msSqlTestHelper.ConnectionProvider);
        const string resource = "Sweeper";

        var firstLock = await provider1.ObtainLockAsync(resource, CancellationToken.None);
        var secondLock = await provider2.ObtainLockAsync(resource, CancellationToken.None);

        Assert.NotEmpty(firstLock);
        Assert.Null(secondLock);

        await provider1.ReleaseLockAsync(resource, firstLock, CancellationToken.None);
        var secondLockAttemptTwo = await provider2.ObtainLockAsync(resource, CancellationToken.None);

        Assert.NotEmpty(secondLockAttemptTwo);
    }

    [Fact]
    public async Task GivenAnExistingLock_WhenConnectionDies_LockIsReleased()
    {
        var resource = Guid.NewGuid().ToString();
        var connection = await ObtainLockForManualDisposal(resource);

        var provider1 = new MySqlLockingProvider((MySqlConnectionProvider)_msSqlTestHelper.ConnectionProvider);

        var lockAttempt = await provider1.ObtainLockAsync(resource, CancellationToken.None);

        // Ensure Lock was not obtained
        Assert.Null(lockAttempt);

        await connection.DisposeAsync();

        var lockAttemptTwo = await provider1.ObtainLockAsync(resource, CancellationToken.None);

        // Ensure Lock was Obtained
        Assert.False(string.IsNullOrEmpty(lockAttemptTwo));
    }

    private async Task<DbConnection> ObtainLockForManualDisposal(string resource)
    {
        var connectionProvider = (MySqlConnectionProvider)_msSqlTestHelper.ConnectionProvider;
        var connection = await connectionProvider.GetConnectionAsync(CancellationToken.None);
        var command = connection.CreateCommand();
        command.CommandText = MySqlLockingQueries.ObtainLockQuery;
        command.Parameters.Add(new MySqlParameter("@RESOURCE_NAME", MySqlDbType.String));
        command.Parameters["@RESOURCE_NAME"].Value = resource;
        command.Parameters.Add(new MySqlParameter("@TIMEOUT", MySqlDbType.UInt32));
        command.Parameters["@TIMEOUT"].Value = 1;

        var respone = await command.ExecuteScalarAsync(CancellationToken.None);

        //Assert Lock was successful
        int.TryParse(respone.ToString(), out var responseCode);
        Assert.True(responseCode == 1);

        return connection;
    }
}
