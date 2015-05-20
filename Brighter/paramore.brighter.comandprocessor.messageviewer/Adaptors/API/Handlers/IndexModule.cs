using Nancy;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers
{
    public class IndexModule : NancyModule
    {
        public IndexModule() 
        {
            Get["/"] = _ => Response.AsFile("assets/views/index.html", "text/html");
            //Get["/"] = _ => Response.AsFile("index.html", "text/html");
        }
    }
}