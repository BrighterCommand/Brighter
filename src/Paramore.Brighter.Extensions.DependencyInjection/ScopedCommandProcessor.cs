using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    public class ScopedCommandProcessor : IAmAScopedCommandProcessor
    {
        private readonly IAmAHandlerFactory _handlerFactory;
        private readonly IAmAHandlerFactoryAsync _handlerFactoryAsync;
        private readonly CommandProcessor _commandProcessor;

        public ScopedCommandProcessor(IAmACommandProcessor commandProcessor, IAmAHandlerFactory handlerFactory, IAmAHandlerFactoryAsync handlerFactoryAsync)
        {
            _handlerFactory = handlerFactory;
            _handlerFactoryAsync = handlerFactoryAsync;
            _commandProcessor = (CommandProcessor)commandProcessor;
        }
        
        public void Send<T>(T command) where T : class, IRequest
        {
            _commandProcessor.Send(command, _handlerFactory);
        }

        public async Task SendAsync<T>(T command, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            await _commandProcessor.SendAsync(command, _handlerFactoryAsync, continueOnCapturedContext, cancellationToken);
        }

        public void Publish<T>(T @event) where T : class, IRequest
        {
            _commandProcessor.Publish(@event, _handlerFactory);
        }

        public async Task PublishAsync<T>(T @event, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            await _commandProcessor.PublishAsync(@event, _handlerFactoryAsync, continueOnCapturedContext,
                cancellationToken);
        }

        public void Post<T>(T request) where T : class, IRequest
        {
            _commandProcessor.Post(request);
        }

        public async Task PostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            await _commandProcessor.PostAsync(request, continueOnCapturedContext, cancellationToken);
        }

        public Guid DepositPost<T>(T request) where T : class, IRequest
        {
            return _commandProcessor.DepositPost(request);
        }

        public async Task<Guid> DepositPostAsync<T>(T request, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            return await _commandProcessor.DepositPostAsync(request, continueOnCapturedContext, cancellationToken);
        }

        public void ClearOutbox(params Guid[] posts)
        {
            _commandProcessor.ClearOutbox(posts);
        }

        public async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await _commandProcessor.ClearOutboxAsync(posts, continueOnCapturedContext, cancellationToken);
        }

        public TResponse Call<T, TResponse>(T request, int timeOutInMilliseconds) where T : class, ICall where TResponse : class, IResponse
        {
            return _commandProcessor.Call<T, TResponse>(request, timeOutInMilliseconds);
        }
    }
}
