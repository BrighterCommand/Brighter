namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyImplicitHandler : RequestHandler<MyCommand>
    {
        [MyLoggingHandler(step:1)]
        public override MyCommand Handle(MyCommand command)
        {
            return base.Handle(command);
        }
    }
}