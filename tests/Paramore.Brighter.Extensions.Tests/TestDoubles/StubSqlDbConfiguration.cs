using System;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public partial class TestBrighterExtension
{
    public class StubSqlDbConfiguration : IAmARelationalDatabaseConfiguration
    {
        public bool JsonMessagePayload => throw new NotImplementedException();
        public string ConnectionString => throw new NotImplementedException();
        public string OutBoxTableName => throw new NotImplementedException();
        public string InBoxTableName => throw new NotImplementedException();
        public bool BinaryMessagePayload => throw new NotImplementedException();

        public string DatabaseName => throw new NotImplementedException();

        public string QueueStoreTable => throw new NotImplementedException();

        public string? SchemaName => null;
    }
}