using System;
using System.Collections.Generic;

namespace DocumentsAndFolders.Sqs.Core.Ports.DB
{
    public sealed class FakeDB
    {
        private static readonly Lazy<FakeDB> lazy =
            new Lazy<FakeDB>(() => new FakeDB());

        public static FakeDB Instance { get { return lazy.Value; } }

        private FakeDB()
        {
            Documents = new Dictionary<int, Document>();
            Folders = new Dictionary<int, Folder>();
        }

        public Dictionary<int, Folder> Folders { get; set; }

        public Dictionary<int, Document> Documents { get; private set; }

        public Document GetDocument(int id)
        {
            return Documents.ContainsKey(id) ? Documents[id] : null;
        }

        public Folder GetFolder(int id)
        {
            return Folders.ContainsKey(id) ? Folders[id] : null;
        }

        public void AddUpdateDocument(Document document)
        {
            Documents[document.DocumentId] = document;
        }

        public void AddUpdateFolder(Folder folder)
        {
            Folders[folder.FolderId] = folder;
        }
    }
}