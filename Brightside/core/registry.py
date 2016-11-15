""""
File             : registry.py
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
from typing import Callable, Dict, List, TypeVar

from core.handler import Handler, Request
from core.messaging import BrightsideMessage
from core.exceptions import ConfigurationException


class Registry:
    """
        Provides a registry of commands and handlers i.e. the observer pattern
    """
    def __init__(self) -> None:
        self._registry = dict()  # type: Dict[str, List[Callable[[], Handler]]]

    def register(self, request_class: Request, handler_factory: Callable[[], Handler]) -> None:
        """
        Register the handler for the command
        :param request_class: The command or event to dispatch. It must implement getKey()
        :param handler_factory: A factory method to create the handler to dispatch to
        :return:
        """
        key = request_class.__name__
        is_command = request_class.is_command()
        is_event = request_class.is_event()
        is_present = key in self._registry
        if is_command and is_present:
            raise ConfigurationException("A handler for this request has already been registered")
        elif is_event and is_present:
            self._registry[key].append(handler_factory)
        elif is_command or is_event:
            self._registry[key] = [handler_factory]

    def lookup(self, request: Request) -> List[Callable[[], Handler]]:
        """
        Looks up the handler associated with a request - matches the key on the request to a registered handler
        :param request: The request we want to find a handler for
        :return:
        """
        key = request.__class__.__name__
        if key not in self._registry:
            if request.is_command():
                raise ConfigurationException("There is no handler registered for this request")
            elif request.is_event():
                return []  # type: Callable[[] Handler]

        return self._registry[key]

R = TypeVar('R', bound=Request)


class MessageMapperRegistry:
    """
        Provides a registry of message mappers, used to serialize a command to a message, which a producer can send over the wire
    """
    def __init__(self) -> None:
        self._registry = dict()  # type: Dict[str, Callable[[Request], BrightsideMessage]]

    def register(self, request_class: Request, mapper_func: Callable[[Request], BrightsideMessage]) -> None:
        """Adds a message mapper to a factory, using the requests key
        :param mapper_func: A callback that creates a BrightsideMessage from a Request
        :param request_class: A request type
        """

        key = request_class.__name__
        if key not in self._registry:
            self._registry[key] = mapper_func
        else:
            raise ConfigurationException("There is already a message mapper defined for this key; there can be only one")

    def lookup(self, request_class: Request) -> Callable[[Request], BrightsideMessage]:
        """
        Looks up the message mapper function associated with this class. Function should take in a Request derived class
         and return a BrightsideMessage derived class, for sending on the wire
        :param request_class:
        :return:
        """
        key = request_class.__class__.__name__
        if key not in self._registry:
            raise ConfigurationException("There is no message mapper associated with this key; we require a mapper")
        else:
            return self._registry[key]








