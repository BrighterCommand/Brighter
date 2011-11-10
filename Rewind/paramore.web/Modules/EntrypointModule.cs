using Nancy;

namespace Paramore.Web.Modules
{
    public class EntrypointModule : NancyModule
    {
        public EntrypointModule()
        {
            Get["/"] = parameters => "<h1>Welcome to Paramore!</h1><p>Nothing to see here at the moment.</p>";
        }
    }
}