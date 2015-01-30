In principle the controlbus is another dispatcher, it's just configured from pre-designed components
So we can just have code here to create a control bus, and create a dispatchbuilder passing in pre-configured channels and handlers to those channels
The channel names should reflect a naming scheme so that you can use wildcards to address, so we need to expose a factory for creating a control bus that creates
a base name say 'paramore.brighter.myapp' to which we add 'paramore.brighter.myapp.configuration' and 'paramore.brighter.myapp.heartbeat' etc.
The controlbus also publishes out on a key derived from that base under 'paramore.brighter.statistics'

