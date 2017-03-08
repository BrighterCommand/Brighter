using System;
using Paramore.Brighter.MessageViewer.WindowsService.Configuration;

namespace Paramore.Brighter.MessageViewer.WindowsService
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