#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

#pragma warning disable CS0618

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

/// <summary>
/// Characterises that the public API of the types the sweeper health check work touched is a source-compatible superset of the branch point.
/// </summary>
public class PublicApiBaselineTests
{
    private const string Optional = " = optional";

    private enum Kind { Constructor, Method, Property, Field }

    private sealed record Baseline(Kind Kind, string Name, string Type, string[] Parameters, string Accessors = "");

    private static Baseline Ctor(params string[] parameters) => new(Kind.Constructor, ".ctor", "void", parameters);
    private static Baseline Method(string type, string name, params string[] parameters) => new(Kind.Method, name, type, parameters);
    private static Baseline Property(string type, string name, string accessors) => new(Kind.Property, name, type, [], accessors);
    private static Baseline Field(string type, string name) => new(Kind.Field, name, type, []);

    private static readonly Dictionary<Type, Baseline[]> s_baseline = new()
    {
        [typeof(IAmABrighterSweeperMeter)] =
        [
            Method("void", "RecordTickLag", "TimeSpan lag"),
            Method("void", "RecordSweep", "SweepOutcome outcome", "TimeSpan duration")
        ],
        [typeof(IAmAnOutboxCircuitBreaker)] =
        [
            Method("void", "CoolDown"),
            Method("void", "TripTopic", "RoutingKey topic"),
            Property("IEnumerable<RoutingKey>", "TrippedTopics", "get")
        ],
        [typeof(TimedOutboxSweeper)] =
        [
            Ctor("IServiceScopeFactory serviceScopeFactory", "IDistributedLock distributedLock", "TimedOutboxSweeperOptions options",
                "TimeProvider timeProvider" + Optional, "IAmABrighterSweeperMeter meter" + Optional),
            Method("Task", "StartAsync", "CancellationToken cancellationToken"),
            Method("Task", "StopAsync", "CancellationToken cancellationToken"),
            Method("void", "Dispose")
        ],
        [typeof(TimedOutboxSweeperOptions)] =
        [
            Ctor(),
            Property("int", "TimerInterval", "get;set"),
            Property("TimeSpan", "MinimumMessageAge", "get;set"),
            Property("int", "BatchSize", "get;set"),
            Field("Dictionary<string, object>", "Args"),
            Property("bool", "UseBulk", "get;set")
        ],
        [typeof(BrighterServiceActivatorHealthCheck)] =
        [
            Ctor("IDispatcher dispatcher"),
            Ctor("IDispatcher dispatcher", "int channelFailureThreshold"),
            Method("Task<HealthCheckResult>", "CheckHealthAsync", "HealthCheckContext context", "CancellationToken cancellationToken" + Optional)
        ],
        [typeof(Dispatcher)] =
        [
            Property("IAmACommandProcessor", "CommandProcessor", "get"),
            Property("IEnumerable<Subscription>", "Subscriptions", "get"),
            Property("IEnumerable<IAmAConsumer>", "Consumers", "get"),
            Property("HostName", "HostName", "get;set"),
            Property("DispatcherState", "State", "get"),
            Property("TimeSpan", "ShutdownTimeout", "get;set"),
            Ctor("IAmACommandProcessor commandProcessor", "IEnumerable<Subscription> subscriptions",
                "IAmAMessageMapperRegistry messageMapperRegistry" + Optional,
                "IAmAMessageMapperRegistryAsync messageMapperRegistryAsync" + Optional,
                "IAmAMessageTransformerFactory messageTransformerFactory" + Optional,
                "IAmAMessageTransformerFactoryAsync messageTransformerFactoryAsync" + Optional,
                "IAmARequestContextFactory requestContextFactory" + Optional,
                "IAmABrighterTracer tracer" + Optional,
                "InstrumentationOptions instrumentationOptions" + Optional,
                "bool ownsRegistry" + Optional,
                "bool ownsTransformerFactories" + Optional,
                "TimeSpan? shutdownTimeout" + Optional),
            Method("void", "Dispose"),
            Method("ValueTask", "DisposeAsync"),
            Method("Task", "End"),
            Method("void", "Open", "SubscriptionName subscriptionName"),
            Method("void", "Open", "Subscription subscription"),
            Method("void", "Receive"),
            Method("void", "Shut", "SubscriptionName subscriptionName"),
            Method("void", "Shut", "Subscription subscription"),
            Method("DispatcherStateItem[]", "GetState"),
            Method("void", "SetActivePerformers", "string connectionName", "int numberOfPerformers")
        ],
        [typeof(IDispatcher)] =
        [
            Property("IEnumerable<IAmAConsumer>", "Consumers", "get"),
            Property("HostName", "HostName", "get;set"),
            Method("Task", "End"),
            Method("void", "Open", "Subscription subscription"),
            Method("void", "Open", "SubscriptionName subscriptionName"),
            Method("void", "Receive"),
            Method("void", "Shut", "Subscription subscription"),
            Method("void", "Shut", "SubscriptionName subscriptionName"),
            Method("DispatcherStateItem[]", "GetState"),
            Method("void", "SetActivePerformers", "string connectionName", "int numberOfPerformers")
        ]
    };

    [Fact]
    public void When_comparing_public_members_with_the_baseline_should_keep_every_existing_member()
    {
        //Arrange
        var missing = new List<string>();

        //Act
        foreach (var (type, members) in s_baseline)
            foreach (var member in members)
                if (!Satisfies(type, member))
                    missing.Add($"{type.Name}: {Describe(member)}");

        var circuitBreakerMembers = DeclaredMembers(typeof(IAmAnOutboxCircuitBreaker));
        var circuitBreakerBaseline = s_baseline[typeof(IAmAnOutboxCircuitBreaker)].Select(Describe).OrderBy(s => s).ToArray();
        var healthCheckAttributes = typeof(BrighterServiceActivatorHealthCheck).GetCustomAttributesData()
            .Select(a => a.AttributeType)
            .Where(t => t.Namespace != "System.Runtime.CompilerServices" && t.Namespace != "Microsoft.CodeAnalysis")
            .Select(t => t.Name)
            .ToArray();

        //Assert
        Assert.True(missing.Count == 0, "Baseline members missing or not source-compatible:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
        Assert.Equal(circuitBreakerBaseline, circuitBreakerMembers);
        Assert.Equal([nameof(ObsoleteAttribute)], healthCheckAttributes);
    }

    private static string[] DeclaredMembers(Type type)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var methods = type.GetMethods(flags).Where(m => !m.IsSpecialName).Select(m => Describe(FromMethod(m)));
        var properties = type.GetProperties(flags).Select(p => Describe(FromProperty(p)));
        return methods.Concat(properties).OrderBy(s => s).ToArray();
    }

    private static bool Satisfies(Type type, Baseline baseline)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        switch (baseline.Kind)
        {
            case Kind.Constructor:
                return type.GetConstructors(flags).Any(c => ParametersSatisfy(baseline, c.GetParameters(), true));
            case Kind.Method:
                var extrasAllowed = !type.IsInterface;
                return type.GetMethods(flags).Any(m =>
                    m.Name == baseline.Name
                    && TypeName(m.ReturnType) == baseline.Type
                    && ParametersSatisfy(baseline, m.GetParameters(), extrasAllowed && !(m.IsVirtual && !m.IsFinal)));
            case Kind.Property:
                var property = type.GetProperty(baseline.Name, flags);
                if (property is null || TypeName(property.PropertyType) != baseline.Type)
                    return false;
                return baseline.Accessors.Split(';').All(a =>
                    a == "get" ? property.GetMethod?.IsPublic == true : property.SetMethod?.IsPublic == true);
            default:
                var field = type.GetField(baseline.Name, flags);
                return field is not null && TypeName(field.FieldType) == baseline.Type;
        }
    }

    private static bool ParametersSatisfy(Baseline baseline, ParameterInfo[] actual, bool extrasAllowed)
    {
        if (actual.Length < baseline.Parameters.Length)
            return false;
        if (actual.Length > baseline.Parameters.Length && !extrasAllowed)
            return false;

        for (var i = 0; i < baseline.Parameters.Length; i++)
        {
            var wantsOptional = baseline.Parameters[i].EndsWith(Optional, StringComparison.Ordinal);
            var expected = baseline.Parameters[i].Replace(Optional, string.Empty);
            if ($"{TypeName(actual[i].ParameterType)} {actual[i].Name}" != expected)
                return false;
            if (wantsOptional && !actual[i].IsOptional)
                return false;
        }

        return actual.Skip(baseline.Parameters.Length).All(p => p.IsOptional);
    }

    private static Baseline FromMethod(MethodInfo method) =>
        Method(TypeName(method.ReturnType), method.Name, method.GetParameters().Select(p => $"{TypeName(p.ParameterType)} {p.Name}").ToArray());

    private static Baseline FromProperty(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod?.IsPublic == true) accessors.Add("get");
        if (property.SetMethod?.IsPublic == true) accessors.Add("set");
        return Property(TypeName(property.PropertyType), property.Name, string.Join(";", accessors));
    }

    private static string Describe(Baseline member) =>
        $"{member.Kind} {member.Type} {member.Name}({string.Join(", ", member.Parameters.Select(p => p.Replace(Optional, string.Empty)))}) {member.Accessors}".Trim();

    private static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return TypeName(underlying) + "?";
        if (type.IsArray)
            return TypeName(type.GetElementType()!) + "[]";
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }
}
