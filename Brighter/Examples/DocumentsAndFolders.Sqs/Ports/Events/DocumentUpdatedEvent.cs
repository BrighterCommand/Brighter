using System;

using paramore.brighter.commandprocessor;

namespace DocumentsAndFolders.Sqs.Ports.Events
{
    public class DocumentUpdatedEvent : Event
    {
        public DocumentUpdatedEvent(Guid id, int documentId, string title, int folderId)
            : base(id)
        {
            DocumentId = documentId;
            Title = title;
            FolderId = folderId;
        }

        public int DocumentId { get; set; }

        public string Title { get; set; }

        public int FolderId { get; set; }
    }
}