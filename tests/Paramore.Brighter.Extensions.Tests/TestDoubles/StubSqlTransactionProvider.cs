using System;
using System.Data.Common;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public partial class TestBrighterExtension
{
    public class StubSqlTransactionProvider : RelationalDbTransactionProvider
    {
        public override DbConnection GetConnection()
        {
            throw new NotImplementedException();
        }
    }
}