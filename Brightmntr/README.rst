Brighter Monitoring Console
===========================
This package is intended to support monitoring services using Brighter. By adding a Monitor attribute to a request handler we can output diagnostic information to the Brighter control bus in real time.
This console is intended to work a little like top or tail in that it provides a continuous stream of messages displayed to the terminal, until you quit.
It works with stdin and stdout and stderr, so that you can chain it's results into other commands, such as grep, sed or awk to search for values.
We believe that a graphical representation is a better dashboard for monitoring but that this raw information is useful for processing.
