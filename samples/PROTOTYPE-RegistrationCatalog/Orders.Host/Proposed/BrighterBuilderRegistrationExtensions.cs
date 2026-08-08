// ─────────────────────────────────────────────────────────────────────────────────────
// PROTOTYPE — THROWAWAY. Stand-in for the AddRegistrations extension that would ship in
// Paramore.Brighter.Extensions.DependencyInjection (see ADR 0062). All the "thinking" the
// old design baked into generated code lives here instead: registry casts, the default-
// mapper guarantee, and (in the real thing) the open-generic path.
//
// The casts below are legitimate in the real implementation because this code lives in the
// same package that owns ServiceCollectionSubscriberRegistry — it is not a consumer-visible
// hazard the way a cast in generated code is.
// ─────────────────────────────────────────────────────────────────────────────────────
using System.Linq;
using Paramore.Brighter;

namespace Paramore.Brighter.Extensions.DependencyInjection;

public static class BrighterBuilderRegistrationExtensions
{
    public static IBrighterBuilder AddRegistrations(this IBrighterBuilder builder, params RegistrationCatalog[] catalogs)
    {
        // SET SEMANTICS: applying registrations is a union, not an append. Identical entries are
        // de-duplicated across the catalogs in this call (Distinct over value records), and entries
        // already present in the registry are skipped so *chained* AddRegistrations calls union too
        // (overlapping groups — e.g. a handler in both Billing and Urgent — register once).
        // The prototype checks via the registry's inspector; the real implementation would make the
        // registry's Add itself idempotent for exact duplicates.
        builder.Handlers(r =>
        {
            var registry = (ServiceCollectionSubscriberRegistry)r;
            foreach (var handler in catalogs.SelectMany(c => c.Handlers).Where(h => !h.IsAsync).Distinct())
                if (!registry.GetHandlerTypes(handler.RequestType!).Contains(handler.HandlerType))
                    registry.Add(handler.RequestType!, handler.HandlerType);
        });

        builder.AsyncHandlers(r =>
        {
            var registry = (ServiceCollectionSubscriberRegistry)r;
            foreach (var handler in catalogs.SelectMany(c => c.Handlers).Where(h => h.IsAsync).Distinct())
                if (!registry.GetHandlerTypes(handler.RequestType!).Contains(handler.HandlerType))
                    registry.Add(handler.RequestType!, handler.HandlerType);
        });

        // Always called, even with zero mappers: preserves the default-message-mapper
        // guarantee (EnsureDefaultMessageMapperIsRegistered) that AutoFromAssemblies gives.
        builder.MapperRegistry(r =>
        {
            foreach (var mapper in catalogs.SelectMany(c => c.Mappers).Distinct())
            {
                if (mapper.IsAsync)
                    r.AddAsync(mapper.RequestType, mapper.MapperType);
                else
                    r.Add(mapper.RequestType, mapper.MapperType);
            }
        });

        var transforms = catalogs.SelectMany(c => c.Transforms).ToList();
        if (transforms.Count > 0)
        {
            builder.Transforms(r =>
            {
                foreach (var transform in transforms)
                    r.Add(transform);
            });
        }

        return builder;
    }
}
