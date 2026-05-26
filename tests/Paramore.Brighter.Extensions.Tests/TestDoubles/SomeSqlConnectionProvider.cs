using System;
using System.Data.Common;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public partial class TestBrighterExtension
{
    public class SomeSqlConnectionProvider : RelationalDbConnectionProvider
    {
        public override DbConnection GetConnection()
        {
            throw new NotImplementedException();
        }
    }
}