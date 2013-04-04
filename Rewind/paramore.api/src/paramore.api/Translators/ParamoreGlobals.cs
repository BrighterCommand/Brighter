namespace Paramore.Adapters.Presentation.API.Translators
{
    //TODO: Change these to read from a configuration file

    internal static class ParamoreGlobals
    {
        private static string hostName = "localhost:59280";
        private static string map= "map";
        private const string self = "self";

        public static string HostName
        {
            get { return hostName; }
        }

        public static string Self
        {
            get { return self; }
        }

        public static string Map
        {
            get { return map; }
        }
    }
}