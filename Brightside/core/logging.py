from functools import wraps
import logging

from core.exceptions import ConfigurationException
from core.handler import Request

ENTRY_MESSAGE = "Entering {}"
EXIT_MESSAGE = "Exiting {}"


class log_handler:
    def __init__(self, func, level=logging.DEBUG, name=None, entry_message=None, exit_message=None):
        self._func = func
        self._level = level
        self._name = name
        self._entry_message = entry_message
        self._exit_message = exit_message

    def __call__(self, *args, **kwargs):
        log_name = self._name if self._name else self._func.__module__
        log = logging.getLogger(log_name)
        entry_log_msg = self._entry_message if self._entry_message else ENTRY_MESSAGE.format(self._func.__name__)
        exit_log_msg = self._exit_message if self._exit_message else EXIT_MESSAGE.format(self._func.__name__)

        @wraps(self._func)
        def wrapper(*args_wrapped, **kwargs_wrapped):

            # add serialization of command to log_msg
            # we assume that the command is always the first positional argument
            # as it should be only argument, and we check for its type to be sure
            request = args[0]
            if not isinstance(request, Request):
                raise ConfigurationException("A handler must take a Request derived class as its first positional argument {}", func.__name__)

            log.log(self._level, entry_log_msg)
            response = self._func(*args_wrapped, **kwargs_wrapped)
            log.log(self._level, exit_log_msg)
            return response

        return wrapper
