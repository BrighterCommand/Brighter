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
using System.Threading;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// The one <see cref="MessageMapperRegistry"/> a single <c>ValidatePipelines()</c> run shares between
    /// the core <c>PipelineValidator</c> and <see cref="ScopeConfigurationValidator"/> (ADR 0074 step 5b) -
    /// registered once, built lazily and at most once, and drained by whichever of the two disposes last.
    /// </summary>
    /// <remarks>
    /// Wraps a <see cref="Lazy{T}"/> that is <see langword="null"/> exactly when no
    /// <c>ServiceCollectionMessageMapperRegistryBuilder</c> was registered, matching every other optional
    /// mapper-registry input in <c>ValidatePipelines()</c>. <see cref="Value"/> is read by
    /// <see cref="ArtefactExclusionSet.Build"/>; <see cref="Factory"/> is handed to <c>PipelineValidator</c>'s
    /// own <c>mapperRegistryFactory</c> parameter, so both validators resolve the same instance regardless of
    /// which one builds it first. <see cref="IDisposable"/> because the container drains every singleton it
    /// created; double disposal (by this type and by <c>PipelineValidator</c>, should a caller also dispose
    /// that) is safe, because <see cref="MessageMapperRegistry.Dispose"/> claims with a single
    /// <see cref="Interlocked.Exchange(ref int, int)"/>.
    /// </remarks>
    internal sealed class ValidationMapperRegistry : IDisposable
    {
        private readonly Lazy<MessageMapperRegistry>? _registry;
        private int _disposed;

        /// <summary>
        /// Constructs the shared registry.
        /// </summary>
        /// <param name="mapperRegistryFactory">Builds the underlying <see cref="MessageMapperRegistry"/>, or
        /// <see langword="null"/> when no <c>ServiceCollectionMessageMapperRegistryBuilder</c> was
        /// registered.</param>
        public ValidationMapperRegistry(Func<MessageMapperRegistry>? mapperRegistryFactory)
        {
            _registry = mapperRegistryFactory is null ? null : new Lazy<MessageMapperRegistry>(mapperRegistryFactory);
        }

        /// <summary>
        /// The shared registry, built on first access; <see langword="null"/> when no mapper registry
        /// builder was registered.
        /// </summary>
        public MessageMapperRegistry? Value => _registry?.Value;

        /// <summary>
        /// A factory that resolves to <see cref="Value"/>, for <c>PipelineValidator</c>'s own
        /// <c>mapperRegistryFactory</c> parameter; <see langword="null"/> over the same condition as
        /// <see cref="Value"/>.
        /// </summary>
        public Func<MessageMapperRegistry>? Factory => _registry is null ? null : () => _registry.Value;

        /// <summary>
        /// Disposes the registry, if one was built.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_registry is { IsValueCreated: true })
                _registry.Value.Dispose();
        }
    }
}
