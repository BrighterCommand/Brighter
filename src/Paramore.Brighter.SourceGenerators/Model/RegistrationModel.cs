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

namespace Paramore.Brighter.SourceGenerators.Model;

/// <summary>
/// Pure-data description of what the generator should emit for a single registration method.
/// Deliberately free of Roslyn types so the writer can be unit-tested without a Compilation.
/// </summary>
public sealed record RegistrationModel(
    string? Namespace,
    string ContainingTypeAccessibility,
    string ContainingTypeName,
    bool ContainingTypeIsStatic,
    string MethodAccessibility,
    string MethodName,
    string ReturnTypeFullyQualified,
    string ParameterTypeFullyQualified,
    string ParameterName,
    bool IsExtensionMethod,
    EquatableArray<HandlerEntry> Handlers,
    EquatableArray<HandlerEntry> AsyncHandlers,
    EquatableArray<MapperEntry> Mappers,
    EquatableArray<MapperEntry> AsyncMappers,
    EquatableArray<string> Transforms,
    string HintName);

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
