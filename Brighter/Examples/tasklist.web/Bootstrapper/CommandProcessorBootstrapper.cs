using Nancy;
using paramore.commandprocessor;
using tasklist.web.Commands;
using tasklist.web.Handlers;

namespace tasklist.web.Bootstrapper
{
    public class CommandProcessorBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureApplicationContainer(TinyIoC.TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);
            //try to register the command handlers
            container.Register<IHandleRequests<AddTaskCommand>, AddTaskCommandHandler>().AsMultiInstance();
        }
    }
}