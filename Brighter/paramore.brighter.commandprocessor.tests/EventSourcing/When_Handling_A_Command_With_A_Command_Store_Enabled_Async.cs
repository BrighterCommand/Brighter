using System.Threading.Tasks;
using FakeItEasy;
using Machine.Specifications;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.eventsourcing.Handlers;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.EventSourcing.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.EventSourcing
{
    [Subject(typeof(CommandSourcingHandler<>))]
    public class When_Handling_A_Command_With_A_Command_Store_Enabled_Async
    {
        private static MyCommand s_command;
        private static IAmAnAsyncCommandStore s_commandStore;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            s_commandStore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerRequestHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerRequestHandlerAsync>();
            container.Register<IAmAnAsyncCommandStore>(s_commandStore);
            container.Register<ILog>(logger);

            s_command = new MyCommand {Value = "My Test String"};

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

        };

        private Because of = () => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_command));

        private It should_store_the_command_to_the_command_store = () => s_commandStore.GetAsync<MyCommand>(s_command.Id).Result.Value.ShouldEqual(s_command.Value);
    }
}
