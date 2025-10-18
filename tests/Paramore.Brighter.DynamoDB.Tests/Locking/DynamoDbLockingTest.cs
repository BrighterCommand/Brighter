using System;
using Paramore.Brighter.Base.Test.Locking;
using Paramore.Brighter.Locking.DynamoDb;

namespace Paramore.Brighter.DynamoDB.Tests.Locking;

public class DynamoDbLockingTest : DistributedLockingAsyncTest
{
    private readonly string _leaseholderGroupId = Uuid.NewAsString();
    protected override TimeSpan DelayBetweenTryAcquireLockOnSameResource { get; } = TimeSpan.FromSeconds(11);

    protected override IDistributedLock CreateDistributedLock()
    {
       var tableName = DynamoDbLockingTable.EnsureTableIsCreatedAsync(Const.DynamoDbClient)
           .GetAwaiter()
           .GetResult();

       return new DynamoDbLockingProvider(Const.DynamoDbClient,
           new DynamoDbLockingProviderOptions(tableName, _leaseholderGroupId)
           {
               LeaseValidity = TimeSpan.FromSeconds(10)
           });
    }
}
