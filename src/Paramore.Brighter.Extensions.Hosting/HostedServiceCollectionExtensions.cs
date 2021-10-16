using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Hosting
{
    public static class HostedServiceCollectionExtensions
    {
        public static IBrighterBuilder UseOutboxSweeper(this IBrighterBuilder brighterBuilder,
            TimedOutboxSweeperOptions options = default)
        {
            brighterBuilder.Services.AddSingleton<TimedOutboxSweeperOptions>(options);
            brighterBuilder.Services.AddHostedService<TimedOutboxSweeper>();
            return brighterBuilder;
        }
    }
}
