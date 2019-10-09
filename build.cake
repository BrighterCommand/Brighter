///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "LiteUnitTests");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
   // Executed BEFORE the first task.
   Information("Running tasks...");
});

Teardown(ctx =>
{
   // Executed AFTER the last task.
   Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Build")
.Does(() => {
    var settings = new DotNetCoreBuildSettings
    {
        Configuration = configuration,
    };
    DotNetCoreBuild("Brighter.sln", settings);
});

Task("LiteUnitTests")
  .IsDependentOn("Build")
  .Does(() =>
        {
          var settings = new DotNetCoreTestSettings
            {
                Configuration = configuration,
                NoBuild = true,
                Filter = "Category!=RMQ&Category!=RMQDelay&Category!=AWS&Category!=RESTMS&Category!=Kafka&Category!=Redis&Category!=PostgreSql&Category!=MySql&Category!=MSSQL&Category!=DynamoDB",
                Verbosity  = DotNetCoreVerbosity.Minimal
            };
          DotNetCoreTest("./tests/Paramore.Brighter.Tests/Paramore.Brighter.Tests.csproj", settings);
        });

Task("AllUnitTests")
  .IsDependentOn("Build")
  .Does(() =>
        {
          var settings = new DotNetCoreTestSettings
            {
                Configuration = configuration,
                NoBuild = true,
                Filter = "Category!=RESTMS",
                Verbosity  = DotNetCoreVerbosity.Minimal
            };
          DotNetCoreTest("./tests/Paramore.Brighter.Tests/Paramore.Brighter.Tests.csproj", settings);
        });


RunTarget(target);