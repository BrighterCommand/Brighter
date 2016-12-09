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
from functools import wraps
import logging

from core.exceptions import ConfigurationException
from core.handler import Request

ENTRY_MESSAGE = "Entering {}"
EXIT_MESSAGE = "Exiting {}"


def log_handler(level=logging.DEBUG, name=None, entry_message=None, exit_message=None):
    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            log_name = name if name else func.__module__
            log = logging.getLogger(log_name)
            entry_log_msg = entry_message if entry_message else ENTRY_MESSAGE.format(func.__name__)
            exit_log_msg = exit_message if exit_message else EXIT_MESSAGE.format(func.__name__)

            # we assume that the request is always the first positional argument
            # as it should be only argument, and we check for its type to be sure
            # We also assume that the command has a __str__ method if more detailed
            # diagnostic information; we don't rely on a message mapper here
            request = args[1]
            if not isinstance(request, Request):
                raise ConfigurationException("A handler must take a Request derived class as its first positional argument {}", func.__name__)

            request_info = " {}".format(str(request))

            log.log(level, entry_log_msg + request_info)
            response = func(*args, **kwargs)
            log.log(level, exit_log_msg + request_info)
            return response

        return wrapper
    return decorator
