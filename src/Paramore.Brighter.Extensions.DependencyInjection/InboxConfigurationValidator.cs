#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection;

internal sealed class InboxConfigurationValidator(IServiceProvider serviceProvider) : IAmAPipelineValidator
{
    public PipelineValidationResult Validate()
    {
        var inspector = serviceProvider.GetService<IAmASubscriberRegistryInspector>()
            ?? (IAmASubscriberRegistryInspector)serviceProvider.GetRequiredService<ServiceCollectionSubscriberRegistry>();
        var configuration = serviceProvider.GetService<IAmConsumerOptions>()?.InboxConfiguration;
        using var builder = new PipelineBuilder<IRequest>(inspector, configuration);
        var options = serviceProvider.GetService<IBrighterOptions>();
        using var scope = new ServiceProviderLifetimeScope(serviceProvider,
            options?.HandlerLifetime ?? ServiceLifetime.Transient,
            options?.IsolateTransientHandlerScope ?? true);
        var failures = new Dictionary<Type, string?>();
        var errors = new List<ValidationError>();

        foreach (var pipeline in builder.Describe())
            ValidatePipeline(pipeline, scope, failures, errors);

        return new PipelineValidationResult(errors, []);
    }

    private static void ValidatePipeline(HandlerPipelineDescription pipeline, ServiceProviderLifetimeScope scope,
        Dictionary<Type, string?> failures, List<ValidationError> errors)
    {
        foreach (var step in pipeline.BeforeSteps.Concat(pipeline.AfterSteps))
        {
            var inboxType = RequiredInboxType(step.HandlerType);
            if (inboxType is null)
                continue;

            if (!failures.TryGetValue(inboxType, out var failure))
            {
                failure = ResolveFailure(scope, inboxType);
                failures.Add(inboxType, failure);
            }

            if (failure is not null)
                errors.Add(new ValidationError(ValidationSeverity.Error,
                    $"Handler '{pipeline.HandlerType.Name}'",
                    $"'{step.AttributeType.Name}' requires a resolvable {inboxType.Name}. " +
                    "Set ConsumersOptions.InboxConfiguration or register a compatible inbox service. " + failure));
        }
    }

    private static Type? RequiredInboxType(Type handlerType)
    {
        if (handlerType == typeof(UseInboxHandler<>))
            return typeof(IAmAnInboxSync);
        if (handlerType == typeof(UseInboxHandlerAsync<>))
            return typeof(IAmAnInboxAsync);
        return null;
    }

    private static string? ResolveFailure(ServiceProviderLifetimeScope scope, Type inboxType)
    {
        try
        {
            var inbox = scope.GetOrCreate<IAmAnInbox>(inboxType);
            return inboxType.IsInstanceOfType(inbox)
                ? null
                : "The inbox service is missing or its factory returned an incompatible value.";
        }
        catch (Exception exception)
        {
            return $"Inbox resolution failed ({exception.GetType().Name}): {exception.Message}";
        }
    }
}
