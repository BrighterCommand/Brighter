using Paramore.Brighter.ServiceActivator.Control.Api.Responses;
using Paramore.Brighter.ServiceActivator.Control.Extensions;

namespace Paramore.Brighter.ServiceActivator.Control.Api;

public static class ApiExtensions
{
    /// <summary>
    /// Add the Brighter Control endpoints
    /// </summary>
    /// <param name="builder">The endpoint route builder</param>
    /// <param name="baseRoute">The url to add the endpoints at</param>
    /// <returns></returns>
    public static IEndpointRouteBuilder MapBrighterControlEndpoints(this IEndpointRouteBuilder builder, string baseRoute = "/control")
    {
        builder.MapGet($"{baseRoute}/status", (IDispatcher dispatcher) => new GetStatusResponse(dispatcher.GetNodeStatus()));
        builder.MapMethods($"{baseRoute}/subscriptions/{{subscriptionName}}/performers/{{numberOfPerformers:int}}", ["PATCH"],
            (string subscriptionName, int numberOfPerformers, IDispatcher dispatcher) =>
            {
                var status = dispatcher.GetState();

                if (!status.Any(s => s.Name.Equals(subscriptionName, StringComparison.InvariantCultureIgnoreCase)))
                    return Results.BadRequest($"No such subscription {subscriptionName}");

                dispatcher.SetActivePerformers(subscriptionName, numberOfPerformers);

                return Results.Ok($"Active performers for {subscriptionName} set to {numberOfPerformers}");

            });

        return builder;
    }
}
