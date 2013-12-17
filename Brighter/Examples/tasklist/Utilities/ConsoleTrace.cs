using System;
using Tasklist.Ports;

namespace Tasklist.Utilities
{
    public class ConsoleTrace : ITraceOutput
    {
        public void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}