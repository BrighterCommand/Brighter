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

using System;

namespace Paramore.Brighter;

/// <summary>
/// Marks a <c>static partial</c> method that the Brighter source generator
/// (<c>Paramore.Brighter.SourceGenerators</c>) implements, registering the handlers, message
/// mappers and message transforms it discovers in the declaring compilation.
/// </summary>
/// <remarks>
/// <para>
/// The method must be <c>static partial</c>, return
/// <c>Paramore.Brighter.Extensions.DependencyInjection.IBrighterBuilder</c>, take a single
/// parameter of that same type (extension methods are supported), and be declared in a
/// non-nested, non-generic type. Violations are reported as <c>BRGEN001</c>–<c>BRGEN004</c>
/// and <c>BRGEN008</c>.
/// </para>
/// <para>
/// This attribute lives in core Brighter rather than being emitted by the generator so that a
/// library and its test project — which typically see each other's internals through
/// <c>InternalsVisibleTo</c> — do not each declare a conflicting copy of it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public static partial class OrdersRegistrations
/// {
///     [BrighterRegistrations]
///     public static partial IBrighterBuilder AddOrders(this IBrighterBuilder builder);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class BrighterRegistrationsAttribute : Attribute
{
}

/// <summary>
/// Excludes a handler, message mapper or message transform from discovery by the Brighter source
/// generator. The type is left out of every generated registration method in its compilation.
/// </summary>
/// <remarks>
/// Use this for types the generator would otherwise register but that you want to register by
/// hand (or not at all) — for example a test double, or a generic mapper reported by
/// <c>BRGEN005</c> that you have deliberately chosen not to close.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ExcludeFromBrighterRegistrationAttribute : Attribute
{
}
