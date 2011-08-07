using Nancy;

namespace Paramore.Web.Modules
{
    public class ScheduledMeetingsModule : NancyModule
    {
        public ScheduledMeetingsModule() : base("/scheduler")
        {
            Get["/{date}"] = parameters =>
            {
                return View["Meeting"];
            };
            Post["/add"] = parameters =>
            {
                return Response.AsRedirect("/scheduler/10-AUG-2011");
            };
        }
    }
}
