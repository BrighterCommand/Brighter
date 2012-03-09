using System;
using tasklist.web.Handlers;

namespace tasklist.web.Utilities
{
    public class ConsoleTrace : ITraceOutput
    {
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}