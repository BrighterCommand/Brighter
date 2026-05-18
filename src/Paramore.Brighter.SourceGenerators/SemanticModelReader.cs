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
using Microsoft.CodeAnalysis;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Single point in the generator that touches the semantic model. Validates the user's
/// partial method, walks the source module to discover handlers/mappers/transforms, and
/// projects everything into the Roslyn-free <see cref="RegistrationModel"/>.
/// </summary>
public static class SemanticModelReader
{
    private const string ExcludeAttributeName = "Paramore.Brighter.ExcludeFromBrighterRegistrationAttribute";

    public static bool TryBuildModel(
        IMethodSymbol method,
        Compilation compilation,
        MarkerSymbols symbols,
        out RegistrationModel? model,
        out Diagnostic? diagnostic)
    {
        model = null;
        if (!Validate(method, symbols, out diagnostic))
            return false;

        var discovered = Discover(compilation, symbols);
        model = Project(method, discovered);
        return true;
    }

    internal static bool Validate(IMethodSymbol method, MarkerSymbols symbols, out Diagnostic? diagnostic)
    {
        diagnostic = null;
        var location = method.Locations.FirstOrDefault();

        if (!method.IsPartialDefinition)
            return Fail(Diagnostics.MustBePartial, location, method.Name, out diagnostic);

        if (!method.IsStatic)
            return Fail(Diagnostics.MustBeStatic, location, method.Name, out diagnostic);

        if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, symbols.BrighterBuilder))
            return Fail(Diagnostics.WrongReturnType, location, method.Name, out diagnostic);

        if (method.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, symbols.BrighterBuilder))
            return Fail(Diagnostics.WrongSignature, location, method.Name, out diagnostic);

        return true;
    }

    private static bool Fail(DiagnosticDescriptor descriptor, Location? location, string methodName, out Diagnostic? diagnostic)
    {
        diagnostic = Diagnostic.Create(descriptor, location, methodName);
        return false;
    }

    private static DiscoveredSymbols Discover(Compilation compilation, MarkerSymbols symbols)
    {
        var result = new DiscoveredSymbols();
        var excludeAttr = compilation.GetTypeByMetadataName(ExcludeAttributeName);

        foreach (var type in EnumerateNamedTypes(compilation.SourceModule.GlobalNamespace))
        {
            if (!IsRegistrationCandidate(type, excludeAttr))
                continue;
            ClassifyType(type, symbols, result);
        }

        return result;
    }

    private static bool IsRegistrationCandidate(INamedTypeSymbol type, INamedTypeSymbol? excludeAttr)
    {
        if (type.TypeKind != TypeKind.Class)
            return false;
        if (type.IsAbstract || type.IsImplicitClass || type.IsAnonymousType)
            return false;
        if (!IsReachableFromGeneratedCode(type))
            return false;
        if (excludeAttr is not null && HasAttribute(type, excludeAttr))
            return false;
        return true;
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attribute) =>
        type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static void ClassifyType(INamedTypeSymbol type, MarkerSymbols symbols, DiscoveredSymbols result)
    {
        var isTransform = false;
        foreach (var iface in type.AllInterfaces)
        {
            if (TryClassifyGenericInterface(type, iface, symbols, result))
                continue;
            if (IsTransformInterface(iface, symbols))
                isTransform = true;
        }

        if (isTransform && !type.IsGenericType)
            result.Transforms.Add(type);
    }

    private static bool TryClassifyGenericInterface(
        INamedTypeSymbol type,
        INamedTypeSymbol iface,
        MarkerSymbols symbols,
        DiscoveredSymbols result)
    {
        if (!iface.IsGenericType || iface.TypeArguments.Length != 1)
            return false;

        var def = iface.OriginalDefinition;
        var requestType = iface.TypeArguments[0];

        if (SymbolEqualityComparer.Default.Equals(def, symbols.HandleRequests))
            result.Handlers.Add((requestType, type));
        else if (SymbolEqualityComparer.Default.Equals(def, symbols.HandleRequestsAsync))
            result.AsyncHandlers.Add((requestType, type));
        else if (SymbolEqualityComparer.Default.Equals(def, symbols.MessageMapper) && !type.IsGenericType)
            result.Mappers.Add((requestType, type));
        else if (SymbolEqualityComparer.Default.Equals(def, symbols.MessageMapperAsync) && !type.IsGenericType)
            result.AsyncMappers.Add((requestType, type));

        return true;
    }

    private static bool IsTransformInterface(INamedTypeSymbol iface, MarkerSymbols symbols) =>
        SymbolEqualityComparer.Default.Equals(iface, symbols.MessageTransform) ||
        SymbolEqualityComparer.Default.Equals(iface, symbols.MessageTransformAsync);

    private static bool IsReachableFromGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        }
        return true;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol child:
                    foreach (var t in EnumerateNamedTypes(child))
                        yield return t;
                    break;
                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var nested in EnumerateNested(type))
                        yield return nested;
                    break;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var n in EnumerateNested(nested))
                yield return n;
        }
    }

    private static RegistrationModel Project(IMethodSymbol method, DiscoveredSymbols discovered)
    {
        var containingType = method.ContainingType;
        var ns = containingType.ContainingNamespace;
        var hasNamespace = ns is { IsGlobalNamespace: false };

        return new RegistrationModel(
            Namespace: hasNamespace ? ns!.ToDisplayString() : null,
            ContainingTypeAccessibility: AccessibilityModifier(containingType.DeclaredAccessibility),
            ContainingTypeName: containingType.Name,
            ContainingTypeIsStatic: containingType.IsStatic,
            MethodAccessibility: AccessibilityModifier(method.DeclaredAccessibility),
            MethodName: method.Name,
            ReturnTypeFullyQualified: FullyQualified(method.ReturnType),
            ParameterTypeFullyQualified: FullyQualified(method.Parameters[0].Type),
            ParameterName: method.Parameters[0].Name,
            IsExtensionMethod: method.IsExtensionMethod,
            Handlers: new EquatableArray<HandlerEntry>(discovered.Handlers.Select(MapHandler)),
            AsyncHandlers: new EquatableArray<HandlerEntry>(discovered.AsyncHandlers.Select(MapHandler)),
            Mappers: new EquatableArray<MapperEntry>(discovered.Mappers.Select(MapMapper)),
            AsyncMappers: new EquatableArray<MapperEntry>(discovered.AsyncMappers.Select(MapMapper)),
            Transforms: new EquatableArray<string>(discovered.Transforms.Select(FullyQualified)),
            HintName: BuildHintName(containingType, method.Name));
    }

    private static HandlerEntry MapHandler((ITypeSymbol Request, INamedTypeSymbol Handler) entry)
    {
        if (IsOpenGeneric(entry.Handler))
        {
            return new HandlerEntry(
                RequestTypeFullyQualified: string.Empty,
                HandlerTypeFullyQualified: UnboundGenericName(entry.Handler),
                IsOpenGeneric: true);
        }
        return new HandlerEntry(
            RequestTypeFullyQualified: FullyQualified(entry.Request),
            HandlerTypeFullyQualified: FullyQualified(entry.Handler),
            IsOpenGeneric: false);
    }

    private static MapperEntry MapMapper((ITypeSymbol Request, INamedTypeSymbol Mapper) entry) =>
        new(FullyQualified(entry.Request), FullyQualified(entry.Mapper));

    private static string BuildHintName(INamedTypeSymbol containingType, string methodName)
    {
        var raw = containingType.ToDisplayString();
        var sanitized = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
            sanitized.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        return $"{sanitized}__{methodName}.g.cs";
    }

    private static string FullyQualified(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsOpenGeneric(INamedTypeSymbol type) =>
        type.IsUnboundGenericType || (type.IsGenericType && type.IsDefinition);

    private static string UnboundGenericName(INamedTypeSymbol type)
    {
        var name = FullyQualified(type);
        var lt = name.IndexOf('<');
        if (lt < 0)
            return name;
        var arity = type.TypeParameters.Length;
        return name.Substring(0, lt + 1) + new string(',', arity - 1) + ">";
    }

    private static string AccessibilityModifier(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "internal"
    };

    private sealed class DiscoveredSymbols
    {
        public List<(ITypeSymbol Request, INamedTypeSymbol Handler)> Handlers { get; } = new();
        public List<(ITypeSymbol Request, INamedTypeSymbol Handler)> AsyncHandlers { get; } = new();
        public List<(ITypeSymbol Request, INamedTypeSymbol Mapper)> Mappers { get; } = new();
        public List<(ITypeSymbol Request, INamedTypeSymbol Mapper)> AsyncMappers { get; } = new();
        public List<INamedTypeSymbol> Transforms { get; } = new();
    }
}
