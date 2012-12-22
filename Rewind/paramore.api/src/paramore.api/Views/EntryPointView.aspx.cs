using OpenRasta.Codecs.WebForms;
using paramore.api.Resources;

namespace paramore.api.Views
{
    public partial class EntryPointView : ResourceView<EntryPoint>
    {
        public EntryPoint MyEntryPoint
        {
            get { return Resource; }
        }
         
    }
}