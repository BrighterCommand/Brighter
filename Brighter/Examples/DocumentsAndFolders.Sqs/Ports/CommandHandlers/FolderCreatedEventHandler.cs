using System;

using DocumentsAndFolders.Sqs.Ports.DB;
using DocumentsAndFolders.Sqs.Ports.Events;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;

namespace Greetings.Ports.CommandHandlers
{
    public class FolderCreatedEventHandler : RequestHandler<FolderCreatedEvent>
    {
        public FolderCreatedEventHandler(ILog logger) : base(logger) { }

        public override FolderCreatedEvent Handle(FolderCreatedEvent @event)
        {
            Console.WriteLine("Received FolderCreatedEvent");
            Console.WriteLine("----------------------------------");
            Console.WriteLine("FolderId: {0}, Title: {1}", @event.FolderId, @event.Title);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");


            FakeDB.Instance.AddUpdateFolder(new Folder(@event.FolderId, @event.Title));
            
            return base.Handle(@event);
        }
    }
}