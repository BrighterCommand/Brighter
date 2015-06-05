using System.Collections.Generic;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources
{
    public class RepostView
    {
        public RepostView()
        {
            Ids = new List<string>();
        }

        public List<string> Ids { get; set; }
    }
}