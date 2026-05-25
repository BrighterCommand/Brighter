using System;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public partial class TestBrighterExtension
{
    public class StubRelationDatabaseOutboxQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand => throw new NotImplementedException();

        public string PagedReadCommand => throw new NotImplementedException();

        public string PagedOutstandingCommand => throw new NotImplementedException();

        public string PagedOutstandingCommandInStatement => throw new NotImplementedException();

        public string AddCommand => throw new NotImplementedException();

        public string BulkAddCommand => throw new NotImplementedException();

        public string MarkDispatchedCommand => throw new NotImplementedException();

        public string MarkMultipleDispatchedCommand => throw new NotImplementedException();

        public string GetMessageCommand => throw new NotImplementedException();

        public string GetMessagesCommand => throw new NotImplementedException();

        public string DeleteMessagesCommand => throw new NotImplementedException();

        public string GetNumberOfOutstandingMessagesCommand => throw new NotImplementedException();
    }
}