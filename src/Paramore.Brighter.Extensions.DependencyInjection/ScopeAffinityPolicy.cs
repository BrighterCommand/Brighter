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

using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Decides what affinity a pipeline should ask an <see cref="IAmAScopeProvider"/> for, from the
    /// configured lifetimes of everyone who participates in it, so that whichever one factory ends up
    /// making the ask does not need to know it is one of several.
    /// </summary>
    /// <remarks>
    /// A handler pipeline's participating set is its own handler lifetime alone. A transform pipeline's
    /// is structural rather than actual: the mapper and transformer lifetimes both count, whether or not
    /// the mapper declares a transform and whether or not a transformer factory instance exists.
    /// </remarks>
    internal sealed class ScopeAffinityPolicy
    {
        private readonly IBrighterOptions? _options;

        /// <summary>
        /// Constructs a policy over the resolved <see cref="IBrighterOptions"/>, or <see langword="null"/>
        /// where none is registered.
        /// </summary>
        public ScopeAffinityPolicy(IBrighterOptions? options) => _options = options;

        /// <summary>
        /// The affinity a handler pipeline should ask for.
        /// </summary>
        /// <returns><see cref="ScopeAffinity.JoinAmbient"/> when the affinity option is
        /// <see cref="ScopeAffinity.JoinAmbient"/> and <see cref="IBrighterOptions.HandlerLifetime"/> is
        /// <see cref="ServiceLifetime.Scoped"/>; <see cref="ScopeAffinity.AlwaysNew"/> otherwise, including
        /// where no options are registered.</returns>
        public ScopeAffinity ForHandlerPipeline()
        {
            if (_options is null) return ScopeAffinity.AlwaysNew;

            return _options.DefaultScopeAffinity == ScopeAffinity.JoinAmbient
                   && _options.HandlerLifetime == ServiceLifetime.Scoped
                ? ScopeAffinity.JoinAmbient
                : ScopeAffinity.AlwaysNew;
        }

        /// <summary>
        /// The affinity a transform pipeline should ask for.
        /// </summary>
        /// <returns><see cref="ScopeAffinity.JoinAmbient"/> when the affinity option is
        /// <see cref="ScopeAffinity.JoinAmbient"/>, at least one of
        /// <see cref="IBrighterOptions.MapperLifetime"/> and <see cref="IBrighterOptions.TransformerLifetime"/>
        /// is <see cref="ServiceLifetime.Scoped"/>, and neither is <see cref="ServiceLifetime.Transient"/>;
        /// <see cref="ScopeAffinity.AlwaysNew"/> otherwise, including where no options are registered. A
        /// <see cref="ServiceLifetime.Singleton"/> participant is ignored.</returns>
        public ScopeAffinity ForTransformPipeline()
        {
            if (_options is null) return ScopeAffinity.AlwaysNew;

            var mapperLifetime = _options.MapperLifetime;
            var transformerLifetime = _options.TransformerLifetime;

            return _options.DefaultScopeAffinity == ScopeAffinity.JoinAmbient
                   && (mapperLifetime == ServiceLifetime.Scoped || transformerLifetime == ServiceLifetime.Scoped)
                   && mapperLifetime != ServiceLifetime.Transient
                   && transformerLifetime != ServiceLifetime.Transient
                ? ScopeAffinity.JoinAmbient
                : ScopeAffinity.AlwaysNew;
        }
    }
}
