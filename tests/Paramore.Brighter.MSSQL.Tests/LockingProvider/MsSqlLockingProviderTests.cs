using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Locking.MsSql;
using Paramore.Brighter.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.LockingProvider;

[Trait("Category", "MSSQL")]
public class MsSqlLockingProviderTests
{
    private readonly MsSqlTestHelper _msSqlTestHelper;

    public MsSqlLockingProviderTests()
    {
        _msSqlTestHelper = new MsSqlTestHelper();
        _msSqlTestHelper.SetupMessageDb();
    }


    [Fact]
    public async Task GivenAMsSqlLockingProvider_WhenLockIsCalled_LockCanBeObtainedAndThenReleased()
    {
        var provider = new MsSqlLockingProvider(_msSqlTestHelper.ConnectionProvider);
        var resource = "Sweeper";

        var result = await provider.ObtainLockAsync(resource, CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(resource, result);

        await provider.ReleaseLockAsync(resource, result, CancellationToken.None);
    }

    [Fact]
    public async Task GivenTwoLockingProviders_WhenLockIsCalledOnBoth_OneFailsUntilTheFirstLockIsReleased()
    {
        var provider1 = new MsSqlLockingProvider(_msSqlTestHelper.ConnectionProvider);
        var provider2 = new MsSqlLockingProvider(_msSqlTestHelper.ConnectionProvider);
        var resource = "Sweeper";

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
        
        var provider1 = new MsSqlLockingProvider(_msSqlTestHelper.ConnectionProvider);
        
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
        var connectionProvider = _msSqlTestHelper.ConnectionProvider;
        var connection = await connectionProvider.GetConnectionAsync(CancellationToken.None);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = MsSqlLockingQueries.ObtainLockQuery;
        command.Parameters.Add(new SqlParameter("@Resource", SqlDbType.NVarChar, 255));
        command.Parameters["@Resource"].Value = resource;
        command.Parameters.Add(new SqlParameter("@LockTimeout", SqlDbType.Int));
        command.Parameters["@LockTimeout"].Value = 0;

        var respone = await command.ExecuteScalarAsync(CancellationToken.None);
        
        //Assert Lock was successful
        int.TryParse(respone.ToString(), out var responseCode);
        Assert.True(responseCode >= 0);

        return connection;
    }
}
