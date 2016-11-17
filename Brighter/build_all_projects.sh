# Build the core projects
pushd paramore.brighter.commandprocessor
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.commandstore.mssql
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.commandstore.sqlite
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messagestore.mssql
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messagestore.sqlite
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messageviewer
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messageviewer.console
dotnet build -f netcoreapp1.0 
popd
pushd paramore.brighter.commandprocessor.messaginggateway.awssqs
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messaginggateway.azureservicebus
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messaginggateway.restms
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.messaginggateway.rmq
dotnet build -f netstandard1.6 
popd
pushd paramore.brighter.commandprocessor.tests.nunit
dotnet build -f netcoreapp1.0 
popd
pushd paramore.brighter.serviceactivator
dotnet build -f netstandard1.6 
popd

# Build the example projects
cd Examples
ushd DocumentsAndFolders.Sqs.Core
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




























