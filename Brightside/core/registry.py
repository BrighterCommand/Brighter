#!/usr/bin/env python
""""
File         : handler.py
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

from core.exceptions import ConfigurationException


class Registry:
    """
        Provides a registry of commands and handlers i.e. the observer pattern
    """
    def __init__(self):
        self._registry = dict()

    def register(self, requestClass, handler_factory):
        """
        Register the handler for the command
        :param requestClass: The command or event to dispatch. It must implement getKey()
        :param handler_factory: A factory method to create the handler to dispatch to
        :return:
        """
        key = requestClass.key
        is_command = requestClass.is_command()
        is_event = requestClass.is_event()
        is_present = key in self._registry
        if is_command and is_present:
            raise ConfigurationException("A handler for this request has already been registered")
        elif is_event and is_present:
            self._registry[key].append(handler_factory)
        elif is_command or is_event:
            self._registry[key] = [handler_factory]

    def lookup(self, request):
        """
        Looks up the handler associated with a request - matches the key on the request to a registered handler
        :param request: The request we want to find a handler for
        :return:
        """
        key = request.key
        if key not in self._registry:
            if request.is_command():
                raise ConfigurationException("There is no handler registered for this request")
            elif request.is_event():
                return []

        return self._registry[key]


class MessageMapperRegistry:
    """
        Provides a registry of message mappers, used to serialize a command to a message, which a producer can send over the wire
    """
    def __init__(self):
        self._registry = dict()

    def register(self, requestClass, mapper_factory):
        """Adds a message mapper to a factory, using the requests key"""
        key = request_key
        if key not in self._registry:
            self._registry[key].append(mapper_factory)
        else:
            raise ConfigurationException("There is already a message mapper defined for this key; there can be only one")







