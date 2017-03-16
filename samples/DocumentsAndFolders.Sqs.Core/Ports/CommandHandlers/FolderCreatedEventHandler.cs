using System;
using DocumentsAndFolders.Sqs.Core.Ports.DB;
using DocumentsAndFolders.Sqs.Core.Ports.Events;
using Paramore.Brighter;

namespace DocumentsAndFolders.Sqs.Core.Ports.CommandHandlers
{
    public class FolderCreatedEventHandler : RequestHandler<FolderCreatedEvent>
    {
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