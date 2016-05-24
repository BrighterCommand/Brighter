using System.Threading;
using ProtoBufReceiver.Configuration;
using SimpleInjector;

namespace ProtoBufReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = new Container();

            var dispatcher = BrighterConfig.Register(container);

            container.Verify();

            dispatcher.Receive();

            do
            {
                Thread.Sleep(1000);
            } while (true);
        }
    }
}
