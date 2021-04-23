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

using System;

namespace Paramore.Brighter.ServiceActivator
{
    internal class ConsumerFactory<TRequest> : IConsumerFactory where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IAmAMessageMapperRegistry _messageMapperRegistry;
        private readonly Subscription _subscription;
        private readonly ConsumerName _consumerName;

        public ConsumerFactory(IAmACommandProcessor commandProcessor, IAmAMessageMapperRegistry messageMapperRegistry, Subscription subscription)
        {
            _commandProcessor = commandProcessor;
            _messageMapperRegistry = messageMapperRegistry;
            _subscription = subscription;
            _consumerName = new ConsumerName($"{_subscription.Name}-{Guid.NewGuid()}");
        }

        public Consumer Create()
        {
            if (_subscription.RunAsync)
                return CreateAsync();
            else
                return CreateBlocking();
        }

        private Consumer CreateBlocking()
        {
            var channel = _subscription.ChannelFactory.CreateChannel(_subscription);
            var messagePump = new MessagePumpBlocking<TRequest>(_commandProcessor, _messageMapperRegistry.Get<TRequest>())
            {
                Channel = channel,
                TimeoutInMilliseconds = _subscription.TimeoutInMiliseconds,
                RequeueCount = _subscription.RequeueCount,
                RequeueDelayInMilliseconds = _subscription.RequeueDelayInMilliseconds,
                UnacceptableMessageLimit = _subscription.UnacceptableMessageLimit
            };

            return new Consumer(_consumerName, _subscription.Name, channel, messagePump);
        }

        private Consumer CreateAsync()
        {
            var channel = _subscription.ChannelFactory.CreateChannel(_subscription);
            var messagePump = new MessagePumpAsync<TRequest>(_commandProcessor, _messageMapperRegistry.Get<TRequest>())
            {
                Channel = channel,
                TimeoutInMilliseconds = _subscription.TimeoutInMiliseconds,
                RequeueCount = _subscription.RequeueCount,
                RequeueDelayInMilliseconds = _subscription.RequeueDelayInMilliseconds,
                UnacceptableMessageLimit = _subscription.UnacceptableMessageLimit
            };

            return new Consumer(_consumerName, _subscription.Name, channel, messagePump);
        }
    }
}
