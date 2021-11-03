using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class MonitoringAsyncHandler<T> : RequestHandlerAsync<T> where T : class, IRequest
    {
        private readonly InstanceCount _counter;

        public MonitoringAsyncHandler(InstanceCount counter)
        {
            _counter = counter;
        }

        public override async Task<T> HandleAsync(T command, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Monitoring");
            Console.WriteLine("---");
            Console.WriteLine($"Type:{command.GetType().Name} - Id:{command.Id} Count:{++_counter.Value}");
            Console.WriteLine("---");
            Console.WriteLine("Monitoring Ends");

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
