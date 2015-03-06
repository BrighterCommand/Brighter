namespace paramore.rewind.adapters.presentation.api.Translators
{
    //TODO: Move declarations that can change to a configuration file
    public static class ParamoreGlobals
    {
        static ParamoreGlobals()
        {
            HostName = "localhost:59280";
            Self = "self";
            Map = "map";
        }

        public static object HostName { get; private set; }
        public static string Map { get; private set; }
        public static string Self { get; private set; }
    }
}