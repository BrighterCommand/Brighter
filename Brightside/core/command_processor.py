"""
File         : command_processor.py
Author           : ian
Created          : 02-15-2016

Last Modified By : ian
Last Modified On : 02-15-2016
***********************************************************************
The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
THE SOFTWARE.
***********************************************************************
"""
from typing import Optional

from core.exceptions import ConfigurationException
from core.registry import Registry, MessageMapperRegistry
from core.messaging import BrightsideMessageStore, BrightsideProducer
from core.handler import Request


class CommandProcessor:
    """ The command processor is actually both a dispatcher - associating a a command with a handler - and a processor
        providing a pipeline for orthogonal operations to be run prior to dispatch.
    """

    def __init__(self,
                 registry: Optional[Registry]=None,
                 message_mapper_registry: Optional[MessageMapperRegistry]=None,
                 message_store: Optional[BrightsideMessageStore]=None,
                 producer: Optional[BrightsideProducer]=None) -> None:
        self._registry = registry
        self._message_mapper_registry = message_mapper_registry
        self._message_store = message_store
        self._producer = producer

    def send(self, request: Request) -> None:
        """
        Dispatches a request. Expects one and one only target handler
        :param request: The request to dispatch
        :return: None, will throw a ConfigurationException if more than one handler factor is registered for the command
        """

        handler_factories = self._registry.lookup(request)
        if len(handler_factories) != 1:
            raise ConfigurationException("There is no handler registered for this request")
        handler = handler_factories[0]()
        handler.handle(request)

    def publish(self, request: Request) -> None:
        """
        Dispatches a request. Expects zero or more target handlers
        :param request: The request to dispatch
        :return: None.
        """
        handler_factories = self._registry.lookup(request)
        for factory in handler_factories:
            handler = factory()
            handler.handle(request)

    def post(self, request: Request) -> None:
        """
        Dispatches a request over middleware. Returns when message put onto outgoing channel by producer,
        does not wait for response from a consuming application i.e. is fire-and-forget
        :param request: The request to dispatch
        :return: None
        """

        if self._producer is None:
            raise ConfigurationException("Command Processor requires a BrightsideProducer to post to a Broker")
        if self._message_mapper_registry is None:
            raise ConfigurationException("Command Processor requires a BrightsideMessage Mapper Registry to post to a Broker")

        message_mapper= self._message_mapper_registry.lookup(request)
        message = message_mapper(request)
        self._message_store.add(message)
        self._producer.send(message)


