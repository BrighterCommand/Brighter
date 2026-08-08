// ─────────────────────────────────────────────────────────────────────────────────────
// THIS FILE IS WHAT THE USER WRITES. Everything else about registration is generated.
//
// The holder declaration is the whole per-assembly ceremony: name, namespace and visibility
// are user-controlled, so any number of assemblies compose in the host without collision.
// [RegistrationGroup] declares named convention scoops (evaluated at build time) — drop the
// attributes and you still get the flat `Catalog`.
// ─────────────────────────────────────────────────────────────────────────────────────
using Paramore.Brighter;

namespace Orders.Domain;

// GenerateBuilderExtensions is the Phase-2 opt-in for fluent sugar (see the host's
// OrdersRegistrations.BuilderExtensions.g.cs). In the real design those extensions are generated
// INTO THIS PROJECT, which would then have to reference the DI package — the prototype parks the
// file in the host instead so Orders.Domain keeps demonstrating the core-only posture.
[BrighterRegistrations(GenerateBuilderExtensions = true)]
[RegistrationGroup("Billing", InNamespace = "Orders.Domain.Billing")]
[RegistrationGroup("Fulfilment", InNamespace = "Orders.Domain.Fulfilment")]
[RegistrationGroup("Urgent", TypeNamePattern = "^Urgent")]
public static partial class OrdersRegistrations;
