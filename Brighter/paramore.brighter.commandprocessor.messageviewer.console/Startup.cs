using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Nancy.Owin;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;

namespace paramore.brighter.comandprocessor.messageviewer.console
{
    public class Startup
    {
        public MessageViewerConfiguration Config { get; set; }

        //override 
        public Startup(IHostingEnvironment env)
        {
            var configFromFile = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .SetBasePath(env.ContentRootPath).Build();

            var messageViewerConfiguration = new MessageViewerConfiguration();
            ConfigurationBinder.Bind(configFromFile, messageViewerConfiguration);
            Config = messageViewerConfiguration;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseOwin(x =>
            x.UseNancy(opt => 
                opt.Bootstrapper = new NancyBootstrapper(Config)));
            Console.WriteLine("Nancy now listening - " + "" + ". Press ctrl-c to stop");
        }
    }
}