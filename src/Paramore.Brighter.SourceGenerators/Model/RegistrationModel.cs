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

using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.SourceGenerators.Model;

/// <summary>
/// Pure-data description of what the generator should emit for a single registration method: the
/// method/type <see cref="Target"/> plus the discovered registrations. Deliberately free of Roslyn
/// types so the writer can be unit-tested without a Compilation.
/// </summary>
internal sealed record RegistrationModel(
    MethodTarget Target,
    EquatableArray<HandlerEntry> Handlers,
    EquatableArray<HandlerEntry> AsyncHandlers,
    EquatableArray<MapperEntry> Mappers,
    EquatableArray<MapperEntry> AsyncMappers,
    EquatableArray<string> Transforms)
{
    /// <summary>
    /// Assemble a model from a per-method target and the flat list of discovered registration
    /// candidates. Pure function over value-equatable inputs.
    /// </summary>
    public static RegistrationModel From(MethodTarget target, EquatableArray<DiscoveredEntry> discovered)
    {
        var sync = new List<HandlerEntry>();
        var async = new List<HandlerEntry>();
        var mappers = new List<MapperEntry>();
        var asyncMappers = new List<MapperEntry>();
        var transforms = new List<string>();

        foreach (var entry in discovered.Distinct())
        {
            switch (entry.Kind)
            {
                case DiscoveredKind.SyncHandler:
                    sync.Add(new HandlerEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified, entry.IsOpenGeneric, entry.RequestAllowsManyHandlers));
                    break;
                case DiscoveredKind.AsyncHandler:
                    async.Add(new HandlerEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified, entry.IsOpenGeneric, entry.RequestAllowsManyHandlers));
                    break;
                case DiscoveredKind.Mapper:
                    mappers.Add(new MapperEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified));
                    break;
                case DiscoveredKind.AsyncMapper:
                    asyncMappers.Add(new MapperEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified));
                    break;
                case DiscoveredKind.Transform:
                    transforms.Add(entry.TypeFullyQualified);
                    break;
            }
        }

        return new RegistrationModel(
            target,
            new EquatableArray<HandlerEntry>(sync),
            new EquatableArray<HandlerEntry>(async),
            new EquatableArray<MapperEntry>(mappers),
            new EquatableArray<MapperEntry>(asyncMappers),
            new EquatableArray<string>(transforms));
    }

    /// <summary>
    /// True when the model would register at least one thing. False for a compilation that contains
    /// no Brighter types at all, where the emitted method would be a no-op.
    /// </summary>
    public bool HasRegistrations =>
        Handlers.Count > 0
        || AsyncHandlers.Count > 0
        || Mappers.Count > 0
        || AsyncMappers.Count > 0
        || Transforms.Count > 0;

    /// <summary>
    /// Non-event requests this model registers more than one handler for, as (request, handler
    /// names) pairs. Only an event is dispatched to several handlers; everything else goes through
    /// <c>Send</c>, which insists on exactly one — so this is always a mistake, but it is only
    /// detectable at dispatch time today (see the reflection scanner's identical behaviour), which
    /// is precisely the class of failure this generator exists to move earlier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sync and async handlers are counted separately, because <c>Send</c> and <c>SendAsync</c>
    /// resolve them independently: one sync and one async handler for the same request is a normal,
    /// working arrangement. Open generics are excluded — they carry no concrete request type.
    /// </para>
    /// <para>
    /// Only duplicates within this compilation are visible. Two handlers for one request contributed
    /// by different assemblies and combined by the host cannot be seen here; catching those belongs
    /// at composition time, where the registrations meet.
    /// </para>
    /// </remarks>
    public IEnumerable<(string RequestType, IReadOnlyList<string> HandlerTypes)> DuplicateHandlers() =>
        DuplicatesIn(Handlers).Concat(DuplicatesIn(AsyncHandlers));

    private static IEnumerable<(string RequestType, IReadOnlyList<string> HandlerTypes)> DuplicatesIn(
        EquatableArray<HandlerEntry> handlers) =>
        handlers
            .Where(static h => h is { IsOpenGeneric: false, RequestAllowsManyHandlers: false })
            .GroupBy(static h => h.RequestTypeFullyQualified)
            .Where(static g => g.Select(static h => h.HandlerTypeFullyQualified).Distinct().Count() > 1)
            .Select(static g => (
                g.Key,
                (IReadOnlyList<string>)g.Select(static h => h.HandlerTypeFullyQualified).Distinct().OrderBy(static n => n, System.StringComparer.Ordinal).ToList()));
}

/// <summary>
/// A handler registration. For closed-generic handlers, both type names are fully qualified
/// (e.g. <c>global::Foo.Bar</c>). For open generics, <see cref="RequestTypeFullyQualified"/>
/// is empty and <see cref="HandlerTypeFullyQualified"/> uses unbound form (e.g. <c>global::Foo&lt;&gt;</c>).
/// </summary>
internal sealed record HandlerEntry(
    string RequestTypeFullyQualified,
    string HandlerTypeFullyQualified,
    bool IsOpenGeneric,
    bool RequestAllowsManyHandlers = true);

internal sealed record MapperEntry(
    string RequestTypeFullyQualified,
    string MapperTypeFullyQualified);
