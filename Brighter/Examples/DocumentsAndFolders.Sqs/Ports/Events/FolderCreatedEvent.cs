using System;
using System.Reflection.Emit;

using paramore.brighter.commandprocessor;

namespace DocumentsAndFolders.Sqs.Ports.Events
{
    public class FolderCreatedEvent : Event {
        public FolderCreatedEvent(Guid id, int folderId, string title)
            : base(id)
        {
            FolderId = folderId;
            Title = title;
        }

        public int FolderId { get; set; }

        public string Title { get; set; }
    }
}