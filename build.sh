# Build the core projects
pushd Paramore.Brighter
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.CommandStore.MsSql
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.CommandStore.Sqlite
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessageStore.MsSql
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessageStore.Sqlite
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessageViewer
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessageViewer.Console
dotnet build -f netcoreapp1.0 
popd
pushd Paramore.Brighter.MessagingGateway.AWSSQS
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessagingGateway.AzureServiceBus
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessagingGateway.RESTMS
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.MessagingGateway.RMQ
dotnet build -f netstandard1.6 
popd
pushd Paramore.Brighter.Tests
dotnet build -f netcoreapp1.0 Paramore.BrighterParamore.Brighter
popd
pushd Paramore.Brighter.ServiceSctivator
dotnet build -f netstandard1.6 
popd

# Build the example projects
cd samples
pushd DocumentsAndFolders.Sqs.Core
dotnet build -f netstandard1.6 
popd
pushd EventSourcing
dotnet build -f netcoreapp1.0 
popd
ushd Greetings
dotnet build -f netstandard1.6 
popd
pushd GreetingsCoreConsole
dotnet build -f netcoreapp1.0 
popd
ushd HelloAsyncListeners
dotnet build -f netstandard1.6
popd
pushd HelloWorld
dotnet build -f netcoreapp1.0 
popd
pushd HelloWorldAsync
dotnet build -f netcoreapp1.0 
popd
pushd ManagementAndMonitoring
dotnet build -f netstandard1.6 
popd
pushd ManagementAndMonitoringCoreConsole
dotnet build -f netcoreapp1.0 
popd




























