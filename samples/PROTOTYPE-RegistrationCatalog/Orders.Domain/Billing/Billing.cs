// PROTOTYPE — THROWAWAY domain types.
using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;

namespace Orders.Domain.Billing;

public sealed class ChargeCard(decimal amount) : Command(Id.Random())
{
    public decimal Amount { get; } = amount;
}

public sealed class ChargeCardHandler : RequestHandler<ChargeCard>
{
    public override ChargeCard Handle(ChargeCard command)
    {
        Console.WriteLine($"        → ChargeCardHandler charged £{command.Amount}");
        return base.Handle(command);
    }
}

public sealed class RefundPayment(decimal amount) : Command(Id.Random())
{
    public decimal Amount { get; } = amount;
}

public sealed class RefundPaymentHandler : RequestHandlerAsync<RefundPayment>
{
    public override Task<RefundPayment> HandleAsync(RefundPayment command, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"        → RefundPaymentHandler refunded £{command.Amount}");
        return base.HandleAsync(command, cancellationToken);
    }
}

public sealed class UrgentRefund(decimal amount) : Command(Id.Random())
{
    public decimal Amount { get; } = amount;
}

// Deliberately named to be scooped by the "Urgent" TypeNamePattern group, across namespaces.
public sealed class UrgentRefundHandler : RequestHandlerAsync<UrgentRefund>
{
    public override Task<UrgentRefund> HandleAsync(UrgentRefund command, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"        → UrgentRefundHandler fast-tracked £{command.Amount}");
        return base.HandleAsync(command, cancellationToken);
    }
}
