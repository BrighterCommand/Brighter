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

using Microsoft.CodeAnalysis;

namespace Paramore.Brighter.SourceGenerators;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor MustBePartial = new(
        "BRGEN001",
        "Brighter registration method must be partial",
        "Method '{0}' marked with [BrighterRegistrations] must be a partial method",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MustBeStatic = new(
        "BRGEN002",
        "Brighter registration method must be static",
        "Method '{0}' marked with [BrighterRegistrations] must be static",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WrongReturnType = new(
        "BRGEN003",
        "Brighter registration method has wrong return type",
        "Method '{0}' must return IBrighterBuilder",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WrongSignature = new(
        "BRGEN004",
        "Brighter registration method has wrong signature",
        "Method '{0}' must accept a single IBrighterBuilder parameter",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericMapperOrTransformIgnored = new(
        "BRGEN005",
        "Generic message mappers and transforms are not registered",
        "Generic type '{0}' implements a Brighter mapper or transform interface but won't be auto-registered; close the generic, write a non-generic wrapper, or mark it with [ExcludeFromBrighterRegistration]",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NestedInOpenGeneric = new(
        "BRGEN006",
        "Types nested in an open generic type are not registered",
        "Type '{0}' is declared inside an open generic type, so its name cannot be written with concrete type arguments at the registration site; move it out of the open generic type, or mark it with [ExcludeFromBrighterRegistration]",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AutoRegistrationSuppressed = new(
        "BRGEN007",
        "Auto-registration suppressed by a manual registration method",
        "Brighter auto-registration is enabled but a manual [BrighterRegistrations] method is present, so the generated BrighterAssemblyRegistrations class was not emitted; remove the manual method or set <BrighterAutoRegistration>false</BrighterAutoRegistration>",
        "Brighter", DiagnosticSeverity.Info, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedContainingType = new(
        "BRGEN008",
        "Brighter registration method must be in a non-nested, non-generic type",
        "Method '{0}' marked with [BrighterRegistrations] must be declared in a non-nested, non-generic type; move it to a top-level type",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BrighterNotReferenced = new(
        "BRGEN009",
        "Brighter is not referenced",
        "Method '{0}' is marked with [BrighterRegistrations] but the compilation does not reference Paramore.Brighter and Paramore.Brighter.Extensions.DependencyInjection, so no registration was generated; add the missing package reference",
        "Brighter", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AutoRegistrationBrighterNotReferenced = new(
        "BRGEN010",
        "Auto-registration is enabled but Brighter is not fully referenced",
        "Brighter auto-registration is enabled but the compilation does not reference both Paramore.Brighter and Paramore.Brighter.Extensions.DependencyInjection, so no registrations were generated; add a reference to Paramore.Brighter.Extensions.DependencyInjection, declare a [BrighterRegistrations] holder in a project that has it, or set <BrighterAutoRegistration>false</BrighterAutoRegistration>",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AutoRegistrationCollision = new(
        "BRGEN011",
        "Auto-registration collides with a registration class from another assembly",
        "Assembly '{0}' already exposes BrighterAssemblyRegistrations to this compilation (via InternalsVisibleTo), so generating it here makes calls to AddFromThisAssembly ambiguous; set <BrighterAutoRegistration>false</BrighterAutoRegistration> in one of the two projects, or replace the auto form with a named [BrighterRegistrations] holder",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateHandler = new(
        "BRGEN012",
        "A non-event request has more than one registered handler",
        "Request '{0}' has {1} registered handlers ({2}); only an event may have more than one handler, so this fails when the request is sent — exclude the unwanted handlers with [ExcludeFromBrighterRegistration], or derive the request from Event if several handlers are intended",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidAutoRegistrationValue = new(
        "BRGEN013",
        "BrighterAutoRegistration is not a valid boolean",
        "The BrighterAutoRegistration property is set to '{0}', which is not 'true' or 'false', so auto-registration was treated as disabled; use <BrighterAutoRegistration>true</BrighterAutoRegistration> or <BrighterAutoRegistration>false</BrighterAutoRegistration>",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AutoRegistrationNameTaken = new(
        "BRGEN014",
        "The auto-registration class name is already declared in this compilation",
        "This compilation already declares Paramore.Brighter.Extensions.DependencyInjection.BrighterAssemblyRegistrations, so auto-registration was not generated; rename that type, or set <BrighterAutoRegistration>false</BrighterAutoRegistration> and register through a [BrighterRegistrations] holder",
        "Brighter", DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
