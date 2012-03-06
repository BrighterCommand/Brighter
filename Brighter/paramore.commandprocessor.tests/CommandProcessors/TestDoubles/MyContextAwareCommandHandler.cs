namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyContextAwareCommandHandler : RequestHandler<MyCommand>
    {
        public static string TestString { get; set; }

        public override MyCommand  Handle(MyCommand command)
        {
            LogContext();
            return base.Handle(command);
        }

        private void LogContext()
        {
            TestString = (string)Context.Bag.TestString;
            Context.Bag["MyContextAwareCommandHandler"] = "I was called and set the context";
        }

    }
}