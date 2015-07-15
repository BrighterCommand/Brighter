using System;

using DocumentsAndFolders.Sqs.Ports.DB;
using DocumentsAndFolders.Sqs.Ports.Events;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.actions;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;

namespace Greetings.Ports.CommandHandlers
{
    public class DocumentUpdatedEventHandler : RequestHandler<DocumentUpdatedEvent>
    {
        public DocumentUpdatedEventHandler(ILog logger) : base(logger) { }

        public override DocumentUpdatedEvent Handle(DocumentUpdatedEvent @event)
        {
            Console.WriteLine("Received DocumentUpdatedEvent");
            Console.WriteLine("----------------------------------");
            Console.WriteLine("DocumentId: {0}, Title: {1}, FolderId: {2}", @event.DocumentId, @event.Title, @event.FolderId);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            if (FakeDB.Instance.GetDocument(@event.DocumentId) == null)
            {
                Console.WriteLine("Document {0} does not exist. Will requeue", @event.DocumentId);
                throw new DeferMessageAction();
            }

            if (FakeDB.Instance.GetFolder(@event.FolderId) == null)
            {
                Console.WriteLine("Folder {0} does not exist for the document {1}. Will requeue", @event.FolderId, @event.DocumentId);
                throw new DeferMessageAction();
            }

            FakeDB.Instance.AddUpdateDocument(new Document(@event.DocumentId, @event.FolderId, @event.Title));

            return base.Handle(@event);
        }
    }
}