using System.Collections.Generic;
using System.Reflection;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Embedded.Conventions;
using Nancy.ViewEngines;
using Paramore.Brighter.MessageViewer.Ports.Handlers;

namespace Paramore.Brighter.MessageViewer.Adaptors.API.Configuration
{
    public class BootstrapperEmbeddedHelper
    {
        private static readonly List<string> _assetLocations = new List<string> { "assets", "Content", "fonts", "Scripts" };

        public static void RegisterStaticEmbedded(NancyConventions nancyConventions)
        {
            //Need to register static content for embedded. Views work out of the box
            var staticContentsConventions = nancyConventions.StaticContentsConventions;
            staticContentsConventions.Clear();

            var assembly = GetAssembly();
            foreach (var asset in _assetLocations)
            {
                staticContentsConventions.Add(EmbeddedStaticContentConventionBuilder.AddDirectory(asset, assembly));
            }
        }

        public static void RegisterViewLocationEmbedded()
        {
            ResourceViewLocationProvider
                .RootNamespaces
                .Add(GetAssembly(), "paramore.brighter.commandprocessor.messageviewer");  //embedded resource directory location
        }

        public static byte[] LoadFavIconEmbedded()
        {
            using (var resourceStream = GetAssembly().GetManifestResourceStream("paramore.brighter.commandprocessor.messageviewer.favicon.ico"))
            {
                var tempFavicon = new byte[resourceStream.Length];
                resourceStream.Read(tempFavicon, 0, (int)resourceStream.Length);
                return tempFavicon;
            }
        }
        private static Assembly GetAssembly()
        {
            return typeof(HandlerFactory).GetTypeInfo().Assembly;
        }

        public static void OnConfigurationBuilder(NancyInternalConfiguration obj)
        {
            obj.ViewLocationProvider = typeof(ResourceViewLocationProvider);
        }
        private static byte[] _favicon;

        private static byte[] LoadFavIcon()
        {
            return LoadFavIconEmbedded();
        }
        public static byte[] GetFavIcon()
        {
            return _favicon ?? (_favicon = LoadFavIcon());
        }
    }
}