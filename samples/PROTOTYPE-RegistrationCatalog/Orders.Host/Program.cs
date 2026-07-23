// ─────────────────────────────────────────────────────────────────────────────────────
// PROTOTYPE driver — builds a fresh container per composition variant, prints the
// catalog(s) (inspectability is part of the design), then fires every message and shows
// which handlers were — and deliberately were not — registered.
// ─────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Orders.Domain;
using Orders.Domain.Billing;
using Orders.Domain.Fulfilment;
using Orders.Domain.Notifications;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;

await Variant(
    "1) Flat: everything the assembly declares",
    ".AddRegistrations(OrdersRegistrations.Catalog)",
    [OrdersRegistrations.Catalog]);

await Variant(
    "2) Named groups from [RegistrationGroup] namespace conventions",
    ".AddRegistrations(OrdersRegistrations.Billing, OrdersRegistrations.Fulfilment)",
    [OrdersRegistrations.Billing, OrdersRegistrations.Fulfilment]);

await Variant(
    "3) Named group from a TypeNamePattern convention (cuts across namespaces)",
    ".AddRegistrations(OrdersRegistrations.Urgent)",
    [OrdersRegistrations.Urgent]);

await Variant(
    "4) Flat + runtime combinators — same scooping, no generator feature involved",
    """.AddRegistrations(OrdersRegistrations.Catalog.Matching(t => t.Name.StartsWith("Urgent")))""",
    [OrdersRegistrations.Catalog.Matching(t => t.Name.StartsWith("Urgent"))]);

await Variant(
    "5) Combinators compose: Billing minus one handler, plus Notifications",
    """.AddRegistrations(OrdersRegistrations.Billing.Without(typeof(UrgentRefundHandler)), OrdersRegistrations.Catalog.InNamespace("Orders.Domain.Notifications"))""",
    [OrdersRegistrations.Billing.Without(typeof(UrgentRefundHandler)),
     OrdersRegistrations.Catalog.InNamespace("Orders.Domain.Notifications")]);

await Variant(
    "6) Phase-2 sugar: generated fluent extensions (one-liners over the same catalogs)",
    ".AddBillingRegistrations().AddUrgentRegistrations()",
    [OrdersRegistrations.Billing, OrdersRegistrations.Urgent],
    b => b.AddBillingRegistrations().AddUrgentRegistrations());

return;

static async Task Variant(string title, string code, RegistrationCatalog[] catalogs, Action<IBrighterBuilder>? compose = null)
{
    compose ??= b => b.AddRegistrations(catalogs);
    Console.WriteLine();
    Console.WriteLine($"══ {title} ".PadRight(100, '═'));
    Console.WriteLine($"   {code}");
    Console.WriteLine($"   catalog contents ({string.Join(" + ", catalogs.AsEnumerable())}):");
    foreach (var catalog in catalogs)
        foreach (var line in catalog.Describe())
            Console.WriteLine($"      {line}");

    var services = new ServiceCollection();
    compose(services.AddBrighter());
    await using var provider = services.BuildServiceProvider();
    var processor = provider.GetRequiredService<IAmACommandProcessor>();

    Console.WriteLine("   sending one of everything:");
    Try("Send(ChargeCard)", () => processor.Send(new ChargeCard(42.00m)));
    await TryAsync("SendAsync(RefundPayment)", () => processor.SendAsync(new RefundPayment(9.99m)));
    await TryAsync("SendAsync(UrgentRefund)", () => processor.SendAsync(new UrgentRefund(100.00m)));
    Try("Send(ShipOrder)", () => processor.Send(new ShipOrder("SKU-1")));
    Try("Send(UrgentShipOrder)", () => processor.Send(new UrgentShipOrder("SKU-2")));
    await TryAsync("PublishAsync(ReceiptRequested)", () => processor.PublishAsync(new ReceiptRequested("a@b.com")));
}

static void Try(string label, Action send)
{
    try
    {
        Console.WriteLine($"     {label}");
        send();
    }
    catch (Exception e)
    {
        Console.WriteLine($"        ✗ {e.GetType().Name}: {FirstLine(e.Message)}");
    }
}

static async Task TryAsync(string label, Func<Task> send)
{
    try
    {
        Console.WriteLine($"     {label}");
        await send();
    }
    catch (Exception e)
    {
        Console.WriteLine($"        ✗ {e.GetType().Name}: {FirstLine(e.Message)}");
    }
}

static string FirstLine(string s)
{
    var i = s.IndexOf('\n');
    return i < 0 ? s : s[..i].TrimEnd();
}
