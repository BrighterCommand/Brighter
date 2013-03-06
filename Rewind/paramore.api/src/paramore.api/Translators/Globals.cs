namespace Paramore.Adapters.Presentation.API.Translators
{
    //TODO: Change these to read from a configuration file

    internal class Globals
    {
        private static string hostName = "//localhost:59280";

        public static string HostName
        {
            get { return hostName; }
        }
    }
}