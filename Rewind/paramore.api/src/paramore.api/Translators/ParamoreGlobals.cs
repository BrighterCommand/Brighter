namespace Paramore.Adapters.Presentation.API.Translators
{
    //TODO: Move declarations that can change to a configuration file
    internal static class ParamoreGlobals
    {
        static ParamoreGlobals()
        {
            HostName = "localhost: xxxxx";
            Self = "self";
            Map = "map";
        }

        public static object HostName { get; private set; }
        public static string Map { get; private set; }
        public static string Self { get; private set; }
    }
}