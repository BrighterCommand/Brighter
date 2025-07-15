﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Paramore.Brighter.Policies.Attributes;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Policies.Handlers;

/// <summary>
/// Class ExceptionPolicyHandler.
/// The <see cref="UsePolicyAttribute"/> supports the use of <a href="https://github.com/michael-wolfenden/Polly">Polly</a> to provide quality of service around exceptions
/// thrown from subsequent steps in the handler pipeline. A Polly Policy can be used to support a Retry or Circuit Breaker approach to exception handling
/// Policies used by the attribute are identified by a string based key, which is used as a lookup into an <see cref="ResiliencePipelineRegistry{TKey}"/> and it is 
/// assumed that you have registered required policies with a Policy Registry such as <see cref="PolicyRegistry"/> and configured that as a 
/// dependency of the <see cref="CommandProcessor"/> using the <see cref="CommandProcessorBuilder"/>
/// The ExceptionPolicyHandler is instantiated by the pipeline when the <see cref="UsePolicyAttribute"/> is added to the <see cref="IHandleRequests{T}.Handle"/> method
/// of the target handler implemented by the client.
/// </summary>
/// <typeparam name="TRequest">The type of the t request.</typeparam>
public class ResilienceExceptionPolicyHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
{
    private bool _initialized;
    private ResiliencePipeline _pipeline = ResiliencePipeline.Empty;
    private ResiliencePipeline<TRequest> _pipelineGeneric = ResiliencePipeline<TRequest>.Empty;

    /// <summary>
    /// Initializes from attribute parameters. This will get the <see cref="PolicyRegistry"/> from the <see cref="IRequestContext"/> and query it for the
    /// policy identified in <see cref="UsePolicyAttribute"/>
    /// Because we call InitializeFromAttributeParams for each request, we need to guard against multiple calls to this method
    /// adding to the list of policies. Once we have the list of policies, then we do not need to re-initialize them.
    /// </summary>
    /// <param name="initializerList">The initializer list.</param>
    /// <exception cref="System.ArgumentException">Could not find the policy for this attribute, did you register it with the command processor's container;initializerList</exception>
    public override void InitializeFromAttributeParams(params object?[] initializerList)
    {
        if (_initialized)
        {
            return;
        }

        var useGeneric = false;
        if (initializerList[1] is bool generic)
        {
            useGeneric = generic;
        }
            
        //we expect the first and only parameter to be a string
        if (initializerList[0] is string pipeline && Context is { ResiliencePipeline: not null })
        {
            if (useGeneric)
            {
                _pipelineGeneric =  Context.ResiliencePipeline.GetPipeline<TRequest>(pipeline);
            }
            else
            {
                _pipeline = Context.ResiliencePipeline.GetPipeline(pipeline);
            }
        }
        
        _initialized = true;
    }

    /// <summary>
    /// Handles the specified command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>TRequest.</returns>
    public override TRequest Handle(TRequest command)
    {
        if (_pipelineGeneric != ResiliencePipeline<TRequest>.Empty)
        {
            return _pipelineGeneric.Execute(() => base.Handle(command));
        }
        
        return _pipeline.Execute(() => base.Handle(command));
    }
}
