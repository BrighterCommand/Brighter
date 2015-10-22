"""
File         : __main__.py
Author           : ian
Created          : 06-20-2015

Last Modified By : ian
Last Modified On : 06-20-2015
***********************************************************************
The MIT License (MIT)
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
THE SOFTWARE.
***********************************************************************

Usage:
  brightmntr [options]

Options:
  -d SECONDS --delay=SECONDS            Specific delay between refreshes (otherwise 5 seconds).
  -n TIMES --updates=TIMES              Update display n times, then exit. Defaults 1o -1, no limit.
  -p PAGESIZE --pagesize=PAGESIZE       Try to show this number of items from the queue, then pause for delay seconds, defaults to 5 items
  -h --help                             Show this screen.


"""

import sys
from brightmntr.worker import Worker
from docopt import docopt
from time import sleep
from .configuration import configure

KEYBOARD_INTERRUPT_SLEEP = 3    # How long before checking for a keyhoard interrupt
DELAY_BETWEEN_REFRESHES = 5     # How long to delay before polling for more input
DISPLAY_N_TIMES = -1            # How many times to display messages, defaults to run forever
PAGE_SIZE = 5

def run(amqp_uri, exchange, routing_key, params):
    # start a monitor output thread, this does the work, whilst the main thread just acts
    # as a control thread to receive the  keyboard input

    worker = Worker(amqp_uri, exchange, routing_key)
    worker.delay_between_refreshes = params['--delay'] if params['--delay'] is not None else DELAY_BETWEEN_REFRESHES
    worker.limit = params['--updates'] if params['--updates'] is not None else DISPLAY_N_TIMES
    worker.page_size = params['--pagesize'] if params['--pagesize'] is not None else PAGE_SIZE
    worker.run()

    # poll for keyboard input to allow the user to quit monitoring
    while True:
        try:
            # just sleep unless we receive an interrupt i.e. CTRL+C
            sleep(KEYBOARD_INTERRUPT_SLEEP)
        except KeyboardInterrupt:
            worker.stop()
            sys.exit(1)


if __name__ == '__main__':
    arguments = docopt(__doc__, version='Brighter Monitoring v0.0')
    exchange, amqp_uri, routing_key = configure()
    run(amqp_uri, exchange, routing_key, arguments)

