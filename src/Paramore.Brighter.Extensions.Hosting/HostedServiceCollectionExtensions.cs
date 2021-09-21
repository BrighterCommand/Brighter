using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Hosting
{
    public static class HostedServiceCollectionExtensions
    {
        public static IBrighterBuilder UseOutboxSweeper(this IBrighterBuilder brighterBuilder)
        {
           brighterBuilder.Services.AddHostedService<TimedOutboxSweeper>();
           return brighterBuilder;
        }
}
}
