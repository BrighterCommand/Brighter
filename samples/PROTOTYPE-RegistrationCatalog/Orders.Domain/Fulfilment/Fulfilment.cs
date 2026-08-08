// PROTOTYPE — THROWAWAY domain types.
using System;
using Paramore.Brighter;

namespace Orders.Domain.Fulfilment;

public sealed class ShipOrder(string sku) : Command(Id.Random())
{
    public string Sku { get; } = sku;
}

public sealed class ShipOrderHandler : RequestHandler<ShipOrder>
{
    public override ShipOrder Handle(ShipOrder command)
    {
        Console.WriteLine($"        → ShipOrderHandler shipped {command.Sku}");
        return base.Handle(command);
    }
}

public sealed class UrgentShipOrder(string sku) : Command(Id.Random())
{
    public string Sku { get; } = sku;
}

// Second "Urgent" type, in a different namespace to Billing.UrgentRefundHandler — the
// TypeNamePattern group cuts across the namespace groups.
public sealed class UrgentShipOrderHandler : RequestHandler<UrgentShipOrder>
{
    public override UrgentShipOrder Handle(UrgentShipOrder command)
    {
        Console.WriteLine($"        → UrgentShipOrderHandler expedited {command.Sku}");
        return base.Handle(command);
    }
}
