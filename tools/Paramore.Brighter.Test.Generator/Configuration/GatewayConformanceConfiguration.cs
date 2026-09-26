#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;

namespace Paramore.Brighter.Test.Generator.Configuration;

/// <summary>
/// Represents the configuration for generating a gateway's channel factory conformance sweep test.
/// </summary>
public class GatewayConformanceConfiguration
{
    /// <summary>
    /// Gets or sets the fully-qualified name of the subscription type expected to be reported by
    /// the sweep. Required. Rendered both as <c>typeof(SubscriptionType).Assembly</c>, to locate
    /// the assembly the sweep examines, and as an expected subject. Must name a type that survives
    /// subsumption - one declaring its own <c>ChannelFactoryType</c>, or a root candidate.
    /// </summary>
    public string SubscriptionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully-qualified names of additional subjects the sweep is expected to
    /// report, beyond <see cref="SubscriptionType"/>. A generic entry is written with its arity
    /// backtick exactly as <see cref="System.Type.FullName"/> reports it (e.g. <c>Ns.Foo`1</c>),
    /// not with angle brackets.
    /// </summary>
    public List<string> AdditionalExpectedSubjects { get; set; } = new();

    /// <summary>
    /// Gets or sets the test category to apply to generated test classes. Unused by the sweep
    /// guard; present for symmetry with the other configuration sections.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the namespace for the generated test code. If null, uses the parent
    /// configuration's namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Returns a copy of this configuration with values it does not set taken from the root
    /// <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The root configuration to inherit unset values from.</param>
    /// <returns>A copy; this instance is unchanged.</returns>
    /// <remarks>
    /// Applied where the model a rendering reads is built, so that describing the work and
    /// performing it see the same model. Applying it on only one of those paths is how the
    /// generator's expected set and its output would come to disagree - which is the drift the
    /// generated-tree audit exists to catch, and so the last place it should be reintroduced.
    /// </remarks>
    internal GatewayConformanceConfiguration WithDefaultsFrom(TestConfiguration configuration)
    {
        var copy = (GatewayConformanceConfiguration)MemberwiseClone();

        if (string.IsNullOrEmpty(copy.Namespace))
        {
            copy.Namespace = configuration.Namespace;
        }

        return copy;
    }
}
