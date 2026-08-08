// ─────────────────────────────────────────────────────────────────────────────────────
// PROTOTYPE — THROWAWAY. Stand-in for a type that would ship in core Paramore.Brighter
// (see ADR 0062). Declared here, in Brighter's namespace, so the call sites read exactly
// as they would for real.
// ─────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter;

/// <summary>
/// Inert description of an assembly's handler/mapper/transform registrations. Constructed by
/// generated code via the additive Add* methods; applied by the DI package's AddRegistrations.
/// </summary>
public sealed class RegistrationCatalog
{
    private readonly List<HandlerRegistration> _handlers = [];
    private readonly List<MapperRegistration> _mappers = [];
    private readonly List<Type> _transforms = [];

    public IReadOnlyList<HandlerRegistration> Handlers => _handlers;
    public IReadOnlyList<MapperRegistration> Mappers => _mappers;
    public IReadOnlyList<Type> Transforms => _transforms;

    // Additive construction surface (never a growing ctor): generated code binds to these.
    public void AddHandler(Type handlerType, Type? requestType, bool isAsync) =>
        _handlers.Add(new HandlerRegistration(handlerType, requestType, isAsync));

    public void AddMapper(Type mapperType, Type requestType, bool isAsync) =>
        _mappers.Add(new MapperRegistration(mapperType, requestType, isAsync));

    public void AddTransform(Type transformType) => _transforms.Add(transformType);

    // ── Runtime combinators ─────────────────────────────────────────────────────────
    // The "flat data" answer to NServiceBus's generated named groups: because a catalog is
    // data, scooping subsets is ordinary library code — no generator feature required.

    /// <summary>Keep entries whose implementation type matches the predicate.</summary>
    public RegistrationCatalog Matching(Func<Type, bool> predicate)
    {
        var subset = new RegistrationCatalog();
        subset._handlers.AddRange(_handlers.Where(h => predicate(h.HandlerType)));
        subset._mappers.AddRange(_mappers.Where(m => predicate(m.MapperType)));
        subset._transforms.AddRange(_transforms.Where(predicate));
        return subset;
    }

    /// <summary>Keep entries whose implementation type lives in the given namespace (or below).</summary>
    public RegistrationCatalog InNamespace(string ns) =>
        Matching(t => t.Namespace == ns || (t.Namespace?.StartsWith(ns + ".", StringComparison.Ordinal) ?? false));

    /// <summary>Remove specific implementation types — composition-time exclusion without touching source.</summary>
    public RegistrationCatalog Without(params Type[] types) =>
        Matching(t => !types.Contains(t));

    public IEnumerable<string> Describe()
    {
        foreach (var h in _handlers)
            yield return $"handler   {h.HandlerType.Name,-24} ← {h.RequestType?.Name ?? "(open generic)",-18} {(h.IsAsync ? "async" : "sync")}";
        foreach (var m in _mappers)
            yield return $"mapper    {m.MapperType.Name,-24} ← {m.RequestType.Name,-18} {(m.IsAsync ? "async" : "sync")}";
        foreach (var t in _transforms)
            yield return $"transform {t.Name}";
    }

    public override string ToString() =>
        $"{_handlers.Count} handlers, {_mappers.Count} mappers, {_transforms.Count} transforms";
}

public readonly record struct HandlerRegistration(Type HandlerType, Type? RequestType, bool IsAsync);

public readonly record struct MapperRegistration(Type MapperType, Type RequestType, bool IsAsync);
