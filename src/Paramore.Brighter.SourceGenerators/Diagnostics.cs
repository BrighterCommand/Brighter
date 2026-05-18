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

public static class Diagnostics
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
}
