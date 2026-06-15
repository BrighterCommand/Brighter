#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;
using global::FluentValidation;

namespace Paramore.Brighter.Validation.FluentValidation.Tests.TestDoubles
{
    /// <summary>
    /// Test Data Builder that wires a real Brighter pipeline for <see cref="GreetingCommand"/> with the
    /// FluentValidation handlers attached, so a test can focus on the evident data (the validator and the
    /// request) rather than the container plumbing.
    /// </summary>
    internal sealed class ValidationPipeline
    {
        private ValidationPipeline(CommandProcessor commandProcessor, HandlerReceipt receipt)
        {
            CommandProcessor = commandProcessor;
            Receipt = receipt;
        }

        public CommandProcessor CommandProcessor { get; }

        public HandlerReceipt Receipt { get; }

        /// <summary>
        /// Builds a synchronous pipeline. Pass <paramref name="validator"/> as null to simulate a missing
        /// validator registration.
        /// </summary>
        public static ValidationPipeline With(IValidator<GreetingCommand>? validator)
            => Build(validator, async: false);

        /// <summary>
        /// Builds an asynchronous pipeline. Pass <paramref name="validator"/> as null to simulate a missing
        /// validator registration.
        /// </summary>
        public static ValidationPipeline WithAsync(IValidator<GreetingCommand>? validator)
            => Build(validator, async: true);

        private static ValidationPipeline Build(IValidator<GreetingCommand>? validator, bool async)
        {
            var receipt = new HandlerReceipt();

            var registry = new SubscriberRegistry();
            if (async)
                registry.RegisterAsync<GreetingCommand, GreetingCommandHandlerAsync>();
            else
                registry.Register<GreetingCommand, GreetingCommandHandler>();

            var container = new ServiceCollection();
            container.AddSingleton(receipt);
            container.AddTransient<GreetingCommandHandler>();
            container.AddTransient<GreetingCommandHandlerAsync>();
            container.AddTransient(typeof(ValidateRequestHandler<>), typeof(FluentValidationRequestHandler<>));
            container.AddTransient(typeof(ValidateRequestHandlerAsync<>), typeof(FluentValidationRequestHandlerAsync<>));
            if (validator is not null)
                container.AddSingleton(validator);
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            PipelineBuilder<GreetingCommand>.ClearPipelineCache();

            var commandProcessor = new CommandProcessor(
                registry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory());

            return new ValidationPipeline(commandProcessor, receipt);
        }
    }
}
