Brighter Management Console
=======================
This package is intended to support administering services built using Brighter.ServiceActivator. The goal is to allow you to start and stop consuming
all the configured channels, and to start and stop individual channels, or even dynamically add new channels at run time. 

One important caveat exists with dynamic configuration, you obviously need the code to consume the messages from a channel that is added 
dynamically to be included in the service that you deploy, this simply allows you to omit them from static configuration and yet dynamically add them.
