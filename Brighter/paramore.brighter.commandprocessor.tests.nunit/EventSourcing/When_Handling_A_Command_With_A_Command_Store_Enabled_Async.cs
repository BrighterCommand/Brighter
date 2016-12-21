using FakeItEasy;
using NUnit.Specifications;
using nUnitShouldAdapter;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor.eventsourcing.Handlers;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.EventSourcing.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.EventSourcing
{
    [Subject(typeof(CommandSourcingHandler<>))]
    public class When_Handling_A_Command_With_A_Command_Store_Enabled_Async : ContextSpecification
    {
        private static MyCommand s_command;
        private static IAmACommandStoreAsync s_commandStore;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandStore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register<IAmACommandStoreAsync>(s_commandStore);
            container.Register<ILog>(A.Fake<ILog>());

            s_command = new MyCommand {Value = "My Test String"};

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        };

        private Because of = () => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_command));

        private It should_store_the_command_to_the_command_store = () => s_commandStore.GetAsync<MyCommand>(s_command.Id).Result.Value.ShouldEqual(s_command.Value);
    }
}
