namespace DocumentsAndFolders.Sqs.Core.Ports.DB
{
    public class Document
    {
        public Document(int documentId, int folderId, string title)
        {
            Title = title;
            FolderId = folderId;
            DocumentId = documentId;
        }

        public int DocumentId { get; private set; }

        public int FolderId { get; private set; }

        public string Title { get; private set; }
    }
}