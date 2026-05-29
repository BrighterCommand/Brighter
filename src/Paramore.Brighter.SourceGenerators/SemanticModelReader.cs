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
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paramore.Brighter.SourceGenerators.Model;

namespace Paramore.Brighter.SourceGenerators;

/// <summary>
/// Holds the two transform entry points that touch Roslyn semantic-model objects. Both produce
/// value-equatable records so the incremental pipeline can cache their output by value rather
/// than by symbol identity (which is never stable across compilations).
/// </summary>
public static class SemanticModelReader
{
    /// <summary>
    /// Transform for <see cref="IncrementalGeneratorInitializationContext.SyntaxProvider"/>
    /// <c>.ForAttributeWithMetadataName</c>: validates the attributed method and projects it
    /// to a Roslyn-free <see cref="MethodCandidate"/>.
    /// </summary>
    public static MethodCandidate ReadMethod(GeneratorAttributeSyntaxContext ctx, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not IMethodSymbol method)
            return new MethodCandidate(null, null);

        var markers = MarkerSymbols.Resolve(ctx.SemanticModel.Compilation);
        if (!markers.IsValid)
            return new MethodCandidate(null, null);

        var diagnostic = ValidateMethod(method, markers);
        if (diagnostic is not null)
            return new MethodCandidate(null, diagnostic);

        var target = ProjectMethod(method);
        return new MethodCandidate(target, null);
    }

    /// <summary>
    /// Transform for <see cref="IncrementalGeneratorInitializationContext.SyntaxProvider"/>
    /// <c>.CreateSyntaxProvider</c>: inspects a single class declaration and projects any
    /// Brighter-related interface implementations to value-equatable records.
    /// </summary>
    public static DiscoveryBatch ReadClass(GeneratorSyntaxContext ctx, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ctx.Node is not ClassDeclarationSyntax cls)
            return DiscoveryBatch.Empty;

        if (ctx.SemanticModel.GetDeclaredSymbol(cls, cancellationToken) is not INamedTypeSymbol type)
            return DiscoveryBatch.Empty;

        if (!IsClassifiable(type))
            return DiscoveryBatch.Empty;

        // Only emit from the "primary" partial declaration so partial classes don't get
        // discovered N times. Order explicitly so the choice is self-evidently stable.
        if (!IsPrimaryDeclaration(type, cls))
            return DiscoveryBatch.Empty;

        var markers = MarkerSymbols.Resolve(ctx.SemanticModel.Compilation);
        if (!markers.IsValid)
            return DiscoveryBatch.Empty;

        if (markers.ExcludeAttribute is not null && HasAttribute(type, markers.ExcludeAttribute))
            return DiscoveryBatch.Empty;

        var entries = new List<DiscoveredEntry>();
        var diagnostics = new List<DiagnosticInfo>();
        ClassifyEntries(type, markers, entries, diagnostics);
        if (entries.Count == 0 && diagnostics.Count == 0)
            return DiscoveryBatch.Empty;
        return new DiscoveryBatch(
            new EquatableArray<DiscoveredEntry>(entries),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attribute) =>
        type.GetAttributes().Any(a => Same(a.AttributeClass, attribute));

    private static DiagnosticInfo? ValidateMethod(IMethodSymbol method, MarkerSymbols markers)
    {
        var location = LocationInfo.From(method.Locations.FirstOrDefault());

        if (!method.IsPartialDefinition)
            return new DiagnosticInfo(Diagnostics.MustBePartial.Id, location, method.Name);

        if (!method.IsStatic)
            return new DiagnosticInfo(Diagnostics.MustBeStatic.Id, location, method.Name);

        if (!Same(method.ReturnType, markers.BrighterBuilder))
            return new DiagnosticInfo(Diagnostics.WrongReturnType.Id, location, method.Name);

        if (method.Parameters.Length != 1 || !Same(method.Parameters[0].Type, markers.BrighterBuilder))
            return new DiagnosticInfo(Diagnostics.WrongSignature.Id, location, method.Name);

        return null;
    }

    private static MethodTarget ProjectMethod(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        var ns = containingType.ContainingNamespace;
        var hasNamespace = ns is { IsGlobalNamespace: false };

        return new MethodTarget(
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
            HintName: BuildHintName(containingType, method.Name));
    }

    private static void ClassifyEntries(
        INamedTypeSymbol type,
        MarkerSymbols markers,
        List<DiscoveredEntry> entries,
        List<DiagnosticInfo> diagnostics)
    {
        var seenTransform = false;
        var unsupportedGenericMapperOrTransform = false;

        foreach (var iface in type.AllInterfaces)
        {
            var entry = TryClassifyInterface(type, iface, markers, ref seenTransform, ref unsupportedGenericMapperOrTransform);
            if (entry is not null)
                entries.Add(entry);
        }

        if (seenTransform && !type.IsGenericType)
            entries.Add(new DiscoveredEntry(DiscoveredKind.Transform, string.Empty, FullyQualified(type), IsOpenGeneric: false));

        if (unsupportedGenericMapperOrTransform)
        {
            var location = LocationInfo.From(type.Locations.FirstOrDefault());
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.GenericMapperOrTransformIgnored.Id,
                location,
                FullyQualified(type)));
        }
    }

    private static DiscoveredEntry? TryClassifyInterface(
        INamedTypeSymbol type,
        INamedTypeSymbol iface,
        MarkerSymbols markers,
        ref bool seenTransform,
        ref bool unsupportedGenericMapperOrTransform)
    {
        if (Same(iface, markers.MessageTransform) || Same(iface, markers.MessageTransformAsync))
        {
            if (type.IsGenericType)
                unsupportedGenericMapperOrTransform = true;
            else
                seenTransform = true;
            return null;
        }

        if (!iface.IsGenericType || iface.TypeArguments.Length != 1)
            return null;

        var def = iface.OriginalDefinition;
        var requestType = iface.TypeArguments[0];

        if (Same(def, markers.HandleRequests))
            return MakeHandlerEntry(DiscoveredKind.SyncHandler, type, requestType);
        if (Same(def, markers.HandleRequestsAsync))
            return MakeHandlerEntry(DiscoveredKind.AsyncHandler, type, requestType);
        if (Same(def, markers.MessageMapper))
        {
            if (type.IsGenericType) { unsupportedGenericMapperOrTransform = true; return null; }
            return new DiscoveredEntry(DiscoveredKind.Mapper, FullyQualified(requestType), FullyQualified(type), IsOpenGeneric: false);
        }
        if (Same(def, markers.MessageMapperAsync))
        {
            if (type.IsGenericType) { unsupportedGenericMapperOrTransform = true; return null; }
            return new DiscoveredEntry(DiscoveredKind.AsyncMapper, FullyQualified(requestType), FullyQualified(type), IsOpenGeneric: false);
        }

        return null;
    }

    private static DiscoveredEntry MakeHandlerEntry(DiscoveredKind kind, INamedTypeSymbol type, ITypeSymbol requestType)
    {
        if (IsOpenGeneric(type))
            return new DiscoveredEntry(kind, string.Empty, UnboundGenericName(type), IsOpenGeneric: true);
        return new DiscoveredEntry(kind, FullyQualified(requestType), FullyQualified(type), IsOpenGeneric: false);
    }

    private static bool IsClassifiable(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
            return false;
        if (type.IsAbstract || type.IsImplicitClass || type.IsAnonymousType)
            return false;
        return IsReachableFromGeneratedCode(type);
    }

    private static bool IsPrimaryDeclaration(INamedTypeSymbol type, ClassDeclarationSyntax cls)
    {
        var refs = type.DeclaringSyntaxReferences;
        if (refs.Length <= 1)
            return true;
        // Deterministic primary pick that doesn't rely on undocumented Roslyn ordering.
        var primary = refs
            .OrderBy(r => r.SyntaxTree.FilePath, System.StringComparer.Ordinal)
            .ThenBy(r => r.Span.Start)
            .First();
        return primary.SyntaxTree == cls.SyntaxTree && primary.Span == cls.Span;
    }

    private static bool IsReachableFromGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        }
        return true;
    }

    private static string BuildHintName(INamedTypeSymbol containingType, string methodName)
    {
        var raw = containingType.ToDisplayString();
        var sanitized = new StringBuilder(raw.Length);
        foreach (var ch in raw)
            sanitized.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        // Append a short stable hash of the original display string so that types differing only
        // in non-identifier characters (e.g. "Foo.Bar" vs "Foo_Bar") don't collide.
        return $"{sanitized}_{Fnv1aHex(raw)}__{methodName}.g.cs";
    }

    private static string Fnv1aHex(string text)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= prime;
        }
        return hash.ToString("x8");
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

    private static bool Same(ISymbol? a, ISymbol? b) =>
        SymbolEqualityComparer.Default.Equals(a, b);

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
}
