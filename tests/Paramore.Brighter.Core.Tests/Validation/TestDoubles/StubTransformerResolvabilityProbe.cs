using System;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// A configurable <see cref="IAmATransformerResolvabilityProbe"/> for tests. The supplied delegate
/// decides resolvability per transformer type, and may throw to exercise the rule's error guard.
/// </summary>
public sealed class StubTransformerResolvabilityProbe(Func<Type, bool> resolves) : IAmATransformerResolvabilityProbe
{
    public static StubTransformerResolvabilityProbe ResolvesNothing { get; } = new(_ => false);

    public static StubTransformerResolvabilityProbe ResolvesEverything { get; } = new(_ => true);

    public bool Resolves(Type transformerType) => resolves(transformerType);
}
