using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Nancy.Owin;
using paramore.brighter.comandprocessor.messageviewer.windowsservice.Configuration;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;

namespace paramore.brighter.comandprocessor.messageviewer.windowsservice
{
    public class Startup
    {
        public MessageViewerConfiguration Config { get; set; }

        public Startup(IHostingEnvironment env)
        {
            var section = MessageViewerSection.GetViewerSection;
            Config = MessageViewerSectionAdapater.Map(section);
        }
        
        public void Configure(IApplicationBuilder app)
        {
            app.UseOwin(x => x.UseNancy(opt => opt.Bootstrapper = new NancyBootstrapper(Config)));
            Console.WriteLine("Nancy now listening - " + "" + ". Press ctrl-c to stop");
        }
    }
}