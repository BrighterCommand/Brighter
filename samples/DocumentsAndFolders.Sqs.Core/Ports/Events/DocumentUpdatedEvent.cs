using System;
using Paramore.Brighter;

namespace DocumentsAndFolders.Sqs.Core.Ports.Events
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