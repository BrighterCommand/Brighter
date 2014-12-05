#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Web.Http;
using CacheCow.Server;
using Microsoft.Practices.Unity;
using Owin;
using WebApiContrib.IoC.Unity;

namespace paramore.brighter.restms.server.Adapters.Service
{
    internal class WebPipeline
    {
        public void Configuration(IAppBuilder builder)
        {
            var configuration = new HttpConfiguration();
            
            ConfigureDependencyInjection(configuration);

            MapRoutes(configuration);

            ConfigureCaching(configuration);
            
            ConfigureFormatting(configuration);

            ConfigureDiagnostics(configuration);
            
            builder.UseWebApi(configuration);
        }

        void ConfigureCaching(HttpConfiguration configuration)
        {
            var cachingServer = new CachingHandler(configuration);
            configuration.MessageHandlers.Add(cachingServer);
        }

        static void ConfigureDiagnostics(HttpConfiguration configuration)
        {
            configuration.EnableSystemDiagnosticsTracing();
        }

        static void ConfigureFormatting(HttpConfiguration configuration)
        {
            var xml = configuration.Formatters.XmlFormatter;
            xml.UseXmlSerializer = true;
        }

        static void MapRoutes(HttpConfiguration configuration)
        {
            configuration.MapHttpAttributeRoutes();
            configuration.Routes.MapHttpRoute(
                name: "DomainAPI",
                routeTemplate: "restms/{controller}/{name}",
                defaults: new {name = RouteParameter.Optional});

            configuration.Routes.MapHttpRoute(
                name: "MessageAPI",
                routeTemplate: "restms/pipe/{pipeName}/{controller}/{messageName}",
                defaults: new { pipeName = RouteParameter.Optional, messageName = RouteParameter.Optional}
                );

        }

        static void ConfigureDependencyInjection(HttpConfiguration configuration)
        {
            var container = new UnityContainer();
            IoCConfiguration.Run(container);
            InitializeDomains.Run(container);
            configuration.DependencyResolver = new UnityResolver(container);
        }
    }
}