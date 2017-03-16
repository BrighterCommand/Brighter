namespace DocumentsAndFolders.Sqs.Core.Ports.DB
{
    public class Folder
    {
        public Folder(int folderId, string title)
        {
            Title = title;
            FolderId = folderId;
        }

        public int FolderId { get; private set; }

        public string Title { get; private set; }
    }
}