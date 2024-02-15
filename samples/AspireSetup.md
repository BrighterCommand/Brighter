Aspire documentation
https://learn.microsoft.com/en-us/dotnet/aspire/

Visual Studio:
Visual Studio 2022 Preview version 17.9 or higher (Optional)

Rider:
plugin is https://plugins.jetbrains.com/plugin/23289--net-aspire


To install the aspire workload
```shell
dotnet workload update
dotnet workload install aspire
```
To check your workload
```shell
dotnet workload list
```
To update your workloads 
```shell
dotnet workload update
```

Aspire template
```shell
dotnet new list aspire
```

Try the sample-starter in a new directory
```shell
mkdir TestAspire
cd TestAspire
dotnet new aspire-starter
dotnet run --project TestAspire.AppHost
```

To run sample from the commandline

```shell
dotnet run --project samples/RMQTaskQueue/AppHost
```


