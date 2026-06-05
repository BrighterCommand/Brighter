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
public sealed record RegistrationModel(
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
                    sync.Add(new HandlerEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified, entry.IsOpenGeneric));
                    break;
                case DiscoveredKind.AsyncHandler:
                    async.Add(new HandlerEntry(entry.RequestTypeFullyQualified, entry.TypeFullyQualified, entry.IsOpenGeneric));
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
}

/// <summary>
/// A handler registration. For closed-generic handlers, both type names are fully qualified
/// (e.g. <c>global::Foo.Bar</c>). For open generics, <see cref="RequestTypeFullyQualified"/>
/// is empty and <see cref="HandlerTypeFullyQualified"/> uses unbound form (e.g. <c>global::Foo&lt;&gt;</c>).
/// </summary>
public sealed record HandlerEntry(
    string RequestTypeFullyQualified,
    string HandlerTypeFullyQualified,
    bool IsOpenGeneric);

public sealed record MapperEntry(
    string RequestTypeFullyQualified,
    string MapperTypeFullyQualified);
