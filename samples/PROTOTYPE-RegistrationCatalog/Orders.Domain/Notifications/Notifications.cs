// PROTOTYPE — THROWAWAY domain types.
using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;

namespace Orders.Domain.Notifications;

public sealed class ReceiptRequested(string email) : Event(Id.Random())
{
    public string Email { get; } = email;
}

public sealed class ReceiptRequestedHandler : RequestHandlerAsync<ReceiptRequested>
{
    public override Task<ReceiptRequested> HandleAsync(ReceiptRequested @event, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"        → ReceiptRequestedHandler emailed {@event.Email}");
        return base.HandleAsync(@event, cancellationToken);
    }
}

// Never invoked by the prototype (no external bus); exists so the catalog has a mapper entry.
public sealed class ReceiptRequestedMapper : IAmAMessageMapper<ReceiptRequested>
{
    public IRequestContext? Context { get; set; }

    public Message MapToMessage(ReceiptRequested request, Publication publication) =>
        throw new NotImplementedException("prototype");

    public ReceiptRequested MapToRequest(Message message) =>
        throw new NotImplementedException("prototype");
}
